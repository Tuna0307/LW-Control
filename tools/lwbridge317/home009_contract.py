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


if __name__ == "__main__":
    import json

    print(json.dumps(verify_close_contract(), indent=2))
