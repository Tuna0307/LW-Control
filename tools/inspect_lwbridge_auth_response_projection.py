#!/usr/bin/env python3
"""Verify LWBridge 0.3.1 auth-service response projection into AuthState and SessionV2."""

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

    def call_target(rva: int) -> int:
        ins = expect(rva, "call")
        require(ins.operands and ins.operands[0].type == X86_OP_IMM, f"0x{rva:X}: expected direct call")
        return int(ins.operands[0].imm - base)

    def rip_target(ins) -> int | None:
        for op in ins.operands:
            if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                return int(ins.address + ins.size + op.mem.disp - base)
        return None

    def ascii_at(rva: int, length: int) -> str:
        off = int(pe.get_offset_from_rva(rva))
        return data[off:off + length].decode("ascii")

    def assert_rip_literal(rva: int, mnemonic: str, literal: str) -> None:
        ins = expect(rva, mnemonic)
        target = rip_target(ins)
        require(target is not None, f"0x{rva:X}: missing RIP target")
        require(ascii_at(target, len(literal)) == literal, f"0x{rva:X}: expected {literal!r}")

    # Login and register share the same successful auth-response projector.
    assert_rip_literal(0x0F287A, "lea", "/api/login")
    assert_rip_literal(0x0F39AA, "lea", "/api/register")
    require(call_target(0x0F20DF) == 0x239937, "login projector call mismatch")
    require(call_target(0x0F3AD9) == 0x239937, "register projector call mismatch")

    # Common login/register projector: token is a response string and is stored
    # in the service raw-token slot. Empty/missing/non-string token is invalid.
    assert_rip_literal(0x239969, "lea", "token")
    require(call_target(0x239981) == 0x23F10C, "token string extractor mismatch")
    expect(0x23999D, "mov", "qword ptr [r14 + 0x10]")
    expect(0x2399A1, "mov", "qword ptr [rdi + 0x3a8]")
    expect(0x2399AC, "movups", "xmmword ptr [rdi + 0x398]")
    expect(0x2399B3, "cmp", "qword ptr [rdi + 0x3a8], 0")
    expect(0x2399BB, "je", "0x140239ad1")
    assert_rip_literal(0x239AD5, "lea", "SESSION_INVALID")

    # expired is admitted only as an actual JSON bool. True cleans runtime
    # artifacts and bypasses new artifact ingestion.
    assert_rip_literal(0x2399CE, "lea", "expired")
    require(call_target(0x2399DB) == 0x5A2051, "expired lookup mismatch")
    expect(0x2399E5, "cmp", "byte ptr [rax], 1")
    expect(0x2399E8, "sete", "cl")
    expect(0x2399EB, "and", "cl, byte ptr [rax + 1]")
    require(call_target(0x2399F7) == 0x23CBB8, "expired cleanup mismatch")
    require(call_target(0x239A11) == 0x23C7EC, "login/register artifact ingestion mismatch")

    # Login/register response identity projection.
    require(call_target(0x239A25) == 0x23C5B0, "login heartbeat config update mismatch")
    assert_rip_literal(0x239A67, "lea", "username")
    require(call_target(0x239A7F) == 0x23F10C, "login username extraction mismatch")
    assert_rip_literal(0x239A92, "lea", "accessRole")
    require(call_target(0x239AB2) == 0x23ED23, "login role normalization mismatch")
    assert_rip_literal(0x239AB7, "lea", "watermarkTraceCode")
    require(call_target(0x239B3A) == 0x23E72B, "login watermark normalization mismatch")
    assert_rip_literal(0x239B47, "lea", "expiresAt")
    require(call_target(0x239B5A) == 0x23DF8E, "login expiresAt extraction mismatch")
    require(call_target(0x239B7B) == 0x237F05, "login result emitter mismatch")
    require(call_target(0x239B93) == 0x23743F, "login SessionV2 save mismatch")

    # Login-result emitter chooses authorized vs locked from the expired flag,
    # and exact ACCOUNT_EXPIRED is installed only on expired=true.
    expect(0x237F21, "mov", "qword ptr [r8 + 0x60]")
    expect(0x237F25, "movzx", "byte ptr [r13]")
    assert_rip_literal(0x237F54, "lea", "locked")
    assert_rip_literal(0x237F5B, "lea", "authorized")
    expect(0x237F62, "test", "bpl, bpl")
    expect(0x237F65, "cmovne", "rdx, rax")
    expect(0x2380D1, "cmp", "byte ptr [r13], 1")
    expect(0x2380F7, "movabs", "0x444552495058455f")
    expect(0x238105, "movabs", "0x5f544e554f434341")

    # Role normalization is exact {normal,premium,admin}; anything else is normal.
    assert_rip_literal(0x23ED2F, "lea", "normal")
    expect(0x23ED55, "cmp", "rcx, 5")
    expect(0x23ED5B, "cmp", "rcx, 7")
    assert_rip_literal(0x23ED78, "lea", "premium")
    assert_rip_literal(0x23EDBA, "lea", "admin")

    # Watermark trace code accepts exactly 12 chars from the recovered alphabet.
    expect(0x23E759, "cmp", "qword ptr [rdx + 0x18], 0xc")
    assert_rip_literal(0x23E763, "lea", "23456789ABCDEFGHJKLMNPQRSTUVWXYZ")
    expect(0x23E76D, "cmp", "r12, 0xc")
    expect(0x23E77A, "mov", "r8d, 0x20")
    require(call_target(0x23E783) == 0x43170, "watermark alphabet lookup mismatch")

    # Renew success: artifact ingest, username with prior-state fallback,
    # optional expiresAt without heartbeat-style fallback, normalized role/
    # watermark, authorized publication, then SessionV2 persistence.
    require(call_target(0x0F4D3B) == 0x23C7EC, "renew artifact ingestion mismatch")
    require(call_target(0x0F4D58) == 0x23C5B0, "renew heartbeat config mismatch")
    expect(0x0F4D97, "movups", "xmmword ptr [rax + 0x2f8]")
    assert_rip_literal(0x0F4DA3, "lea", "username")
    require(call_target(0x0F4DBB) == 0x23F4AB, "renew username fallback extractor mismatch")
    assert_rip_literal(0x0F4DC0, "lea", "expiresAt")
    require(call_target(0x0F4DD8) == 0x23DF8E, "renew expiresAt extraction mismatch")
    require(call_target(0x0F4E0B) == 0x23ED23, "renew role normalization mismatch")
    require(call_target(0x0F4F56) == 0x23E72B, "renew watermark normalization mismatch")
    require(call_target(0x0F4FB4) == 0x2391D4, "renew authorized emitter mismatch")
    require(call_target(0x0F4FD0) == 0x23743F, "renew SessionV2 save mismatch")

    # Heartbeat success uses the same username fallback, but explicitly retains
    # the prior expiresAt when the response omits/mistypes expiresAt.
    require(call_target(0x0F6980) == 0x23C7EC, "heartbeat artifact ingestion mismatch")
    expect(0x0F6999, "movups", "xmmword ptr [rax + 0x2f8]")
    assert_rip_literal(0x0F69A5, "lea", "username")
    require(call_target(0x0F69BD) == 0x23F4AB, "heartbeat username fallback extractor mismatch")
    assert_rip_literal(0x0F69C2, "lea", "expiresAt")
    require(call_target(0x0F69DD) == 0x23DF8E, "heartbeat expiresAt extraction mismatch")
    expect(0x0F69EA, "mov", "edx, 0x338")
    expect(0x0F69EF, "add", "qword ptr [rdi + 0x60]")
    require(call_target(0x0F6A2C) == 0x2A2C0, "heartbeat prior-expiry clone mismatch")
    require(call_target(0x0F6A5F) == 0x23ED23, "heartbeat role normalization mismatch")
    require(call_target(0x0F6A95) == 0x23E72B, "heartbeat watermark normalization mismatch")
    require(call_target(0x0F6AFC) == 0x2391D4, "heartbeat authorized emitter mismatch")
    require(call_target(0x0F6B18) == 0x23743F, "heartbeat SessionV2 save mismatch")

    # Authorized emitter always replaces expiresAt with the supplied projection.
    expect(0x2392D8, "add", "r14, 0x48")
    expect(0x2392DC, "lea", "rdi + 0x338")
    expect(0x239301, "mov", "qword ptr [r14 + 0x10]")
    expect(0x23930D, "movups", "xmmword ptr [r15]")

    # SessionV2 save: protect raw service token, build exact two-key metadata
    # object {accessRole, watermarkTraceCode}, protect that serialized metadata.
    expect(0x23747A, "movabs", "0x6f52737365636361")  # accessRo
    expect(0x237487, "mov", "word ptr [rax + 8], 0x656c")  # le
    expect(0x23749C, "lea", "rdi + 0x308")
    require(call_target(0x2374E9) == 0x5A214E, "metadata accessRole insertion mismatch")
    assert_rip_literal(0x23751B, "movups", "watermarkTraceCode")
    expect(0x23753A, "lea", "rdi + 0x320")
    require(call_target(0x23758C) == 0x5A214E, "metadata watermark insertion mismatch")

    # Verify there are exactly two JSON-object insertion calls in the save builder.
    start, end = 0x23743F, 0x237C5C
    off = int(pe.get_offset_from_rva(start))
    insertion_sites = []
    for ins in md.disasm(data[off:off + (end - start)], base + start):
        if ins.mnemonic == "call" and ins.operands and ins.operands[0].type == X86_OP_IMM:
            if int(ins.operands[0].imm - base) == 0x5A214E:
                insertion_sites.append(int(ins.address - base))
    require(insertion_sites == [0x2374E9, 0x23758C], f"metadata insertion sites drifted: {insertion_sites}")

    # Raw token protection.
    expect(0x237647, "mov", "qword ptr [rdi + 0x3a0]")
    expect(0x23764E, "mov", "qword ptr [rdi + 0x3a8]")
    require(call_target(0x237660) == 0x39F4C5, "raw token protection helper mismatch")

    # Metadata object serialization then protection.
    require(call_target(0x23771C) == 0x5A24DB, "metadata JSON serialization mismatch")
    require(call_target(0x237756) == 0x39F4C5, "metadata protection helper mismatch")

    return {
        "findingId": "LWB-R8-095",
        "date": "2026-09-27",
        "status": "RECOVERED CONTRACT",
        "sourceIdentity": {"path": str(binary), "sha256": digest},
        "loginRegisterProjection": {
            "sharedProjector": "0x239937-0x239BF2",
            "callers": {"login": "0xF20DF", "register": "0xF3AD9"},
            "responseFields": ["token", "expired", "username", "accessRole", "watermarkTraceCode", "expiresAt"],
            "tokenRule": "missing/non-string/empty token -> SESSION_INVALID",
            "expiredRule": "only JSON bool true triggers ticket/envelope cleanup, skips new artifact ingestion, and publishes locked/ACCOUNT_EXPIRED; false/missing/non-bool follows authorized path",
            "persistsSessionV2": True,
        },
        "normalization": {
            "accessRole": {
                "accepted": ["normal", "premium", "admin"],
                "fallback": "normal",
            },
            "watermarkTraceCode": {
                "length": 12,
                "alphabet": "23456789ABCDEFGHJKLMNPQRSTUVWXYZ",
                "invalidOrMissing": "empty normalized string",
            },
        },
        "renewProjection": {
            "username": "response string else retain previous username",
            "expiresAt": "optional response string; no prior-value fallback before authorized emitter",
            "accessRole": "normalize to normal/premium/admin",
            "watermarkTraceCode": "12-char restricted alphabet else empty",
            "then": ["authorized publication", "SessionV2 persistence"],
        },
        "heartbeatProjection": {
            "username": "response string else retain previous username",
            "expiresAt": "optional response string; retain prior expiresAt when absent/mistyped",
            "accessRole": "normalize to normal/premium/admin",
            "watermarkTraceCode": "12-char restricted alphabet else empty",
            "then": ["authorized publication", "SessionV2 persistence"],
        },
        "sessionV2ProtectionSource": {
            "encryptedToken": "current raw service token -> 0x39F4C5 protection",
            "encryptedMetadata": {
                "plaintextJsonKeys": ["accessRole", "watermarkTraceCode"],
                "source": "normalized current AuthState",
                "serialization": "0x5A24DB",
                "protection": "0x39F4C5",
            },
            "restoreDecodeHelperFromR8_093": "0x39F5CE",
        },
        "limits": [
            "0x39F4C5 protection classification reuses established R7 evidence (CryptProtectData plus Base64); crypto internals are not reopened here",
            "no real credentials, tokens, encrypted session data, private keys, or auth network requests are read or executed",
            "service-side token issuance/signature semantics are not reconstructed",
            "no production auth behavior is enabled by this checkpoint",
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
