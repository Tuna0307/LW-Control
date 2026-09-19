#!/usr/bin/env python3
"""Verify recovered LWBridge map.scan.progress scheduler counter/rate semantics."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
from pathlib import Path

from capstone import Cs, CS_ARCH_X86, CS_MODE_64
import pefile

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
IMAGE_BASE = 0x140000000
NORMALIZER = (0x140342926, 0x140342F48)
EVENT_CALL = 0x1403408F8
CLOCK_HELPER = 0x14023F1C0
ROUND_THUNK = 0x14079BAC7
ROUND_IAT = 0x1407B9130


class InspectError(ValueError):
    pass


def raw_offset_for_va(pe: pefile.PE, va: int) -> int:
    return pe.get_offset_from_rva(va - pe.OPTIONAL_HEADER.ImageBase)

def disassemble(blob: bytes, pe: pefile.PE, start_va: int, end_va: int) -> list[dict[str, object]]:
    start = raw_offset_for_va(pe, start_va)
    data = blob[start:start + (end_va - start_va)]
    cs = Cs(CS_ARCH_X86, CS_MODE_64)
    return [
        {"va": insn.address, "mnemonic": insn.mnemonic, "opStr": insn.op_str, "bytes": insn.bytes.hex()}
        for insn in cs.disasm(data, start_va)
    ]


def by_va(instructions: list[dict[str, object]]) -> dict[int, dict[str, object]]:
    return {int(item["va"]): item for item in instructions}


def require_instruction(index: dict[int, dict[str, object]], va: int, mnemonic: str, operand_contains: str) -> None:
    item = index.get(va)
    if item is None:
        raise InspectError(f"missing instruction at VA 0x{va:X}")
    if item["mnemonic"] != mnemonic or operand_contains not in str(item["opStr"]):
        raise InspectError(f"unexpected instruction at VA 0x{va:X}: {item['mnemonic']} {item['opStr']}")


def read_double(blob: bytes, pe: pefile.PE, va: int) -> float:
    return struct.unpack_from("<d", blob, raw_offset_for_va(pe, va))[0]

def imported_name_at(pe: pefile.PE, address: int) -> tuple[str, str] | None:
    pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_IMPORT"]])
    for entry in pe.DIRECTORY_ENTRY_IMPORT:
        dll = entry.dll.decode(errors="replace")
        for imp in entry.imports:
            if imp.address == address:
                name = imp.name.decode(errors="replace") if imp.name else f"ord:{imp.ordinal}"
                return dll, name
    return None


def normalize(total: int, concurrency: int, completed: int, failed: int, inflight: int) -> dict[str, int]:
    completed = min(max(completed, 0), total)
    failed = min(max(failed, 0), total)
    if completed + failed > total:
        raise InspectError("sample has completed+failed beyond total")
    remaining = total - completed - failed
    inflight = min(max(inflight, 0), min(concurrency, remaining))
    return {
        "completedBlocks": completed,
        "readBlocks": completed,
        "failedBlocks": failed,
        "unreadBlocks": remaining - inflight,
        "inflightBlocks": inflight,
    }


def scan_rate(completed: int, elapsed_ms: int) -> float:
    elapsed_ms = max(elapsed_ms, 1)
    per_second = completed / (elapsed_ms / 1000.0)
    return math.floor(per_second * 100.0 + 0.5) / 100.0

def inspect(binary: Path) -> dict[str, object]:
    blob = binary.read_bytes()
    digest = hashlib.sha256(blob).hexdigest()
    if digest != EXPECTED_SHA256:
        raise InspectError(f"unexpected LWBridge SHA-256: {digest}")

    pe = pefile.PE(data=blob, fast_load=False)
    if pe.OPTIONAL_HEADER.ImageBase != IMAGE_BASE:
        raise InspectError(f"unexpected image base: 0x{pe.OPTIONAL_HEADER.ImageBase:X}")

    normalizer = disassemble(blob, pe, *NORMALIZER)
    index = by_va(normalizer)
    require_instruction(index, 0x140342996, "cmp", "rax, rbx")
    require_instruction(index, 0x14034299C, "cmovb", "rdi, rax")
    require_instruction(index, 0x1403429A5, "cmovs", "rdi, rsi")
    require_instruction(index, 0x1403429BE, "cmp", "rax, rbx")
    require_instruction(index, 0x1403429C4, "cmovb", "r14, rax")
    require_instruction(index, 0x1403429CB, "cmovs", "r14, rsi")
    require_instruction(index, 0x1403429D4, "lea", "[r14 + rdi]")
    require_instruction(index, 0x1403429DB, "sub", "rdi, rax")
    require_instruction(index, 0x1403429E9, "lea", "[rip + 0x958440]")
    require_instruction(index, 0x140342A45, "cmp", "rax, rdi")
    require_instruction(index, 0x140342A48, "cmovge", "r15, rdi")
    require_instruction(index, 0x140342A5A, "cmp", "rsi, r15")
    require_instruction(index, 0x140342A5D, "cmovb", "r15, rsi")
    require_instruction(index, 0x140342A66, "cmovs", "r15, rax")
    require_instruction(index, 0x140342BE9, "sub", "rdi, r15")
    require_instruction(index, 0x140342BEF, "cmovle", "rdi, r12")

    require_instruction(index, 0x140342CFD, "call", hex(CLOCK_HELPER))
    require_instruction(index, 0x140342D02, "sub", "rax, qword ptr [r15 + 0x38]")
    require_instruction(index, 0x140342D0F, "cmovge", "rcx, rax")
    require_instruction(index, 0x140342D1B, "divsd", "[rip + 0x4de88d]")
    require_instruction(index, 0x140342D26, "cvtsi2sd", "xmm0, r12")
    require_instruction(index, 0x140342D2B, "divsd", "xmm0, xmm1")
    require_instruction(index, 0x140342D37, "mulsd", "xmm0, xmm7")
    require_instruction(index, 0x140342D3B, "call", hex(ROUND_THUNK))
    require_instruction(index, 0x140342D44, "divsd", "xmm6, xmm7")

    caller = by_va(disassemble(blob, pe, 0x14034040C, 0x140340930))
    require_instruction(caller, 0x140340420, "pcmpeqb", "[rip + 0x4f3e68]")
    require_instruction(caller, 0x140340451, "pcmpeqb", "[rip + 0x956b27]")
    require_instruction(caller, EVENT_CALL, "call", hex(NORMALIZER[0]))
    timing = by_va(disassemble(blob, pe, 0x1400F96D0, 0x1400FA900))
    require_instruction(timing, 0x1400F96D0, "call", hex(CLOCK_HELPER))
    require_instruction(timing, 0x1400F96D5, "add", "rax, 0x2710")
    require_instruction(timing, 0x1400F9ECB, "call", hex(CLOCK_HELPER))
    require_instruction(timing, 0x1400F9ED0, "mov", "qword ptr [r14 + 0xe8], rax")
    require_instruction(timing, 0x1400FA8BA, "mov", "r13, qword ptr [r14 + 0xe8]")

    error_offset = raw_offset_for_va(pe, 0x140C9AE30)
    if not blob[error_offset:error_offset + 74].startswith(
        b"INVALID_SCAN_PROGRESScompleted and failed map blocks exceed the scan total"
    ):
        raise InspectError("recovered INVALID_SCAN_PROGRESS string cluster changed")

    imported = imported_name_at(pe, ROUND_IAT)
    if imported is None or imported[1] != "round":
        raise InspectError(f"expected CRT round import at 0x{ROUND_IAT:X}, got {imported!r}")
    constants = {
        "millisecondsPerSecond": read_double(blob, pe, 0x1408215B0),
        "rateHundredthsScale": read_double(blob, pe, 0x140C96F60),
    }
    if constants != {"millisecondsPerSecond": 1000.0, "rateHundredthsScale": 100.0}:
        raise InspectError(f"unexpected scheduler progress constants: {constants!r}")

    samples = [
        {"total": 100, "concurrency": 8, "completed": 25, "failed": 5, "inflight": 20, "elapsedMs": 2500},
        {"total": 100, "concurrency": 20, "completed": -5, "failed": 3, "inflight": -7, "elapsedMs": 3000},
    ]
    for sample in samples:
        counters = normalize(
            sample["total"], sample["concurrency"], sample["completed"], sample["failed"], sample["inflight"]
        )
        sample["normalized"] = counters
        sample["scanRate"] = scan_rate(counters["completedBlocks"], sample["elapsedMs"])

    return {
        "findingId": "LWB-R6-054",
        "date": "2026-09-13",
        "scope": "map.scan.progress counter validation, inflight bounding, unread derivation and scan-rate calculation",
        "evidenceStatus": "RECOVERED",
        "sourceIdentity": {"path": str(binary), "sha256": digest, "imageBase": hex(IMAGE_BASE)},
        "locators": {
            "eventNormalizer": [hex(NORMALIZER[0]), hex(NORMALIZER[1])],
            "eventNormalizerCall": hex(EVENT_CALL),
            "clockHelper": hex(CLOCK_HELPER),
            "roundThunk": hex(ROUND_THUNK),
            "roundIat": hex(ROUND_IAT),
            "roundImport": {"dll": imported[0], "name": imported[1]},
        },
        "recoveredResult": {
            "completedBlocks": "clamp(input completedBlocks, 0, totalBlocks)",
            "failedBlocks": "clamp(input failedBlocks, 0, totalBlocks)",
            "invalidAccounting": "completedBlocks + failedBlocks > totalBlocks -> INVALID_SCAN_PROGRESS",
            "inflightBlocks": "clamp(input inflightBlocks, 0, min(concurrency, totalBlocks-completedBlocks-failedBlocks))",
            "readBlocks": "completedBlocks",
            "unreadBlocks": "totalBlocks-completedBlocks-failedBlocks-inflightBlocks",
            "scanRate": "round(completedBlocks / max(elapsedMilliseconds,1) * 100000.0) / 100.0",
            "scanRateEquivalent": "round((completedBlocks / (max(elapsedMilliseconds,1)/1000.0))*100.0)/100.0",
            "rounding": "CRT round; nonnegative scan rates therefore round to nearest hundredth with .5 away from zero",
            "constants": constants,
        },
        "samples": samples,
        "validationAndLimits": {
            "liveProven": False,
            "implementedProductionStatus": False,
            "limits": [
                "this finding normalizes already-received map.scan.progress/map.scan.complete counters; it does not recover block coordinate generation",
                "the separate percentage write inside this helper is excluded from the finding until its exact source operand is fully labelled",
                "clock identity is intentionally left unspecified beyond the recovered millisecond-like elapsed arithmetic",
                "production status must still be driven by authoritative scheduler events rather than synthetic counters",
            ],
        },
        "boundedInstructions": normalizer,
        "reproduction": [
            "python tools\\inspect_lwbridge_map_scan_scheduler_progress.py ..\\LW\\lwbridge-0.3.1.exe --json"
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
