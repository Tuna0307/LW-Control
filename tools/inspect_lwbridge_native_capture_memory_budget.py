#!/usr/bin/env python3
"""Recover native capture memory-budget accounting from LWBridge 0.3.1."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
PROXIES = {
    "secure": (0x987F74, 0x95800, "481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400", -0x1000),
    "plain": (0xA1D774, 0x96000, "c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794", 0),
}

class InspectError(ValueError):
    pass

def require(ok: bool, message: str) -> None:
    if not ok:
        raise InspectError(message)
POINT_STRINGS = [
    ("ownerUid", 0x1C0), ("allianceId", 0x1E8), ("playerName", 0x210),
    ("alAbbr", 0x238), ("name", 0x260), ("gatherUid", 0x288),
    ("gatherAllianceId", 0x2B0), ("runtimeClass", 0x2D8), ("eventId", 0x300),
    ("allianceAbbr", 0x328), ("ownerName", 0x350), ("killerId", 0x378),
]
MARCH_STRINGS = [
    ("ownerUid", 0xA8), ("ownerName", 0xD0), ("allianceUid", 0xF8),
    ("allianceName", 0x120), ("allianceAbbr", 0x148),
]

def extract_proxy(outer: bytes, outer_pe: pefile.PE, name: str) -> bytes:
    rva, size, expected, _ = PROXIES[name]
    offset = int(outer_pe.get_offset_from_rva(rva))
    data = outer[offset:offset + size]
    digest = hashlib.sha256(data).hexdigest()
    require(digest == expected, f"{name} proxy SHA-256 mismatch: {digest}")
    return data
def inspect_proxy(name: str, data: bytes) -> dict[str, object]:
    pe = pefile.PE(data=data, fast_load=False)
    base = int(pe.OPTIONAL_HEADER.ImageBase)
    delta = PROXIES[name][3]
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True

    def instruction(rva: int):
        off = int(pe.get_offset_from_rva(rva))
        return next(md.disasm(data[off:off + 16], base + rva))

    def rip_target(ins) -> int | None:
        for operand in ins.operands:
            if operand.type == X86_OP_MEM and operand.mem.base == X86_REG_RIP:
                return int(ins.address + ins.size + operand.mem.disp - base)
        return None

    def direct_target(ins) -> int | None:
        if not ins.operands or ins.operands[0].type != X86_OP_IMM:
            return None
        return int(ins.operands[0].imm - base)

    def expect(rva: int, mnemonic: str, op_text: str | None = None):
        ins = instruction(rva)
        require(ins.mnemonic == mnemonic, f"{name}: 0x{rva:X} mnemonic {ins.mnemonic}")
        if op_text is not None:
            require(ins.op_str == op_text, f"{name}: 0x{rva:X} operand {ins.op_str}")
        return ins
    def expect_rip(rva: int, mnemonic: str, target: int):
        ins = expect(rva, mnemonic)
        actual = rip_target(ins)
        require(actual == target + delta,
                f"{name}: 0x{rva:X} RIP target 0x{actual:X} != 0x{target + delta:X}")
        return ins

    def expect_direct(rva: int, mnemonic: str, target: int):
        ins = expect(rva, mnemonic)
        actual = direct_target(ins)
        require(actual == target,
                f"{name}: 0x{rva:X} direct target 0x{actual:X} != 0x{target:X}")
        return ins

    # Shared growth-admission helper: false means fits; true means exceeds 32 MiB.
    expect(0x36C73, "cmp", "rdx, rcx")
    expect(0x36C76, "jbe")
    expect_rip(0x36C78, "mov", 0x91FB8)
    expect(0x36C7F, "mov", "eax, 0x2000000")
    expect(0x36C84, "cmp", "rcx, rax")
    expect(0x36C87, "jae")
    expect(0x36C89, "sub", "rdx, r8")
    expect(0x36C8C, "sub", "rax, rcx")
    expect(0x36C8F, "cmp", "rdx, rax")
    expect(0x36C92, "ja")
    expect(0x36C94, "xor", "al, al")
    expect(0x36C97, "mov", "al, 1")
    # Optional-string layout proof: +0x10 is logical length, +0x18 is capacity.
    expect(0x3883D, "cmp", "qword ptr [rax + 0x18], 0xf")
    expect(0x3884C, "mov", "r8, qword ptr [rax + 0x10]")

    march_checks = [
        (0x3B280, 0xC8, 0x3B289, 0xC0),
        (0x3B29E, 0xF0, 0x3B2A7, 0xE8),
        (0x3B2B6, 0x118, 0x3B2BF, 0x110),
        (0x3B2CE, 0x140, 0x3B2D7, 0x138),
        (0x3B2E6, 0x168, 0x3B2EF, 0x160),
    ]
    expect(0x3B290, "add", "rdx, 0x1b1")
    expect(0x3B299, "mov", "edx, 0x1b0")
    for cmp_rva, presence, mov_rva, capacity in march_checks:
        expect(cmp_rva, "cmp", f"byte ptr [rcx + 0x{presence:x}], 0")
        expect(mov_rva, "mov")
        require(f"[rcx + 0x{capacity:x}]" in instruction(mov_rva).op_str,
                f"{name}: march capacity offset changed at 0x{mov_rva:X}")
    point_checks = [
        (0x3B329, 0x1E0, 0x3B332, 0x1D8),
        (0x3B347, 0x208, 0x3B350, 0x200),
        (0x3B35F, 0x230, 0x3B368, 0x228),
        (0x3B377, 0x258, 0x3B380, 0x250),
        (0x3B38F, 0x280, 0x3B398, 0x278),
        (0x3B3A7, 0x2A8, 0x3B3B0, 0x2A0),
        (0x3B3BE, 0x2D0, 0x3B3C7, 0x2C8),
        (0x3B3D5, 0x2F8, 0x3B3DE, 0x2F0),
        (0x3B3ED, 0x320, 0x3B3F6, 0x318),
        (0x3B405, 0x348, 0x3B40E, 0x340),
        (0x3B41C, 0x370, 0x3B425, 0x368),
        (0x3B433, 0x398, 0x3B43C, 0x390),
    ]
    expect(0x3B339, "add", "rdx, 0x3a1")
    expect(0x3B342, "mov", "edx, 0x3a0")
    for cmp_rva, presence, mov_rva, capacity in point_checks:
        expect(cmp_rva, "cmp", f"byte ptr [rcx + 0x{presence:x}], 0")
        expect(mov_rva, "mov")
        require(f"[rcx + 0x{capacity:x}]" in instruction(mov_rva).op_str,
                f"{name}: point capacity offset changed at 0x{mov_rva:X}")
    # Point replacement computes old/new footprints, checks only growth, then applies delta.
    expect_direct(0x35E29, "call", 0x3B320)
    expect_direct(0x35FE4, "call", 0x36C70)
    expect(0x35FE9, "test", "al, al")
    expect_direct(0x35FED, "call", 0x3A210)
    expect(0x36016, "sub", "rbx, rsi")
    expect_rip(0x36019, "add", 0x91FB8)

    # March producer inlines the same growth-only 32 MiB admission rule.
    expect_rip(0x34B23, "mov", 0x91FB8)
    expect(0x34B2A, "cmp", "rbx, r14")
    expect(0x34B2D, "jbe")
    expect(0x34B2F, "mov", "ecx, 0x2000000")
    expect(0x34B34, "cmp", "rdx, rcx")
    expect(0x34B37, "jae")
    expect(0x34B39, "mov", "rax, rbx")
    expect(0x34B3C, "sub", "rax, r14")
    expect(0x34B3F, "sub", "rcx, rdx")
    expect(0x34B42, "cmp", "rax, rcx")
    expect(0x34B45, "jbe")
    expect_direct(0x34B47, "call", 0x3A210)
    expect_rip(0x34BF1, "mov", 0x91FB8)
    expect(0x34BF8, "sub", "rbx, r14")
    expect(0x34BFB, "add", "rdx, rbx")
    expect_rip(0x34BFE, "mov", 0x91FB8)

    # Both full-record drains release their exact accounted footprint from the same counter.
    expect_direct(0x38CBD, "call", 0x3B320)
    expect_rip(0x38CC2, "sub", 0x91FB8)
    expect_direct(0x38DDF, "call", 0x3B280)
    expect_rip(0x38DE4, "sub", 0x91FB8)

    # Queue nodes have 0x18 bytes of linkage outside the accounted record footprint.
    expect(0x38D91, "mov", "edx, 0x3b8")
    expect(0x38EE3, "mov", "edx, 0x1c8")

    # Run-ID reset clears all pending queues, dropped count and the byte-accounting counter.
    for rva, target in [
        (0x38C10, 0x920F0), (0x38C1C, 0x92130),
        (0x38C28, 0x92170), (0x38C34, 0x921B0),
    ]:
        expect_rip(rva, "lea", target)
    expect_direct(0x38C17, "call", 0x3C710)
    expect_direct(0x38C23, "call", 0x3C7E0)
    expect_direct(0x38C2F, "call", 0x3C8B0)
    expect_direct(0x38C3B, "call", 0x3C960)
    expect_rip(0x38C40, "mov", 0x91FB0)
    expect_rip(0x38C47, "mov", 0x91FB8)
    expect_rip(0x3A22E, "inc", 0x91FB0)
    return {
        "proxy": name,
        "sha256": hashlib.sha256(data).hexdigest(),
        "budgetBytes": 0x2000000,
        "budgetCounterRva": f"0x{0x91FB8 + delta:X}",
        "droppedCounterRva": f"0x{0x91FB0 + delta:X}",
        "pointFootprint": {
            "fixedBytes": 0x3A0,
            "optionalStrings": [
                {"key": key, "slotOffset": f"0x{slot:X}",
                 "capacityOffset": f"0x{slot + 0x18:X}",
                 "presenceOffset": f"0x{slot + 0x20:X}"}
                for key, slot in POINT_STRINGS
            ],
        },
        "marchFootprint": {
            "fixedBytes": 0x1B0,
            "optionalStrings": [
                {"key": key, "slotOffset": f"0x{slot:X}",
                 "capacityOffset": f"0x{slot + 0x18:X}",
                 "presenceOffset": f"0x{slot + 0x20:X}"}
                for key, slot in MARCH_STRINGS
            ],
        },
        "containerNodeOverheadBytes": 0x18,
    }
def inspect(binary: Path) -> dict[str, object]:
    outer = binary.read_bytes()
    digest = hashlib.sha256(outer).hexdigest()
    require(digest == EXPECTED_SHA256, f"unsupported reference SHA-256: {digest}")
    outer_pe = pefile.PE(data=outer, fast_load=False)
    proxies = [inspect_proxy(name, extract_proxy(outer, outer_pe, name))
               for name in ("secure", "plain")]
    return {
        "findingId": "LWB-R8-085",
        "date": "2026-09-27",
        "evidenceStatus": "RECOVERED CONTRACT",
        "sourceIdentity": {"path": str(binary), "sha256": digest},
        "recoveredResult": {
            "budgetBytes": 0x2000000,
            "budgetMiB": 32,
            "scope": "one shared point/march pending-record memory counter",
            "pointFormula": "0x3A0 + sum(capacity+1 for each present optional string)",
            "marchFormula": "0x1B0 + sum(capacity+1 for each present optional string)",
            "growthOnlyAdmission": True,
            "nodeOverheadExcluded": True,
            "overflowEffect": "native dropped counter increments through 0x3A210",
            "reset": "run-ID change clears queues, dropped counter, and byte counter",
        },
        "proxies": proxies,
        "limits": [
            "does not inspect or cross protected native region 0x3F8E0-0x40A6D",
            "does not recover XluaBridgeMapScanTick traversal, coordinate order, request pacing, or retry/backoff",
            "budget accounting covers full point/march pending records; removal queues use fixed scalar identities and are not charged by this byte counter",
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
