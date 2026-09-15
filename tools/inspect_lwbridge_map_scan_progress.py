#!/usr/bin/env python3
"""Verify recovered LWBridge scan-state derived progress semantics."""

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
NORMALIZER = (0x14033EFAE, 0x14033F886)
DERIVED_RANGE = (0x14033F377, 0x14033F594)
ROUND_THUNK = 0x14079BAC7
ROUND_IAT = 0x1407B9130
CALL_SITES = (0x1400F9044, 0x140341EDC)


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
    offset = raw_offset_for_va(pe, va)
    return struct.unpack_from("<d", blob, offset)[0]


def imported_name_at(pe: pefile.PE, address: int) -> tuple[str, str] | None:
    pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_IMPORT"]])
    for entry in pe.DIRECTORY_ENTRY_IMPORT:
        dll = entry.dll.decode(errors="replace")
        for imp in entry.imports:
            if imp.address == address:
                name = imp.name.decode(errors="replace") if imp.name else f"ord:{imp.ordinal}"
                return dll, name
    return None


def recovered_progress(total: int, completed: int, failed: int, status: str) -> float:
    if total <= 0:
        return 0.0
    if status == "completed":
        return 100.0
    done = completed + failed
    bounded = 0 if done < 0 else min(done, total)
    rounded_tenths = math.floor((bounded / total) * 1000.0 + 0.5)
    return min(rounded_tenths / 10.0, 98.0)


def recovered_unread(total: int, completed: int, failed: int) -> int:
    return max(total - completed - failed, 0)
def inspect(binary: Path) -> dict[str, object]:
    blob = binary.read_bytes()
    digest = hashlib.sha256(blob).hexdigest()
    if digest != EXPECTED_SHA256:
        raise InspectError(f"unexpected LWBridge SHA-256: {digest}")

    pe = pefile.PE(data=blob, fast_load=False)
    if pe.OPTIONAL_HEADER.ImageBase != IMAGE_BASE:
        raise InspectError(f"unexpected image base: 0x{pe.OPTIONAL_HEADER.ImageBase:X}")

    derived = disassemble(blob, pe, *DERIVED_RANGE)
    index = by_va(derived)
    require_instruction(index, 0x14033F3B8, "add", "rax, r12")
    require_instruction(index, 0x14033F3BB, "sub", "r15, rax")
    require_instruction(index, 0x14033F3C4, "cmovle", "r15, r12")
    require_instruction(index, 0x14033F4D9, "xorpd", "xmm6, xmm6")
    require_instruction(index, 0x14033F4E2, "movsd", "[rip + 0x957a76]")
    require_instruction(index, 0x14033F4EC, "test", "al, al")
    require_instruction(index, 0x14033F4F0, "add", "r14, r13")
    require_instruction(index, 0x14033F50B, "cvtsi2sd", "xmm0, rax")
    require_instruction(index, 0x14033F510, "divsd", "xmm0, xmm1")
    require_instruction(index, 0x14033F514, "mulsd", "[rip + 0x4e2094]")
    require_instruction(index, 0x14033F51C, "call", hex(ROUND_THUNK))
    require_instruction(index, 0x14033F525, "divsd", "[rip + 0x957a3b]")
    require_instruction(index, 0x14033F52D, "minsd", "[rip + 0x957a3b]")

    imported = imported_name_at(pe, ROUND_IAT)
    if imported is None or imported[1] != "round":
        raise InspectError(f"expected CRT round import at 0x{ROUND_IAT:X}, got {imported!r}")

    constants = {
        "completedPercent": read_double(blob, pe, 0x140C96F60),
        "tenthsDivisor": read_double(blob, pe, 0x140C96F68),
        "activeClamp": read_double(blob, pe, 0x140C96F70),
        "tenthsScale": read_double(blob, pe, 0x1408215B0),
    }
    expected_constants = {
        "completedPercent": 100.0,
        "tenthsDivisor": 10.0,
        "activeClamp": 98.0,
        "tenthsScale": 1000.0,
    }
    if constants != expected_constants:
        raise InspectError(f"unexpected progress constants: {constants!r}")
    caller_hits: list[str] = []
    text = pe.sections[0]
    text_blob = blob[text.PointerToRawData:text.PointerToRawData + text.SizeOfRawData]
    text_va = pe.OPTIONAL_HEADER.ImageBase + text.VirtualAddress
    for offset in range(len(text_blob) - 5):
        if text_blob[offset] != 0xE8:
            continue
        rel = struct.unpack_from("<i", text_blob, offset + 1)[0]
        source = text_va + offset
        if source + 5 + rel == NORMALIZER[0]:
            caller_hits.append(hex(source))
    if tuple(int(value, 16) for value in caller_hits) != CALL_SITES:
        raise InspectError(f"unexpected normalizer call sites: {caller_hits!r}")

    samples = [
        {"total": 100, "completed": 0, "failed": 0, "status": "scanning"},
        {"total": 100, "completed": 1, "failed": 0, "status": "scanning"},
        {"total": 100, "completed": 97, "failed": 1, "status": "scanning"},
        {"total": 100, "completed": 100, "failed": 0, "status": "scanning"},
        {"total": 100, "completed": 100, "failed": 0, "status": "completed"},
        {"total": 0, "completed": 0, "failed": 0, "status": "completed"},
    ]
    for sample in samples:
        sample["unreadBlocks"] = recovered_unread(sample["total"], sample["completed"], sample["failed"])
        sample["progressPercent"] = recovered_progress(sample["total"], sample["completed"], sample["failed"], sample["status"])
    return {
        "findingId": "LWB-R6-053",
        "date": "2026-09-13",
        "scope": "shared scan-state unread/progress derivation and active-progress cap",
        "evidenceStatus": "RECOVERED",
        "sourceIdentity": {"path": str(binary), "sha256": digest, "imageBase": hex(IMAGE_BASE)},
        "locators": {
            "normalizer": [hex(NORMALIZER[0]), hex(NORMALIZER[1])],
            "derivedRange": [hex(DERIVED_RANGE[0]), hex(DERIVED_RANGE[1])],
            "callSites": caller_hits,
            "roundThunk": hex(ROUND_THUNK),
            "roundIat": hex(ROUND_IAT),
            "roundImport": {"dll": imported[0], "name": imported[1]},
        },
        "recoveredResult": {
            "unreadBlocks": "max(totalBlocks - completedBlocks - failedBlocks, 0)",
            "progressZeroTotal": "totalBlocks <= 0 -> 0.0",
            "progressCompleted": "totalBlocks > 0 and status == completed -> 100.0",
            "progressOther": "min(round(clamp(completedBlocks + failedBlocks, 0, totalBlocks) / totalBlocks * 1000.0) / 10.0, 98.0)",
            "rounding": "CRT round; nonnegative progress therefore rounds to nearest tenth with .5 away from zero",
            "constants": constants,
        },
        "samples": samples,
        "validationAndLimits": {
            "liveProven": False,
            "implementedProductionStatus": False,
            "limits": [
                "this helper derives status fields from already-supplied scan counters; it does not recover block traversal or scheduling",
                "inflight/rate/native capture values require their own authoritative live sources before production wiring",
                "100 percent remains a status-dependent derived display value, not proof that capture drain and durable publication succeeded",
            ],
        },
        "boundedInstructions": derived,
        "reproduction": [
            "python tools\\inspect_lwbridge_map_scan_progress.py ..\\LW\\lwbridge-0.3.1.exe --json"
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
