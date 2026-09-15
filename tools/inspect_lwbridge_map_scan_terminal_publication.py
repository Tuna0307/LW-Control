#!/usr/bin/env python3
"""Verify recovered terminal map-scan failure-list and publication gate."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from capstone import Cs, CS_ARCH_X86, CS_MODE_64
import pefile

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
IMAGE_BASE = 0x140000000
EVENT_START = 0x1403408E0
EVENT_END = 0x140341390


class InspectError(ValueError):
    pass


def raw_offset_for_va(pe: pefile.PE, va: int) -> int:
    return pe.get_offset_from_rva(va - pe.OPTIONAL_HEADER.ImageBase)


def disassemble(blob: bytes, pe: pefile.PE, start_va: int, end_va: int) -> list[dict[str, object]]:
    start = raw_offset_for_va(pe, start_va)
    cs = Cs(CS_ARCH_X86, CS_MODE_64)
    return [
        {"va": insn.address, "mnemonic": insn.mnemonic, "opStr": insn.op_str, "bytes": insn.bytes.hex()}
        for insn in cs.disasm(blob[start:start + (end_va - start_va)], start_va)
    ]


def require_instruction(index, va: int, mnemonic: str, operand_contains: str) -> None:
    item = index.get(va)
    if item is None:
        raise InspectError(f"missing instruction at VA 0x{va:X}")
    if item["mnemonic"] != mnemonic or operand_contains not in str(item["opStr"]):
        raise InspectError(f"unexpected instruction at VA 0x{va:X}: {item['mnemonic']} {item['opStr']}")


def require_bytes(blob: bytes, marker: bytes, label: str) -> None:
    if marker not in blob:
        raise InspectError(f"missing recovered marker: {label}")


def inspect(binary: Path) -> dict[str, object]:
    blob = binary.read_bytes()
    digest = hashlib.sha256(blob).hexdigest()
    if digest != EXPECTED_SHA256:
        raise InspectError(f"unexpected LWBridge SHA-256: {digest}")
    pe = pefile.PE(data=blob, fast_load=False)
    if pe.OPTIONAL_HEADER.ImageBase != IMAGE_BASE:
        raise InspectError(f"unexpected image base: 0x{pe.OPTIONAL_HEADER.ImageBase:X}")

    window = disassemble(blob, pe, EVENT_START, EVENT_END)
    index = {int(item["va"]): item for item in window}
    require_instruction(index, 0x1403410BA, "lea", "[rip + 0x9598a6]")
    require_instruction(index, 0x1403410CA, "call", "0x140345030")
    require_instruction(index, 0x1403410EA, "test", "rax, rax")
    require_instruction(index, 0x1403410ED, "jle", "0x140341163")
    require_instruction(index, 0x140341163, "cmp", "sil, 5")
    require_instruction(index, 0x14034116E, "lea", "[rip + 0x95985b]")
    require_instruction(index, 0x14034117B, "call", "0x1405a2051")
    require_instruction(index, 0x1403411B1, "cmp", "byte ptr [r13], 3")
    require_instruction(index, 0x1403411BD, "test", "r8, r8")
    require_instruction(index, 0x1403411C5, "je", "0x1403411ce")
    require_instruction(index, 0x140341226, "test", "r12, r12")
    require_instruction(index, 0x140341229, "je", "0x1403412c2")
    require_instruction(index, 0x140341252, "call", "0x140646824")
    require_instruction(index, 0x14034127F, "call", "0x14033fbe8")
    require_instruction(index, 0x1403412C2, "lea", "[rip + 0x959987]")
    require_instruction(index, 0x140341345, "call", "0x140341913")
    require_instruction(index, 0x140341362, "xor", "esi, esi")
    require_instruction(index, 0x140341364, "mov", "qword ptr [rsp + 0x20], rsi")
    require_instruction(index, 0x140341374, "call", "0x14025f9af")

    require_bytes(blob, b"failedBlocks", "terminal failed-block field")
    require_bytes(blob, b"lastError", "terminal last-error field")
    require_bytes(blob, b"publishing", "publication phase")

    return {
        "findingId": "LWB-R6-059",
        "date": "2026-09-14",
        "scope": "map.scan.complete terminal failure-list and publishing admission",
        "evidenceStatus": "RECOVERED",
        "sourceIdentity": {"path": str(binary), "sha256": digest, "imageBase": hex(IMAGE_BASE)},
        "recoveredResult": {
            "failedBlocks": "positive failedBlocks contributes a terminal failure; zero or negative does not",
            "lastError": "present nonempty lastError contributes a terminal failure; empty/missing does not",
            "failurePath": "a nonempty terminal failure list is joined and routed through the shared failure/stop cleanup helper",
            "publishingPath": "only an empty terminal failure list writes phase=publishing and calls direct completion",
            "pendingAcksBoundary": "pendingAcks is reported through nativePendingRecords but is not an input to this recovered terminal failure-list branch",
        },
        "locators": {
            "eventWindow": [hex(EVENT_START), hex(EVENT_END)],
            "failedBlocksGate": "0x1403410BA-0x140341163",
            "lastErrorGate": "0x14034116E-0x140341221",
            "failureDispatch": "0x140341226-0x140341284",
            "publishingAndCompletion": "0x1403412C2-0x140341379",
        },
        "limits": [
            "this finding does not prove pending native records or pending acknowledgements are globally irrelevant to completion",
            "native drop/session-stop/error handling is covered separately by LWB-R6-055",
            "game-side traversal, retry, acknowledgement consumption and drain scheduling remain unrecovered",
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
    rendered = json.dumps(result, indent=2)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered + "\n", encoding="utf-8")
    print(f"finding={result['findingId']} status={result['evidenceStatus']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
