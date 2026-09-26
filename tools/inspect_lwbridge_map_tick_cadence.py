#!/usr/bin/env python3
"""Recover the outer xLua proxy pump cadence for MapScanTick in LWBridge 0.3.1."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86_const import X86_OP_MEM, X86_REG_RIP

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
PROXIES = {
    "secure": (0x987F74, 0x95800, "481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400"),
    "plain": (0xA1D774, 0x96000, "c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794"),
}
PUMP = (0x1F800, 0x2014F)
EXPECTED = {
    0x1F9C5: ("call", "qword ptr [rip + 0x4f7b5]"),
    0x1FB34: ("mov", "rax, r15"),
    0x1FB37: ("sub", "rax, qword ptr [rdi + 0x28]"),
    0x1FB3B: ("cmp", "rax, 0x32"),
    0x1FB3F: ("jb", "0x18001fb54"),
    0x1FB41: ("mov", "qword ptr [rdi + 0x28], r15"),
    0x1FB45: ("lea", "rdx, [rip + 0x526fc]"),
    0x1FB4F: ("call", "0x180013410"),
    0x1FB57: ("sub", "rax, qword ptr [rdi + 0x20]"),
    0x1FB5B: ("cmp", "rax, 0x10"),
    0x1FB61: ("mov", "qword ptr [rdi + 0x20], r15"),
    0x1FB65: ("lea", "rdx, [rip + 0x526f4]"),
    0x1FB6F: ("call", "0x180013410"),
    0x1FB77: ("sub", "rax, qword ptr [rdi + 0x18]"),
    0x1FB7B: ("cmp", "rax, 0xc8"),
    0x1FB87: ("mov", "qword ptr [rdi + 0x18], r15"),
    0x1FFF0: ("lea", "rdx, [rip + 0x522d9]"),
    0x1FFFA: ("call", "0x180013410"),
    0x2003F: ("sub", "rax, qword ptr [rdi + 0x30]"),
    0x20043: ("cmp", "rax, 0x1f4"),
    0x20049: ("jb", "0x18002005e"),
    0x2004B: ("mov", "qword ptr [rdi + 0x30], r15"),
    0x2004F: ("lea", "rdx, [rip + 0x52292]"),
    0x20059: ("call", "0x180013410"),
}
NAMES = {
    "XluaBridgeMapScanTick": 0x72248,
    "XluaBridgeInputPoll": 0x72260,
    "XluaBridgeNativeUpdate": 0x722D0,
    "XluaBridgePoll": 0x722E8,
}


class InspectError(ValueError):
    pass


def require(ok: bool, message: str) -> None:
    if not ok:
        raise InspectError(message)
def extract_proxy(outer: bytes, outer_pe: pefile.PE, name: str) -> bytes:
    rva, size, expected = PROXIES[name]
    offset = int(outer_pe.get_offset_from_rva(rva))
    data = outer[offset : offset + size]
    digest = hashlib.sha256(data).hexdigest()
    require(digest == expected, f"{name} proxy SHA-256 mismatch: {digest}")
    return data


def import_name_at(pe: pefile.PE, image_base: int, rva: int) -> tuple[str, str] | None:
    pe.parse_data_directories()
    for entry in getattr(pe, "DIRECTORY_ENTRY_IMPORT", []):
        dll = entry.dll.decode("ascii", "replace")
        for imp in entry.imports:
            if int(imp.address - image_base) == rva:
                return dll, (imp.name or b"").decode("ascii", "replace")
    return None


def inspect_proxy(name: str, data: bytes) -> dict:
    pe = pefile.PE(data=data, fast_load=False)
    base = int(pe.OPTIONAL_HEADER.ImageBase)
    begin, end = PUMP
    off = int(pe.get_offset_from_rva(begin))
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    instructions = list(md.disasm(data[off : off + (end - begin)], base + begin))
    by_rva = {int(ins.address - base): ins for ins in instructions}

    for rva, (mnemonic, op_str) in EXPECTED.items():
        ins = by_rva.get(rva)
        require(ins is not None, f"{name}: missing instruction 0x{rva:X}")
        require((ins.mnemonic, ins.op_str) == (mnemonic, op_str),
                f"{name}: 0x{rva:X} got {ins.mnemonic} {ins.op_str}")
    tick_call = by_rva[0x1FB45]
    update_call = by_rva[0x1FFF0]
    poll_call = by_rva[0x2004F]
    input_call = by_rva[0x1FB65]
    for ins, expected_rva, label in (
        (tick_call, NAMES["XluaBridgeMapScanTick"], "MapScanTick"),
        (input_call, NAMES["XluaBridgeInputPoll"], "InputPoll"),
        (update_call, NAMES["XluaBridgeNativeUpdate"], "NativeUpdate"),
        (poll_call, NAMES["XluaBridgePoll"], "Poll"),
    ):
        mem = next((op for op in ins.operands if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP), None)
        require(mem is not None, f"{name}: {label} name load is not RIP-relative")
        target = int(ins.address + ins.size + mem.mem.disp - base)
        require(target == expected_rva, f"{name}: {label} name RVA mismatch 0x{target:X}")

    timer_import = import_name_at(pe, base, 0x6F180)
    require(timer_import == ("KERNEL32.dll", "GetTickCount64"),
            f"{name}: timer import mismatch {timer_import!r}")

    return {
        "proxy": name,
        "sha256": hashlib.sha256(data).hexdigest(),
        "pumpRva": "0x1F800-0x2014F",
        "timer": {"iatRva": "0x6F180", "import": "KERNEL32.dll!GetTickCount64", "callRva": "0x1F9C5"},
        "cadence": {
            "XluaBridgeMapScanTick": {"thresholdMs": 50, "lastRunOffset": "0x28", "callRva": "0x1FB4F"},
            "XluaBridgeInputPoll": {"thresholdMs": 16, "lastRunOffset": "0x20", "callRva": "0x1FB6F"},
            "XluaBridgeNativeUpdate": {"thresholdMs": 200, "lastRunOffset": "0x18", "callRva": "0x1FFFA"},
            "XluaBridgePoll": {"thresholdMs": 500, "lastRunOffset": "0x30", "callRva": "0x20059"},
        },
    }
def inspect(binary: Path) -> dict:
    outer = binary.read_bytes()
    digest = hashlib.sha256(outer).hexdigest()
    require(digest == EXPECTED_SHA256, f"unsupported LWBridge SHA-256 {digest}")
    outer_pe = pefile.PE(data=outer, fast_load=False)
    proxies = [inspect_proxy(name, extract_proxy(outer, outer_pe, name)) for name in ("secure", "plain")]
    return {
        "findingId": "LWB-R8-080",
        "date": "2026-09-27",
        "evidenceStatus": "RECOVERED CONTRACT",
        "sourceIdentity": {"path": str(binary), "sha256": digest},
        "recoveredResult": {
            "mapScanTickOuterGateMs": 50,
            "timerSource": "GetTickCount64",
            "semantics": (
                "on each serviced proxy pump, unsigned elapsed now-lastMapTick is compared with 50 ms; "
                "below threshold skips, otherwise lastMapTick is updated to now before XluaBridgeMapScanTick is called"
            ),
            "relatedPumpCadenceMs": {"inputPoll": 16, "nativeUpdate": 200, "poll": 500},
            "modeBoundary": (
                "the recovered outer MapScanTick gate uses a fixed 50 ms constant and contains no Normal/Fast branch; "
                "mode-specific work inside XluaBridgeMapScanTick remains unrecovered"
            ),
        },
        "proxies": proxies,
        "limits": [
            "50 ms is a minimum elapsed-time gate serviced by the proxy pump, not a guaranteed exact-period timer",
            "does not recover how many blocks/requests XluaBridgeMapScanTick advances per invocation",
            "does not recover traversal order, request coordinates, retry/backoff, or completion/drain policy",
            "does not change production scanner behavior",
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        result = inspect(args.binary)
    except (InspectError, OSError, pefile.PEFormatError) as exc:
        parser.error(str(exc))
    text = json.dumps(result, indent=2, sort_keys=True)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(text + "\n", encoding="utf-8")
    print(text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
