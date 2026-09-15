#!/usr/bin/env python3
"""Verify recovered LWBridge map-scan completion/capture/stop safety semantics."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from capstone import Cs, CS_ARCH_X86, CS_MODE_64
import pefile

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
IMAGE_BASE = 0x140000000
DIRECT_COMPLETION = 0x14025F9AF
DIRECT_COMPLETION_CALL = 0x140341374
COUNT_HELPER = 0x140345030


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


def require_instruction(index: dict[int, dict[str, object]], va: int, mnemonic: str, operand_contains: str) -> None:
    item = index.get(va)
    if item is None:
        raise InspectError(f"missing instruction at VA 0x{va:X}")
    if item["mnemonic"] != mnemonic or operand_contains not in str(item["opStr"]):
        raise InspectError(f"unexpected instruction at VA 0x{va:X}: {item['mnemonic']} {item['opStr']}")


def index_window(blob: bytes, pe: pefile.PE, start_va: int, end_va: int) -> tuple[list[dict[str, object]], dict[int, dict[str, object]]]:
    window = disassemble(blob, pe, start_va, end_va)
    return window, {int(item["va"]): item for item in window}


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

    completion, completion_index = index_window(blob, pe, 0x14025FB60, 0x14025FD50)
    require_instruction(completion_index, 0x14025FB76, "lea", "[rip + 0xa25bcb]")
    require_instruction(completion_index, 0x14025FB96, "lea", "[rip + 0xa25bd3]")
    require_instruction(completion_index, 0x14025FBB4, "lea", "[rip + 0xa25bdd]")
    require_instruction(completion_index, 0x14025FBCF, "add", "r15, rdx")
    require_instruction(completion_index, 0x14025FBD2, "cmp", "r15, r14")
    require_instruction(completion_index, 0x14025FBD5, "jne", "0x14025fd06")
    require_instruction(completion_index, 0x14025FBDB, "cmp", "qword ptr [rsp + 0x380], 0")
    require_instruction(completion_index, 0x14025FBE4, "jne", "0x14025fd1f")
    require_instruction(completion_index, 0x14025FBEA, "test", "rdx, rdx")
    require_instruction(completion_index, 0x14025FBED, "jne", "0x14025fd1f")

    caller, caller_index = index_window(blob, pe, 0x140341330, 0x140341390)
    require_instruction(caller_index, 0x140341362, "xor", "esi, esi")
    require_instruction(caller_index, 0x140341364, "mov", "qword ptr [rsp + 0x20], rsi")
    require_instruction(caller_index, DIRECT_COMPLETION_CALL, "call", hex(DIRECT_COMPLETION))

    capture, capture_index = index_window(blob, pe, 0x140340990, 0x140341040)
    for va, operand in [
        (0x1403409AF, "[rip + 0x95a357]"), (0x1403409CC, "[rip + 0x95a347]"),
        (0x1403409E7, "[rip + 0x95a33a]"), (0x1403409FF, "[rip + 0x95a336]"),
        (0x140340A20, "[rip + 0x95a329]"), (0x140340A3B, "[rip + 0x95a319]")]:
        require_instruction(capture_index, va, "lea", operand)
    for va in [0x1403409C4, 0x1403409DC, 0x1403409F7, 0x140340A0F, 0x140340A30, 0x140340A4B]:
        require_instruction(capture_index, va, "call", hex(COUNT_HELPER))
    require_instruction(capture_index, 0x1403409E4, "add", "r13, r14")
    require_instruction(capture_index, 0x140340A1A, "add", "rbp, r14")
    require_instruction(capture_index, 0x140340A1D, "add", "rbp, r13")
    require_instruction(capture_index, 0x140340A38, "add", "r13, rbp")
    require_instruction(capture_index, 0x140340A50, "mov", "qword ptr [rsp + 0xf8], rax")
    require_instruction(capture_index, 0x140340CBC, "cmp", "qword ptr [rsp + 0xf8], 0")
    require_instruction(capture_index, 0x140340CC5, "jle", "0x140340fec")

    stop1, stop1_index = index_window(blob, pe, 0x14033FBE8, 0x14033FE70)
    require_instruction(stop1_index, 0x14033FCBB, "lea", "[rip + 0x95ae4b]")
    require_instruction(stop1_index, 0x14033FCDA, "mov", "word ptr [r15], 1")
    require_instruction(stop1_index, 0x14033FCE0, "lea", "[rip + 0x95ae31]")
    require_instruction(stop1_index, 0x14033FD4A, "lea", "[rip + 0x95ac2e]")
    require_instruction(stop1_index, 0x14033FD69, "mov", "byte ptr [r12], 2")
    require_instruction(stop1_index, 0x14033FD71, "movups", "xmmword ptr [r12 + 8], xmm0")
    require_instruction(stop1_index, 0x14033FDDF, "lea", "[rip + 0x95abf3]")
    require_instruction(stop1_index, 0x14033FDFE, "mov", "word ptr [r14], 1")
    require_instruction(stop1_index, 0x14033FE40, "lea", "[rip + 0x95ad19]")

    stop2, stop2_index = index_window(blob, pe, 0x140343D99, 0x1403442C5)
    require_instruction(stop2_index, 0x140343F8C, "lea", "[rip + 0x956b7a]")
    require_instruction(stop2_index, 0x140343FB3, "mov", "word ptr [r15], 1")
    require_instruction(stop2_index, 0x140343FB9, "lea", "[rip + 0x956b58]")
    require_instruction(stop2_index, 0x140344029, "lea", "[rip + 0x95694f]")
    require_instruction(stop2_index, 0x140344048, "mov", "byte ptr [r15], 2")
    require_instruction(stop2_index, 0x14034404F, "movups", "xmmword ptr [r15 + 8], xmm0")
    require_instruction(stop2_index, 0x14034422E, "lea", "[rip + 0x9567a4]")
    require_instruction(stop2_index, 0x14034424D, "mov", "word ptr [r12], 1")
    require_instruction(stop2_index, 0x140344293, "lea", "[rip + 0x9568c6]")

    require_bytes(blob, b"INCOMPLETE_SCAN", "completion error code")
    require_bytes(blob, b"direct map scan is incomplete", "incomplete completion diagnostic")
    require_bytes(blob, b"direct map scan contains failed batches", "failed-batch completion diagnostic")
    require_bytes(blob, b"native world capture dropped ", "native dropped-record diagnostic")
    require_bytes(blob, b"native world capture session stopped", "native capture stopped diagnostic")
    require_bytes(blob, b"stopMapScan", "stop command marker")

    return {
        "findingId": "LWB-R6-055",
        "date": "2026-09-13",
        "scope": "map.scan.complete capture safety, publication prerequisite and stopped-state cleanup",
        "evidenceStatus": "RECOVERED",
        "sourceIdentity": {"path": str(binary), "sha256": digest, "imageBase": hex(IMAGE_BASE)},
        "recoveredResult": {
            "nativePendingRecords": "pendingPoints + pendingMarches + pendingPointRemovals + pendingMarchRemovals + pendingAcks",
            "nativeDroppedRecords": "tracked separately; positive values enter failure handling before publishing",
            "directCompletionCoverage": "completedBlocks + failedBlocks == totalBlocks",
            "directCompletionFailure": "failedBlocks != 0 -> INCOMPLETE_SCAN / direct map scan contains failed batches",
            "extraFailedBatchInput": "the direct-completion helper rejects nonzero input, but the production map-scan caller writes zero to the fifth argument slot",
            "stoppedState": {
                "isReading": False,
                "phase": "idle",
                "inflightBlocks": 0,
                "resumeAvailable": False,
            },
            "stopCommand": "both recovered cleanup paths contain a conditional stopMapScan bridge call before state publication",
        },
        "locators": {
            "directCompletionFunction": hex(DIRECT_COMPLETION),
            "directCompletionCall": hex(DIRECT_COMPLETION_CALL),
            "nativeCaptureWindow": ["0x140340990", "0x140341040"],
            "stopCleanupPaths": [["0x14033fcb0", "0x14033fe70"], ["0x140343f80", "0x1403442c5"]],
        },
        "validationAndLimits": {
            "liveProven": False,
            "implementedProductionStatus": False,
            "limits": [
                "nativePendingRecords is recovered as a reported aggregate; this finding does not claim pending>0 alone rejects publication",
                "the production caller hard-codes the extra direct-completion failed-batch input to zero",
                "stopMapScan dispatch is conditional on the recovered active bridge/session predicate; this finding does not rename that predicate",
                "this finding does not recover block coordinate generation, retry policy or resume serialization",
            ],
        },
        "boundedInstructions": {
            "completion": completion,
            "caller": caller,
            "capture": capture,
            "stopPathA": stop1,
            "stopPathB": stop2,
        },
        "reproduction": [
            "python tools\\inspect_lwbridge_map_scan_completion_safety.py ..\\LW\\lwbridge-0.3.1.exe --json"
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=Path)
    parser.add_argument("--json", action="store_true")
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
    print(rendered if args.json else f"finding={result['findingId']} status={result['evidenceStatus']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
