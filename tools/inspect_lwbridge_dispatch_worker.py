#!/usr/bin/env python3
"""Verify recovered original LWBridge Dispatch worker timing/arming anchors."""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path

from inspect_lwbridge_map_scan import InspectError, inspect


EXPECTED_SHA256 = (
    "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
)
WORKER_RVA = 0xF04EC

REQUIRED = [
    "function 0xF04EC-0xF1C9E",
    "0xF08F7: mov     r9d, 0x2710",
    "0xF109F: lea     rcx",
    "'running'",
    "0xF1302: add     r13, 0x7530",
    "'dispatch'",
    "'armMapPlunderinvalid scheduled target'",
    "0xF1669: mov     qword ptr [rbx + 0x1b8], 0x1388",
    "'DISPATCH_PLUNDER_RESPONSE_TIMEOUT'",
    "'waiting_connectionserver response timeout'",
    "'src\\\\services\\\\dispatch_plunder.rs'",
]


def verify(binary: Path) -> dict[str, object]:
    digest = hashlib.sha256(binary.read_bytes()).hexdigest()
    if digest != EXPECTED_SHA256:
        raise InspectError(
            f"unsupported lwbridge SHA-256 {digest}; expected {EXPECTED_SHA256}"
        )
    report = inspect(binary, (), dump_rva=WORKER_RVA)
    missing = [value for value in REQUIRED if value not in report]
    if missing:
        raise InspectError(
            "Dispatch worker dump missing expected anchors: "
            + ", ".join(repr(value) for value in missing)
        )
    return {
        "ok": True,
        "binary": str(binary),
        "sha256": digest,
        "workerRva": f"0x{WORKER_RVA:X}",
        "armLeadMilliseconds": 0x2710,
        "responseHorizonMilliseconds": 0x7530,
        "armCallTimeoutMilliseconds": 0x1388,
        "executorCommand": "armMapPlunder",
        "requestKind": "dispatch",
        "runningBeforeArm": True,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=Path)
    args = parser.parse_args()
    try:
        result = verify(args.binary.resolve())
    except (InspectError, OSError, ValueError) as exc:
        parser.error(str(exc))
        return 2
    print("PASS original Dispatch worker timing/arming inspection")
    for key, value in result.items():
        if key != "ok":
            print(f"{key}={value}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
