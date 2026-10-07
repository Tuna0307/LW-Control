#!/usr/bin/env python3
"""Read-only semantic inspector for current-v22 Map protected-provider Lua bodies.

Last War v22 stores Lua 5.3 chunks in format=1 with the standard Lua-5.3 size_t
header byte omitted. This tool parses that compact form directly. Normalization
is available only as an in-memory/scratch transformation for compatible Lua-5.3
execution and never rewrites the installed package.
"""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import sys
from pathlib import Path
from typing import Any, Iterable

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
import run_live_resource_probe as probe  # noqa: E402

_parser_spec = importlib.util.spec_from_file_location(
    "_dispatch_parser", TOOLS / "inspect_current_dispatch_plunder_sources.py"
)
if _parser_spec is None or _parser_spec.loader is None:
    raise RuntimeError("unable to load compact Lua parser")
_dispatch = importlib.util.module_from_spec(_parser_spec)
_parser_spec.loader.exec_module(_dispatch)

EXPECTED_PACKAGE_SHA256 = "248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22"
EXPECTED_FILE_VERSION = 3
EXPECTED_CONTENT_VERSION = 22

SEMANTIC_ENTRIES = [
    "Net/Msgs/RadarCenter/DetectEventGetTreasureClaimInfoMessage.luac",
    "Net/Msgs/RadarCenter/DetectEventClaimTreasureMessage.luac",
    "Net/Msgs/RadarCenter/PushDetectTreasureClaimMessage.luac",
    "DataCenter/RadarCenterDataManager/DetectEventGetTreasureClaimInfo.luac",
    "DataCenter/RadarCenterDataManager/DetectEventTreasureClaimPlayerInfo.luac",
    "DataCenter/RadarCenterDataManager/RadarCenterDataManager.luac",
    "Util/UIUtil.luac",
    "Util/MarchUtil.luac",
    "UI/UIWorldPoint/Component/UIWorldPointBtn.luac",
    "Scene/WorldShortTimeEffectBubble/WorldShortTimeEffectBubbleManager.luac",
    "DataCenter/ActivityListData/ActGhostrecon/ActGhostreconManager.luac",
    "DataCenter/ActivityListData/ActGhostrecon/ActGhostreconTaskInfo.luac",
    "DataCenter/ActivityListData/ActGhostrecon/ActGhostreconTaskTemplate.luac",
    "Net/Msgs/Ghostrecon/GhostreconGetTaskListMessage.luac",
    "Net/Msgs/Ghostrecon/GhostReconStealMessage.luac",
    "Net/Msgs/Ghostrecon/PushGhostReconStealMessage.luac",
    "DataCenter/ActivityListData/ActivityListDataManager.luac",
]


def normalize_compact_lua53(data: bytes) -> bytes:
    """Convert verified Last War compact Lua-5.3 header to standard form in memory."""
    if data[:6] != b"\x1bLua\x53\x01":
        raise ValueError("not a Last War Lua-5.3 compact-format chunk")
    if data[6:12] != b"\x19\x93\r\n\x1a\n":
        raise ValueError("unexpected LUAC_DATA")
    if data[12:16] != bytes((4, 4, 8, 8)):
        raise ValueError("unexpected compact serialized-size header")
    # Standard Lua 5.3 is 04 08 04 08 08 (int,size_t,instruction,integer,number).
    return data[:5] + b"\x00" + data[6:13] + b"\x08" + data[13:]


def compact_from_standard_lua53(data: bytes) -> bytes:
    """Inverse transform used only for independently compiled fixture validation."""
    if data[:6] != b"\x1bLua\x53\x00" or data[12:17] != bytes((4, 8, 4, 8, 8)):
        raise ValueError("not the expected standard Lua-5.3 chunk layout")
    return data[:5] + b"\x01" + data[6:13] + data[14:]


def _rk(value: int, constants: list[Any]) -> str:
    if value & 0x100:
        index = value & 0xFF
        item = constants[index] if index < len(constants) else "<out-of-range>"
        return f"K[{index}]={item!r}"
    return f"R[{value}]"


def disassemble(proto: dict[str, Any]) -> list[dict[str, Any]]:
    constants = proto["constants"]
    result: list[dict[str, Any]] = []
    for pc, instruction in enumerate(proto["code"]):
        op = _dispatch.opcode(instruction)
        a = _dispatch.instruction_a(instruction)
        b = _dispatch.instruction_b(instruction)
        c = _dispatch.instruction_c(instruction)
        bx = _dispatch.instruction_bx(instruction)
        sbx = bx - 131071
        if op == "LOADK":
            text = f"R[{a}] <- K[{bx}]={constants[bx]!r}"
        elif op == "CLOSURE":
            child = proto["children"][bx]
            text = f"R[{a}] <- child[{bx}] lines={child['line']}-{child['lastLine']}"
        elif op == "SETTABLE":
            text = f"R[{a}][{_rk(b, constants)}] <- {_rk(c, constants)}"
        elif op in ("GETTABLE", "GETTABUP", "SELF"):
            text = f"A={a} B={_rk(b, constants)} C={_rk(c, constants)}"
        elif op in ("EQ", "LT", "LE", "ADD", "SUB", "MUL", "MOD", "POW", "DIV", "IDIV"):
            text = f"A={a} B={_rk(b, constants)} C={_rk(c, constants)}"
        elif op == "JMP":
            text = f"A={a} sBx={sbx} target={pc + 1 + sbx}"
        else:
            text = f"A={a} B={b} C={c} Bx={bx}"
        result.append(
            {
                "pc": pc,
                "line": proto["lines"][pc] if pc < len(proto["lines"]) else None,
                "raw": f"0x{instruction:08x}",
                "op": op,
                "operands": text,
            }
        )
    return result


