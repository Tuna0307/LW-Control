"""Byte-level assertions for the HOME 009 R1 C launch/readiness recovery (reference EXE only; nothing is executed).

Each assertion binds a recovered fact in ``evidence/.../r1/launch-readiness-recovery.md`` to instruction boundaries,
immediates, branch targets or string bytes of lwbridge-0.3.17.exe (SHA-256 4E9C3113...D6783, gated by home009_native).
Run: python tools/lwbridge317/home009_launch_contract.py   (exit status 1 on any failed assertion)
"""
from __future__ import annotations

import json
import sys

import home009_native as n

FN_LAUNCH = (0x1D5009, 0x1DDBB2)
FN_CMD_START = (0x20715F, 0x207CA9)
FN_RECONCILE = (0x203021, 0x2057DC)
FN_RESTART = (0x2057DC, 0x20715F)
_cache: dict[tuple[int, int], dict[int, tuple[str, str]]] = {}


def ins(span):
    if span not in _cache:
        _cache[span] = {i.address - n.BASE: (i.mnemonic, i.op_str) for i in n.dis_range(*span)}
    return _cache[span]


def at(span, rva, mnemonic, op=None):
    got = ins(span).get(rva)
    assert got is not None, f"{rva:#x}: no instruction boundary"
    assert got[0] == mnemonic, f"{rva:#x}: {got} != {mnemonic}"
    if op is not None:
        assert got[1] == op, f"{rva:#x}: {got[1]!r} != {op!r}"


def target(span, rva, mnemonic):
    got = ins(span).get(rva)
    assert got is not None and got[0] == mnemonic, f"{rva:#x}: {got} != {mnemonic}"
    return int(got[1], 16) - n.BASE


def text(rva, expected: bytes):
    got = n.pe.get_data(rva, len(expected))
    assert got == expected, f"{rva:#x}: {got!r} != {expected!r}"


def call_target(span, rva):
    return target(span, rva, "call")


