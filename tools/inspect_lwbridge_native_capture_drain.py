#!/usr/bin/env python3
"""Recover native-capture drain ordering and budget from LWBridge 0.3.1."""

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
SERIALIZER = (0x38AB0, 0x39F15)

class InspectError(ValueError):
    pass

def require(ok: bool, message: str) -> None:
    if not ok:
        raise InspectError(message)
def extract_proxy(outer: bytes, outer_pe: pefile.PE, name: str) -> bytes:
    rva, size, expected = PROXIES[name]
    offset = int(outer_pe.get_offset_from_rva(rva))
    data = outer[offset:offset + size]
    digest = hashlib.sha256(data).hexdigest()
    require(digest == expected, f"{name} proxy SHA-256 mismatch: {digest}")
    return data

def inspect_proxy(name: str, data: bytes) -> dict[str, object]:
    pe = pefile.PE(data=data, fast_load=False)
    base = int(pe.OPTIONAL_HEADER.ImageBase)
    begin, end = SERIALIZER
    off = int(pe.get_offset_from_rva(begin))
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    rows = list(md.disasm(data[off:off + (end - begin)], base + begin))
    by_rva = {int(ins.address - base): ins for ins in rows}

    def ins(rva: int, mnemonic: str, op_text: str) -> None:
        item = by_rva.get(rva)
        require(item is not None, f"{name}: missing 0x{rva:X}")
        require((item.mnemonic, item.op_str) == (mnemonic, op_text),
                f"{name}: 0x{rva:X} got {item.mnemonic} {item.op_str}")
    ins(0x38C83, "mov", "esi, 0x400")

    ins(0x38CB0, "test", "rsi, rsi")
    ins(0x38CDD, "add", "qword ptr [rsp + 0x78], 0x3a0")
    require(by_rva[0x38D72].mnemonic == "dec", f"{name}: points pending decrement changed")
    ins(0x38DA1, "dec", "rsi")

    ins(0x38DD2, "test", "rsi, rsi")
    ins(0x38E00, "add", "qword ptr [rsp + 0x30], 0x1b0")
    require(by_rva[0x38EC4].mnemonic == "dec", f"{name}: marches pending decrement changed")
    ins(0x38EF3, "dec", "rsi")

    ins(0x38F24, "test", "rsi, rsi")
    ins(0x38F3E, "add", "qword ptr [rsp + 0x60], 4")
    require(by_rva[0x38FD0].mnemonic == "dec", f"{name}: point-removal pending decrement changed")
    ins(0x38FF6, "dec", "rsi")

    ins(0x39020, "test", "rsi, rsi")
    ins(0x3903C, "add", "qword ptr [rsp + 0x48], 8")
    require(by_rva[0x390F7].mnemonic == "dec", f"{name}: march-removal pending decrement changed")
    ins(0x3911D, "dec", "rsi")
    counter_base = 0x91100 if name == "secure" else 0x92100
    pending_targets = {
        0x38D72: counter_base,
        0x38EC4: counter_base + 0x40,
        0x38FD0: counter_base + 0x80,
        0x390F7: counter_base + 0xC0,
    }
    for rva, expected_target in pending_targets.items():
        item = by_rva[rva]
        mem = next((op for op in item.operands
                    if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP), None)
        require(mem is not None, f"{name}: pending counter at 0x{rva:X} not RIP-relative")
        target = int(item.address + item.size + mem.mem.disp - base)
        require(target == expected_target,
                f"{name}: pending counter target mismatch at 0x{rva:X}: 0x{target:X}")

    ins(0x39925, "mov", "edx, dword ptr [rcx + rbx*4]")
    ins(0x3992C, "call", "0x18002feb0")
    ins(0x39980, "mov", "dl, 0x22")
    ins(0x39990, "mov", "rdx, qword ptr [rdx + rbx*8]")
    ins(0x39997, "call", "0x180030320")
    ins(0x3999F, "mov", "dl, 0x22")

    return {
        "proxy": name,
        "sha256": hashlib.sha256(data).hexdigest(),
        "serializerRva": "0x38AB0-0x39F15",
        "sharedDrainBudget": 1024,
        "drainOrder": ["points", "marches", "pointRemovals", "marchRemovals"],
        "pendingCounterRvas": {
            "points": f"0x{counter_base:X}",
            "marches": f"0x{counter_base + 0x40:X}",
            "pointRemovals": f"0x{counter_base + 0x80:X}",
            "marchRemovals": f"0x{counter_base + 0xC0:X}",
        },
    }
def inspect(binary: Path) -> dict[str, object]:
    outer = binary.read_bytes()
    digest = hashlib.sha256(outer).hexdigest()
    require(digest == EXPECTED_SHA256, f"unsupported reference SHA-256: {digest}")
    outer_pe = pefile.PE(data=outer, fast_load=False)
    proxies = [
        inspect_proxy(name, extract_proxy(outer, outer_pe, name))
        for name in ("secure", "plain")
    ]
    return {
        "findingId": "LWB-R8-081",
        "date": "2026-09-27",
        "evidenceStatus": "RECOVERED CONTRACT",
        "sourceIdentity": {"path": str(binary), "sha256": digest},
        "recoveredResult": {
            "sharedDrainBudget": 1024,
            "drainOrder": ["points", "marches", "pointRemovals", "marchRemovals"],
            "budgetSemantics": "one shared counter is initialized once and decremented across all four drain loops without reset",
            "destructiveDrain": "each copied item is unlinked/freed and decrements its queue pending counter",
            "pointRemovalEncoding": "numeric 32-bit values",
            "marchRemovalEncoding": "quoted string values",
            "acks": "not part of this drain; R8-079 proves native-capture acks=[] and pendingAcks=0",
        },
        "proxies": proxies,
        "limits": [
            "does not recover MapScanTick block traversal or coordinate order",
            "does not recover producer retry/backoff rules",
            "does not establish how often the serializer is called by the outer application beyond separately recovered pump behavior",
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
    rendered = json.dumps(result, indent=2, sort_keys=True)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered + "\n", encoding="utf-8")
    print(rendered)
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
