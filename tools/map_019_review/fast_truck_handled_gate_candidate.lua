-- Inert MAP-019 fast-Truck candidate. This module is not loaded by production.
-- `received` and `handled` are game-owned DateTime values. The caller supplies
-- the same native CompareTo function as _map017CompareDateTimes; never compare
-- tostring() values to establish whether a March payload has been handled.
-- A changed receive stamp represents ONE new push, as in the existing lane.
-- A second receive before the first was observed handled is ambiguous: fail
-- closed and let the scan owner restore/fallback rather than lose transient rows.
local M = {}

function M.new(initial_received)
    return { lastReceivedLabel = initial_received and tostring(initial_received) or "nil",
             lastReceivedValue = initial_received,
             pendingReceived = nil, consumed = 0, deferred = 0 }
end

function M.step(state, received, handled, outstanding, compare)
    if type(state) ~= "table" or type(compare) ~= "function" then
        return nil, "invalid_handled_gate"
    end
    if received == nil or handled == nil then
        return nil, "march_datetime_unavailable"
    end
    local label = tostring(received)
    if label ~= state.lastReceivedLabel then
        if state.pendingReceived ~= nil then
            return nil, "overlapping_unhandled_march_push"
        end
        if not outstanding then
            return nil, "unattributed_march_push"
        end
        if state.lastReceivedValue ~= nil and compare(received, state.lastReceivedValue) ~= 1 then
            return nil, "nonmonotonic_march_receive"
        end
        state.pendingReceived = received
        state.lastReceivedLabel = label
        state.lastReceivedValue = received
    end
    if state.pendingReceived == nil then return "idle" end
    local compared = compare(handled, state.pendingReceived)
    if compared ~= -1 and compared ~= 0 and compared ~= 1 then
        return nil, "march_datetime_comparison_unavailable"
    end
    if compared < 0 then
        state.deferred = state.deferred + 1
        return "deferred"
    end
    state.pendingReceived = nil
    state.consumed = state.consumed + 1
    return "capture"
end

return M
