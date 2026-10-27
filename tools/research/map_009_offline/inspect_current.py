#!/usr/bin/env python3
"""Hash-locked read-only inspection of the installed Last War v24 Lua package.

Lineage: compact_lua53.py selects only parser primitives from
archive/research-offline-controller:tools/inspect_current_dispatch_plunder_sources.py.
The register-aware root method mapper and disassembly layout adapt
archive/research-offline-controller:tools/lwbridge317/inspect_map_provider_semantics.py.
No game process, writes to installed files, or live-probe main() calls.
"""
from __future__ import annotations

import argparse
import fnmatch
import hashlib
import json
import sys
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS.parents[1] / 'src' / 'LWBridge.GameScripts'))
sys.path.insert(0, str(Path(__file__).resolve().parent))
import run_live_resource_probe as probe  # noqa: E402
import compact_lua53 as lua  # noqa: E402

PACKAGE_SHA256 = "d520dcd3b6f2b2c02ec3c4e42c2dc34fada513a4af955337401a2868d90ab495"
ASSEMBLY_SHA256 = "bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e"
FILE_VERSION = 3
CONTENT_VERSION = 24
ENTRY_COUNT = 18741


def load_entries(path: Path) -> dict[str, bytes]:
    with path.open("rb") as file:
        digest = hashlib.file_digest(file, "sha256").hexdigest()
    if digest != PACKAGE_SHA256:
        raise ValueError(f"unexpected installed v24 LWLF hash: {digest}")
    fv, cv, entries = probe.read_lwlf(path)
    if (fv, cv, len(entries)) != (FILE_VERSION, CONTENT_VERSION, ENTRY_COUNT):
        raise ValueError(f"unexpected LWLF identity: {fv}, {cv}, {len(entries)}")
    return dict(entries)


def walk(proto: dict, path: str = "root"):
    yield path, proto
    for index, child in enumerate(proto["children"]):
        yield from walk(child, f"{path}/{index}")


def root_methods(root: dict) -> dict[str, str]:
    constants, children = root["constants"], root["children"]
    registers = {}
    result = {}
    for instruction in root["code"]:
        op = lua.opcode(instruction)
        a, b, c = (lua.instruction_a(instruction), lua.instruction_b(instruction),
                   lua.instruction_c(instruction))
        if op == "CLOSURE":
            index = lua.instruction_bx(instruction)
            registers[a] = index if index < len(children) else None
        elif op == "MOVE":
            registers[a] = registers.get(b)
        elif op == "SETTABLE" and b & 256 and not c & 256:
            ci = b & 255
            if ci < len(constants) and isinstance(constants[ci], str) and registers.get(c) is not None:
                result[constants[ci]] = f"root/{registers[c]}"
        elif op in {"LOADK", "LOADKX", "LOADBOOL", "LOADNIL", "GETUPVAL",
                    "GETTABUP", "GETTABLE", "NEWTABLE", "SELF", "ADD", "SUB",
                    "MUL", "MOD", "POW", "DIV", "IDIV", "BAND", "BOR", "BXOR",
                    "SHL", "SHR", "UNM", "BNOT", "NOT", "LEN", "CONCAT", "TESTSET", "VARARG"}:
            registers.pop(a, None)
    return result


def rk(value: int, constants: list) -> str:
    if value & 0x100:
        i = value & 0xff
        return f"K[{i}]={constants[i]!r}" if i < len(constants) else f"K[{i}]!"
    return f"R[{value}]"


def disassemble(proto: dict, near: str | None = None) -> list[str]:
    lines = []
    constants = proto["constants"]
    for pc, code in enumerate(proto["code"]):
        op = lua.opcode(code)
        a, b, c = lua.instruction_a(code), lua.instruction_b(code), lua.instruction_c(code)
        bx = lua.instruction_bx(code)
        if op == "LOADK":
            detail = f"R[{a}] <- K[{bx}]={constants[bx]!r}"
        elif op == "CLOSURE":
            detail = f"R[{a}] <- child[{bx}]"
        elif op == "JMP":
            detail = f"A={a} sBx={bx - 131071} target={pc + 1 + bx - 131071}"
        elif op in {"GETTABUP", "GETTABLE", "SETTABUP", "SETTABLE", "SELF", "EQ", "LT", "LE", "ADD", "SUB", "MUL", "DIV", "IDIV"}:
            detail = f"A={a} B={rk(b, constants)} C={rk(c, constants)}"
        else:
            detail = f"A={a} B={b} C={c} Bx={bx}"
        line = proto["lines"][pc] if pc < len(proto["lines"]) else "?"
        lines.append(f"pc={pc:04d} sourceLine={line} {op:9s} {detail}")
    if near:
        matches = [i for i, line in enumerate(lines) if near.lower() in line.lower()]
        selected = {i for m in matches for i in range(max(0, m - 12), min(len(lines), m + 15))}
        return [line for i, line in enumerate(lines) if i in selected]
    return lines


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--package", type=Path, default=probe.paths()["data"])
    ap.add_argument("--entry", action="append", help="glob module name (repeatable)")
    ap.add_argument("--catalog", action="store_true", help="list names, with --entry patterns")
    ap.add_argument("--method", help="named root method or prototype path (root/0/...)")
    ap.add_argument("--find", help="string to locate within prototypes")
    ap.add_argument("--instructions", action="store_true")
    ap.add_argument("--near", help="filter instructions around matching text")
    ap.add_argument("--output", type=Path, help="write report to ignored scratch JSON")
    args = ap.parse_args()
    entries = load_entries(Path(args.package))
    patterns = args.entry or []
    names = [name for name in entries if any(fnmatch.fnmatchcase(name, pat) for pat in patterns)]
    if args.catalog:
        print("\n".join(names))
        return
    if not names:
        ap.error("use --entry to select current-package modules")
    report = {}
    for name in names:
        encoded = entries[name]
        decoded = probe.decode_lenc(encoded)
        root = lua.parse_chunk(decoded)
        methods = root_methods(root)
        protos = dict(walk(root))
        matching = [(path, x) for path, x in protos.items()
                    if not args.find or args.find.lower() in "\n".join(
                        str(v) for v in x["constants"]).lower()]
        if args.method:
            path = methods.get(args.method, args.method)
            matching = [(path, protos[path])] if path in protos else []
        report[name] = {
            "encodedSha256": hashlib.sha256(encoded).hexdigest(),
            "decodedSha256": hashlib.sha256(decoded).hexdigest(),
            "methods": methods,
            "matches": [{"path": path, "lines": [p["line"], p["lastLine"]],
                         "params": p["params"], "constants": p["constants"],
                         "codeCount": len(p["code"]),
                         **({"instructions": disassemble(p, args.near)} if args.instructions else {})}
                        for path, p in matching],
        }
    content = json.dumps(report, ensure_ascii=False, indent=2) + "\n"
    if args.output:
        if "map-009-offline" not in str(args.output.resolve()).lower():
            raise ValueError("raw method reports must stay under artifacts/map-009-offline")
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(content, encoding="utf-8")
        print(f"wrote {len(report)} modules -> {args.output}")
    else:
        print(content)


if __name__ == "__main__":
    main()
