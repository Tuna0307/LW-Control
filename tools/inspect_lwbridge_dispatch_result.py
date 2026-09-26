#!/usr/bin/env python3
"""Verify the recovered original Dispatch post-arm/result state machine.

R7-082 read-only inspector. It hash-locks the verified 0.3.1 executable and
checks exact disassembly anchors for the async result event, result persistence,
deadline, connection-sensitive arm failure, daily-limit shutdown and history
pruning semantics.
"""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path

from inspect_lwbridge_map_scan import InspectError, inspect


EXPECTED_SHA256 = (
    "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
)
WORKER_RVA = 0xF04EC
RESULT_HANDLER_RVA = 0x33DAAB
CONNECTION_PREDICATE_RVA = 0x3CF086

EVENT_TYPE = "map.dispatch-plunder-result"
RESULT_FIELDS = [
    "serverId",
    "taskUuid",
    "success",
    "errorCode",
    "serverDayStartAt",
]
KNOWN_ERRORS = [
    "DISPATCH_PLUNDER_INVALID_TARGET",
    "DISPATCH_PLUNDER_REQUEST_PENDING",
    "DISPATCH_PLUNDER_MANAGER_UNAVAILABLE",
    "DISPATCH_PLUNDER_DAILY_LIMIT_REACHED",
    "DISPATCH_PLUNDER_CROSS_SERVER_UNAVAILABLE",
    "DISPATCH_PLUNDER_RESPONSE_TIMEOUT",
    "DISPATCH_PLUNDER_INVALID_SCHEDULE",
    "DISPATCH_PLUNDER_ALREADY_ARMED",
    "DISPATCH_PLUNDER_SEND_FAILED",
]

HANDLER_REQUIRED = [
    "function 0x33DAAB-0x33EAE3",
    "0x33DAF8: cmp     qword ptr [rax + 0x18], 0x1b",
    "'h-plunder-resultmap.dispatch-plu '",
    "'map.dispatch-plu '",
    "'serverIdABC'",
    "'taskUuidDISPATCH_PLUNDER_SERVER_REJECTED:",
    "'successerrorCode",
    "'errorCode",
    "serverDayStartAt",
    "'DISPATCH_PLUNDER_INVALID_TARGET",
    "'DISPATCH_PLUNDER_REQUEST_PENDING",
    "'DISPATCH_PLUNDER_MANAGER_UNAVAILABLE",
    "'DISPATCH_PLUNDER_DAILY_LIMIT_REACHED",
    "'DISPATCH_PLUNDER_CROSS_SERVER_UNAVAILABLE",
    "'DISPATCH_PLUNDER_RESPONSE_TIMEOUT",
    "'DISPATCH_PLUNDER_INVALID_SCHEDULE",
    "'DISPATCH_PLUNDER_ALREADY_ARMED",
    "'DISPATCH_PLUNDER_SEND_FAILED",
    "'DISPATCH_PLUNDER_SERVER_REJECTED:",
    "0x33E46C: lea     rcx",
    "'succeeded'",
    "'failedsucceeded'",
    "0x33E7C1: lea     rdx",
    "'DISPATCH_PLUNDER_DAILY_LIMIT_REACHED-stop remaining dispatch plunder jobs failed: '",
    "0x33E9C0: call    0x14026e64a",
    "'*dispatch plunder history prune failed day='",
    "'bridge://dispatch-plunder-changed&recover dispatch plunder jobs failed: '",
]

WORKER_REQUIRED = [
    "function 0xF04EC-0xF1C9E",
    "0xF06D0: call    0x1403cf086",
    "0xF06D5: test    al, al",
    "0xF06D7: je      0x1400f0ba8",
    "'DISPATCH_PLUNDER_GAME_DISCONNECTEDDISPATCH_PLUNDER_RESPONSE_TIMEOUT'",
    "0xF12ED: mov     rax, qword ptr [rbx + 0x108]",
    "0xF12F4: mov     r13, qword ptr [rbx + 0x178]",
    "0xF12FB: cmp     rax, r13",
    "0xF12FE: cmovg   r13, rax",
    "0xF1302: add     r13, 0x7530",
    "0xF1343: mov     qword ptr [r15 + 0x28], r13",
    "0xF1669: mov     qword ptr [rbx + 0x1b8], 0x1388",
    "0xF1721: call    0x1403cf086",
    "0xF1726: mov     ebp, eax",
    "0xF17A6: test    bpl, bpl",
    "0xF17B3: cmovne  rcx, rax",
    "'failedINVALID_TREASURE_CLAIM_SCOPE",
    "'waiting_connectionserver response timeout'",
    "0xF17C5: cmovne  rdx, rax",
]


