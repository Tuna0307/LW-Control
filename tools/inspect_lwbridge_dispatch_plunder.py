#!/usr/bin/env python3
"""Reproduce the recovered original LWBridge Dispatch plunder contract.

This inspector is read-only. It verifies the immutable 0.3.1 executable and
frontend asset hashes, exact frontend wrappers, and the recovered PE runtime
functions/SQL/error strings used by R7-080.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from inspect_lwbridge_map_scan import InspectError, inspect


EXPECTED_BINARY_SHA256 = (
    "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
)
EXPECTED_FRONTEND_SHA256 = (
    "062ebd2362885af05e07bf4f0de00ed833293a83fefa6b54c26975fa1db9df47"
)

FUNCTIONS = {
    "public_schedule": (
        0x115331,
        [
            "secret task rows are required",
            "select between 1 and 200 secret tasks",
            "secret task scheduling data is invalid",
            "bridge://dispatch-plunder-changed",
        ],
    ),
    "cancel_target_parser": (
        0x2315B8,
        [
            "server ID and secret task UUID are required",
        ],
    ),
    "schedule_store": (
        0x268029,
        [
            "INSERT INTO dispatch_plunder_jobs",
            "scheduled plunder job is missing",
        ],
    ),
    "cancel_store": (
        0x266840,
        [
            "UPDATE dispatch_plunder_jobs",
        ],
    ),
    "cancel_service": (
        0x33EE06,
        [
            "scheduled plunder job not found",
            "bridge://dispatch-plunder-changed",
        ],
    ),
}

FRONTEND_SNIPPETS = [
    "map_dispatch_plunder_schedule",
    "maxRandomDelaySeconds",
    "randomDelaySeconds",
    "map_dispatch_plunder_cancel",
    "{serverId:e,taskUuid:t}",
]

BINARY_STRINGS = {
    "schedule_sql": b"INSERT INTO dispatch_plunder_jobs(",
    "cancel_sql": b"UPDATE dispatch_plunder_jobs SET status='cancelled'",
    "cancel_not_found": b"scheduled plunder job not found",
    "changed_event": b"bridge://dispatch-plunder-changed",
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def require_hash(path: Path, expected: str, label: str) -> str:
    digest = sha256(path)
    if digest != expected:
        raise InspectError(
            f"unsupported {label} SHA-256 {digest}; expected {expected}"
        )
    return digest


def inspect_contract(binary: Path, frontend: Path) -> dict[str, object]:
    binary_digest = require_hash(
        binary, EXPECTED_BINARY_SHA256, "lwbridge executable"
    )
    frontend_digest = require_hash(
        frontend, EXPECTED_FRONTEND_SHA256, "frontend API asset"
    )

    frontend_text = frontend.read_text(encoding="utf-8")
    missing_frontend = [
        snippet for snippet in FRONTEND_SNIPPETS
        if snippet not in frontend_text
    ]
    if missing_frontend:
        raise InspectError(
            "frontend Dispatch wrapper snippets missing: "
            + ", ".join(repr(value) for value in missing_frontend)
        )

    binary_data = binary.read_bytes()
    string_offsets: dict[str, str] = {}
    for name, needle in BINARY_STRINGS.items():
        offset = binary_data.find(needle)
        if offset < 0:
            raise InspectError(
                f"binary Dispatch contract string missing: {name}"
            )
        string_offsets[name] = f"0x{offset:X}"

    function_reports: dict[str, dict[str, object]] = {}
    raw_reports: dict[str, str] = {}
    for name, (rva, needles) in FUNCTIONS.items():
        report = inspect(binary, (), dump_rva=rva)
        raw_reports[name] = report
        missing = [needle for needle in needles if needle not in report]
        if missing:
            raise InspectError(
                f"{name} function 0x{rva:X} is missing expected annotations: "
                + ", ".join(repr(value) for value in missing)
            )
        first_function = next(
            (
                line.strip()
                for line in report.splitlines()
                if line.startswith("function ")
            ),
            "",
        )
        function_reports[name] = {
            "rva": f"0x{rva:X}",
            "runtimeFunction": first_function,
            "verifiedAnnotations": needles,
        }

    public_schedule = raw_reports["public_schedule"]
    cancel_parser = raw_reports["cancel_target_parser"]
    server_identity = {
        "schedulePositiveI64Only": (
            "cmp     qword ptr [rsp + 0x30], 0" in public_schedule
            and "jle     0x140115c79" in public_schedule
            and "0x1869f" not in public_schedule.lower()
            and "99999" not in public_schedule
        ),
        "cancelPositiveI64Only": (
            "test    rdi, rdi" in cancel_parser
            and "jle     0x1402316e0" in cancel_parser
            and "0x1869f" not in cancel_parser.lower()
            and "99999" not in cancel_parser
        ),
    }
    if not all(server_identity.values()):
        raise InspectError(
            "Dispatch server identity no longer matches recovered positive-i64-only semantics"
        )

    store_call = public_schedule.find("call    0x140268029")
    changed_event = public_schedule.find("bridge://dispatch-plunder-changed")
    if store_call < 0 or changed_event < 0 or store_call >= changed_event:
        raise InspectError(
            "Dispatch schedule store/change-event ordering no longer matches recovered wrapper"
        )

    return {
        "ok": True,
        "binary": str(binary),
        "binarySha256": binary_digest,
        "frontend": str(frontend),
        "frontendSha256": frontend_digest,
        "frontendSnippets": FRONTEND_SNIPPETS,
        "binaryStringOffsets": string_offsets,
        "functions": function_reports,
        "serverIdentity": server_identity,
        "scheduleOrdering": {
            "storeCallBeforeChangedEvent": True,
            "storeCall": "0x140268029",
            "changedEvent": "bridge://dispatch-plunder-changed",
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=Path)
    parser.add_argument(
        "frontend",
        type=Path,
        nargs="?",
        default=Path(
            "evidence/lwbridge-0.3.1/frontend/assets/api-ClPPi2JT.js"
        ),
    )
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        result = inspect_contract(
            args.binary.resolve(),
            args.frontend.resolve(),
        )
    except (InspectError, OSError, ValueError) as exc:
        parser.error(str(exc))
        return 2

    if args.json:
        print(json.dumps(result, indent=2))
    else:
        print("PASS Dispatch plunder original-contract inspection")
        print(f"binarySha256={result['binarySha256']}")
        print(f"frontendSha256={result['frontendSha256']}")
        for name, row in result["functions"].items():
            print(f"{name}: {row['runtimeFunction']}")
        for name, offset in result["binaryStringOffsets"].items():
            print(f"{name}: offset={offset}")
        print("serverIdentity=positive-i64-only (schedule+cancel; no 99999 cap)")
        print("scheduleOrdering=store-before-bridge://dispatch-plunder-changed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
