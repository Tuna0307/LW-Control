#!/usr/bin/env python3
"""Verify the exact 0.3.17 Map scan unread/progress derivation contract."""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path

from capstone import Cs, CS_ARCH_X86, CS_MODE_64
import pefile


REFERENCE_SHA256 = "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
IMAGE_BASE = 0x140000000
NORMALIZER = (0x14042612D, 0x140426A05)
DERIVED = (0x1404264F6, 0x140426713)


class InspectError(ValueError):
    pass


def raw_offset(pe: pefile.PE, va: int) -> int:
    return pe.get_offset_from_rva(va - int(pe.OPTIONAL_HEADER.ImageBase))


def read_double(blob: bytes, pe: pefile.PE, va: int) -> float:
    return struct.unpack_from("<d", blob, raw_offset(pe, va))[0]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    blob = args.binary.read_bytes()
    digest = hashlib.sha256(blob).hexdigest()
    if digest != REFERENCE_SHA256:
        raise InspectError(f"unexpected reference SHA-256: {digest}")
    pe = pefile.PE(data=blob, fast_load=False)
    if int(pe.OPTIONAL_HEADER.ImageBase) != IMAGE_BASE:
        raise InspectError("unexpected image base")

    start, end = DERIVED
    code = blob[raw_offset(pe, start):raw_offset(pe, start) + end - start]
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    instructions = [
        {"va": f"0x{i.address:X}", "mnemonic": i.mnemonic, "opStr": i.op_str}
        for i in md.disasm(code, start)
    ]
    by_va = {int(x["va"], 0): x for x in instructions}

    expected = {
        0x140426537: ("add", "rax, r12"),
        0x14042653A: ("sub", "r15, rax"),
        0x140426543: ("cmovle", "r15, r12"),
        0x14042666F: ("add", "r14, r13"),
        0x140426672: ("cmp", "r14, r15"),
        0x14042667A: ("cmovb", "r15, r14"),
        0x140426680: ("test", "r14, r14"),
        0x140426683: ("cmovns", "rax, r15"),
        0x14042668F: ("divsd", "xmm0, xmm1"),
        0x140426693: ("mulsd", "xmm0"),
        0x14042669B: ("call", "0x1407a3107"),
        0x1404266A4: ("divsd", "xmm6"),
        0x1404266AC: ("minsd", "xmm6"),
    }
    for va, (mnemonic, operand) in expected.items():
        row = by_va.get(va)
        if row is None or row["mnemonic"] != mnemonic or operand not in row["opStr"]:
            raise InspectError(f"unexpected instruction at 0x{va:X}: {row}")

    # Resolve current RIP-relative constants from the decoded instruction ends.
    constants_va = {
        "completedPercent": 0x14084AED8,
        "tenthsScale": 0x1408295A0,
        "tenthsDivisor": 0x140D64950,
        "activeClamp": 0x140D64958,
    }
    constants = {name: read_double(blob, pe, va) for name, va in constants_va.items()}
    required = {
        "completedPercent": 100.0,
        "tenthsScale": 1000.0,
        "tenthsDivisor": 10.0,
        "activeClamp": 98.0,
    }
    if constants != required:
        raise InspectError(f"unexpected current progress constants: {constants}")

    pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_IMPORT"]])
    round_target = 0x1407A3107
    round_tail = blob[raw_offset(pe, round_target):raw_offset(pe, round_target) + 16]

    result = {
        "schema": 1,
        "findingId": "LWB317-RE-MAP-002-PROGRESS",
        "evidenceState": "EXACT_BYTES_STATIC_CONTRACT",
        "source": {"path": str(args.binary), "sha256": digest},
        "locators": {
            "normalizer": [f"0x{NORMALIZER[0]:X}", f"0x{NORMALIZER[1]:X}"],
            "derivedRange": [f"0x{DERIVED[0]:X}", f"0x{DERIVED[1]:X}"],
            "roundLikeTarget": f"0x{round_target:X}",
            "roundLikeBytes": round_tail.hex(),
            "constantVas": {k: f"0x{v:X}" for k, v in constants_va.items()},
        },
        "contract": {
            "unreadBlocks": "max(totalBlocks - completedBlocks - failedBlocks, 0)",
            "progressZeroTotal": "totalBlocks <= 0 -> 0.0",
            "progressCompleted": "totalBlocks > 0 and phase/status compares equal to completed -> 100.0",
            "progressActive": "min(roundLike(clamp(completedBlocks + failedBlocks, 0, totalBlocks) / totalBlocks * 1000.0) / 10.0, 98.0)",
            "constants": constants,
        },
        "instructions": instructions,
        "limits": [
            "The call target at 0x1407A3107 is recorded as round-like based on the recovered arithmetic structure; this artifact does not claim an import name unless separately resolved.",
            "The normalizer derives presentation state from supplied counters and does not prove traversal/retry scheduling or durable publication by itself.",
        ],
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
