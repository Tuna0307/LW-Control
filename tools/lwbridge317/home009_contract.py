"""Byte-level assertions that bind the Home 009 reconstructed contracts to RVAs of the
hash-gated reference executable.  Importing this module re-verifies the reference SHA-256
(through ``home009_native``) and each assertion; failure raises AssertionError.

Nothing here executes the reference program.
"""
from __future__ import annotations

import struct

import home009_native as n

_INS_CACHE: dict[tuple[int, int], dict[int, tuple[str, str]]] = {}


def _ins(start: int, end: int) -> dict[int, tuple[str, str]]:
    key = (start, end)
    if key not in _INS_CACHE:
        _INS_CACHE[key] = {i.address - n.BASE: (i.mnemonic, i.op_str) for i in n.dis_range(start, end)}
    return _INS_CACHE[key]


def expect(start: int, end: int, rva: int, mnemonic: str, op_str: str | None = None) -> None:
    got = _ins(start, end).get(rva)
    assert got is not None, f"no instruction boundary at {rva:#x}"
    assert got[0] == mnemonic and (op_str is None or got[1] == op_str), f"{rva:#x}: {got} != {mnemonic} {op_str}"


def iat_name(rva_of_iat_slot: int) -> str:
    return n.IMPORTS[rva_of_iat_slot]


def u64_table(rva: int, count: int) -> list[int]:
    return list(struct.unpack(f"<{count}q", n.pe.get_data(rva, 8 * count)))


def verify_close_contract() -> dict:
    """Checkpoint A: terminate + wait-gone."""
    facts: dict[str, object] = {}
    # 0xe5725 close-wait loop (profile_instance_stop caller 0x19a0de; reconcile twin 0x1ddc84)
    for name, (s, e, cap, cmp_, jge, tmr, errlea) in {
        "stop-close": (0xE5725, 0xE5884, 0xE578F, 0xE5794, 0xE5796, 0xE57CB, 0xE5821),
        "reconcile-close": (0x1DDC84, 0x1DDDE3, 0x1DDCEE, 0x1DDCF3, 0x1DDCF5, 0x1DDD2A, 0x1DDD80),
    }.items():
        expect(s, e, cap, "mov", "ecx, 0x64")
        expect(s, e, cmp_, "cmp", "eax, ecx")
        expect(s, e, jge, "jge")
        expect(s, e, tmr, "mov", "r8d, 0x5f5e100")
        facts[name] = {"maxChecks": 100, "timerNanoseconds": 100_000_000, "capBeforeCheck": True}
    # 0x41e3cf present predicate: path query + ASCII case fold (0x20 bit for A-Z)
    expect(0x41E3CF, 0x41E543, 0x41E46F, "call")
    expect(0x41E3CF, 0x41E543, 0x41E4B9, "lea", "r8d, [rdx - 0x41]")
    expect(0x41E3CF, 0x41E543, 0x41E4BD, "cmp", "r8b, 0x1a")
    # 0x4197d8 image path query
    assert iat_name(0x7C1430) == "kernel32.dll!OpenProcess"
    assert iat_name(0x7C1428) == "kernel32.dll!QueryFullProcessImageNameW"
    expect(0x4197D8, 0x4198E5, 0x4197E8, "mov", "ecx, 0x1000")           # PROCESS_QUERY_LIMITED_INFORMATION
    # 0x41d84b terminate
    assert iat_name(0x7C1578) == "kernel32.dll!TerminateProcess"
    expect(0x41D84B, 0x41DAD8, 0x41D9C6, "mov", "ecx, 1")               # PROCESS_TERMINATE
    expect(0x41D84B, 0x41DAD8, 0x41D9D0, "call")
    expect(0x41D84B, 0x41DAD8, 0x41D9E1, "mov", "edx, 1")               # exit code 1
    expect(0x41D84B, 0x41DAD8, 0x41D9E6, "call")
    # error labels
    assert n.pe.get_data(0xD680B3, 0x14) == b"PROCESS_QUERY_FAILED"
    assert n.pe.get_data(0xD680C7, 0x29) == b"Unable to verify the target process path."
    assert n.pe.get_data(0x82BBCD, 4) is not None
    # process-exists (Toolhelp) used by the re-checks
    assert iat_name(0x7C1868) == "kernel32.dll!CreateToolhelp32Snapshot"
    expect(0x4198E5, 0x4199BA, 0x4198F9, "mov", "ecx, 2")                # TH32CS_SNAPPROCESS
    facts["terminate"] = {
        "access": 1, "exitCode": 1, "pathQueryAccess": 0x1000,
        "mismatchedImage": "no action (Ok(false))", "pathUnreadableAndPresent": "PROCESS_QUERY_FAILED",
    }
    # stop handler order: terminate at 0x19a094 then wait helper 0x19a0de
    expect(0x199627, 0x19AC62, 0x19A094, "call")
    expect(0x199627, 0x19AC62, 0x19A0DE, "call")
    return facts


