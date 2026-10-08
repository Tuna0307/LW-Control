"""Byte-level assertions for the HOME 009 R1 D recovery of the original reconcile/profile-selection contract.

Reference-only (nothing is executed).  Binds the facts in ``r1/profile-intent-recovery.md`` to instruction boundaries,
immediates and string bytes of lwbridge-0.3.17.exe (SHA-256 4E9C3113...D6783, gated by home009_native).
Run: python tools/lwbridge317/home009_profile_contract.py   (exit status 1 on any failed assertion)
"""
from __future__ import annotations

import json
import sys

import home009_native as n
from home009_launch_contract import at, call_target, target, text

RECON = (0x203021, 0x2057DC)


def verify() -> dict:
    facts: dict[str, object] = {}

    # ---- profile store: SQL, ordering and record layout ---------------------------------------------
    sql = (b"SELECT id, display_name, role_name, server_id, game_uid, note,\n"
           b"                        display_order, enabled, locked_reason, is_primary,\n"
           b"                        created_at, updated_at, last_launched_at\n"
           b"                 FROM profiles ORDER BY display_order, created_at, id")
    text(0xD4E5CA, sql)
    text(0xD4EA5A, b"enabled")
    text(0xD4EA6D, b"isPrimary")
    ser = (0x328924, 0x328BF8)
    at(ser, 0x328A9E, "lea", "r9, [rsi + 0xd0]")            # record+0xd0 serialised under key "enabled" (0xd4ea5a, len 7)
    at(ser, 0x328AB1, "mov", "r8d, 7")
    at(ser, 0x328B02, "lea", "r9, [rsi + 0xd1]")            # record+0xd1 under "isPrimary" (len 9)
    at(ser, 0x328B15, "mov", "r8d, 9")
    facts["profileStore"] = {"db": "controller.db", "order": "display_order, created_at, id",
                             "record": "0xd8 bytes: enabled @+0xd0, isPrimary @+0xd1, id String @+0x18/+0x20"}

    # ---- reconcile pipeline -----------------------------------------------------------------------
    at(RECON, 0x203519, "cmp", "byte ptr [rbx + 0x66b0], 5")        # payload is a JSON object
    text(0x83CF58, b"autoLaunchAll")
    at(RECON, 0x203530, "mov", "r8d, 0xd")
    at(RECON, 0x20354A, "mov", "al, 1")                              # missing / non-boolean => true
    at(RECON, 0x203551, "mov", "byte ptr [rbx + 0x68d1], al")
    assert call_target(RECON, 0x203576) == 0x1DDBB2                  # pre-step
    assert call_target(RECON, 0x2035CE) == 0x23F685                  # capacity-bounded profile prepare
    assert call_target(RECON, 0x2035FF) == 0x325ABE                  # SELECT ... ORDER BY display_order, created_at, id
    assert call_target(RECON, 0x203668) == 0x23F466                  # per-profile runtime status list (0x78-byte items)
    assert call_target(RECON, 0x2036D6) == 0x39E6E1                  # records -> (id, enabled, reason) entries
    at(RECON, 0x2036E9, "mov", "r8b, byte ptr [rbx + 0x68d1]")      # autoLaunchAll captured by the filter closure
    assert call_target(RECON, 0x20371D) == 0x39E153                  # filter: enabled && (restartRequired || autoLaunchAll)
    assert call_target(RECON, 0x203745) == 0x39E77D                  # ids of the restartRequired subset
    assert call_target(RECON, 0x2037A8) == 0x1E70F7                  # authorization-state read-lock guard
    at(RECON, 0x203776, "mov", "al, byte ptr [rdi + 0x140]")        # lock poison flag (NOT a launch preference)
    at(RECON, 0x203780, "setne", "cl")
    assert call_target(RECON, 0x2053FA) == 0x1D5009                  # the single launch call, closeUnmanaged slot = false
    at(RECON, 0x2053D9, "mov", "word ptr [rbx + 0x71f0], 0")

    # record -> entry mapper 0x392eb9 (called through 0x39e6e1 -> 0x3fc04d)
    m = (0x392EB9, 0x392FD8)
    at(m, 0x392F33, "mov", "rsi, qword ptr [r15 + 0x18]")           # id ptr
    at(m, 0x392F37, "mov", "rdi, qword ptr [r15 + 0x20]")           # id len
    at(m, 0x392F72, "mov", "rax, qword ptr [rbp + r12 + 0x38]")     # status.reason ptr
    at(m, 0x392F77, "mov", "rcx, qword ptr [rbp + r12 + 0x40]")     # status.reason len
    at(m, 0x392F86, "mov", "dl, byte ptr [r15 + 0xd0]")              # enabled
    at(m, 0x392F9F, "mov", "byte ptr [r10 + r8*8 + 0x10], dl")       # entry.enabled
    # filter 0x39e153: entry.enabled == 1, then closure 0x2ea391
    f = (0x39E153, 0x39E26A)
    at(f, 0x39E193, "cmp", "byte ptr [r8 + 0x10], 1")
    assert call_target(f, 0x39E1A0) == 0x2EA391
    at(f, 0x39E1AA, "cmp", "r12, 2")                                  # tag 2 = dropped
    # closure 0x2ea391: reason == "restartRequired" => tag 0; no reason => autoLaunchAll => tag 1; other reason => dropped
    c = (0x2EA391, 0x2EA48D)
    at(c, 0x2EA39D, "mov", "rax, qword ptr [r8 + 0x18]")              # reason ptr
    at(c, 0x2EA3A6, "cmp", "qword ptr [r8 + 0x20], 0xf")              # len("restartRequired")
    at(c, 0x2EA3AD, "movabs", "rcx, 0x5274726174736572")              # "restartR"
    at(c, 0x2EA3BA, "movabs", "rdx, 0x6465726975716552")              # "equired"
    at(c, 0x2EA3FD, "cmp", "byte ptr [rax], 1")                        # autoLaunchAll (reason absent)
    at(c, 0x2EA456, "mov", "qword ptr [rsi], 1")                       # kind 1 = auto
    at(c, 0x2EA473, "mov", "qword ptr [rsi], 0")                       # kind 0 = restartRequired
    at(c, 0x2EA415, "mov", "qword ptr [rsi], 2")                       # dropped
    # 0x39e77d keeps kind 0 only
    r = (0x39E77D, 0x39E862)
    at(r, 0x39E7AA, "cmp", "dword ptr [rbx], 1")
    # 0x1e70f7: poisoned guard => STATE_UNAVAILABLE "authorization state is unavailable"
    g = (0x1E70F7, 0x1E716D)
    at(g, 0x1E7104, "cmp", "dword ptr [rdx], 1")
    text(0x1E7112 + 7 + 0x655287, b"STATE_UNAVAILABLEauthorization state is unavailable")
    facts["reconcile"] = {
        "input": "payload autoLaunchAll (default true); NO persisted launch preference is read natively",
        "selection": "enabled profiles in display_order, created_at, id",
        "filter": "reason == restartRequired (any autoLaunchAll) | reason absent && autoLaunchAll | otherwise dropped",
        "launch": "sequential 0x1d5009 with closeUnmanaged=false; per-profile errors {profileId, error} appended, loop continues",
        "supersedes": "008/009 reading of [cfg+0x140] as a global auto-launch gate (it is the authorization-state lock poison flag)",
    }
    return facts


if __name__ == "__main__":
    try:
        print(json.dumps(verify(), indent=2))
    except AssertionError as exc:
        print("FAIL:", exc)
        sys.exit(1)
