#!/usr/bin/env python3
"""Recover native-capture ACK serialization constants from LWBridge 0.3.1.

Static/read-only and hash-gated. The verifier extracts both embedded xLua
proxies from the verified reference EXE and proves that their native-capture
JSON serializer emits an empty ACK array and zero pending ACK count, while the
other four pending queue counts are serialized dynamically.
"""

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
LITERALS = {
    "acks": b'],"acks":[],"ready":',
    "pendingAcks": b',"pendingAcks":0',
    "pendingMarchRemovals": b',"pendingMarchRemovals":',
    "pendingPointRemovals": b',"pendingPointRemovals":',
    "pendingMarches": b',"pendingMarches":',
    "pendingPoints": b',"pendingPoints":',
    "dropped": b',"dropped":',
}
EXPECTED_XREFS = {
    "acks": 0x399D3,
    "pendingPoints": 0x399F1,
    "pendingMarches": 0x39A0B,
    "pendingPointRemovals": 0x39A25,
    "pendingMarchRemovals": 0x39A3F,
    "pendingAcks": 0x39A5A,
    "dropped": 0x39A69,
}


class InspectError(ValueError):
    pass


def require(condition: bool, message: str) -> None:
    if not condition:
        raise InspectError(message)


def extract_proxy(outer: bytes, outer_pe: pefile.PE, name: str) -> bytes:
    rva, size, expected = PROXIES[name]
    offset = int(outer_pe.get_offset_from_rva(rva))
    data = outer[offset : offset + size]
    digest = hashlib.sha256(data).hexdigest()
    require(digest == expected, f"{name} proxy SHA-256 mismatch: {digest}")
    return data
def inspect_proxy(name: str, data: bytes) -> dict:
    pe = pefile.PE(data=data, fast_load=False)
    base = int(pe.OPTIONAL_HEADER.ImageBase)
    begin, end = SERIALIZER
    off = int(pe.get_offset_from_rva(begin))

    literal_rvas: dict[str, int] = {}
    for label, literal in LITERALS.items():
        pos = data.find(literal)
        require(pos >= 0, f"{name}: missing literal {label}")
        literal_rvas[label] = int(pe.get_rva_from_offset(pos))

    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    instructions = list(md.disasm(data[off : off + (end - begin)], base + begin))
    by_rva = {int(ins.address - base): ins for ins in instructions}

    observed: dict[str, int] = {}
    for ins in instructions:
        for operand in ins.operands:
            if operand.type != X86_OP_MEM or operand.mem.base != X86_REG_RIP:
                continue
            target = int(ins.address + ins.size + operand.mem.disp - base)
            for label, rva in literal_rvas.items():
                if rva <= target < rva + len(LITERALS[label]):
                    observed[label] = int(ins.address - base)

    for label, expected in EXPECTED_XREFS.items():
        require(observed.get(label) == expected, f"{name}: {label} xref mismatch: {observed.get(label)!r}")

    def require_ins(rva: int, mnemonic: str, text: str) -> None:
        ins = by_rva.get(rva)
        require(ins is not None, f"{name}: missing instruction 0x{rva:X}")
        require(ins.mnemonic == mnemonic and ins.op_str == text,
                f"{name}: 0x{rva:X} got {ins.mnemonic} {ins.op_str}")

    # Four non-ACK pending counts append a field label and then a dynamic integer.
    require_ins(0x399F8, "call", "0x1800018d0")
    require_ins(0x39A00, "mov", "rdx, rdi")
    require_ins(0x39A03, "call", "0x180030550")
    require_ins(0x39A12, "call", "0x1800018d0")
    require_ins(0x39A1A, "mov", "rdx, r12")
    require_ins(0x39A1D, "call", "0x180030550")
    require_ins(0x39A2C, "call", "0x1800018d0")
    require_ins(0x39A34, "mov", "rdx, r13")
    require_ins(0x39A37, "call", "0x180030550")
    require_ins(0x39A46, "call", "0x1800018d0")
    require_ins(0x39A4E, "mov", "rdx, qword ptr [rbp - 0x70]")
    require_ins(0x39A52, "call", "0x180030550")

    # ACK count is embedded in the literal. The next field starts immediately.
    require_ins(0x39A61, "call", "0x1800018d0")
    require_ins(0x39A66, "mov", "rcx, rax")
    require_ins(0x39A69, "lea", "rdx, [rip + 0x39db8]")

    # ACK array is embedded empty in the literal preceding ready.
    require_ins(0x399D3, "lea", "rdx, [rip + 0x39ee6]")
    require_ins(0x399DE, "call", "0x1800018d0")

    return {
        "proxy": name,
        "sha256": hashlib.sha256(data).hexdigest(),
        "serializerRva": f"0x{begin:X}-0x{end:X}",
        "literalRvas": {k: f"0x{v:X}" for k, v in literal_rvas.items()},
        "xrefRvas": {k: f"0x{v:X}" for k, v in observed.items() if k in EXPECTED_XREFS},
    }
def inspect(binary: Path) -> dict:
    outer = binary.read_bytes()
    digest = hashlib.sha256(outer).hexdigest()
    require(digest == EXPECTED_SHA256, f"unsupported LWBridge SHA-256 {digest}")
    outer_pe = pefile.PE(data=outer, fast_load=False)

    proxy_results = [
        inspect_proxy(name, extract_proxy(outer, outer_pe, name))
        for name in ("secure", "plain")
    ]

    return {
        "findingId": "LWB-R8-079",
        "date": "2026-09-27",
        "evidenceStatus": "RECOVERED CONTRACT",
        "sourceIdentity": {"path": str(binary), "sha256": digest},
        "recoveredResult": {
            "acks": "native capture serializer emits []",
            "pendingAcks": "native capture serializer emits 0",
            "dynamicPendingCounts": [
                "pendingPoints",
                "pendingMarches",
                "pendingPointRemovals",
                "pendingMarchRemovals",
            ],
            "ownershipConclusion": (
                "the verified 0.3.1 native-capture envelope does not carry a dynamic ACK queue; "
                "ACK item consumption/drain must not be invented from host pendingAcks handling"
            ),
        },
        "proxies": proxy_results,
        "limits": [
            "does not recover XluaBridgeMapScanTick scheduling or block traversal order",
            "does not recover retry/backoff policy",
            "does not prove no acknowledgement concept exists elsewhere in protected script logic",
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