def root_methods(root: dict[str, Any]) -> dict[str, dict[str, Any]]:
    """Map class-table method names to closures using register-aware root dataflow."""
    constants = root["constants"]
    children = root["children"]
    registers: dict[int, int] = {}
    methods: dict[str, dict[str, Any]] = {}
    for instruction in root["code"]:
        op = _dispatch.opcode(instruction)
        a = _dispatch.instruction_a(instruction)
        b = _dispatch.instruction_b(instruction)
        c = _dispatch.instruction_c(instruction)
        if op == "CLOSURE":
            child_index = _dispatch.instruction_bx(instruction)
            if child_index < len(children):
                registers[a] = child_index
            continue
        if op == "MOVE":
            if b in registers:
                registers[a] = registers[b]
            else:
                registers.pop(a, None)
            continue
        if op == "SETTABLE" and (b & 0x100) and not (c & 0x100):
            key_index = b & 0xFF
            child_index = registers.get(c)
            if key_index < len(constants) and child_index is not None:
                key = constants[key_index]
                if isinstance(key, str):
                    methods[key] = children[child_index]
            continue
        # Root chunks in this corpus normally batch CLOSURE then SETTABLE. Avoid
        # false names by invalidating registers for obvious writers.
        if op in {
            "LOADK", "LOADKX", "LOADBOOL", "LOADNIL", "GETUPVAL", "GETTABUP",
            "GETTABLE", "NEWTABLE", "SELF", "ADD", "SUB", "MUL", "MOD", "POW",
            "DIV", "IDIV", "BAND", "BOR", "BXOR", "SHL", "SHR", "UNM", "BNOT",
            "NOT", "LEN", "CONCAT", "TESTSET", "CLOSURE", "VARARG"
        }:
            registers.pop(a, None)
    return methods


def walk_protos(root: dict[str, Any], path: str = "root") -> Iterable[tuple[str, dict[str, Any]]]:
    yield path, root
    for index, child in enumerate(root["children"]):
        yield from walk_protos(child, f"{path}/child[{index}]")


def find_by_strings(root: dict[str, Any], required: list[str]) -> list[tuple[str, dict[str, Any]]]:
    found = []
    for path, proto in walk_protos(root):
        values = {x for x in proto["constants"] if isinstance(x, str)}
        if all(item in values for item in required):
            found.append((path, proto))
    return found


def load_package(package: Path) -> tuple[dict[str, bytes], dict[str, Any]]:
    raw = package.read_bytes()
    digest = hashlib.sha256(raw).hexdigest()
    if digest != EXPECTED_PACKAGE_SHA256:
        raise ValueError(f"unsupported current package SHA-256 {digest}")
    fv, cv, entries = probe.read_lwlf(package)
    if (fv, cv) != (EXPECTED_FILE_VERSION, EXPECTED_CONTENT_VERSION):
        raise ValueError(f"unsupported package version {fv}/{cv}")
    return dict(entries), {
        "path": str(package),
        "sha256": digest,
        "fileVersion": fv,
        "contentVersion": cv,
        "entryCount": len(entries),
    }


def inspect_entry(entry_map: dict[str, bytes], name: str) -> dict[str, Any]:
    if name not in entry_map:
        raise ValueError(f"package missing {name}")
    encoded = entry_map[name]
    decoded = probe.decode_lenc(encoded)
    root = _dispatch.parse_chunk(decoded)
    methods = root_methods(root)
    return {
        "entry": name,
        "encodedSha256": hashlib.sha256(encoded).hexdigest(),
        "decodedSha256": hashlib.sha256(decoded).hexdigest(),
        "decodedBytes": len(decoded),
        "headerHex": decoded[:32].hex(),
        "normalizedSha256": hashlib.sha256(normalize_compact_lua53(decoded)).hexdigest(),
        "root": {
            "source": root["source"],
            "line": root["line"],
            "lastLine": root["lastLine"],
            "codeCount": len(root["code"]),
            "childCount": len(root["children"]),
        },
        "methods": {
            key: {
                "lines": [value["line"], value["lastLine"]],
                "params": value["params"],
                "stack": value["stack"],
                "constants": value["constants"],
                "prototypePath": next(
                    path for path, proto in walk_protos(root) if proto is value
                ),
                "instructions": disassemble(value),
            }
            for key, value in sorted(methods.items())
        },
    }


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("package", nargs="?", type=Path, default=Path(probe.paths()["data"]))
    ap.add_argument("--entry", action="append", default=[])
    ap.add_argument("--find", action="append", default=[],
                    help="comma-separated constants; reports matching prototype paths")
    ap.add_argument("--output", type=Path)
    args = ap.parse_args()

    entry_map, package_info = load_package(args.package.resolve())
    names = args.entry or SEMANTIC_ENTRIES
    report: dict[str, Any] = {"schema": 1, "package": package_info, "entries": {}}
    for name in names:
        row = inspect_entry(entry_map, name)
        if args.find:
            root = _dispatch.parse_chunk(probe.decode_lenc(entry_map[name]))
            row["findings"] = {}
            for item in args.find:
                required = [x for x in item.split(",") if x]
                row["findings"][item] = [
                    {
                        "prototypePath": path,
                        "lines": [proto["line"], proto["lastLine"]],
                        "constants": [x for x in proto["constants"] if isinstance(x, str)],
                        "instructions": disassemble(proto),
                    }
                    for path, proto in find_by_strings(root, required)
                ]
        report["entries"][name] = row

    text = json.dumps(report, indent=2, ensure_ascii=False) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(text, encoding="utf-8")
    else:
        print(text, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
