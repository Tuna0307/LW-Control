#!/usr/bin/env python3
"""Verify recovered direct Map Scan publication ownership and stale-run guards."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from capstone import Cs, CS_ARCH_X86, CS_MODE_64
import pefile

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
IMAGE_BASE = 0x140000000
COMPLETION_START = 0x14025F9AF
COMPLETION_END = 0x140260A7D

class InspectError(ValueError):
    pass


def raw_offset_for_va(pe: pefile.PE, va: int) -> int:
    return pe.get_offset_from_rva(va - pe.OPTIONAL_HEADER.ImageBase)


def disassemble(blob: bytes, pe: pefile.PE, start_va: int, end_va: int) -> list[dict[str, object]]:
    start = raw_offset_for_va(pe, start_va)
    cs = Cs(CS_ARCH_X86, CS_MODE_64)
    data = blob[start:start + (end_va - start_va)]
    return [{"va": i.address, "mnemonic": i.mnemonic, "opStr": i.op_str} for i in cs.disasm(data, start_va)]

def require_instruction(index: dict[int, dict[str, object]], va: int, mnemonic: str, operand_contains: str) -> None:
    item = index.get(va)
    if item is None:
        raise InspectError(f"missing instruction at VA 0x{va:X}")
    if item["mnemonic"] != mnemonic or operand_contains not in str(item["opStr"]):
        raise InspectError(f"unexpected instruction at VA 0x{va:X}: {item['mnemonic']} {item['opStr']}")


def require_once(blob: bytes, marker: bytes, label: str) -> None:
    count = blob.count(marker)
    if count != 1:
        raise InspectError(f"expected one {label} marker, found {count}")


def inspect(binary: Path) -> dict[str, object]:
    blob = binary.read_bytes()
    digest = hashlib.sha256(blob).hexdigest()
    if digest != EXPECTED_SHA256:
        raise InspectError(f"unexpected LWBridge SHA-256: {digest}")
    pe = pefile.PE(data=blob, fast_load=False)
    if pe.OPTIONAL_HEADER.ImageBase != IMAGE_BASE:
        raise InspectError(f"unexpected image base: 0x{pe.OPTIONAL_HEADER.ImageBase:X}")

    window = disassemble(blob, pe, COMPLETION_START, COMPLETION_END)
    index = {int(item["va"]): item for item in window}
    require_instruction(index, 0x140260440, "lea", "rip")
    require_instruction(index, 0x14026045B, "call", "0x14029fd8d")
    require_instruction(index, 0x140260465, "mov", "qword ptr [rsp + 0xa8]")
    require_instruction(index, 0x1402604DE, "test", "rax, rax")
    require_instruction(index, 0x1402604E1, "je", "0x140260679")
    require_instruction(index, 0x140260505, "lea", "rip")
    require_instruction(index, 0x1402605C7, "lea", "rip")
    require_instruction(index, 0x1402608FF, "call", "0x140276d7b")

    transition_sql = b"UPDATE scan_runs SET status='completed',error=NULL,updated_at=?1 WHERE id=?2 AND status='running'"
    require_once(blob, transition_sql, "conditional completion update")
    require_once(blob, b"map scan is not running", "stale-run failure")
    require_once(blob, b"map scan disappeared", "post-commit missing-run failure")
    require_once(blob, b"DELETE FROM scan_runs WHERE server_id=?1 AND id<>?2", "same-server old-run prune")

    scan_blocks = blob.count(b"scan_blocks")
    if scan_blocks != 2:
        raise InspectError(f"expected two literal scan_blocks uses, found {scan_blocks}")
    for dml in (b"INSERT INTO scan_blocks", b"UPDATE scan_blocks", b"DELETE FROM scan_blocks"):
        if dml in blob:
            raise InspectError(f"unexpected literal scan_blocks DML: {dml.decode()}")

    return {
        "findingId": "LWB-R6-058",
        "date": "2026-09-13",
        "scope": "transactional direct-scan publication ownership and stale-run rejection",
        "evidenceStatus": "RECOVERED",
        "sourceIdentity": {"path": str(binary), "sha256": digest, "imageBase": hex(IMAGE_BASE)},
        "recoveredResult": {
            "completionTransition": transition_sql.decode(),
            "zeroAffectedRows": {"code": "INVALID_SCAN", "message": "map scan is not running"},
            "postCommitReread": "the completed run is read again by exact run id after the completion transaction",
            "missingPostCommitRun": {"code": "INVALID_SCAN", "message": "map scan disappeared"},
            "cleanupOrdering": "staging cleanup and same-server old-run pruning occur only after a nonzero completion transition",
            "scanBlocksLiteralSurface": "schema creation plus ordered SELECT only; no literal INSERT/UPDATE/DELETE scan_blocks SQL exists in the verified binary",
        },
        "limits": [
            "this does not recover game-side block traversal, grouping, pacing or retry",
            "absence of literal scan_blocks DML does not prove no dynamically assembled or indirect writer exists",
            "this finding does not wire production publication before the common live scanner exists",
        ],
        "locators": {
            "directCompletion": "0x14025F9AF-0x140260A7D",
            "conditionalTransition": "0x140260440-0x1402604E1",
            "stagingCleanup": "0x140260505",
            "oldRunPrune": "0x1402605C7",
            "postCommitReread": "0x1402608FF",
        },
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
