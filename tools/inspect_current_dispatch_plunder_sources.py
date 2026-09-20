#!/usr/bin/env python3
"""Hash-lock and inspect the current-v19 Dispatch plunder Lua contract.

Read-only inspector for R7-081. It parses just enough of Last War's Lua 5.3
bytecode variant to map top-level class methods to child prototypes and verify
the exact constants used by the authoritative current client.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path

import run_live_resource_probe as probe


EXPECTED_PACKAGE_SHA256 = (
    "e7c5742a44d5f5862e4eb6c94944b4150969b6c4bd0a1c1cb9a337cfd1141fa2"
)
EXPECTED_FILE_VERSION = 3
EXPECTED_CONTENT_VERSION = 19

MODULES = {
    "steal_message": (
        "Net/Msgs/DispatchTask/DispatchStealMessage.luac",
        "301b51900f73a95429823cdaf257f5f361f062ff3b7d5d1ac8cba2e7c3daba1a",
    ),
    "manager": (
        "DataCenter/ActivityListData/ActDispatchTaskDataManager.luac",
        "21cf16b7fe6da46f468a4d6f1f259ae6fd972a100ad9398c57a2e701abd8162d",
    ),
    "world_button": (
        "UI/UIWorldPoint/Component/UIWorldPointBtn.luac",
        "d2f099d11172a80e7c8eda63fd410d3360fea965d8269691061be5a7d8f54d79",
    ),
    "mark_item": (
        "UI/UIActivityCenterTable/Component/DispatchTask/DispatchTaskMarkItem.luac",
        "f5a9c720426110162ca73aba807547fcfccdd4f312b614231cd426b5a9996845",
    ),
    "mark_data": (
        "UI/UIActivityCenterTable/Component/DispatchTask/DispatchTaskMarkData.luac",
        "38b80bb72ce756c2b92bbff4524b5722b3da6a4ee4113a740b7c660e3b7f8487",
    ),
    "msg_defines": (
        "Net/Config/MsgDefines.luac",
        "4f0117f3da036315a3e1d9ef8ab15ba87c66efaac561524497cd6eea0e20f8ee",
    ),
    "msg_map": (
        "Net/Config/MsgMap.luac",
        "23da7680bc49ecb4946839cf853018a163c4b19809695acb29cf623fe5bc4306",
    ),
}

OPS = [
    "MOVE", "LOADK", "LOADKX", "LOADBOOL", "LOADNIL", "GETUPVAL",
    "GETTABUP", "GETTABLE", "SETTABUP", "SETUPVAL", "SETTABLE",
    "NEWTABLE", "SELF", "ADD", "SUB", "MUL", "MOD", "POW", "DIV",
    "IDIV", "BAND", "BOR", "BXOR", "SHL", "SHR", "UNM", "BNOT",
    "NOT", "LEN", "CONCAT", "JMP", "EQ", "LT", "LE", "TEST",
    "TESTSET", "CALL", "TAILCALL", "RETURN", "FORLOOP", "FORPREP",
    "TFORCALL", "TFORLOOP", "SETLIST", "CLOSURE", "VARARG", "EXTRAARG",
]


class InspectError(RuntimeError):
    pass


class Reader:
    def __init__(self, data: bytes):
        self.data = data
        self.offset = 0

    def read(self, size: int) -> bytes:
        end = self.offset + size
        if end > len(self.data):
            raise InspectError(
                f"unexpected Lua bytecode EOF at {self.offset} reading {size}"
            )
        value = self.data[self.offset:end]
        self.offset = end
        return value

    def u8(self) -> int:
        return self.read(1)[0]

    def i32(self) -> int:
        return struct.unpack("<i", self.read(4))[0]

    def u32(self) -> int:
        return struct.unpack("<I", self.read(4))[0]

    def i64(self) -> int:
        return struct.unpack("<q", self.read(8))[0]

    def f64(self) -> float:
        return struct.unpack("<d", self.read(8))[0]

    def string(self) -> str | None:
        first = self.u8()
        if first == 0:
            return None
        size = first if first < 0xFF else self.u32()
        if size < 1:
            raise InspectError("invalid Lua string size")
        return self.read(size - 1).decode("utf-8", "replace")


def parse_proto(reader: Reader, parent_source: str | None = None) -> dict:
    source = reader.string() or parent_source
    line = reader.i32()
    last_line = reader.i32()
    params = reader.u8()
    vararg = reader.u8()
    stack = reader.u8()
    code = [reader.u32() for _ in range(reader.i32())]

    constants: list[object] = []
    for _ in range(reader.i32()):
        tag = reader.u8()
        if tag == 0:
            value = None
        elif tag == 1:
            value = bool(reader.u8())
        elif tag == 3:
            value = reader.f64()
        elif tag == 0x13:
            value = reader.i64()
        elif tag in (4, 0x14):
            value = reader.string()
        else:
            raise InspectError(f"unsupported Lua constant tag 0x{tag:02X}")
        constants.append(value)

    upvalues = [(reader.u8(), reader.u8()) for _ in range(reader.i32())]
    children = [
        parse_proto(reader, source)
        for _ in range(reader.i32())
    ]
    lines = [reader.i32() for _ in range(reader.i32())]
    locals_ = [
        (reader.string(), reader.i32(), reader.i32())
        for _ in range(reader.i32())
    ]
    upvalue_names = [reader.string() for _ in range(reader.i32())]
    return {
        "source": source,
        "line": line,
        "lastLine": last_line,
        "params": params,
        "vararg": vararg,
        "stack": stack,
        "code": code,
        "constants": constants,
        "upvalues": upvalues,
        "children": children,
        "lines": lines,
        "locals": locals_,
        "upvalueNames": upvalue_names,
    }


def parse_chunk(data: bytes) -> dict:
    reader = Reader(data)
    if reader.read(4) != b"\x1bLua":
        raise InspectError("decoded module is not Lua bytecode")
    if reader.u8() != 0x53 or reader.u8() != 1:
        raise InspectError("unexpected Last War Lua bytecode version/format")
    if reader.read(6) != b"\x19\x93\r\n\x1a\n":
        raise InspectError("unexpected Lua bytecode signature")
    if reader.read(4) != bytes((4, 4, 8, 8)):
        raise InspectError("unexpected Last War Lua serialized-size header")
    reader.read(8)  # LUAC_INT
    reader.read(8)  # LUAC_NUM
    reader.u8()     # root upvalue count
    root = parse_proto(reader)
    if reader.offset != len(data):
        raise InspectError(
            f"unexpected trailing Lua bytecode: {len(data) - reader.offset} bytes"
        )
    return root


def opcode(instruction: int) -> str:
    value = instruction & 0x3F
    return OPS[value] if value < len(OPS) else f"OP{value}"


def root_methods(root: dict) -> dict[str, dict]:
    constants = root["constants"]
    code = root["code"]
    children = root["children"]
    methods: dict[str, dict] = {}
    for index in range(len(code) - 1):
        closure = code[index]
        assign = code[index + 1]
        if opcode(closure) != "CLOSURE" or opcode(assign) != "SETTABLE":
            continue
        child_index = (closure >> 14) & 0x3FFFF
        key_operand = (assign >> 23) & 0x1FF
        if not (key_operand & 0x100):
            continue
        constant_index = key_operand & 0xFF
        if child_index >= len(children) or constant_index >= len(constants):
            continue
        name = constants[constant_index]
        if isinstance(name, str):
            methods[name] = children[child_index]
    return methods


def string_constants(proto: dict) -> list[str]:
    return [value for value in proto["constants"] if isinstance(value, str)]


def require_constants(
    label: str,
    proto: dict,
    expected: list[str],
) -> dict[str, object]:
    strings = string_constants(proto)
    missing = [value for value in expected if value not in strings]
    if missing:
        raise InspectError(f"{label} missing constants: {missing}")
    return {
        "line": proto["line"],
        "lastLine": proto["lastLine"],
        "verifiedConstants": expected,
    }


def find_proto(root: dict, expected: list[str]) -> dict:
    stack = [root]
    matches = []
    while stack:
        item = stack.pop()
        strings = string_constants(item)
        if all(value in strings for value in expected):
            matches.append(item)
        stack.extend(reversed(item["children"]))
    if len(matches) != 1:
        raise InspectError(
            f"expected exactly one prototype for {expected}, got {len(matches)}"
        )
    return matches[0]


def instruction_a(instruction: int) -> int:
    return (instruction >> 6) & 0xFF


def instruction_b(instruction: int) -> int:
    return (instruction >> 23) & 0x1FF


def instruction_c(instruction: int) -> int:
    return (instruction >> 14) & 0x1FF


def instruction_bx(instruction: int) -> int:
    return (instruction >> 14) & 0x3FFFF


def require_msg_define_mapping(
    root: dict,
    key: str,
    command: str,
) -> dict[str, object]:
    constants = root["constants"]
    key_index = constants.index(key)
    command_index = constants.index(command)
    code = root["code"]
    for pc in range(len(code) - 2):
        first, second, third = code[pc:pc + 3]
        if (
            opcode(first) == "LOADK"
            and instruction_a(first) == 1
            and instruction_bx(first) == key_index
            and opcode(second) == "LOADK"
            and instruction_a(second) == 2
            and instruction_bx(second) == command_index
            and opcode(third) == "SETTABLE"
            and instruction_a(third) == 0
            and instruction_b(third) == 1
            and instruction_c(third) == 2
        ):
            return {
                "line": root["line"],
                "lastLine": root["lastLine"],
                "verifiedConstants": [key, command],
                "instructionPc": pc,
            }
    raise InspectError(
        f"MsgDefines did not assign {key!r} -> {command!r} with the expected table pattern"
    )


def require_msg_map_mapping(
    root: dict,
    key: str,
    module: str,
) -> dict[str, object]:
    constants = root["constants"]
    key_index = constants.index(key)
    module_index = constants.index(module)
    if not constants or constants[0] != "MsgDefines":
        raise InspectError("MsgMap constant 0 is no longer MsgDefines")
    code = root["code"]
    for pc in range(len(code) - 4):
        get_global, load_key, get_value, load_module, assign = code[pc:pc + 5]
        if (
            opcode(get_global) == "GETTABUP"
            and instruction_a(get_global) == 1
            and instruction_c(get_global) == 0x100
            and opcode(load_key) == "LOADK"
            and instruction_a(load_key) == 2
            and instruction_bx(load_key) == key_index
            and opcode(get_value) == "GETTABLE"
            and instruction_a(get_value) == 1
            and instruction_b(get_value) == 1
            and instruction_c(get_value) == 2
            and opcode(load_module) == "LOADK"
            and instruction_a(load_module) == 2
            and instruction_bx(load_module) == module_index
            and opcode(assign) == "SETTABLE"
            and instruction_a(assign) == 0
            and instruction_b(assign) == 1
            and instruction_c(assign) == 2
        ):
            return {
                "line": root["line"],
                "lastLine": root["lastLine"],
                "verifiedConstants": ["MsgDefines", key, module],
                "instructionPc": pc,
            }
    raise InspectError(
        f"MsgMap did not map MsgDefines.{key} to {module!r} with the expected table pattern"
    )


def inspect_current(package_path: Path) -> dict[str, object]:
    raw_package = package_path.read_bytes()
    package_hash = hashlib.sha256(raw_package).hexdigest()
    if package_hash != EXPECTED_PACKAGE_SHA256:
        raise InspectError(
            f"unsupported current package SHA-256 {package_hash}; "
            f"expected {EXPECTED_PACKAGE_SHA256}"
        )

    file_version, content_version, entries = probe.read_lwlf(package_path)
    if file_version != EXPECTED_FILE_VERSION or content_version != EXPECTED_CONTENT_VERSION:
        raise InspectError(
            f"unsupported package version {file_version}/{content_version}"
        )
    entry_map = dict(entries)

    decoded: dict[str, bytes] = {}
    parsed: dict[str, dict] = {}
    module_report: dict[str, object] = {}
    for key, (name, expected_hash) in MODULES.items():
        if name not in entry_map:
            raise InspectError(f"current package missing {name}")
        body = probe.decode_lenc(entry_map[name])
        digest = hashlib.sha256(body).hexdigest()
        if digest != expected_hash:
            raise InspectError(
                f"decoded module hash mismatch for {name}: {digest}"
            )
        decoded[key] = body
        parsed[key] = parse_chunk(body)
        module_report[key] = {
            "entry": name,
            "decodedSha256": digest,
            "decodedBytes": len(body),
        }

    steal_methods = root_methods(parsed["steal_message"])
    manager_methods = root_methods(parsed["manager"])
    mark_data_methods = root_methods(parsed["mark_data"])

    checks = {
        "dispatchStealCommand": require_msg_define_mapping(
            parsed["msg_defines"],
            "DispatchSteal",
            "hero.dispatch.steal",
        ),
        "dispatchStealMessageMap": require_msg_map_mapping(
            parsed["msg_map"],
            "DispatchSteal",
            "Net.Msgs.DispatchTask.DispatchStealMessage",
        ),
        "dispatchStealOnCreate": require_constants(
            "DispatchStealMessage.OnCreate",
            steal_methods["OnCreate"],
            ["OnCreate", "sfsObj", "PutLong", "uuid", "PutInt", "targetServer"],
        ),
        "dispatchStealHandleMessage": require_constants(
            "DispatchStealMessage.HandleMessage",
            steal_methods["HandleMessage"],
            [
                "HandleMessage", "errorCode", "ShowTipsId", "reward",
                "fromDispatchStealMessage", "AddRewardsAndRes",
                "ActDispatchTaskDataManager", "ShowReward",
                "UpdateTodayNum", "UpdateSteal", "uuid",
            ],
        ),
        "markItemSendGate": require_constants(
            "DispatchTaskMarkItem steal send",
            find_proto(
                parsed["mark_item"],
                [
                    "GetTodayStealNum", "steal_count", "IsOpenCrossSteal",
                    "NeedIntercept", "DispatchSteal", "GetMissionUuid",
                    "GetMissionServerId",
                ],
            ),
            [
                "GetTodayStealNum", "steal_count", "IsOpenCrossSteal",
                "NeedIntercept", "DispatchSteal", "GetMissionUuid",
                "GetMissionServerId",
            ],
        ),
        "worldPointStealGate": require_constants(
            "UIWorldPointBtn Dispatch steal gate",
            find_proto(
                parsed["world_button"],
                [
                    "completionTime", "rewarded", "ownerUid", "protect_times",
                    "stealList", "GetTodayStealNum", "steal_count",
                    "steal_maxtimes", "IsOpenCrossSteal", "NeedIntercept",
                    "DispatchSteal",
                ],
            ),
            [
                "completionTime", "rewarded", "ownerUid", "protect_times",
                "stealList", "GetTodayStealNum", "steal_count",
                "steal_maxtimes", "IsOpenCrossSteal", "NeedIntercept",
                "DispatchSteal",
            ],
        ),
        "isOpenCrossSteal": require_constants(
            "ActDispatchTaskDataManager.IsOpenCrossSteal",
            manager_methods["IsOpenCrossSteal"],
            [
                "dispatchtask_setting", "k18", "GetCheckServerOpenTime",
                "GetServerOpenDaysByTimeStamp",
            ],
        ),
        "updateTodayNum": require_constants(
            "ActDispatchTaskDataManager.UpdateTodayNum",
            manager_methods["UpdateTodayNum"],
            [
                "todayStealNum", "todayAssistNum",
                "DispatchTaskTodayNumUpdate",
            ],
        ),
        "updateSteal": require_constants(
            "ActDispatchTaskDataManager.UpdateSteal",
            manager_methods["UpdateSteal"],
            ["GetOneMarkByMissionUuid", "UpdateSteal"],
        ),
        "markDataUpdateSteal": require_constants(
            "DispatchTaskMarkData.UpdateSteal",
            mark_data_methods["UpdateSteal"],
            ["StealList", "uid", "DispatchTaskStealSuccess", "GetMissionUuid"],
        ),
    }

    dispatch_named_entries = [
        (name, probe.decode_lenc(body))
        for name, body in entries
        if "dispatch" in name.lower()
    ]
    expiry_mentions = [
        name for name, body in dispatch_named_entries
        if b"taskExpireTime" in body
    ]
    if expiry_mentions:
        raise InspectError(
            "current Dispatch-named modules unexpectedly use taskExpireTime: "
            + ", ".join(expiry_mentions)
        )

    return {
        "ok": True,
        "package": str(package_path),
        "packageSha256": package_hash,
        "fileVersion": file_version,
        "contentVersion": content_version,
        "entryCount": len(entries),
        "modules": module_report,
        "checks": checks,
        "dispatchNamedTaskExpireTimeMentions": expiry_mentions,
        "dispatcherContract": {
            "commandKey": "DispatchSteal",
            "wireCommand": "hero.dispatch.steal",
            "messageModule": "Net.Msgs.DispatchTask.DispatchStealMessage",
            "hookSurface": "module-returned DispatchStealMessage class table HandleMessage",
        },
        "derivedRowContract": {
            "plunderAt": "completionTime + LwDispatchTask.protect_times * 60000",
            "stolenCount": "stealList.Count",
            "maxStealCount": "LwDispatchTask.steal_maxtimes",
            "taskExpireTime": "UNKNOWN; no current-v19 Dispatch-named Lua module uses taskExpireTime",
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "package",
        type=Path,
        nargs="?",
        default=Path(probe.paths()["data"]),
    )
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        result = inspect_current(args.package.resolve())
    except (InspectError, OSError, ValueError) as exc:
        parser.error(str(exc))
        return 2
    if args.json:
        print(json.dumps(result, indent=2))
    else:
        print("PASS current-v19 Dispatch plunder source inspection")
        print(f"packageSha256={result['packageSha256']}")
        print(
            f"fileVersion={result['fileVersion']} "
            f"contentVersion={result['contentVersion']} "
            f"entries={result['entryCount']}"
        )
        for name, row in result["checks"].items():
            print(
                f"{name}: lines {row['line']}-{row['lastLine']} "
                f"constants={len(row['verifiedConstants'])}"
            )
        print("taskExpireTime=UNKNOWN (no Dispatch-named current-v19 Lua usage)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
