"""Build or verify an encrypted read-only current-build Rally snapshot candidate.

Preparation only rewrites a copy of the official LWLF-v3 package. The preserved
official LuaEntry remains byte-identical inside the candidate, and the injected
probe contains no rally join, march start, UI click, or network-send path.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

try:
    from .extract_lenc_v3 import decode_lenc_bytes, derive_xlua_key_nonce, encode_lenc_bytes
    from .install_loader_probe import (
        InstallRefused,
        LUA_ENTRY,
        ORIGINAL_LUA_ENTRY,
        PROBE_ENTRY,
        _entry_map,
        _entry_source,
        _xlua_path,
        crc32_file,
        discover_paths,
        preflight,
        read_lwlf,
        write_lwlf,
    )
except ImportError:
    from extract_lenc_v3 import decode_lenc_bytes, derive_xlua_key_nonce, encode_lenc_bytes
    from install_loader_probe import (
        InstallRefused,
        LUA_ENTRY,
        ORIGINAL_LUA_ENTRY,
        PROBE_ENTRY,
        _entry_map,
        _entry_source,
        _xlua_path,
        crc32_file,
        discover_paths,
        preflight,
        read_lwlf,
        write_lwlf,
    )


PROBE_VERSION = "lwcontrol-rally-loader-probe-1"


def _builder_source() -> str:
    return Path(__file__).with_name("current_rally_snapshot_probe.lua").read_text(encoding="utf-8")


def _probe_source() -> bytes:
    source = r'''local M = { VERSION = "__PROBE_VERSION__" }
local root = (os.getenv("LOCALAPPDATA") or ".") .. [[\LWControl\runtime]]
local heartbeat_path = root .. [[\rally-loader-probe.json]]
local snapshot_path = root .. [[\rally-snapshot.json]]
local status_path = root .. [[\rally-snapshot-status.json]]
local last_heartbeat = 0
local capture_complete = false
local stable_signature = nil
local stable_since = nil
local STABLE_SECONDS = 3
local registration_method = nil
local update_callback = nil
local timer_handle = nil

local builder = (function()
__SNAPSHOT_BUILDER__
end)()

local function safe_get(target, key)
    if target == nil then return nil end
    local ok, value = pcall(function() return target[key] end)
    return ok and value or nil
end

local function invoke(target, method, ...)
    local fn = safe_get(target, method)
    if type(fn) ~= "function" then return false, nil end
    local ok, value = pcall(fn, target, ...)
    if ok then return true, value end
    ok, value = pcall(fn, ...)
    return ok, ok and value or nil
end

local function invoke_static(target, method, ...)
    local fn = safe_get(target, method)
    if type(fn) ~= "function" then return false, nil end
    local ok, value = pcall(fn, ...)
    return ok, ok and value or nil
end

local function each_value(collection, limit, consume)
    if collection == nil then return 0 end
    local values = safe_get(collection, "Values")
    if values ~= nil and values ~= collection then return each_value(values, limit, consume) end
    local count = 0
    if type(collection) == "table" then
        for key, item in pairs(collection) do
            count = count + 1
            if consume(item, key) == false or count >= limit then break end
        end
        return count
    end
    local ok_enum, enumerator = invoke(collection, "GetEnumerator")
    if ok_enum and enumerator ~= nil then
        while count < limit do
            local ok_move, moved = invoke(enumerator, "MoveNext")
            if not ok_move or moved ~= true then break end
            local current = safe_get(enumerator, "Current")
            local item = safe_get(current, "Value")
            if item == nil then item = current end
            count = count + 1
            if consume(item, safe_get(current, "Key")) == false then break end
        end
        return count
    end
    local length = tonumber(safe_get(collection, "Count") or safe_get(collection, "Length"))
    if length ~= nil then
        for index = 0, math.min(length - 1, limit - 1) do
            local item = safe_get(collection, index)
            if item == nil then item = safe_get(collection, index + 1) end
            if item ~= nil then
                count = count + 1
                if consume(item, index) == false then break end
            end
        end
    end
    return count
end

local function world_march_manager(world)
    if world == nil then return nil end
    local manager = safe_get(world, "MarchDataManager")
    if manager == nil then
        local ok_manager, output = invoke(world, "get_MarchDataManager")
        if ok_manager then manager = output end
    end
    return manager
end

local function find_active_world_context()
    local direct = safe_get(rawget(_G, "SceneManager"), "World")
    if direct ~= nil and safe_get(direct, "isActiveAndEnabled") ~= false
        and type(safe_get(direct, "HandleFormationMarch")) == "function" then
        local manager = world_march_manager(direct)
        if manager ~= nil then return manager, "SceneManager.World" end
    end
    local cs = rawget(_G, "CS")
    local resources = safe_get(safe_get(cs, "UnityEngine"), "Resources")
    local scene_type = safe_get(cs, "WorldScene")
    local typeof_fn = rawget(_G, "typeof")
    if resources == nil or scene_type == nil or type(typeof_fn) ~= "function" then return nil, "" end
    local ok_type, reflected_type = pcall(typeof_fn, scene_type)
    if not ok_type then return nil, "" end
    local ok_objects, objects = invoke_static(resources, "FindObjectsOfTypeAll", reflected_type)
    if not ok_objects or objects == nil then return nil, "" end
    local found = nil
    each_value(objects, 32, function(candidate)
        if safe_get(candidate, "isActiveAndEnabled") == true
            and type(safe_get(candidate, "HandleFormationMarch")) == "function" then
            local manager = world_march_manager(candidate)
            if manager ~= nil then found = manager; return false end
        end
        return true
    end)
    return found, found ~= nil and "Resources.FindObjectsOfTypeAll" or ""
end

local function json_string(value)
    if value == nil then return "null" end
    value = tostring(value)
    value = string.gsub(value, '[%z\1-\31\\"]', function(ch)
        if ch == '"' then return '\\"' end
        if ch == '\\' then return '\\\\' end
        if ch == '\b' then return '\\b' end
        if ch == '\f' then return '\\f' end
        if ch == '\n' then return '\\n' end
        if ch == '\r' then return '\\r' end
        if ch == '\t' then return '\\t' end
        return string.format('\\u%04x', string.byte(ch))
    end)
    return '"' .. value .. '"'
end

local ARRAY_KEYS = { rallies = true, formations = true, memberNames = true }

local function json_encode(value, force_array)
    local kind = type(value)
    if value == nil then return "null" end
    if kind == "boolean" then return value and "true" or "false" end
    if kind == "number" then
        if value ~= value or value == math.huge or value == -math.huge then return "null" end
        return tostring(value)
    end
    if kind == "string" then return json_string(value) end
    if kind ~= "table" then return json_string(tostring(value)) end
    if force_array then
        local parts = { "[" }
        for index, item in ipairs(value) do
            if index > 1 then parts[#parts + 1] = "," end
            parts[#parts + 1] = json_encode(item, false)
        end
        parts[#parts + 1] = "]"
        return table.concat(parts)
    end
    local keys = {}
    for key in pairs(value) do keys[#keys + 1] = tostring(key) end
    table.sort(keys)
    local parts = { "{" }
    for index, key in ipairs(keys) do
        if index > 1 then parts[#parts + 1] = "," end
        parts[#parts + 1] = json_string(key)
        parts[#parts + 1] = ":"
        parts[#parts + 1] = json_encode(value[key], ARRAY_KEYS[key] == true)
    end
    parts[#parts + 1] = "}"
    return table.concat(parts)
end

local function write_json(path, value)
    local file = io.open(path, "wb")
    if not file then return false end
    file:write(json_encode(value, false))
    file:close()
    return true
end

local function write_heartbeat(now)
    if now == last_heartbeat then return true end
    local ok = write_json(heartbeat_path, { version = M.VERSION, loaded = true, updatedAt = now })
    if ok then last_heartbeat = now end
    return ok
end

local function write_status(now, state, err, world_source)
    return write_json(status_path, {
        probeVersion = M.VERSION,
        builderVersion = builder.VERSION,
        state = state,
        updatedAt = now,
        error = err,
        worldMarchSource = world_source or "",
        registrationMethod = registration_method or "",
        explicitNetworkSends = 0,
        joinActions = 0,
    })
end

local function signature(snapshot)
    local parts = {
        tostring(snapshot.player and snapshot.player.uid or ""),
        tostring(snapshot.observedRallyCount or 0),
        tostring(snapshot.formationCount or 0),
        tostring(snapshot.freeFormationCount or 0),
    }
    for _, rally in ipairs(snapshot.rallies or {}) do
        parts[#parts + 1] = table.concat({
            tostring(rally.uuid or ""), tostring(rally.canJoin == true),
            tostring(rally.inTeam == true), tostring(rally.waitTime or ""),
            tostring(rally.memberCount or ""),
        }, ":")
    end
    for _, formation in ipairs(snapshot.formations or {}) do
        parts[#parts + 1] = table.concat({
            tostring(formation.uuid or ""), tostring(formation.isFree == true),
            tostring(formation.currentRallyId or ""), tostring(formation.stamina or ""),
        }, ":")
    end
    return table.concat(parts, "|")
end

local function capture(now)
    local data_center = rawget(_G, "DataCenter")
    if type(data_center) ~= "table" then return nil, "DataCenter unavailable", "" end
    local war_manager = safe_get(data_center, "AllianceWarDataManager")
    local formation_manager = safe_get(data_center, "ArmyFormationDataManager")
    local player = safe_get(rawget(_G, "LuaEntry"), "Player")
    local world_manager, world_source = find_active_world_context()
    local captured_at = os.date("!%Y-%m-%dT%H:%M:%SZ", now)
    local snapshot, err = builder.Build(
        war_manager, formation_manager, world_manager, player,
        "live-" .. tostring(now), captured_at, world_source)
    if snapshot == nil then return nil, err or "snapshot builder refused state", world_source end
    return snapshot, nil, world_source
end

function M.Pump()
    local now = tonumber(os.time()) or 0
    write_heartbeat(now)
    if capture_complete then return true end
    local ok, snapshot, err, world_source = pcall(capture, now)
    if not ok then write_status(now, "error", snapshot, ""); return true end
    if snapshot == nil then write_status(now, "waiting", err, world_source); return true end
    local current_signature = signature(snapshot)
    if current_signature ~= stable_signature then
        stable_signature = current_signature
        stable_since = now
        write_status(now, "stabilizing", nil, world_source)
        return true
    end
    if stable_since == nil or now - stable_since < STABLE_SECONDS then
        write_status(now, "stabilizing", nil, world_source)
        return true
    end
    if not write_json(snapshot_path, snapshot) then
        write_status(now, "error", "rally snapshot file could not be opened", world_source)
        return true
    end
    capture_complete = true
    write_status(now, "captured", nil, world_source)
    return true
end

function M.Register()
    if registration_method ~= nil then return true end
    update_callback = function() M.Pump() end

    local manager = rawget(_G, "UpdateManager")
    local instance = manager
    if manager ~= nil and type(safe_get(manager, "GetInstance")) == "function" then
        local ok_instance, value = invoke(manager, "GetInstance")
        if ok_instance and value ~= nil then instance = value end
    end
    local add_update = safe_get(instance, "AddUpdate")
    if type(add_update) == "function" then
        local ok_update = pcall(add_update, instance, update_callback)
        if not ok_update then ok_update = pcall(add_update, update_callback) end
        if ok_update then registration_method = "UpdateManager.AddUpdate" end
    end

    if registration_method == nil then
        local cs = rawget(_G, "CS")
        local entries = {}
        local global_entry = rawget(_G, "GameEntry")
        local cs_entry = cs and safe_get(cs, "GameEntry") or nil
        if global_entry ~= nil then entries[#entries + 1] = global_entry end
        if cs_entry ~= nil and cs_entry ~= global_entry then entries[#entries + 1] = cs_entry end
        for _, entry in ipairs(entries) do
            local timer = safe_get(entry, "Timer")
            local register_repeat = safe_get(timer, "RegisterTimerRepeat")
            if type(register_repeat) == "function" then
                local ok_timer, handle = pcall(register_repeat, timer, 0.25, 0.25, update_callback)
                if not ok_timer then
                    ok_timer, handle = pcall(register_repeat, 0.25, 0.25, update_callback)
                end
                if ok_timer then
                    timer_handle = handle
                    registration_method = "GameEntry.Timer.RegisterTimerRepeat"
                    break
                end
            end
        end
    end

    M.Pump()
    return registration_method ~= nil
end

M.Register()
return M
'''
    source = source.replace("__PROBE_VERSION__", PROBE_VERSION)
    source = source.replace("__SNAPSHOT_BUILDER__", _builder_source())
    return source.encode("utf-8")


def _encoded_entries(paths: dict[str, Path], entries: list[tuple[str, bytes]]):
    mapped = _entry_map(entries)
    official = mapped.get(LUA_ENTRY)
    if official is None:
        raise InstallRefused(f"{LUA_ENTRY} is missing from LWScripts.data")
    native = derive_xlua_key_nonce(_xlua_path(paths))
    wrapper_source = _entry_source()
    probe_source = _probe_source()
    wrapper = encode_lenc_bytes(wrapper_source, native["key"], native["nonce"])
    probe = encode_lenc_bytes(probe_source, native["key"], native["nonce"])
    if decode_lenc_bytes(wrapper, native["key"], native["nonce"])["decoded"] != wrapper_source:
        raise InstallRefused("Rally wrapper LENC round-trip failed")
    if decode_lenc_bytes(probe, native["key"], native["nonce"])["decoded"] != probe_source:
        raise InstallRefused("Rally probe LENC round-trip failed")

    mapped[ORIGINAL_LUA_ENTRY] = bytes(official)
    mapped[LUA_ENTRY] = wrapper
    mapped[PROBE_ENTRY] = probe
    output: list[tuple[str, bytes]] = []
    seen: set[str] = set()
    for name, _ in entries:
        output.append((name, mapped[name]))
        seen.add(name)
    for name in (ORIGINAL_LUA_ENTRY, PROBE_ENTRY):
        if name not in seen:
            output.append((name, mapped[name]))
    return output, native, bytes(official), wrapper_source, probe_source


def prepare(paths: dict[str, Path], output_dir: Path) -> dict[str, object]:
    before = preflight(paths)
    if before["file_version"] != 3:
        raise InstallRefused("Rally snapshot probe requires current LWLF version 3")
    output_dir = output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    outputs = [output_dir / "LWScripts.data", output_dir / "LWScripts.txt", output_dir / "version.txt"]
    if any(path.exists() for path in outputs):
        raise InstallRefused("Rally candidate output already exists")

    file_version, content_version, entries = read_lwlf(paths["data"])
    prepared, native, official, wrapper_source, probe_source = _encoded_entries(paths, entries)
    write_lwlf(outputs[0], file_version, content_version, prepared)
    verify_version, verify_content, verify_entries = read_lwlf(outputs[0])
    verify_map = _entry_map(verify_entries)
    if (verify_version, verify_content) != (file_version, content_version):
        raise InstallRefused("Rally candidate header did not round-trip")
    if verify_map.get(ORIGINAL_LUA_ENTRY) != official:
        raise InstallRefused("Rally candidate did not preserve official LuaEntry")
    if decode_lenc_bytes(verify_map[LUA_ENTRY], native["key"], native["nonce"])["decoded"] != wrapper_source:
        raise InstallRefused("Rally serialized wrapper verification failed")
    if decode_lenc_bytes(verify_map[PROBE_ENTRY], native["key"], native["nonce"])["decoded"] != probe_source:
        raise InstallRefused("Rally serialized probe verification failed")

    length = outputs[0].stat().st_size
    crc = crc32_file(outputs[0])
    outputs[1].write_text(f"{length}|{crc}", encoding="utf-8")
    outputs[2].write_text(str(content_version), encoding="utf-8")
    return {
        "changed_installed_files": False,
        "output_dir": str(output_dir),
        "file_version": file_version,
        "content_version": content_version,
        "entry_count": len(verify_entries),
        "package_length": length,
        "package_crc32": crc,
        "package_sha256": hashlib.sha256(outputs[0].read_bytes()).hexdigest(),
        "probe_source_sha256": hashlib.sha256(probe_source).hexdigest(),
        "builder_source_sha256": hashlib.sha256(_builder_source().encode("utf-8")).hexdigest(),
        "xlua_sha256": native["sha256"],
        "serialized_round_trip_verified": True,
        "preflight": before,
    }


def verify(paths: dict[str, Path], candidate_dir: Path) -> dict[str, object]:
    candidate_dir = candidate_dir.resolve()
    data = candidate_dir / "LWScripts.data"
    metadata = candidate_dir / "LWScripts.txt"
    version = candidate_dir / "version.txt"
    if not data.is_file() or not metadata.is_file() or not version.is_file():
        raise InstallRefused("Rally candidate is incomplete")
    file_version, content_version, entries = read_lwlf(data)
    mapped = _entry_map(entries)
    native = derive_xlua_key_nonce(_xlua_path(paths))
    official_file_version, official_content_version, official_entries = read_lwlf(paths["data"])
    official_map = _entry_map(official_entries)
    if (file_version, content_version) != (official_file_version, official_content_version):
        raise InstallRefused("Rally candidate LWLF version does not match installed build")
    if mapped.get(ORIGINAL_LUA_ENTRY) != official_map.get(LUA_ENTRY):
        raise InstallRefused("Rally candidate original LuaEntry does not match installed build")
    if decode_lenc_bytes(mapped[LUA_ENTRY], native["key"], native["nonce"])["decoded"] != _entry_source():
        raise InstallRefused("Rally candidate wrapper does not match expected source")
    if decode_lenc_bytes(mapped[PROBE_ENTRY], native["key"], native["nonce"])["decoded"] != _probe_source():
        raise InstallRefused("Rally candidate probe does not match expected source")
    expected_length, expected_crc = metadata.read_text(encoding="utf-8").strip().split("|", 1)
    if int(expected_length) != data.stat().st_size or int(expected_crc) != crc32_file(data):
        raise InstallRefused("Rally candidate metadata length/CRC mismatch")
    if int(version.read_text(encoding="utf-8").strip()) != content_version:
        raise InstallRefused("Rally candidate version.txt mismatch")
    return {
        "candidate_dir": str(candidate_dir),
        "file_version": file_version,
        "content_version": content_version,
        "entry_count": len(entries),
        "package_sha256": hashlib.sha256(data.read_bytes()).hexdigest(),
        "probe_source_sha256": hashlib.sha256(_probe_source()).hexdigest(),
        "builder_source_sha256": hashlib.sha256(_builder_source().encode("utf-8")).hexdigest(),
        "verified": True,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--prepare-dir", type=Path)
    group.add_argument("--verify-dir", type=Path)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        paths = discover_paths()
        result = prepare(paths, args.prepare_dir) if args.prepare_dir else verify(paths, args.verify_dir)
    except (InstallRefused, OSError, ValueError, KeyError) as exc:
        payload = {"ok": False, "error": str(exc), "error_type": type(exc).__name__}
        print(json.dumps(payload, indent=2) if args.json else f"REFUSED: {exc}")
        return 2
    payload = {"ok": True, **result}
    print(json.dumps(payload, indent=2) if args.json else payload)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
