#!/usr/bin/env python3
"""Hash-locked static verifier for LWBridge 0.3.1 Map ingestion paths."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86_const import X86_OP_MEM, X86_REG_RIP

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
IMAGE_BASE = 0x140000000

CALLS = {
    0x3400C1: 0x341FCC,
    0x3406D8: 0x2574CD,
    0x342330: 0x25F01D,
    0x25F2D1: 0x2785BA,
    0x25F368: 0x27A1E3,
    0x2575A8: 0x2785BA,
    0x25767E: 0x27A1E3,
}

FIELD_XREFS = {
    0x34204D: "scanRunId",
    0x34211F: "selectedTypes",
    0x3421D4: "serverId",
    0x3421FB: "tileWidth",
    0x342215: "tileHeight",
    0x34223F: "records",
    0x342440: "type",
    0x342594: "payload",
}

TABLE_XREFS = {
    0x25F361: "scan_records",
    0x257666: "map_records",
}

EVENT_LITERALS = (
    b"map.native.capture",
    b"map.scan.complete",
)


class InspectError(ValueError):
    pass


def _instruction_at(pe: pefile.PE, data: bytes, rva: int):
    offset = pe.get_offset_from_rva(rva)
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    rows = list(md.disasm(data[offset : offset + 16], IMAGE_BASE + rva, count=1))
    if not rows:
        raise InspectError(f"no instruction at RVA 0x{rva:X}")
    return rows[0]


def _rip_ascii(pe: pefile.PE, data: bytes, rva: int) -> str:
    ins = _instruction_at(pe, data, rva)
    for operand in ins.operands:
        if operand.type == X86_OP_MEM and operand.mem.base == X86_REG_RIP:
            va = ins.address + ins.size + operand.mem.disp
            offset = pe.get_offset_from_rva(va - IMAGE_BASE)
            end = offset
            while end < min(len(data), offset + 320) and 0x20 <= data[end] <= 0x7E:
                end += 1
            return data[offset:end].decode("ascii", "replace")
    raise InspectError(f"RVA 0x{rva:X} has no RIP-relative memory operand")


def inspect(path: Path) -> dict[str, object]:
    data = path.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if digest != EXPECTED_SHA256:
        raise InspectError(f"unsupported SHA-256 {digest}")

    pe = pefile.PE(data=data, fast_load=False)
    verified_calls: dict[str, str] = {}
    for rva, target in CALLS.items():
        ins = _instruction_at(pe, data, rva)
        if ins.mnemonic != "call" or int(ins.op_str, 16) != IMAGE_BASE + target:
            raise InspectError(
                f"call mismatch at RVA 0x{rva:X}: {ins.mnemonic} {ins.op_str}"
            )
        verified_calls[f"0x{rva:X}"] = f"0x{target:X}"

    verified_fields: dict[str, str] = {}
    for rva, expected in FIELD_XREFS.items():
        text = _rip_ascii(pe, data, rva)
        if expected not in text:
            raise InspectError(
                f"field mismatch at RVA 0x{rva:X}: expected {expected!r}, got {text!r}"
            )
        verified_fields[f"0x{rva:X}"] = expected

    verified_tables: dict[str, str] = {}
    for rva, expected in TABLE_XREFS.items():
        text = _rip_ascii(pe, data, rva)
        if not text.startswith(expected):
            raise InspectError(
                f"table mismatch at RVA 0x{rva:X}: expected {expected!r}, got {text!r}"
            )
        verified_tables[f"0x{rva:X}"] = expected

    length_check = _instruction_at(pe, data, 0x340083)
    packed_a = _instruction_at(pe, data, 0x34008D)
    packed_b = _instruction_at(pe, data, 0x34009A)
    if length_check.mnemonic != "cmp" or length_check.op_str != "rbx, 0xb":
        raise InspectError("map.records length compare changed")
    if packed_a.mnemonic != "movabs" or "0x6f6365722e70616d" not in packed_a.op_str:
        raise InspectError("map.records packed compare A changed")
    if packed_b.mnemonic != "movabs" or "0x7364726f6365722e" not in packed_b.op_str:
        raise InspectError("map.records packed compare B changed")

    events = {"map.records": ["packed compare at RVA 0x340083-0x3400AB"]}
    for literal in EVENT_LITERALS:
        hits = []
        cursor = 0
        while True:
            hit = data.find(literal, cursor)
            if hit < 0:
                break
            hits.append(hit)
            cursor = hit + 1
        if not hits:
            raise InspectError(f"missing event literal {literal!r}")
        events[literal.decode("ascii")] = [f"0x{x:X}" for x in hits]

    return {
        "sha256": digest,
        "commonMapEventHandler": "0x14033FFA1-0x140341913",
        "mapRecordsHandler": "0x140341FCC-0x140342926",
        "scanRecordBatchHelper": "0x14025F01D-0x14025F5C8",
        "nativeCapturePublishedHelper": "0x1402574CD-0x1402577E2",
        "normalizedRecordBuilder": "0x1402785BA-0x1402793BE",
        "sharedUpsert": "0x14027A1E3-0x14027AA73",
        "verifiedCalls": verified_calls,
        "verifiedFieldXrefs": verified_fields,
        "verifiedTableXrefs": verified_tables,
        "eventCompilerMarkers": events,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        result = inspect(args.binary)
    except (InspectError, OSError, pefile.PEFormatError, ValueError) as exc:
        parser.error(str(exc))
    encoded = json.dumps(result, indent=2, sort_keys=True)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded + "\n", encoding="utf-8")
    print(encoded)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
