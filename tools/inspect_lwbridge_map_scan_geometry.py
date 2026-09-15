#!/usr/bin/env python3
"""Verify recovered LWBridge Map Scan geometry and scalar completion gates."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from capstone import Cs, CS_ARCH_X86, CS_MODE_64
import pefile

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
IMAGE_BASE = 0x140000000
SCAN_FUNCTION = (0x1400F9333, 0x1400FB8D8)
COMPLETION_FUNCTION = (0x14025F9AF, 0x140260A7D)
GEOMETRY_RANGE = (0x1400F9E37, 0x1400F9EC4)
COMPLETION_RANGE = (0x14025FB76, 0x14025FBF3)


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
        raise InspectError(
            f"unexpected instruction at VA 0x{va:X}: {item['mnemonic']} {item['opStr']}"
        )


def ceil_div_20(value: int) -> int:
    if value <= 0:
        raise ValueError("positive dimension required")
    return 1 + ((value - 1) // 20)


def inspect(binary: Path) -> dict[str, object]:
    blob = binary.read_bytes()
    digest = hashlib.sha256(blob).hexdigest()
    if digest != EXPECTED_SHA256:
        raise InspectError(f"unexpected LWBridge SHA-256: {digest}")

    pe = pefile.PE(data=blob, fast_load=True)
    if pe.OPTIONAL_HEADER.ImageBase != IMAGE_BASE:
        raise InspectError(f"unexpected image base: 0x{pe.OPTIONAL_HEADER.ImageBase:X}")

    geometry = disassemble(blob, pe, *GEOMETRY_RANGE)
    completion = disassemble(blob, pe, *COMPLETION_RANGE)
    geometry_index = by_va(geometry)
    completion_index = by_va(completion)

    require_instruction(geometry_index, 0x1400F9E37, "mov", "[r14 + 0xc8]")
    require_instruction(geometry_index, 0x1400F9E41, "jle", "0x1400fb593")
    require_instruction(geometry_index, 0x1400F9E47, "mov", "[r14 + 0xd0]")
    require_instruction(geometry_index, 0x1400F9E51, "jle", "0x1400fb593")
    require_instruction(geometry_index, 0x1400F9E57, "add", "9")
    require_instruction(geometry_index, 0x1400F9E5B, "movabs", "0x6666666666666667")
    require_instruction(geometry_index, 0x1400F9EB9, "imul", "r8")
    require_instruction(geometry_index, 0x1400F9EBD, "mov", "[r14 + 0xe0]")

    require_instruction(completion_index, 0x14025FBCF, "add", "r15, rdx")
    require_instruction(completion_index, 0x14025FBD2, "cmp", "r15, r14")
    require_instruction(completion_index, 0x14025FBD5, "jne", "0x14025fd06")
    require_instruction(completion_index, 0x14025FBEA, "test", "rdx, rdx")
    require_instruction(completion_index, 0x14025FBED, "jne", "0x14025fd1f")

    for raw in (
        b"MAP_SIZE_UNAVAILABLE",
        b"world map dimensions are unavailable",
        b"INCOMPLETE_SCAN",
        b"direct map scan is incomplete",
        b"direct map scan contains failed batches",
    ):
        if raw not in blob:
            raise InspectError(f"missing recovered literal: {raw!r}")

    samples = [1, 9, 10, 11, 19, 20, 21, 39, 40, 41, 2000]
    sample_grid = {
        str(value): ceil_div_20(value)
        for value in samples
    }

    return {
        "findingId": "LWB-R6-052",
        "date": "2026-09-13",
        "scope": "Map Scan positive map dimensions, block-grid cardinality, initial counters and scalar direct-completion prerequisite",
        "evidenceStatus": "RECOVERED",
        "sourceIdentity": {"path": str(binary), "sha256": digest, "imageBase": hex(IMAGE_BASE)},
        "locators": {
            "scanFunction": [hex(SCAN_FUNCTION[0]), hex(SCAN_FUNCTION[1])],
            "geometryRange": [hex(GEOMETRY_RANGE[0]), hex(GEOMETRY_RANGE[1])],
            "completionFunction": [hex(COMPLETION_FUNCTION[0]), hex(COMPLETION_FUNCTION[1])],
            "completionRange": [hex(COMPLETION_RANGE[0]), hex(COMPLETION_RANGE[1])],
            "totalBlocksStore": "0x1400F9EBD -> state +0xE0",
            "initialCounters": "0x1400FA5C7-0x1400FA6E0",
            "scalarCompletionGate": "0x14025FB76-0x14025FBED",
            "incompleteErrors": "0x14025FD06-0x14025FD3F",
        },
        "recoveredResult": {
            "dimensionRequirement": "tileWidth > 0 and tileHeight > 0; otherwise MAP_SIZE_UNAVAILABLE / world map dimensions are unavailable",
            "blockColumns": "ceil(tileWidth / 20)",
            "blockRows": "ceil(tileHeight / 20)",
            "totalBlocks": "ceil(tileWidth / 20) * ceil(tileHeight / 20)",
            "derivation": "for each positive dimension the code computes floor((dimension+9)/10), then ceil(that/2), which equals ceil(dimension/20)",
            "initialCounters": {"readBlocks": 0, "unreadBlocks": "totalBlocks", "failedBlocks": 0, "inflightBlocks": 0},
            "scalarCompletionPrerequisite": "completedBlocks + failedBlocks must equal totalBlocks; failedBlocks must then be zero before the direct-completion path can continue",
        },
        "sampleCeilDiv20": sample_grid,
        "validationAndLimits": {
            "validation": "hash-locked bounded disassembly and literal verification",
            "liveProven": False,
            "unknownBlocked": [
                "exact block traversal/order and block request coordinates",
                "meaning of the additional non-scalar failure input checked before publication",
                "retry/resume and native capture drain gates beyond the recovered scalar prerequisite",
            ],
        },
        "implementationImpact": {
            "unblocked": "shared-engine block-grid cardinality and initial progress accounting can be implemented without guessing",
            "stillBlocked": "full scheduler ordering/retry/resume/publication eligibility remains gated",
        },
        "reproduction": [
            "python tools\\inspect_lwbridge_map_scan_geometry.py ..\\LW\\lwbridge-0.3.1.exe --json"
        ],
        "boundedInstructions": {
            "geometry": geometry,
            "completion": completion,
        },
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
