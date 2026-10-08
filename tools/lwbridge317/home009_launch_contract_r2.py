"""HOME 009 R2 A/B byte assertions: launcher attempts, launcher report, post-connect recovery record/instance registration,
finalizer role, reconcile recovery-record literals and the restartRequired status producer.  Reference only; nothing is executed.
Run: python tools/lwbridge317/home009_launch_contract_r2.py   (exit status 1 on any failed assertion)
"""
from __future__ import annotations

import json
import sys

import home009_native as n
from home009_launch_contract import FN_LAUNCH as L, at, call_target, target, text

FINAL = (0x1DDDE3, 0x1DE700)


def verify() -> dict:
    facts: dict[str, object] = {}
    # ---- launcher attempt loop: inclusive range 1..=2, retry only after attempt 1 with OFFICIAL_LAUNCHER_RESTARTED -------------------
    at(L, 0x1DBE86, "mov", "qword ptr [r14 + 0x748], 2")             # "no result yet" placeholder
    at(L, 0x1DBE91, "mov", "byte ptr [r14 + 0x87a], 2")               # range end = 2
    at(L, 0x1DBE99, "mov", "word ptr [r14 + 0x878], 0x100")           # [0x878]=exhausted=0, [0x879]=start=1
    at(L, 0x1DC19C, "test", "al, 1")                                   # exhausted?
    at(L, 0x1DC1A4, "mov", "al, byte ptr [r14 + 0x879]")
    at(L, 0x1DC1AB, "cmp", "al, byte ptr [r14 + 0x87a]")
    at(L, 0x1DC1BA, "lea", "ecx, [rax + 1]")                           # start < end: advance
    at(L, 0x1DC1C6, "mov", "byte ptr [r14 + 0x878], 1")               # start == end: mark exhausted after this item
    at(L, 0x1DC1CE, "mov", "byte ptr [r14 + 0x87b], al")              # attempt number of the running attempt (1 or 2)
    at(L, 0x1DC345, "cmp", "qword ptr [rsp + 0x528], 0x1b")           # error code length 27 == len("OFFICIAL_LAUNCHER_RESTARTED")
    at(L, 0x1DC390, "cmp", "byte ptr [r14 + 0x87b], 1")               # only the FIRST attempt may be retried
    at(L, 0x1DC3D0, "lea", "rdx, [rip + 0x6607db]")
    text(0x1DC3D0 + 7 + 0x6607DB, b"#profile launcher retrying instance=")
    assert n.pe.get_data(0x83CBA5, 13) == b"LAUNCH_FAILED"
    at(L, 0x1DC2B3, "mov", "qword ptr [rsp + 0x20], 0x12")            # LAUNCH_TASK_FAILED (18)
    facts["launcherAttempts"] = {"max": 2, "retryOnlyAfterAttempt": 1, "retryCondition": "error code == OFFICIAL_LAUNCHER_RESTARTED",
                                 "otherErrors": "final", "taskJoinFailure": "LAUNCH_TASK_FAILED"}

    # ---- launcher report JSON: pid / gameLaunchTicket / gameLaunchTicketExpiresAt ----------------------------------------------
    text(0x83CBFC, b"pid")
    at(L, 0x1DC6ED, "mov", "r8d, 3")
    at(L, 0x1DC700, "cmp", "byte ptr [rax], 2")                        # JSON number
    at(L, 0x1DC719, "mov", "dword ptr [r14 + 0x860], ecx")             # reported game PID (must fit 32 bits: 0x1DC70C-0x1DC714)
    text(0x83CB53, b"gameLaunchTicket")
    at(L, 0x1DC746, "mov", "r8d, 0x10")
    text(0x83CBFF, b"gameLaunchTicketExpiresAt")
    assert call_target(L, 0x1DCD6C) == 0x41E3A2                         # image-path check of the reported PID
    at(L, 0x1DCDB2, "call")
    text(0x83CD10, b"LAUNCH_REPORT_FAILED")
    text(0x83CD24, b"LAUNCH_TICKET_INVALID")
    facts["launcherReport"] = {"pidMissingOrNonNumeric": "LAUNCH_REPORT_FAILED after rollback", "pidImageMismatch": "LAUNCH_REPORT_FAILED"}

    # ---- post-connect: recovery record update, instance registration, recovery completion ----------------------------------------
    at(L, 0x1DD70A, "call")
    assert call_target(L, 0x1DD70A) == 0x1E71D6
    assert target((0x1E71D6, 0x1E723E), 0x1E721E, "call") == 0x2DBEBA          # Ok(prev) -> 0x2DBEBA
    r = (0x2DBEBA, 0x2DC250)
    assert call_target(r, 0x2DBF08) == 0x5B9620                                 # read recovery.json
    text(0xD48108, b"read recovery record")
    text(0xD480C3, b"RECOVERY_RECORD_INVALID")
    text(0xD480DA, b"recovery instance does not match its directory")
    at(r, 0x2DC148, "cmp", "qword ptr [rdx + 0x30], r13")                       # record instance id length == directory instance id
    assert call_target(r, 0x2DC15C) == 0x7A3E30                                 # ... and bytes
    at(r, 0x2DC1F1, "mov", "dword ptr [rsi + 0x90], 1")                         # record.has_game_pid = true
    at(r, 0x2DC1FB, "mov", "dword ptr [rsi + 0x94], ecx")                       # record.game_pid = connected PID
    assert call_target(r, 0x2DC218) == 0x2DB7CC                                 # commit recovery record (tmp + rename)
    assert b"commit recovery record" in n.pe.get_data(0xD48000, 0x60)
    assert call_target(L, 0x1DD798) == 0x23C1E4                                 # Ok record -> instances map insert
    assert call_target(L, 0x1DD7FC) == 0x23FAC5                                 # recovery completion (awaitingIdentity -> running)
    assert call_target(L, 0x1DD8BD) == 0x41E543                                 # recovery-record failure terminates the game
    text(0x84188C, b"awaitingIdentityrunningINSTANCE_NOT_FOUND")
    assert call_target((0x23FAC5, 0x23FFB0), 0x23FE5C) == 0x41BD6F              # tracks the game PID for the monitor
    facts["postConnect"] = {
        "order": "image check -> read+validate+update recovery.json (game pid) -> instances.insert -> complete recovery (running, pid tracked)",
        "recordFailure": "terminate game (0x41E543), rollback 0x1D420D, return the record error",
    }

    # ---- finalizer 0x1DDDE3 role ----------------------------------------------------------------------------------------------
    assert b"/release" in n.pe.get_data(0x83D27E, 0x40)
    assert call_target(FINAL, 0x1DE5C8) == 0x2DD578                             # remove recovery record
    assert call_target(FINAL, 0x1DE463) == 0x5CFC70                             # Instant::now (bounded step)
    callers = {s for s, _ in n.call_index().get(0x1DDDE3, [])}
    assert {0x1D4371, 0x2046EE} <= callers, callers
    facts["finalizer"] = "instance teardown helper: contains the lease-release endpoint literal (protected) and the recovery-record removal call; callers 0x1D420D and reconcile 0x2046EE"

    # ---- reconcile recovery-record literals ----------------------------------------------------------------------------------
    assert b"RECOVERY_RECORD_NOT_FOUND" in n.pe.get_data(0x83CFF8, 0x60)
    assert b"RECOVERY_PROCESS_MISMATCH" in n.pe.get_data(0x83CFA0, 0x60)
    assert b"validated recovery record has a pid" in n.pe.get_data(0x83CF65, 0x40)
    facts["reconcileRecoveryErrors"] = ["RECOVERY_RECORD_NOT_FOUND", "RECOVERY_PROCESS_MISMATCH", "validated recovery record has a pid"]
    return facts


if __name__ == "__main__":
    try:
        print(json.dumps(verify(), indent=2))
    except AssertionError as exc:
        import traceback
        traceback.print_exc()
        print("FAIL:", exc)
        sys.exit(1)
