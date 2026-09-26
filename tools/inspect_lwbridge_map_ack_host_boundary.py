#!/usr/bin/env python3
"""Verify the original LWBridge host boundary for Map native acknowledgements."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86_const import X86_OP_MEM, X86_REG_RIP
import pefile

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
IMAGE_BASE = 0x140000000
EVENT_START = 0x140340990
EVENT_END = 0x140341390
COUNT_HELPER = 0x140345030


class InspectError(ValueError):
    pass


def raw_offset_for_va(pe: pefile.PE, va: int) -> int:
    return pe.get_offset_from_rva(va - pe.OPTIONAL_HEADER.ImageBase)


def disassemble(blob: bytes, pe: pefile.PE, start_va: int, end_va: int):
    start = raw_offset_for_va(pe, start_va)
    cs = Cs(CS_ARCH_X86, CS_MODE_64)
    cs.detail = True
    return list(cs.disasm(blob[start:start + (end_va - start_va)], start_va))


def rip_ascii(blob: bytes, pe: pefile.PE, insn) -> str | None:
    for operand in insn.operands:
        if operand.type == X86_OP_MEM and operand.mem.base == X86_REG_RIP:
            va = insn.address + insn.size + operand.mem.disp
            try:
                offset = raw_offset_for_va(pe, va)
            except Exception:
                continue
            end = offset
            while end < min(len(blob), offset + 128) and 0x20 <= blob[end] <= 0x7E:
                end += 1
            if end - offset >= 3:
                return blob[offset:end].decode("ascii", "replace")
    return None


def require(index, va: int, mnemonic: str, operand: str = ""):
    insn = index.get(va)
    if insn is None:
        raise InspectError(f"missing instruction at 0x{va:X}")
    if insn.mnemonic != mnemonic or operand not in insn.op_str:
        raise InspectError(f"unexpected instruction at 0x{va:X}: {insn.mnemonic} {insn.op_str}")
    return insn


def inspect(binary: Path) -> dict[str, object]:
    blob = binary.read_bytes()
    digest = hashlib.sha256(blob).hexdigest()
    if digest != EXPECTED_SHA256:
        raise InspectError(f"unexpected LWBridge SHA-256: {digest}")
    pe = pefile.PE(data=blob, fast_load=False)
    if pe.OPTIONAL_HEADER.ImageBase != IMAGE_BASE:
        raise InspectError(f"unexpected image base: 0x{pe.OPTIONAL_HEADER.ImageBase:X}")

    rows = disassemble(blob, pe, EVENT_START, EVENT_END)
    index = {insn.address: insn for insn in rows}

    fields = {
        0x1403409AF: ("pendingPoints", 0xD),
        0x1403409CC: ("pendingMarches", 0xE),
        0x1403409E7: ("pendingPointRemovals", 0x14),
        0x1403409FF: ("pendingMarchRemovals", 0x14),
        0x140340A20: ("pendingAcks", 0xB),
        0x140340A3B: ("dropped", 7),
    }
    verified_fields = {}
    for va, (name, size) in fields.items():
        insn = require(index, va, "lea")
        text = rip_ascii(blob, pe, insn) or ""
        if not text.startswith(name):
            raise InspectError(f"field mismatch at 0x{va:X}: {text!r}")
        call = require(index, va + {0x1403409AF:0x15,0x1403409CC:0x10,0x1403409E7:0x10,0x1403409FF:0x10,0x140340A20:0x10,0x140340A3B:0x10}[va], "call")
        if call.op_str != hex(COUNT_HELPER):
            raise InspectError(f"count helper mismatch after {name}")
        verified_fields[name] = hex(va)
    require(index, 0x1403409E4, "add", "r13, r14")
    require(index, 0x140340A1A, "add", "rbp, r14")
    require(index, 0x140340A1D, "add", "rbp, r13")
    require(index, 0x140340A38, "add", "r13, rbp")
    require(index, 0x140340A9F, "mov", "rbp, r13")
    pending_write = require(index, 0x140340AA6, "lea")
    if not (rip_ascii(blob, pe, pending_write) or "").startswith("nativePendingRecords"):
        raise InspectError("nativePendingRecords write marker changed")
    require(index, 0x140340ACB, "mov", "qword ptr [r14 + 8], rbp")
    require(index, 0x140340ACF, "mov", "qword ptr [r14 + 0x10], r13")

    require(index, 0x140340AD3, "mov", "rbp, qword ptr [rsp + 0xf8]")
    require(index, 0x140340ADB, "mov", "r13, rbp")
    require(index, 0x140340CBC, "cmp", "qword ptr [rsp + 0xf8], 0")
    require(index, 0x140340CC5, "jle", "0x140340fec")

    post_write_pending_xrefs = []
    for insn in rows:
        if not (0x140340AD3 <= insn.address < EVENT_END):
            continue
        text = rip_ascii(blob, pe, insn)
        if text and text.startswith("nativePendingRecords"):
            post_write_pending_xrefs.append(hex(insn.address))
    if post_write_pending_xrefs:
        raise InspectError(f"unexpected post-write pending read: {post_write_pending_xrefs}")

    return {
        "findingId": "LWB-R8-077",
        "date": "2026-09-27",
        "scope": "original host-side Map acknowledgement and native-drain boundary",
        "evidenceStatus": "RECOVERED CONTRACT",
        "sourceIdentity": {"path": str(binary), "sha256": digest, "imageBase": hex(IMAGE_BASE)},
        "recoveredResult": {
            "nativePendingRecords": "pendingPoints + pendingMarches + pendingPointRemovals + pendingMarchRemovals + pendingAcks",
            "pendingAcksRole": "count-only input to nativePendingRecords in map.scan.complete host handling",
            "hostAckItemParsing": "no acks[] item lookup is performed in the recovered map.scan.complete host window",
            "terminalAdmission": "the pending aggregate is serialized, then its working registers are overwritten before the terminal failure/publication branch",
            "dropGate": "positive dropped remains an explicit host failure/cleanup input",
            "ownershipConclusion": "ack item schema, ack consumption and any ack-to-block completion linkage occur below the recovered host event layer",
        },
        "locators": {
            "eventWindow": [hex(EVENT_START), hex(EVENT_END)],
            "fieldLookups": verified_fields,
            "pendingAggregateFinalAdd": "0x140340A38",
            "pendingStateWrite": "0x140340AA6-0x140340ACF",
            "aggregateRegisterOverwrite": "0x140340AD3-0x140340ADB",
            "droppedGate": "0x140340CBC-0x140340CC5",
        },
        "limits": [
            "this does not recover the protected proxy acks[] item schema",
            "this does not prove pending queues are irrelevant before map.scan.complete is emitted",
            "this does not recover XluaBridgeMapScanTick scheduling, traversal order or retry policy",
            "the protected RVA boundary 0x3F8E0-0x40A6D is not inspected by this verifier",
        ],
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
    rendered = json.dumps(result, indent=2, sort_keys=True)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered + "\n", encoding="utf-8")
    print(rendered)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
