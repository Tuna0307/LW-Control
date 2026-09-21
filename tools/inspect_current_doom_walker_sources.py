#!/usr/bin/env python3
"""Hash-lock the current-v20 Doomsday SuperRunningBoss (special 32) source contract.

Historical R7-107 called this "Doom Walker"; live type-8/special-11 proof now
shows ordinary Doom Walker is a separate RunningMonster family.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import run_live_resource_probe as probe
import inspect_current_dispatch_plunder_sources as lua

EXPECTED_PACKAGE_SHA256 = "fedd635a7f972843b72d274128e2d443d81463272d86497e5a8a32223c6bb7a9"
EXPECTED_FILE_VERSION = 3
EXPECTED_CONTENT_VERSION = 20

MODULES = {
    "enum": ("Global/EnumType.luac", "b9de2ea2957c0e120d42fdcc01c0141862c6c0f3736eaaf95e63c8985957e31d"),
    "manager": ("DataCenter/LWDoomsday/LWDoomsdayManager.luac", "815bc11e241e29a0142fc9adb462a7d9bed67b3ce050e2b40ee5ec72c2dd6f37"),
    "message": ("Net/Msgs/Activity/Doomsday/ActivityDoomsdayMainInfoMessage.luac", "e9e43b7e83e2afd281f8132a2b5c3a83d2f0b09be772759b2954af6ed58cc8bf"),
    "ui": ("UI/UIActivityCenterTable/Component/Doomsday/UIDoomsday.luac", "afb2c81ec9ed2aa762476002a9bee534b2834c8aaeba59cfea97f95fd204f1ab"),
    "boss_item": ("UI/UIActivityCenterTable/Component/Doomsday/UIDoomsdayBossItem.luac", "355d22bd69821747206293cbe18c6b140fa21d551843d8d0b16a597590e72379"),
    "msg_defines": ("Net/Config/MsgDefines.luac", "4f0117f3da036315a3e1d9ef8ab15ba87c66efaac561524497cd6eea0e20f8ee"),
    "msg_map": ("Net/Config/MsgMap.luac", "23da7680bc49ecb4946839cf853018a163c4b19809695acb29cf623fe5bc4306"),
}

class DoomInspectError(RuntimeError):
    pass


def require_one_arg_send(proto: dict) -> dict:
    constants = proto["constants"]
    wanted = ["SFSNetwork", "SendMessage", "MsgDefines", "ActivityDoomsdayMainInfo"]
    indexes = {name: constants.index(name) for name in wanted}
    code = proto["code"]
    for pc in range(len(code) - 4):
        a, b, c, d, e = code[pc:pc + 5]
        if (
            lua.opcode(a) == "GETTABUP" and lua.instruction_c(a) == 0x100 + indexes["SFSNetwork"]
            and lua.opcode(b) == "GETTABLE" and lua.instruction_c(b) == 0x100 + indexes["SendMessage"]
            and lua.opcode(c) == "GETTABUP" and lua.instruction_c(c) == 0x100 + indexes["MsgDefines"]
            and lua.opcode(d) == "GETTABLE" and lua.instruction_c(d) == 0x100 + indexes["ActivityDoomsdayMainInfo"]
            and lua.opcode(e) == "CALL" and lua.instruction_b(e) == 2
        ):
            return {"instructionPc": pc, "argumentCount": 1, "verifiedConstants": wanted}
    raise DoomInspectError("UIDoomsday.OnCreate no longer sends ActivityDoomsdayMainInfo with one argument")


def require_enum_value(root: dict, name: str, value: int) -> dict:
    constants = root["constants"]
    name_indexes = {i for i, item in enumerate(constants) if item == name}
    value_indexes = {i for i, item in enumerate(constants) if item == value}
    for pc in range(len(root["code"]) - 2):
        first, second, third = root["code"][pc:pc + 3]
        if (lua.opcode(first) == "LOADK" and lua.instruction_bx(first) in name_indexes
                and lua.opcode(second) == "LOADK" and lua.instruction_bx(second) in value_indexes
                and lua.opcode(third) == "SETTABLE"):
            return {"instructionPc": pc, "name": name, "value": value}
    raise DoomInspectError(f"{name} no longer maps to {value}")

def inspect(package_path: Path) -> dict:
    raw = package_path.read_bytes()
    package_sha = hashlib.sha256(raw).hexdigest()
    if package_sha != EXPECTED_PACKAGE_SHA256:
        raise DoomInspectError(f"unsupported package SHA-256 {package_sha}")
    file_version, content_version, entries = probe.read_lwlf(package_path)
    if (file_version, content_version) != (EXPECTED_FILE_VERSION, EXPECTED_CONTENT_VERSION):
        raise DoomInspectError(f"unsupported package version {file_version}/{content_version}")
    entry_map = dict(entries)
    parsed, module_report = {}, {}
    for key, (name, expected_sha) in MODULES.items():
        if name not in entry_map:
            raise DoomInspectError(f"missing current module {name}")
        body = probe.decode_lenc(entry_map[name])
        digest = hashlib.sha256(body).hexdigest()
        if digest != expected_sha:
            raise DoomInspectError(f"decoded hash mismatch for {name}: {digest}")
        parsed[key] = lua.parse_chunk(body)
        module_report[key] = {"entry": name, "decodedSha256": digest, "decodedBytes": len(body)}

    ui_methods = lua.root_methods(parsed["ui"])
    manager_methods = lua.root_methods(parsed["manager"])
    boss_methods = lua.root_methods(parsed["boss_item"])
    checks = {
        "requestCommand": lua.require_msg_define_mapping(
            parsed["msg_defines"], "ActivityDoomsdayMainInfo", "activity.doomsday.main.info"),
        "responseModule": lua.require_msg_map_mapping(
            parsed["msg_map"], "ActivityDoomsdayMainInfo",
            "Net.Msgs.Activity.Doomsday.ActivityDoomsdayMainInfoMessage"),
        "uiReadOnlySend": require_one_arg_send(ui_methods["OnCreate"]),
        "responseForward": lua.require_constants(
            "ActivityDoomsdayMainInfoMessage",
            lua.find_proto(parsed["message"], ["DataCenter", "LWDoomsdayManager", "OnGetMainInfo", "errorCode"]),
            ["DataCenter", "LWDoomsdayManager", "OnGetMainInfo", "errorCode"]),

        "managerBossNormalization": lua.require_constants(
            "LWDoomsdayManager.OnGetMainInfo", manager_methods["OnGetMainInfo"],
            ["boss_info", "server_boss", "alliance_boss", "monster_id", "monster_uid",
             "point_id", "expire_time", "monsterId", "uid", "targetPos", "refreshTime",
             "theaterBosses", "allianceBosses"]),
        "bossUiClassification": lua.require_constants(
            "UIDoomsdayBossItem.Refresh", boss_methods["Refresh"],
            ["WorldMonsterSpecialType", "SuperRunningBoss", "Monster", "monsterId",
             "targetPos", "level", "name", "pic_name"]),
        "superRunningBossEnum": require_enum_value(parsed["enum"], "SuperRunningBoss", 32),
    }
    return {
        "ok": True,
        "schemaVersion": 1,
        "package": {
            "sha256": package_sha,
            "fileVersion": file_version,
            "contentVersion": content_version,
            "entryCount": len(entries),
        },
        "modules": module_report,
        "checks": checks,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("package", type=Path)
    parser.add_argument("--pretty", action="store_true")
    args = parser.parse_args()
    report = inspect(args.package.resolve())
    print(json.dumps(report, indent=2 if args.pretty else None, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