def require(report: str, required: list[str], label: str) -> None:
    missing = [value for value in required if value not in report]
    if missing:
        raise InspectError(
            f"{label} missing expected anchors: "
            + ", ".join(repr(value) for value in missing)
        )


def verify(binary: Path) -> dict[str, object]:
    digest = hashlib.sha256(binary.read_bytes()).hexdigest()
    if digest != EXPECTED_SHA256:
        raise InspectError(
            f"unsupported lwbridge SHA-256 {digest}; expected {EXPECTED_SHA256}"
        )

    handler = inspect(binary, (), dump_rva=RESULT_HANDLER_RVA)
    worker = inspect(binary, (), dump_rva=WORKER_RVA)
    connection = inspect(binary, (), dump_rva=CONNECTION_PREDICATE_RVA)
    require(handler, HANDLER_REQUIRED, "Dispatch result handler")
    require(worker, WORKER_REQUIRED, "Dispatch worker")
    require(
        connection,
        [
            "function 0x3CF086-0x3CF0E7",
            "0x3CF09B: call    0x1403c573f",
            "0x3CF0C3: call    0x1403c2d1b",
            "0x3CF0DD: mov     eax, ebx",
        ],
        "Dispatch connection predicate",
    )

    # The compiler compares two overlapping 16-byte chunks of the 27-byte
    # event key. Together they recover exactly:
    #   0..15  "map.dispatch-plu"
    #   11..26 "h-plunder-result"
    if EVENT_TYPE[:16] != "map.dispatch-plu" or EVENT_TYPE[11:] != "h-plunder-result":
        raise InspectError("internal Dispatch result event reconstruction is inconsistent")

    return {
        "ok": True,
        "binary": str(binary),
        "sha256": digest,
        "workerRva": f"0x{WORKER_RVA:X}",
        "resultHandlerRva": f"0x{RESULT_HANDLER_RVA:X}",
        "connectionPredicateRva": f"0x{CONNECTION_PREDICATE_RVA:X}",
        "eventType": EVENT_TYPE,
        "eventTypeLength": len(EVENT_TYPE),
        "payloadFields": RESULT_FIELDS,
        "knownErrors": KNOWN_ERRORS,
        "unknownServerError": "DISPATCH_PLUNDER_SERVER_REJECTED: <detail>",
        "terminalResult": {
            "successTrue": "succeeded",
            "successFalse": "failed",
            "identity": "(serverId, taskUuid)",
        },
        "pendingResultDeadline": "max(now, executeAt) + 30000ms",
        "armCallTimeoutMilliseconds": 5000,
        "connectionSemantics": {
            "preArmUnavailable": (
                "waiting_connection / DISPATCH_PLUNDER_GAME_DISCONNECTED"
            ),
            "armFailureConnected": "failed / preserve arm error",
            "armFailureDisconnected": "waiting_connection / preserve arm error",
        },
        "dailyLimit": (
            "after current result persistence, "
            "DISPATCH_PLUNDER_DAILY_LIMIT_REACHED stops remaining active jobs"
        ),
        "historyPrune": "optional positive serverDayStartAt prunes old Dispatch history",
        "changeEvent": "bridge://dispatch-plunder-changed",
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=Path)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        result = verify(args.binary.resolve())
    except (InspectError, OSError, ValueError) as exc:
        parser.error(str(exc))
        return 2

    if args.json:
        import json
        print(json.dumps(result, indent=2))
    else:
        print("PASS original Dispatch post-arm/result inspection")
        print(f"sha256={result['sha256']}")
        print(f"eventType={result['eventType']}")
        print(f"payloadFields={','.join(result['payloadFields'])}")
        print(f"pendingResultDeadline={result['pendingResultDeadline']}")
        print(
            "armFailureConnected="
            + result["connectionSemantics"]["armFailureConnected"]
        )
        print(
            "armFailureDisconnected="
            + result["connectionSemantics"]["armFailureDisconnected"]
        )
        print(f"dailyLimit={result['dailyLimit']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
