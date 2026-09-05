-- Current-game daily-task read-only snapshot builder.
--
-- This module performs no file I/O and sends no network messages. It accepts the
-- manager/template/state objects as arguments, validates the recovered state
-- relationships, and returns a plain Lua table that the loader-accepted writer
-- serializes using the version-1 JSON contract.

local M = {
    VERSION = "lwcontrol-daily-state-probe-1",
    SCHEMA_VERSION = 1,
    MODE = "state",
}

local BOX_COUNT = 5

local function fail(message)
    return nil, message
end

local function object_like(value)
    local kind = type(value)
    return kind == "table" or kind == "userdata"
end

local function symbolic_state(value, task_state)
    if value == task_state.NoComplete then return "NoComplete" end
    if value == task_state.CanReceive then return "CanReceive" end
    if value == task_state.Received then return "Received" end
    return nil
end

local function contains(values, wanted)
    for _, value in ipairs(values) do
        if value == wanted then return true end
    end
    return false
end

local function normalize_received_stages(cur_reward)
    if type(cur_reward) ~= "table" then
        return fail("curReward is not a table; refusing an incomplete daily-task snapshot")
    end

    local output = {}
    local seen = {}
    for _, value in ipairs(cur_reward) do
        if type(value) ~= "number" or value % 1 ~= 0 or value < 1 or value > BOX_COUNT then
            return fail("curReward contains an invalid daily chest stage")
        end
        if seen[value] then
            return fail("curReward contains a duplicate daily chest stage")
        end
        seen[value] = true
        table.insert(output, value)
    end
    table.sort(output)
    return output
end

local function read_thresholds(daily_box_active)
    if type(daily_box_active) ~= "table" then
        return fail("dailyBoxActive is not a table")
    end

    local output = {}
    for index = 1, BOX_COUNT do
        local point = daily_box_active[index]
        if type(point) ~= "number" or point % 1 ~= 0 or point < 0 then
            return fail("dailyBoxActive is missing or has an invalid threshold at index " .. tostring(index))
        end
        output[index] = point
    end
    return output
end

local function read_tasks(manager, template_manager, task_state)
    if type(manager.dailyQuestTasks) ~= "table" then
        return fail("dailyQuestTasks is not a table")
    end

    local output = {}
    local derived_point = 0
    local seen = {}
    for task_id, info in pairs(manager.dailyQuestTasks) do
        if not object_like(info) then
            return fail("dailyQuestTasks contains an invalid task record")
        end
        local id = tostring(task_id)
        if id == "" or seen[id] then
            return fail("dailyQuestTasks contains a missing or duplicate task identity")
        end
        seen[id] = true

        local state = symbolic_state(info.state, task_state)
        if state == nil then
            return fail("dailyQuestTasks contains a task state outside the recovered symbolic set")
        end

        local template = template_manager:GetQuestTemplate(task_id)
        local template_point = nil
        if template ~= nil then
            template_point = template.point
            if type(template_point) ~= "number" or template_point % 1 ~= 0 or template_point < 0 then
                return fail("daily task template contains an invalid point value")
            end
        end

        if state == "Received" and template_point ~= nil then
            derived_point = derived_point + template_point
        end

        table.insert(output, {
            taskId = id,
            state = state,
            templatePoint = template_point,
        })
    end

    table.sort(output, function(left, right)
        return left.taskId < right.taskId
    end)
    return output, derived_point
end

function M.Build(manager, template_manager, task_state, capture_id, captured_at)
    if not object_like(manager) or not object_like(template_manager) or not object_like(task_state) then
        return fail("daily-task snapshot dependencies are unavailable")
    end
    if type(manager.GetCurValue) ~= "function" or type(manager.GetBoxState) ~= "function"
        or type(template_manager.GetQuestTemplate) ~= "function" then
        return fail("daily-task snapshot dependencies do not expose the recovered manager contract")
    end
    if type(capture_id) ~= "string" or capture_id == "" or type(captured_at) ~= "string" or captured_at == "" then
        return fail("capture metadata is incomplete")
    end

    local received_stages, stage_error = normalize_received_stages(manager.curReward)
    if received_stages == nil then return nil, stage_error end

    local thresholds, threshold_error = read_thresholds(manager.dailyBoxActive)
    if thresholds == nil then return nil, threshold_error end

    local tasks, derived_point = read_tasks(manager, template_manager, task_state)
    if tasks == nil then return nil, derived_point end

    local manager_point = manager:GetCurValue()
    if type(manager_point) ~= "number" or manager_point % 1 ~= 0 or manager_point < 0 then
        return fail("DailyTaskManager:GetCurValue returned an invalid value")
    end
    if manager_point ~= derived_point then
        return fail("DailyTaskManager:GetCurValue disagrees with task/template-point derivation")
    end

    local boxes = {}
    for index = 1, BOX_COUNT do
        local game_state = symbolic_state(manager:GetBoxState(index, manager_point), task_state)
        if game_state == nil then
            return fail("DailyTaskManager:GetBoxState returned an unknown state")
        end

        local derived_state
        if contains(received_stages, index) then
            derived_state = "Received"
        elseif thresholds[index] <= manager_point then
            derived_state = "CanReceive"
        else
            derived_state = "NoComplete"
        end
        if game_state ~= derived_state then
            return fail("DailyTaskManager:GetBoxState disagrees with recovered chest-state derivation")
        end

        table.insert(boxes, {
            index = index,
            activationPoint = thresholds[index],
            state = game_state,
        })
    end

    return {
        schemaVersion = M.SCHEMA_VERSION,
        mode = M.MODE,
        captureId = capture_id,
        capturedAt = captured_at,
        heartbeat = {
            probeVersion = M.VERSION,
            observedAt = captured_at,
        },
        tasks = tasks,
        currentPoint = manager_point,
        receivedStages = received_stages,
        boxes = boxes,
    }
end

return M
