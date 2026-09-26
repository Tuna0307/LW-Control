#!/usr/bin/env python3
"""Verify recovered LWBridge map-scan start ownership and resume gating."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from capstone import Cs, CS_ARCH_X86, CS_MODE_64
import pefile

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
IMAGE_BASE = 0x140000000
SCAN_WORKER = 0x1400F9333
IS_READING_HELPER = 0x14033FB88


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


def index_window(blob: bytes, pe: pefile.PE, start_va: int, end_va: int):
    window = disassemble(blob, pe, start_va, end_va)
    return window, {int(item["va"]): item for item in window}


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

    worker, worker_index = index_window(blob, pe, 0x1400F9333, 0x1400F9440)
    require_instruction(worker_index, 0x1400F939A, "call", "0x1403cf086")
    require_instruction(worker_index, 0x1400F93A7, "call", hex(IS_READING_HELPER))
    require_instruction(worker_index, 0x1400F93AE, "je", "0x1400f943c")
    require_bytes(blob, b"SCAN_RUNNING", "original duplicate-start error code")
    require_bytes(blob, b"map scan already running", "original duplicate-start message")
    require_bytes(blob, b"GAME_CONNECTION_UNAVAILABLE", "missing-connection error code")
    require_bytes(blob, b"game connection unavailable", "missing-connection message")

    command, command_index = index_window(blob, pe, 0x140160330, 0x1401607B8)
    require_instruction(command_index, 0x14016034F, "lea", "[rip + 0x6c2732]")
    require_instruction(command_index, 0x140160356, "mov", "r8d, 6")
    require_instruction(command_index, 0x140160366, "cmp", "byte ptr [rax], 1")
    require_instruction(command_index, 0x14016036B, "cmp", "byte ptr [rax + 1], 0")
    require_instruction(command_index, 0x14016036F, "je", "0x14016073a")
    require_instruction(command_index, 0x140160381, "lea", "[rip + 0x6c2732]")
    require_instruction(command_index, 0x140160388, "mov", "r8d, 0xf")
    require_instruction(command_index, 0x1401603DE, "cmp", "byte ptr [rcx], 1")
    require_instruction(command_index, 0x1401603E1, "jne", "0x14016073a")
    require_instruction(command_index, 0x1401603E7, "cmp", "byte ptr [rcx + 1], 0")
    require_instruction(command_index, 0x1401603EB, "je", "0x14016073a")
    require_instruction(command_index, 0x1401607A1, "call", hex(SCAN_WORKER))
    false_windows = []
    initial_false, initial_index = index_window(blob, pe, 0x1400FA929, 0x1400FA954)
    require_instruction(initial_index, 0x1400FA929, "lea", "rip")
    require_instruction(initial_index, 0x1400FA948, "mov", "ax, 1")
    require_instruction(initial_index, 0x1400FA94C, "mov", "word ptr [r12], ax")
    false_windows.append(initial_false)

    immediate_false_sites = [
        (0x14033F84C, 0x14033F86B),
        (0x14033FB04, 0x14033FB23),
        (0x14033FDDF, 0x14033FDFE),
        (0x140341520, 0x14034153F),
        (0x14034422E, 0x14034424D),
    ]
    for field_va, write_va in immediate_false_sites:
        window, index = index_window(blob, pe, field_va, write_va + 8)
        require_instruction(index, field_va, "lea", "rip")
        require_instruction(index, write_va, "mov", ", 1")
        false_windows.append(window)

    false_write_sites = [(0x1400FA929, 0x1400FA94C), *immediate_false_sites]

    return {
        "findingId": "LWB-R6-056",
        "date": "2026-09-13",
        "scope": "map_scan_start ownership, duplicate-start rejection and explicit resume gating",
        "evidenceStatus": "RECOVERED",
        "sourceIdentity": {"path": str(binary), "sha256": digest, "imageBase": hex(IMAGE_BASE)},
        "recoveredResult": {
            "duplicateStart": {"code": "SCAN_RUNNING", "message": "map scan already running"},
            "missingConnection": {"code": "GAME_CONNECTION_UNAVAILABLE", "message": "game connection unavailable"},
            "resumeField": "resume",
            "resumeGateField": "resumeAvailable",
            "freshFallback": "resume=false, missing/invalid resume, or resumeAvailable not exact boolean true reaches the fresh-start branch",
            "resumePath": "resume=true plus current resumeAvailable=true reuses current scan state before re-entering the same scan worker",
            "resumeAvailableWrites": "all recovered construction/reset sites encode false; no true producer is recovered in 0.3.1",
        },
        "limits": [
            "no Map Scan retry/backoff policy is claimed by this finding",
            "no block traversal/order/request coordinates are recovered here",
            "no true-producing resumeAvailable state constructor is recovered",
            "the rebuild must not advertise resumability merely because the resume input branch exists",
        ],
        "locators": {
            "scanWorker": "0x1400F9333-0x1400FB8D8",
            "publicCommand": "0x14015FD9C-0x140160C76",
            "duplicateStartCheck": "0x1400F939A-0x1400F943C",
            "resumeInputGate": "0x14016034F-0x1401603EB",
            "workerReentry": "0x1401607A1",
            "resumeAvailableFalseSites": [hex(write_va) for _, write_va in false_write_sites],
        },
        "instructionWindows": {
            "worker": worker,
            "command": command,
            "resumeAvailableFalse": false_windows,
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
