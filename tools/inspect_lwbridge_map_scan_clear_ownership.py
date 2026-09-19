#!/usr/bin/env python3
"""Verify recovered LWBridge Map Data Clear ownership and live-server validation."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from capstone import Cs, CS_ARCH_X86, CS_MODE_64
import pefile

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
IMAGE_BASE = 0x140000000
CLEAR_HANDLER_START = 0x14034475E
CLEAR_HANDLER_END = 0x1403449E6


class InspectError(ValueError):
    pass


def raw_offset_for_va(pe: pefile.PE, va: int) -> int:
    return pe.get_offset_from_rva(va - pe.OPTIONAL_HEADER.ImageBase)


def disassemble(blob: bytes, pe: pefile.PE, start_va: int, end_va: int) -> list[dict[str, object]]:
    start = raw_offset_for_va(pe, start_va)
    cs = Cs(CS_ARCH_X86, CS_MODE_64)
    data = blob[start:start + (end_va - start_va)]
    return [
        {"va": insn.address, "mnemonic": insn.mnemonic, "opStr": insn.op_str, "bytes": insn.bytes.hex()}
        for insn in cs.disasm(data, start_va)
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

    window = disassemble(blob, pe, CLEAR_HANDLER_START, CLEAR_HANDLER_END)
    index = {int(item["va"]): item for item in window}

    require_instruction(index, 0x1403447AA, "lea", "rip")
    require_instruction(index, 0x14034480A, "cmp", "byte ptr [rax], 1")
    require_instruction(index, 0x14034480F, "cmp", "byte ptr [rax + 1], 0")
    require_instruction(index, 0x140344837, "test", "r14, r14")
    require_instruction(index, 0x14034483A, "jle", "0x14034495b")
    require_instruction(index, 0x140344854, "call", "0x140345030")
    require_instruction(index, 0x140344859, "cmp", "rax, r14")
    require_instruction(index, 0x14034485C, "jne", "0x14034495b")
    require_instruction(index, 0x140344879, "call", "0x1405a2051")
    require_instruction(index, 0x140344893, "call", "0x1405a1bb4")
    require_instruction(index, 0x14034489A, "je", "0x14034495b")
    require_instruction(index, 0x1403448B3, "call", "0x140256a58")
    require_instruction(index, 0x1403448D0, "lea", "rip")
    require_instruction(index, 0x14034493E, "call", "0x140341913")

    require_bytes(blob, b"SCAN_RUNNING", "active-scan clear error code")
    require_bytes(blob, b"stop the map scan first", "active-scan clear error message")
    require_bytes(blob, b"SERVER_UNAVAILABLE", "unavailable-server clear error code")
    require_bytes(blob, b"current server id unavailable", "unavailable-server clear error message")
    require_bytes(blob, b"serverIdSource", "current server source field")
    require_bytes(blob, b"live", "required current server source")
    require_bytes(blob, b"idle", "post-clear phase")

    return {
        "findingId": "LWB-R6-057",
        "date": "2026-09-13",
        "scope": "map_scan_clear ownership, active-scan conflict and current-live-server validation",
        "evidenceStatus": "RECOVERED",
        "sourceIdentity": {"path": str(binary), "sha256": digest, "imageBase": hex(IMAGE_BASE)},
        "recoveredResult": {
            "activeScan": {"code": "SCAN_RUNNING", "message": "stop the map scan first"},
            "serverUnavailable": {"code": "SERVER_UNAVAILABLE", "message": "current server id unavailable"},
            "serverGate": "requested serverId must be positive, match current scan-state serverId, and current serverIdSource must equal live",
            "clearCall": "server-scoped clear executes only after the ownership/server gates pass",
            "postClear": "phase is reset to idle and updated state is published",
        },
        "limits": [
            "this finding does not create or infer a live current-server source for the rebuild",
            "late-result generation invalidation after Clear remains unrecovered",
            "the exact protected game-side block scheduler remains unrecovered",
        ],
        "locators": {
            "clearHandler": "0x14034475E-0x1403449E6",
            "activeReadGate": "0x1403447AA-0x140344832",
            "serverGate": "0x140344837-0x14034489A",
            "clearCall": "0x1403448B3",
            "idlePublish": "0x1403448D0-0x14034493E",
        },
        "instructionWindow": window,
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
