#!/usr/bin/env python3
"""Validate the committed LWB-R5-005 focused disassembly evidence.

This checker intentionally reads the durable text excerpt only. It does not
execute or disassemble the LWBridge reference binary.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


EXPECTED_NORMALIZED_SHA256 = "66aa1db0dac98117e13e156cec13030bd62ecd1a34ae8eb925bbc3f5db1103c1"

REQUIRED_SNIPPETS = (
    "0x14002888d: mov      rdi, r9",
    "0x140028890: mov      rbx, r8",
    "0x140028893: mov      r14, rdx",
    "0x140028896: mov      r15, rcx",
    "0x140028899: call     0x14006ac50",
    "0x14002889e: mov      r8d, 0x3c",
    "0x1400288aa: call     0x14006f5e0",
    "0x1400288c2: mov      edx, 0x2faf080",
    "0x1400288c7: call     0x14006b7b0",
    "0x1400288d9: jae      0x14002895f",
    "0x1400288df: lea      rcx, [rbp - 0x30]",
    "0x1400288e3: mov      rdx, r15",
    "0x1400288e6: mov      r8, r14",
    "0x1400288e9: call     0x140060b30",
    "0x1400288fa: cmp      qword ptr [rbp - 0x20], rdi",
    "0x140028904: mov      rcx, r13",
    "0x140028907: mov      rdx, rbx",
    "0x14002890a: mov      r8, rdi",
    "0x14002890d: call     0x1400816b7",
    "0x140028914: jne      0x14002892e",
    "0x14002892e: lea      rdi, [rip + 0x5ed9b] ; 'LAUNCH_TICKET_OWNERSHIP_CHANGED",
    "0x140028935: mov      edx, 0x1f",
    "0x14002895f: mov      edx, 0x21",
    "0x140028964: lea      rdi, [rip + 0x5eda4] ; 'LAUNCH_TICKET_CONSUMPTION_TIMEOUT",
    "0x1400289fd: lea      rdi, [rip + 0x5eceb] ; 'LAUNCH_TICKET_CONSUMPTION_FAILED",
    "0x140028a1c: lea      rdi, [rip + 0x5eccc] ; 'LAUNCH_TICKET_CONSUMPTION_FAILED",
    "0x140028a7b: mov      edx, 0x20",
    "0x14001c9ee: call     0x1400816b7",
    "0x14001c9f7: cmovne   r13, rax",
)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "path",
        nargs="?",
        default="evidence/lwbridge-implementation/2026-09-08-r5-ticket-consumption-disassembly.txt",
    )
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    path = Path(args.path)
    data = path.read_bytes()
    text = data.decode("utf-8-sig")
    normalized = data.replace(b"\r\n", b"\n")
    normalized_sha256 = hashlib.sha256(normalized).hexdigest()

    missing = [snippet for snippet in REQUIRED_SNIPPETS if snippet not in text]
    marker_lengths = {
        "LAUNCH_TICKET_OWNERSHIP_CHANGED": len("LAUNCH_TICKET_OWNERSHIP_CHANGED"),
        "LAUNCH_TICKET_CONSUMPTION_FAILED": len("LAUNCH_TICKET_CONSUMPTION_FAILED"),
        "LAUNCH_TICKET_CONSUMPTION_TIMEOUT": len("LAUNCH_TICKET_CONSUMPTION_TIMEOUT"),
    }

    result = {
        "ok": not missing and normalized_sha256 == EXPECTED_NORMALIZED_SHA256,
        "path": str(path),
        "normalizedSha256": normalized_sha256,
        "expectedNormalizedSha256": EXPECTED_NORMALIZED_SHA256,
        "missingSnippets": missing,
        "markerLengths": marker_lengths,
        "validated": {
            "pollQueryCallPreferredVa": "0x1400288E9 -> 0x140060B30",
            "observedLengthComparisonPreferredVa": "0x1400288FA",
            "sequenceComparisonPreferredVa": "0x140028904-0x140028914 -> 0x1400816B7",
            "ownershipChangedPreferredVa": "0x14002892E",
            "timeoutPreferredVa": "0x14002895F-0x14002896B",
            "failedPreferredVa": ["0x1400289FD", "0x140028A1C"],
        },
        "limits": [
            "This validates the committed focused disassembly excerpt, not a fresh extraction from the binary.",
            "The semantic identities of the first two poll-query inputs are unresolved.",
            "The time units represented by 0x3C and 0x02FAF080 are unresolved.",
            "The exact result-variant meanings dispatched from 0x140028970 are unresolved.",
        ],
    }

    if args.json:
        print(json.dumps(result, indent=2))
    else:
        print(f"LWB-R5-005 evidence check: {'PASS' if result['ok'] else 'FAIL'}")
        print(f"normalized-sha256={normalized_sha256}")
        if missing:
            print("missing snippets:")
            for snippet in missing:
                print(f"- {snippet}")

    return 0 if result["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
