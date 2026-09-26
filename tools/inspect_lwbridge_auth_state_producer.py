#!/usr/bin/env python3
"""Verify the LWBridge 0.3.1 public AuthState producer and transition contract."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"

class InspectError(ValueError):
    pass

def require(ok: bool, message: str) -> None:
    if not ok:
        raise InspectError(message)

def inspect(binary: Path) -> dict[str, object]:
    data = binary.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    require(digest == EXPECTED_SHA256, f"unsupported reference SHA-256: {digest}")

    pe = pefile.PE(data=data, fast_load=False)
    base = int(pe.OPTIONAL_HEADER.ImageBase)
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True

    def ins_at(rva: int):
        off = int(pe.get_offset_from_rva(rva))
        return next(md.disasm(data[off:off + 16], base + rva))

    def expect(rva: int, mnemonic: str, contains: str | None = None):
        ins = ins_at(rva)
        require(ins.mnemonic == mnemonic, f"0x{rva:X}: expected {mnemonic}, got {ins.mnemonic}")
        if contains is not None:
            require(contains in ins.op_str, f"0x{rva:X}: expected operand containing {contains!r}, got {ins.op_str!r}")
        return ins

    def direct_call_target(rva: int) -> int:
        ins = expect(rva, "call")
        require(ins.operands and ins.operands[0].type == X86_OP_IMM, f"0x{rva:X}: expected direct call")
        return int(ins.operands[0].imm - base)

    def rip_target(ins) -> int | None:
        for op in ins.operands:
            if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                return int(ins.address + ins.size + op.mem.disp - base)
        return None

    def read_ascii(rva: int, length: int) -> str:
        raw = int(pe.get_offset_from_rva(rva))
        return data[raw:raw + length].decode("ascii")

    def assert_rip_literal(rva: int, mnemonic: str, literal: str) -> None:
        ins = expect(rva, mnemonic)
        target = rip_target(ins)
        require(target is not None, f"0x{rva:X}: missing RIP target")
        require(read_ascii(target, len(literal)) == literal, f"0x{rva:X}: expected {literal!r}")

    # Public AuthState serializer: exact eight fields and fixed struct offsets.
    serializer_fields = [
        ("phase", 0x23E9CB, 0x00, 5),
        ("username", 0x23E9F8, 0x18, 8),
        ("accessRole", 0x23EA27, 0x30, 10),
        ("watermarkTraceCode", 0x23EA56, 0x48, 18),
        ("expiresAt", 0x23EA85, 0x60, 9),
        ("lastHeartbeatAt", 0x23EAB4, 0x78, 15),
        ("lockedUntil", 0x23EAE2, 0x90, 11),
        ("errorCode", 0x23EB0C, 0xA8, 9),
    ]
    serializer = []
    for name, rva, field_offset, length in serializer_fields:
        ins = expect(rva, "lea", "rdx")
        target = rip_target(ins)
        require(target is not None, f"{name}: missing key target")
        require(read_ascii(target, length) == name, f"{name}: serializer key mismatch")
        serializer.append({"name": name, "offset": hex(field_offset), "length": length})

    # Inline phase constructors.
    expect(0x238DC7, "movabs", "0x676e696b63656863")  # checking
    expect(0x239213, "movabs", "0x7a69726f68747561")  # authoriz
    expect(0x239220, "mov", "0x6465")                 # ed
    expect(0x239799, "mov", "0x63617267")             # grac
    expect(0x239795, "mov", "0x65")                   # e
    expect(0x238599, "movabs", "0x754f64656e676973")  # signedOu
    expect(0x2385A6, "mov", "0x74")                   # t
    expect(0x237CA1, "mov", "0x6b636f6c")             # lock
    expect(0x237C9B, "mov", "0x6465")                 # ed

    # Default role for signed-out/locked families is "normal".
    expect(0x237D2C, "mov", "0x6d726f6e")
    expect(0x237D26, "mov", "0x6c61")
    expect(0x238603, "mov", "0x6d726f6e")
    expect(0x2385FD, "mov", "0x6c61")

    # Exact terminal error constructors.
    assert_rip_literal(0x239045, "movups", "NETWORK_GRACE_EXPIRED")
    expect(0x2395C4, "movabs", "0x444552495058455f")
    expect(0x2395D2, "movabs", "0x5f544e554f434341")
    expect(0x2382AD, "movabs", "0x5f4e4f4953534553")
    expect(0x2382BA, "movabs", "0x44494c41564e495f")

    # Authorized copies identity projection, refreshes public heartbeat string,
    # and clears lockedUntil/errorCode.
    require(direct_call_target(0x239316) == 0x23F244, "authorized: lastHeartbeatAt formatter mismatch")
    expect(0x23937E, "mov", "qword ptr [rdi + 0x368]")
    expect(0x2393A7, "mov", "qword ptr [rdi + 0x380]")

    # SignedOut clears user-visible identity/timing/error state.
    expect(0x2385D3, "mov", "qword ptr [rdi + 0x300], 0")
    expect(0x238637, "mov", "qword ptr [rdi + 0x330]")
    expect(0x238668, "mov", "qword ptr [rdi + 0x338]")
    expect(0x238691, "mov", "qword ptr [rdi + 0x350]")
    expect(0x2386BA, "mov", "qword ptr [rdi + 0x368]")
    expect(0x2386E3, "mov", "qword ptr [rdi + 0x380]")

    # Checking only changes phase and clears current public errorCode.
    expect(0x238E28, "mov", "qword ptr [rdi + 0x380]")

    # All phase emitters publish the complete current state through bridge://auth-state.
    assert_rip_literal(0x238EE8, "lea", "bridge://auth-state")
    assert_rip_literal(0x239467, "lea", "bridge://auth-state")
    assert_rip_literal(0x2387A3, "lea", "bridge://auth-state")

    # Transition ownership.
    calls = {
        "renew_locked": (0x0F4EF1, 0x238825),
        "renew_authorized": (0x0F4FB4, 0x2391D4),
        "heartbeat_authorized": (0x0F6AFC, 0x2391D4),
        "heartbeat_grace": (0x0F6DA9, 0x239756),
        "heartbeat_locked": (0x0F6E7D, 0x238825),
        "supervisor_checking": (0x0F707C, 0x238D8B),
        "supervisor_session_invalid": (0x0F7384, 0x238289),
        "supervisor_signed_out": (0x0F7C99, 0x23855D),
        "logout_signed_out": (0x2021E3, 0x23855D),
        "login_projection": (0x0F20DF, 0x239937),
    }
    for label, (site, target) in calls.items():
        require(direct_call_target(site) == target, f"{label}: target mismatch")

    # Grace clock: initialize internal +0x420 on first eligible failure,
    # then admit grace while elapsed <= 900000 ms.
    expect(0x0F6D53, "cmp", "qword ptr [rcx + 0x420], 0")
    require(direct_call_target(0x0F6D5D) == 0x23F1C0, "grace: initial now-ms helper mismatch")
    expect(0x0F6D66, "mov", "qword ptr [rcx + 0x420], rax")
    require(direct_call_target(0x0F6D7A) == 0x23CB51, "grace: eligibility predicate mismatch")
    require(direct_call_target(0x0F6D83) == 0x23F1C0, "grace: elapsed now-ms helper mismatch")
    expect(0x0F6D8C, "sub", "qword ptr [rdx + 0x420]")
    expect(0x0F6D93, "cmp", "0xdbba0")
    expect(0x0F6D99, "jg", "0x1400f6de7")

    # Auth admission uses the equivalent < 900001 comparison recovered in R8-091.
    expect(0x23DEB2, "cmp", "0xdbba1")
    expect(0x23DEB8, "jge", "0x14023deca")

    # auth_state command is snapshot-only with respect to this producer family.
    state_start, state_end = 0x19EEA9, 0x19F5C3
    emitter_targets = {
        0x237C5C, 0x237F05, 0x238289, 0x23855D, 0x238825, 0x238A66,
        0x238D8B, 0x238F5B, 0x2391D4, 0x2394DA, 0x239756, 0x239937,
    }
    off = int(pe.get_offset_from_rva(state_start))
    direct_emitter_calls = []
    for ins in md.disasm(data[off:off + (state_end - state_start)], base + state_start):
        if ins.mnemonic == "call" and ins.operands and ins.operands[0].type == X86_OP_IMM:
            target = int(ins.operands[0].imm - base)
            if target in emitter_targets:
                direct_emitter_calls.append({"site": hex(int(ins.address - base)), "target": hex(target)})
    require(not direct_emitter_calls, f"auth_state unexpectedly mutates producer: {direct_emitter_calls}")
    assert_rip_literal(0x19F0C8, "lea", "STATE_UNAVAILABLE")
    assert_rip_literal(0x19F0CF, "lea", "authorization state is unavailable")

    return {
        "findingId": "LWB-R8-092",
        "date": "2026-09-27",
        "status": "RECOVERED CONTRACT",
        "sourceIdentity": {"path": str(binary), "sha256": digest},
        "publicAuthState": {
            "sizeBytes": 0xC0,
            "event": "bridge://auth-state",
            "fields": serializer,
            "phases": ["checking", "authorized", "grace", "locked", "signedOut"],
            "signedOutDefaultRole": "normal",
            "lockedDefaultRole": "normal",
        },
        "producerTransitions": {
            "renew": ["authorized", "locked"],
            "heartbeat": ["authorized", "grace", "locked"],
            "supervisor": ["checking", "locked-or-signedOut for session error", "signedOut"],
            "logout": ["signedOut"],
            "loginProjection": "delegates through 0x239937 and then the common state builders",
        },
        "graceWindow": {
            "internalClockOffset": "0x420",
            "initializeIfZero": True,
            "elapsedAdmission": "elapsedMs <= 900000",
            "admissionEquivalentFromR8_091": "elapsedMs < 900001",
            "eligibilityPredicateRva": "0x23CB51",
        },
        "exactTerminalErrors": [
            "NETWORK_GRACE_EXPIRED",
            "ACCOUNT_EXPIRED",
            "SESSION_INVALID",
        ],
        "authStateCommand": {
            "command": "auth_state",
            "directEmitterCalls": direct_emitter_calls,
            "stateUnavailableCode": "STATE_UNAVAILABLE",
            "stateUnavailableMessage": "authorization state is unavailable",
            "classification": "snapshot read; no direct producer mutation",
        },
        "limits": [
            "no credentials, password material, private keys, or live auth requests are used",
            "the two-deadline grace eligibility predicate at 0x23CB51 is source-locked but its surrounding field names are not assigned here",
            "public expiresAt/lastHeartbeatAt/lockedUntil representation is serialized through original helpers; this checkpoint does not invent timestamp formats",
            "no rebuild live-auth behavior is enabled by this checkpoint",
        ],
    }

def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        result = inspect(args.binary)
    except (InspectError, OSError, pefile.PEFormatError) as exc:
        parser.error(str(exc))
    rendered = json.dumps(result, indent=2, sort_keys=True)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered + "\n", encoding="utf-8")
    print(rendered)
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
