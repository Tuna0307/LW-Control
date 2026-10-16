"""CONTROLLED / INERT Lua-shape test for ghost_reward_list in current_live_resource_probe.lua.

Runs only the extracted helper under Lua 5.3 (lupa) against a fake RewardManager that
mirrors the installed game's RewardManager:ParseRewardsStr / ParseOneRewardStr semantics
(entries "itemId;type;count" joined by "|"; an unresolvable GOODS template is silently
dropped; non-GOODS types only get rewardType/itemId/count). This is NOT genuine game
evidence. No game, launcher or process is touched.
"""
from pathlib import Path
import json
import sys

repo = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(repo / "artifacts/map-013-lua-site"))
from lupa.lua53 import LuaRuntime

source = (repo / "tools/current_live_resource_probe.lua").read_text(encoding="utf-8")
start = source.index("local function ghost_reward_list(raw, ratio)")
end = source.index("local function ghost_aoi_records(", start)
helper = source[start:end]

PRELUDE = r'''
local function safe_get(target, key)
    if target == nil then return nil end
    local ok, value = pcall(function() return target[key] end)
    return ok and value or nil
end
local function call(target, method, ...)
    local fn = safe_get(target, method)
    if type(fn) ~= "function" then return false, nil end
    local ok, value = pcall(fn, target, ...)
    if ok then return true, value end
    ok, value = pcall(fn, ...)
    return ok, ok and value or nil
end
'''

HARNESS = r'''
templates = { [710005] = "Speedup", [520002] = "Gem", [520003] = "Chip" }
local function make_manager(name_for_type)
    local m = {}
    function m:ParseOneRewardStr(text)
        local a, b, c = string.match(text, "(%d+)[,;](%d+)[,;](%d+)")
        if a == nil then return nil end
        local id, t, n = tonumber(a), tonumber(b), tonumber(c)
        local entry
        if t == 7 then
            if templates[id] == nil then return nil end
            entry = { iconName = "icon/" .. id, itemName = templates[id] }
        elseif t == 27 and name_for_type then
            entry = { itemName = "ResItem" .. id, iconName = "res/" .. id }
        end
        entry = entry or {}
        entry.itemId, entry.count, entry.rewardType = id, n, t
        return entry
    end
    function m:ParseRewardsStr(text)
        local out = {}
        if text == nil or text == "" then return out end
        for part in string.gmatch(text, "([^|]+)|?") do
            local e = self:ParseOneRewardStr(part)
            if e ~= nil then table.insert(out, e) end
        end
        return out
    end
    function m:GetNameByType(t, id)
        if name_for_type then return "ByType" .. t .. "_" .. id end
        return nil
    end
    return m
end
function run(raw, mode, ratio)
    local named = mode ~= "unnamed"
    if mode == "no_manager" then DataCenter = {} else DataCenter = { RewardManager = make_manager(named) } end
    return ghost_reward_list(raw, ratio or 100)
end
'''

lua = LuaRuntime(unpack_returned_tuples=True)
lua.execute(PRELUDE + helper + HARNESS)
run = lua.globals().run


def listify(value):
    if value is None:
        return None
    return [{k: v for k, v in entry.items()} for entry in value.values()]


def check(condition, why):
    if not condition:
        raise AssertionError("MAP014_GHOST_REWARD_LUA: " + why)


known = listify(run("710005;7;20|520002;7;10|5001;27;200", "named"))
check([e["key"] for e in known] == ["reward:7:710005", "reward:7:520002", "reward:27:5001"], "stable identity keys in config order")
check([e["count"] for e in known] == [20, 10, 200] and known[0]["name"] == "Speedup" and known[0]["iconPath"] == "icon/710005", "name/icon/count")
check(known[2]["rewardType"] == 27 and known[2]["itemId"] == 5001 and known[2]["name"] == "ResItem5001", "non-goods identity retained")
merged = listify(run("710005;7;20|710005;7;5", "named"))
check(len(merged) == 1 and merged[0]["count"] == 25, "same identity sums like Truck")
check(run("710005;7;20|999999;7;1", "named") is None, "dropped/unresolvable entry makes the whole list unknown, never partial")
for bad in (None, "", "garbage", "710005;7", "710005;7;0", "|"):
    check(run(bad, "named") is None, f"unusable config {bad!r} must be unknown")
check(run("710005;7;20", "no_manager") is None, "no RewardManager means unknown")
# Plunder share (WorldPointGhostrecon popup): floor(count * steal_reward_ratio / 100); owner screenshot 20/3/50/1.
lua.execute("templates = templates or {}; for _, i in ipairs({710005, 520004, 500104, 2270041}) do templates[i] = 'Item' .. i end")
plunder = listify(run("710005;7;200|520004;7;30|500104;7;500|7038;27;10|2270041;7;5", "named", 10))
check([(e["key"], e["count"]) for e in plunder] == [("reward:7:710005", 20), ("reward:7:520004", 3), ("reward:7:500104", 50), ("reward:27:7038", 1)],
      "plunder share = floor(count*ratio/100), zero entries (hero shard 5 -> 0) dropped")
check(run("710005;7;200", "named", "not-a-number") is None and run("710005;7;200", "named", 0) is None, "unknown steal ratio -> unknown, never the full reward")
fallback = listify(run("5001;27;200", "unnamed"))
check(fallback is None or fallback[0]["name"] == "reward:27:5001", "unnamed resource keeps identity, name falls back to key")
by_type = listify(run("5001;27;200", "by_type"))
check(by_type[0]["name"] == "ResItem5001" or by_type[0]["name"].startswith("ByType"), "game name accessors preferred")

# All real installed config rows, if the read-only research dump is present (ignored artifact).
dump = repo / "artifacts/map014-ghost-research/lw_ghostrecon_tasks.json"
rows_checked = 0
if dump.exists():
    rows = json.loads(dump.read_text(encoding="utf-8"))
    ids = {int(part.split(";")[0]) for row in rows.values() for part in row["base_reward_show"].split("|") if part.split(";")[1] == "7"}
    lua.execute("templates = {}\n" + "\n".join(f"templates[{i}] = 'Item{i}'" for i in sorted(ids)))
    for cfg_id, row in rows.items():
        result = listify(run(row["base_reward_show"], "named"))
        parts = row["base_reward_show"].split("|")
        check(result is not None and len(result) == len({";".join(p.split(";")[:2]) for p in parts}),
              f"cfg {cfg_id} resolved with one entry per distinct identity")
        rows_checked += 1
print(f"MAP014_GHOST_REWARD_LUA_SHAPE_CONTROLLED_INERT_OK installedConfigRowsExercised={rows_checked}")