def verify() -> dict:
    facts: dict[str, object] = {}
    L = FN_LAUNCH

    # ---- closeUnmanaged input (start command 0x20715f; reconcile/restart store false) -----------------
    at(FN_CMD_START, 0x207784, "lea", "rdx, [rip + 0x634dbd]")
    text(0x207784 + 7 + 0x634DBD, b"closeUnmanaged")
    at(FN_CMD_START, 0x20778B, "mov", "r8d, 0xe")
    at(FN_CMD_START, 0x20779E, "cmp", "byte ptr [rax], 1")          # JSON boolean tag
    at(FN_CMD_START, 0x2077A3, "mov", "al, byte ptr [rax + 1]")
    at(FN_CMD_START, 0x2077AF, "xor", "eax, eax")                   # missing / non-boolean => false
    at(FN_CMD_START, 0x2077DC, "mov", "byte ptr [rbx + 0x5e40], al")  # state + 0x868
    assert call_target(FN_CMD_START, 0x207808) == 0x1D5009
    at(FN_RECONCILE, 0x2053D9, "mov", "word ptr [rbx + 0x71f0], 0")  # 0x6988 + 0x868 / +0x869 => false
    assert call_target(FN_RECONCILE, 0x2053FA) == 0x1D5009
    at(FN_RESTART, 0x206940, "mov", "word ptr [rbx + 0x6710], 0")    # 0x5ea8 + 0x868
    assert call_target(FN_RESTART, 0x206963) == 0x1D5009
    at(L, 0x1D50AB, "mov", "cl, byte ptr [r14 + 0x868]")
    at(L, 0x1D50B2, "mov", "byte ptr [r14 + 0x86a], cl")
    facts["closeUnmanaged"] = {"command": "payload bool, default false", "reconcile": False, "restart": False}

    # ---- unmanaged list + gate ----------------------------------------------------------------------
    assert call_target(L, 0x1D6533) == 0x2A2887
    at(L, 0x1D6542, "je")
    assert target(L, 0x1D6542, "je") == 0x1D73BA                    # empty list: nothing to close
    at(L, 0x1D6548, "cmp", "byte ptr [r14 + 0x86a], 0")
    assert target(L, 0x1D6550, "je") == 0x1D6699                    # closeUnmanaged false => reject path
    text(0x1D678A + 7 + 0x665EFC, b"UNMANAGED_GAME_RUNNINGpids"[:22])
    at(L, 0x1D6781, "mov", "qword ptr [rsp + 0x20], 0x16")          # message length 22 == code
    # ---- sequential path-verified terminate, error aborts ---------------------------------------------
    at(L, 0x1D6578, "mov", "rax, qword ptr [r14 + 0x878]")
    at(L, 0x1D657F, "cmp", "rax, qword ptr [r14 + 0x880]")
    assert target(L, 0x1D6586, "je") == 0x1D693F                    # end of list => deadline setup
    at(L, 0x1D65A5, "mov", "r9d, dword ptr [rax]")                   # pid (u32 element)
    assert call_target(L, 0x1D65AB) == 0x41E543
    at(L, 0x1D65B0, "cmp", "rdi, qword ptr [r14 + 0x888]")
    assert target(L, 0x1D65B7, "jo") == 0x1D6578                    # Ok niche => next pid; otherwise Err aborts
    # ---- 5 s deadline, list/empty/deadline/100 ms loop ------------------------------------------------
    assert call_target(L, 0x1D693F) == 0x5CFC70                     # Instant::now
    at(L, 0x1D6944, "mov", "r8d, 5")
    at(L, 0x1D694D, "xor", "r9d, r9d")
    assert call_target(L, 0x1D6950) == 0x5DC950                     # Instant + Duration(5 s, 0 ns)
    at(L, 0x1D6955, "mov", "qword ptr [r14 + 0x498], rax")
    at(L, 0x1D695C, "mov", "dword ptr [r14 + 0x4a0], edx")
    assert target(L, 0x1D6966, "jmp") == 0x1D723B
    assert call_target(L, 0x1D7254) == 0x41DEA4                     # list processes of <root>\Game\LastWar.exe
    assert call_target(L, 0x1D72A8) == 0x2A2887                     # filter managed pids, sort
    at(L, 0x1D72C4, "test", "rdi, rdi")
    assert target(L, 0x1D72C7, "je") == 0x1D7346                    # empty => closed
    assert call_target(L, 0x1D72C9) == 0x5CFC70
    at(L, 0x1D72D0, "cmp", "edx, dword ptr [r14 + 0x4a0]")
    at(L, 0x1D72D7, "setae", "cl")
    at(L, 0x1D72DC, "cmp", "rax, qword ptr [r14 + 0x498]")
    at(L, 0x1D72E3, "setae", "dl")
    at(L, 0x1D72E6, "cmove", "edx, ecx")                            # (secs, nanos) lexicographic now >= deadline
    assert target(L, 0x1D72EB, "jne") == 0x1D74E5                   # deadline reached => timeout path
    at(L, 0x1D7305, "mov", "r8d, 0x5f5e100")                        # 100 ms
    assert call_target(L, 0x1D730B) == 0x641035                     # sleep future
    assert target(L, 0x1D7341, "jmp") == 0x1D723B                   # loop back to the list step
    at(L, 0x1D7563, "mov", "qword ptr [rsp + 0x20], 0x12")
    text(0x1D756C + 7 + 0x665165, b"GAME_CLOSE_TIMEOUT")
    facts["unmanagedClose"] = {"deadlineSeconds": 5, "pollMilliseconds": 100, "order": "list, empty?, deadline?, sleep",
                               "timeoutCode": "GAME_CLOSE_TIMEOUT", "comparison": "now >= deadline"}

    # ---- managed-pid filter and the sort helper ------------------------------------------------------
    at((0x39D7DE, 0x39D898), 0x39D809, "call")
    assert call_target((0x39D7DE, 0x39D898), 0x39D809) == 0x30A354   # contains(pid) in the managed set
    at((0x39D7DE, 0x39D898), 0x39D813, "jne")                       # contained => skipped
    at((0x30A354, 0x30A405), 0x30A35A, "cmp", "qword ptr [rcx + 0x18], 0")  # empty set => not contained
    sp = (0x2A2887, 0x2A2935)
    assert call_target(sp, 0x2A28B5) == 0x39D7DE
    at(sp, 0x2A28CA, "cmp", "rdi, 2")
    at(sp, 0x2A2905, "cmp", "rdi, 0x15")
    assert call_target(sp, 0x2A291C) == 0x25D68D                     # insertion sort < 21
    assert call_target(sp, 0x2A292E) == 0x2EE38D                     # larger sort

    # ---- bridge-connect wait (pipe registry) ----------------------------------------------------------
    assert call_target(L, 0x1DD114) == 0x2C9034                     # wall-clock milliseconds
    at(L, 0x1DD119, "add", "rax, 0x15f90")                           # + 90_000 ms
    at(L, 0x1DD11F, "mov", "qword ptr [r14 + 0x808], rax")
    at(L, 0x1DD14A, "mov", "qword ptr [rsp + 0x20], rax")            # same value is the registry expiry
    assert call_target(L, 0x1DD152) == 0x2C75CA
    at(L, 0x1DD159, "cmp", "rax, qword ptr [r14 + 0x878]")
    assert target(L, 0x1DD163, "jo") == 0x1DD3F2                    # Ok => wait loop (checks first, no initial sleep)
    assert call_target(L, 0x1DD198) == 0x41E543                     # Err => terminate launched game
    at(L, 0x1DD3B6, "mov", "r8d, 0xee6b280")                         # 250 ms
    assert call_target(L, 0x1DD3BC) == 0x641035
    assert call_target(L, 0x1DD3F2) == 0x2C9034
    at(L, 0x1DD3F7, "cmp", "rax, qword ptr [r14 + 0x808]")
    assert target(L, 0x1DD3FE, "jge") == 0x1DD431                   # now >= deadline => final lookup
    assert call_target(L, 0x1DD424) == 0x2C6A65                     # pid_of(key)
    assert target(L, 0x1DD42B, "je") == 0x1DD3A2                    # not found => sleep and repeat
    assert call_target(L, 0x1DD45C) == 0x2C6A65
    assert target(L, 0x1DD463, "je") == 0x1DD4FD                    # not found after exit of loop => timeout failure
    at(L, 0x1DD469, "mov", "esi, dword ptr [r14 + 0x860]")           # launcher-reported pid
    assert call_target(L, 0x1DD494) == 0x2C6A65
    at(L, 0x1DD49B, "cmovne", "esi, edx")                            # registry pid wins when present
    assert call_target(L, 0x1DD4BD) == 0x41E3A2                     # image-path check of that pid
    assert target(L, 0x1DD4C4, "je") == 0x1DD6B5                    # mismatch => LAUNCH_REPORT_FAILED
    assert call_target(L, 0x1DD592) == 0x41E543                     # timeout path terminates the game
    at(L, 0x1DD6A0, "mov", "qword ptr [rsp + 0x20], 0x14")
    text(0x1DD6A9 + 7 + 0x65F720, b"BRIDGE_START_TIMEOUT")
    at(L, 0x1DD6B5, "mov", "qword ptr [rsp + 0x20], 0x14")
    text(0x1DD6BE + 7 + 0x65F64B, b"LAUNCH_REPORT_FAILED")
    text(0x83CD6A, b"\x1fprofile launch failed instance=")
    facts["bridgeConnect"] = {"deadlineMilliseconds": 90000, "pollMilliseconds": 250, "clock": "0x2c9034 wall clock ms",
                              "order": "now>=deadline?, pid_of?, sleep", "timeoutCode": "BRIDGE_START_TIMEOUT"}

    # ---- registry functions ------------------------------------------------------------------------
    r1 = (0x2C75CA, 0x2C777C)
    text(0xD475D0, b"PIPE_INSTANCE_MISSING")
    text(0xD475E5, b"named pipe instance is not pending")
    text(0xD4758C, b"PIPE_REGISTRATION_INVALID")
    text(0xD475A5, b"named pipe registration cannot be refreshed")
    text(0x2C7627 + 7 + 0x5F4D4A - 0x0, b"STATE_UNAVAILABLE")
    at(r1, 0x2C76C2, "jle")                                           # expiry must be > 0
    at(r1, 0x2C76C4, "cmp", "byte ptr [rax + 0x40], 0")               # not yet connected
    at(r1, 0x2C76CA, "mov", "qword ptr [rax + 0x38], rcx")            # entry.expires = deadline
    r2 = (0x2C6A65, 0x2C6AE1)
    assert call_target(r2, 0x2C6A81) == 0x46750
    assert call_target(r2, 0x2C6AB5) == 0x2C8D3C
    at(r2, 0x2C6ABF, "mov", "edi, dword ptr [rax + 0x28]")            # entry.pid (u32)
    facts["registry"] = {"0x2c75ca": "refresh_pending(key, expiry_ms>0)", "0x2c6a65": "pid_of(key)->Option<u32>"}
    return facts


if __name__ == "__main__":
    try:
        print(json.dumps(verify(), indent=2))
    except AssertionError as exc:
        print("FAIL:", exc)
        sys.exit(1)