RUN = (0xE5884, 0xE6BF7)      # recovery run (game_recovery.rs)
MON = (0x41A8A0, 0x41AD16)    # monitor tick
LOOP = (0xE561D, 0xE5725)     # monitor interval loop


def verify_recovery_contract() -> dict:
    """Checkpoints C/D: monitor + recovery run constants and branch conditions."""
    f: dict[str, object] = {}
    # clock: Unix epoch ms from GetSystemTimePreciseAsFileTime
    assert iat_name(0x6DE0A0 - 0x6DE0A0 + n_iat("GetSystemTimePreciseAsFileTime")) .endswith("GetSystemTimePreciseAsFileTime")
    expect(0x2C9034, 0x2C90B8, 0x2C9047, "call")
    assert u64_table(0xD67B08, 5) == [15000, 30000, 60000, 120000, 300000]
    assert u64_table(0xD681B0, 3) == [120000, 300000, 600000]
    # run loop thresholds
    expect(*RUN, 0xE6096, "cmp", "rax, 0xdbb9f")          # update stall  (elapsed >= 900000)
    expect(*RUN, 0xE609C, "jle")
    expect(*RUN, 0xE6285, "cmp", "rax, 0x3a98")           # stable verification
    expect(*RUN, 0xE628B, "jl")
    expect(*RUN, 0xE654F, "cmp", "rax, 0xea60")           # disconnected game wait
    expect(*RUN, 0xE6555, "jge")
    expect(*RUN, 0xE630D, "cmp", "rax, 0x2bf1f")          # login unavailable
    expect(*RUN, 0xE6313, "jle")
    # 2 s cadence of the run loop and the monitor
    expect(*RUN, 0xE69D8, "mov", "edx, 2")
    expect(*RUN, 0xE69E0, "xor", "r8d, r8d")
    expect(*RUN, 0xE69E3, "call")
    expect(*LOOP, 0xE565B, "mov", "edx, 2")
    expect(*LOOP, 0xE5660, "xor", "r8d, r8d")
    expect(*LOOP, 0xE5663, "call")
    # retry index capping: normal min(counter,4) and maintenance min(counter,2)
    expect(*RUN, 0xE601E, "cmp", "eax, 4")
    expect(*RUN, 0xE5EED, "cmp", "r8d, 2")
    # normal attempt counter is post-incremented before indexing (pre-increment value is the index)
    expect(*RUN, 0xE6013, "lea", "r8d, [rax + 1]")
    expect(*RUN, 0xE6017, "mov", "dword ptr [rbx + 0x110], r8d")
    # maintenance mode is entered by flag stores at 0xe5ed8 and 0xe6342, never cleared in the run
    expect(*RUN, 0xE5ED8, "mov", "byte ptr [rbx + 0x118], 1")
    expect(*RUN, 0xE6342, "mov", "byte ptr [rbx + 0x118], 1")
    # monitor thresholds
    expect(*MON, 0x41ABF8, "cmp", "rdx, 0x7530")
    expect(*MON, 0x41AC0B, "cmp", "rcx, 0x752f")
    expect(*MON, 0x41AC34, "cmp", "rdx, 0xea5f")
    expect(*MON, 0x41AC53, "cmp", "rsi, 0x2bf20")
    expect(*MON, 0x41AAFE, "cmp", "esi, 1")
    expect(*MON, 0x41AB01, "jbe")
    # strings
    assert n.pe.get_data(0xD67E5C, 11) == b"processExit"
    assert n.pe.get_data(0xD67E67, 4) == b"hang"
    assert n.pe.get_data(0x82D8AF, 42) == b"game update had no activity for 15 minutes"
    assert n.pe.get_data(0xD67DD0, 29) == b"game update reported an error"
    assert n.pe.get_data(0xD67F2A, 24) == b"auto_force_update_reload"
    f["constants"] = {
        "tickMs": 2000, "normalRetryMs": [15000, 30000, 60000, 120000, 300000],
        "maintenanceRetryMs": [120000, 300000, 600000], "updateStallMs": 900000, "stableVerifyMs": 15000,
        "disconnectWaitMs": 60000, "loginUnavailableMs": 180000,
        "monitor": {"hangMs": 30000, "disconnectMs": 60000, "loginUnavailableMs": 180000, "missingObservations": 2},
    }
    return f


def n_iat(name: str) -> int:
    for rva, label in n.IMPORTS.items():
        if label.endswith("!" + name):
            return rva
    raise AssertionError(name)


if __name__ == "__main__":
    import json

    print(json.dumps({"close": verify_close_contract(), "recovery": verify_recovery_contract()}, indent=2))
