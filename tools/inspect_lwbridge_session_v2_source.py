#!/usr/bin/env python3
"""Verify LWBridge 0.3.1 SessionV2 persistence, restore, and auth-source ownership."""

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

    # Service constructor owns legacy/v2 paths and service endpoint configuration.
    assert_rip_literal(0x23CC63, "lea", "LWBRIDGE_AUTH_URL")
    assert_rip_literal(0x23CCE4, "movups", "https://auth.songunity.com")
    path_literals = [
        (0x23D039, "auth-session.json"),
        (0x23D057, "auth-credentials.json"),
        (0x23D081, "auth-session.v2.json"),
        (0x23D0A4, "auth-credentials.v2.json"),
        (0x23D0C7, "device-key-cleanup.pending"),
        (0x23D0F2, "authorization.challenge"),
        (0x23D111, "authorization.ticket"),
        (0x23D130, "package-key.envelope"),
        (0x23D153, "build.manifest"),
    ]
    for rva, literal in path_literals:
        assert_rip_literal(rva, "lea", literal)

    # Heartbeat interval: default 120s, config accepts exactly 30..300 inclusive.
    assert_rip_literal(0x23C5C1, "lea", "heartbeatIntervalSeconds")
    expect(0x23C5EF, "lea", "rax - 0x1e")
    expect(0x23C5F3, "cmp", "0x10f")
    expect(0x23C604, "mov", "qword ptr [rsi + 0x3e0], rax")
    expect(0x23D433, "mov", "qword ptr [rsi + 0x3e0], 0x78")

    # Exact SessionV2 serializer field keys and fixed source offsets.
    serializer_fields = [
        ("version", 0x23EBAF, 0x80, 7),
        ("username", 0x23EBD5, 0x00, 8),
        ("expiresAt", 0x23EC07, 0x48, 9),
        ("lastHeartbeatAt", 0x23EC36, 0x60, 15),
        ("graceStartedAt", 0x23EC65, 0x78, 14),
        ("encryptedToken", 0x23EC90, 0x18, 14),
        ("encryptedMetadata", 0x23ECB7, 0x30, 17),
    ]
    for name, rva, _offset, length in serializer_fields:
        assert_rip_literal(rva, "lea", name)

    # Save builder constructs SessionV2 version=2 and serializes through 0x23EB78.
    expect(0x2377ED, "mov", "byte ptr [rsp + 0x130], 2")
    require(direct_call_target(0x2378DD) == 0x23EB78, "SessionV2 serializer call mismatch")
    # Destination path uses active v2 session PathBuf slice at service +0x100/+0x108.
    expect(0x23788F, "mov", "qword ptr [rdi + 0x100]")
    expect(0x237896, "mov", "qword ptr [rdi + 0x108]")
    require(direct_call_target(0x237B98) == 0x23D8FC, "SessionV2 write helper mismatch")

    # Restore prefers the v2 session path, then falls back to legacy if v2 is absent.
    expect(0x23657E, "mov", "qword ptr [rdx + 0x100]")
    expect(0x236585, "mov", "qword ptr [rdx + 0x108]")
    require(direct_call_target(0x236592) == 0x5C6A40, "v2 existence check mismatch")
    require(direct_call_target(0x2365B0) == 0x5B2060, "v2 read mismatch")
    expect(0x236649, "mov", "qword ptr [rdi + 0xc0]")
    expect(0x236650, "mov", "qword ptr [rdi + 0xc8]")
    require(direct_call_target(0x23665D) == 0x5C6A40, "legacy existence check mismatch")
    require(direct_call_target(0x23667B) == 0x23F402, "legacy restore/migration parser mismatch")

    # Typed SessionV2 deserializer wrapper calls the generated seven-field serde implementation.
    require(direct_call_target(0x2A8163) == 0x315E6D, "generated SessionV2 deserializer mismatch")
    require(direct_call_target(0x236795) == 0x2A812E, "SessionV2 startup deserializer wrapper mismatch")
    # Struct size is 0x88 and version byte at +0x80 is semantically required to equal 2.
    expect(0x2367FE, "mov", "r8d, 0x88")
    expect(0x236A11, "cmp", "byte ptr [rbx + 0x38], 2")
    expect(0x236A15, "jne", "0x140236ce2")

    # Encrypted token and metadata use the secure-storage decode helper.
    require(direct_call_target(0x236A36) == 0x39F5CE, "encryptedToken decode helper mismatch")
    require(direct_call_target(0x236E32) == 0x39F5CE, "encryptedMetadata decode helper mismatch")
    # Restore failures normalize to SESSION_INVALID.
    assert_rip_literal(0x236D89, "lea", "SESSION_INVALID")
    assert_rip_literal(0x236EB5, "lea", "SESSION_INVALID")

    # Successful metadata parse feeds the common authorization-role projection.
    require(direct_call_target(0x237085) == 0x23B9EB, "restored auth projection mismatch")

    # Restore ownership is centralized in the heartbeat/session supervisor.
    require(direct_call_target(0x0F7C08) == 0x236567, "session supervisor restore call mismatch")
    # No usable restored session best-effort removes ticket/envelope artifacts, then publishes normal signedOut.
    require(direct_call_target(0x0F7C82) == 0x23CBB8, "ticket/envelope cleanup mismatch")
    require(direct_call_target(0x0F7C99) == 0x23855D, "signedOut emitter mismatch")

    # Low-level secure/device-key vocabulary remains distinct beneath restore.
    secure_codes = [
        ("SECURE_STORAGE_UNAVAILABLE", 0x39F60D),
        ("DEVICE_KEY_MISSING", 0x23EECC),
        ("DEVICE_KEY_MISMATCH", 0x23EEDF),
        ("DEVICE_KEY_UNAVAILABLE", 0x23EEF8),
        ("KEY_ENVELOPE_EXPIRED", 0x23EF2C),
    ]
    for literal, rva in secure_codes:
        ins = ins_at(rva)
        target = rip_target(ins)
        require(target is not None, f"{literal}: missing RIP target")
        require(read_ascii(target, len(literal)) == literal, f"{literal}: literal mismatch")

    return {
        "findingId": "LWB-R8-093",
        "date": "2026-09-27",
        "status": "RECOVERED CONTRACT",
        "sourceIdentity": {"path": str(binary), "sha256": digest},
        "service": {
            "authUrlEnv": "LWBRIDGE_AUTH_URL",
            "defaultAuthUrl": "https://auth.songunity.com",
            "heartbeatIntervalSeconds": {
                "default": 120,
                "acceptedMin": 30,
                "acceptedMax": 300,
            },
        },
        "storagePaths": {
            "legacySession": "auth-session.json",
            "legacyCredentials": "auth-credentials.json",
            "sessionV2": "auth-session.v2.json",
            "credentialsV2": "auth-credentials.v2.json",
            "deviceKeyCleanupPending": "device-key-cleanup.pending",
            "authorizationChallenge": "authorization.challenge",
            "authorizationTicket": "authorization.ticket",
            "packageKeyEnvelope": "package-key.envelope",
            "buildManifest": "build.manifest",
        },
        "sessionV2": {
            "sizeBytes": 0x88,
            "serializedVersion": 2,
            "requiredVersion": 2,
            "fields": [
                {"name": name, "offset": hex(offset)}
                for name, _rva, offset, _length in serializer_fields
            ],
            "serializedOrder": [name for name, *_ in serializer_fields],
        },
        "restore": {
            "priority": ["auth-session.v2.json", "auth-session.json legacy fallback"],
            "centralOwner": "heartbeat/session supervisor 0xF6F41-0xF7F47",
            "encryptedTokenDecode": "0x39F5CE",
            "encryptedMetadataDecode": "0x39F5CE",
            "decodeFailure": "SESSION_INVALID",
            "roleProjection": "0x23B9EB from decrypted metadata plus persisted session identity/timing",
            "noUsableSession": "best-effort delete authorization.ticket and package-key.envelope, then publish signedOut",
        },
        "persistence": {
            "serializer": "0x23EB78",
            "writeHelper": "0x23D8FC",
            "target": "active v2 session path",
        },
        "lowLevelErrorVocabulary": [
            "SECURE_STORAGE_UNAVAILABLE",
            "DEVICE_KEY_MISSING",
            "DEVICE_KEY_MISMATCH",
            "DEVICE_KEY_UNAVAILABLE",
            "KEY_ENVELOPE_EXPIRED",
        ],
        "limits": [
            "no real credentials, encrypted session payloads, device keys, private keys, or auth requests are read or executed",
            "legacy auth-session.json migration internals below the v2-first fallback are not fully reconstructed here",
            "the low-level secure-storage/device-key algorithms are not reconstructed; only host-visible ownership and error boundaries are recorded",
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
