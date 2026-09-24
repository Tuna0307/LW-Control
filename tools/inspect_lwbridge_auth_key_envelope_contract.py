#!/usr/bin/env python3
"""Verify original LWBridge 0.3.1 auth/key-envelope transport contracts.

Static/read-only, hash-gated to the verified reference EXE. This inspector
recovers the host-side package-key-envelope transport grammar and auth binding:
response field names, artifact names, accepted token magics, exact two-segment
canonical URL-safe Base64 token framing, internal expiry location, password
challenge parameters, login body field names, auth endpoints and required
headers.

It does not authenticate, send network requests, read credentials, read the
persisted device private key, decrypt package-key.envelope, execute LWBridge,
or inspect the historically restricted secure-proxy RVA 0x3F8E0-0x40A6D.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path
from typing import Any

import capstone
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP


EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
EXPECTED_IMAGE_BASE = 0x140000000

FUNCTIONS = {
    "auth_artifact_ingest": (0x23C7EC, 0x23CB51),
    "auth_ticket_envelope_pair_valid": (0x23CB51, 0x23CBB8),
    "token_payload_expiry": (0x23E465, 0x23E72B),
    "base64_canonical_decode": (0x214338, 0x2144E5),
    "decimal_i64_parse": (0x28B198, 0x28B2F1),
    "password_challenge_validate": (0x23E239, 0x23E465),
    "login_future": (0xF1C9E, 0xF2FBD),
    "auth_http_request": (0xF5347, 0xF643A),
    "heartbeat_future": (0xF643A, 0xF6F41),
}

TOKEN_VALIDATOR_RVA = FUNCTIONS["token_payload_expiry"][0]
BASE64_WRAPPER_RVA = FUNCTIONS["base64_canonical_decode"][0]
BASE64_ENGINE_RVA = 0x836BF0
BASE64_ALPHABET = b"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_"
AUTH_MAGIC_TABLE_RVA = 0xC80160
KEY_MAGIC_TABLE_RVA = 0xC80188


class InspectError(ValueError):
    pass


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def call_target(ins) -> int | None:
    if ins.mnemonic != "call" or not ins.operands:
        return None
    op = ins.operands[0]
    return op.imm if op.type == X86_OP_IMM else None


def rip_target(ins) -> int | None:
    for op in ins.operands:
        if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
            return ins.address + ins.size + op.mem.disp
    return None


def disassemble(pe: pefile.PE, data: bytes, begin: int, end: int):
    offset = pe.get_offset_from_rva(begin)
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    return list(
        md.disasm(
            data[offset : offset + (end - begin)],
            pe.OPTIONAL_HEADER.ImageBase + begin,
        )
    )


def require_instruction(
    table: dict[int, Any],
    rva: int,
    mnemonic: str,
    contains: str | None = None,
):
    ins = table.get(rva)
    if ins is None:
        raise InspectError(f"missing instruction at RVA 0x{rva:X}")
    if ins.mnemonic != mnemonic:
        raise InspectError(
            f"RVA 0x{rva:X} mnemonic {ins.mnemonic!r}; expected {mnemonic!r}"
        )
    if contains is not None and contains not in ins.op_str:
        raise InspectError(
            f"RVA 0x{rva:X} operands {ins.op_str!r}; expected {contains!r}"
        )
    return ins


def ascii_at(pe: pefile.PE, data: bytes, rva: int, length: int) -> str:
    offset = pe.get_offset_from_rva(rva)
    raw = data[offset : offset + length]
    try:
        return raw.decode("ascii")
    except UnicodeDecodeError as exc:
        raise InspectError(f"RVA 0x{rva:X} is not ASCII") from exc


def find_unique_ascii(pe: pefile.PE, data: bytes, value: str) -> int:
    needle = value.encode("ascii")
    hits: list[int] = []
    pos = 0
    while True:
        hit = data.find(needle, pos)
        if hit < 0:
            break
        try:
            hits.append(int(pe.get_rva_from_offset(hit)))
        except pefile.PEFormatError:
            pass
        pos = hit + 1
    if not hits:
        raise InspectError(f"literal {value!r} not found")
    # Adjacent packed Rust strings can create multiple substring hits. For
    # this verifier the literal's presence is sufficient unless a specific
    # instruction target is asserted separately.
    return hits[0]


def rust_str_descriptor(pe: pefile.PE, data: bytes, rva: int) -> str:
    offset = pe.get_offset_from_rva(rva)
    va, length = struct.unpack_from("<QQ", data, offset)
    image_base = int(pe.OPTIONAL_HEADER.ImageBase)
    if not image_base <= va < image_base + int(pe.OPTIONAL_HEADER.SizeOfImage):
        raise InspectError(f"descriptor RVA 0x{rva:X} pointer is outside image")
    return ascii_at(pe, data, va - image_base, int(length))


def inspect(path: Path) -> dict[str, Any]:
    data = path.read_bytes()
    sha = digest(data)
    if sha != EXPECTED_SHA256:
        raise InspectError(
            f"unsupported LWBridge SHA-256 {sha}; expected {EXPECTED_SHA256}"
        )

    pe = pefile.PE(data=data, fast_load=False)
    base = int(pe.OPTIONAL_HEADER.ImageBase)
    if base != EXPECTED_IMAGE_BASE:
        raise InspectError(
            f"image base 0x{base:X}; expected 0x{EXPECTED_IMAGE_BASE:X}"
        )

    pe.parse_data_directories(
        directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]]
    )
    unwind = {
        (int(entry.struct.BeginAddress), int(entry.struct.EndAddress))
        for entry in pe.DIRECTORY_ENTRY_EXCEPTION
    }
    for name, bounds in FUNCTIONS.items():
        if name == "decimal_i64_parse":
            # This optimized parser is a leaf without its own unwind entry.
            continue
        if bounds not in unwind:
            raise InspectError(
                f"{name} unwind range 0x{bounds[0]:X}-0x{bounds[1]:X} missing"
            )

    ins_by_rva: dict[int, Any] = {}
    for begin, end in FUNCTIONS.values():
        for ins in disassemble(pe, data, begin, end):
            ins_by_rva[ins.address - base] = ins

    # Exact accepted magic sets: auth ticket = LWAT1/LWAT2, key envelope = LWKE1.
    auth_magics = [
        rust_str_descriptor(pe, data, AUTH_MAGIC_TABLE_RVA),
        rust_str_descriptor(pe, data, AUTH_MAGIC_TABLE_RVA + 0x10),
    ]
    key_magics = [rust_str_descriptor(pe, data, KEY_MAGIC_TABLE_RVA)]
    if auth_magics != ["LWAT1", "LWAT2"]:
        raise InspectError(f"auth magic table drifted: {auth_magics!r}")
    if key_magics != ["LWKE1"]:
        raise InspectError(f"key-envelope magic table drifted: {key_magics!r}")

    # Pair-valid function wires ticket -> two magics and envelope -> one magic.
    ticket_table = require_instruction(ins_by_rva, 0x23CB70, "lea")
    envelope_table = require_instruction(ins_by_rva, 0x23CB95, "lea")
    if rip_target(ticket_table) != base + AUTH_MAGIC_TABLE_RVA:
        raise InspectError("authorization ticket magic-table target drifted")
    if rip_target(envelope_table) != base + KEY_MAGIC_TABLE_RVA:
        raise InspectError("key-envelope magic-table target drifted")
    require_instruction(ins_by_rva, 0x23CB77, "mov", "r9d, 2")
    require_instruction(ins_by_rva, 0x23CB9C, "mov", "r9d, 1")
    for rva in (0x23CB7D, 0x23CBA2):
        ins = require_instruction(ins_by_rva, rva, "call")
        if call_target(ins) != base + TOKEN_VALIDATOR_RVA:
            raise InspectError(f"token validator call at RVA 0x{rva:X} drifted")

    # Generic token grammar: exactly two dot-separated segments.
    require_instruction(ins_by_rva, 0x23E4ED, "movabs", "0x2e0000002e")
    split_calls = [0x23E505, 0x23E521, 0x23E537]
    for rva in split_calls:
        require_instruction(ins_by_rva, rva, "call")
    require_instruction(ins_by_rva, 0x23E50D, "je")
    require_instruction(ins_by_rva, 0x23E529, "je")
    require_instruction(ins_by_rva, 0x23E53F, "jne")

    # First segment is canonical-decoded with one static URL-safe engine.
    decode_call = require_instruction(ins_by_rva, 0x23E563, "call")
    if call_target(decode_call) != base + BASE64_WRAPPER_RVA:
        raise InspectError("token Base64 wrapper target drifted")
    engine_ref_decode = require_instruction(ins_by_rva, 0x214368, "lea")
    engine_ref_reencode = require_instruction(ins_by_rva, 0x214439, "lea")
    for ins in (engine_ref_decode, engine_ref_reencode):
        if rip_target(ins) != base + BASE64_ENGINE_RVA:
            raise InspectError("Base64 engine reference drifted")
    engine_offset = pe.get_offset_from_rva(BASE64_ENGINE_RVA)
    engine_prefix = data[engine_offset : engine_offset + 3]
    engine_alphabet = data[
        engine_offset + 3 : engine_offset + 3 + len(BASE64_ALPHABET)
    ]
    if engine_prefix != bytes([0, 0, 2]):
        raise InspectError(f"Base64 engine config bytes drifted: {engine_prefix.hex()}")
    if engine_alphabet != BASE64_ALPHABET:
        raise InspectError("Base64 URL-safe alphabet drifted")
    decode_engine_call = require_instruction(ins_by_rva, 0x21437D, "call")
    if call_target(decode_engine_call) != base + 0x4980AC:
        raise InspectError("Base64 decode engine call target drifted")
    # Wrapper re-encodes with the same engine then length+memcmp compares input,
    # enforcing canonical representation rather than merely accepting a decode.
    require_instruction(ins_by_rva, 0x214443, "call")
    require_instruction(ins_by_rva, 0x214448, "cmp", "r14")
    require_instruction(ins_by_rva, 0x21445A, "call")

    # The linked crate/source path is embedded and independently identifies
    # base64 0.22.1's general-purpose decoder.
    base64_source = (
        "C:\\Users\\Administrator\\.cargo\\registry\\src\\"
        "index.crates.io-1949cf8c6b5b557f\\base64-0.22.1\\"
        "src\\engine\\general_purpose\\decode.rs"
    )
    if base64_source.encode("ascii") not in data:
        raise InspectError("base64-0.22.1 decode.rs source path is missing")

    # Decoded first segment is UTF-8, pipe-split, requires >= 3 fields;
    # field[0] is magic and field[2] is signed decimal seconds * 1000.
    require_instruction(ins_by_rva, 0x23E58C, "call")
    require_instruction(ins_by_rva, 0x23E61C, "movabs", "0x7c0000007c")
    require_instruction(ins_by_rva, 0x23E641, "cmp", "3")
    require_instruction(ins_by_rva, 0x23E65A, "call")
    decimal_call = require_instruction(ins_by_rva, 0x23E673, "call")
    if call_target(decimal_call) != base + 0x28B198:
        raise InspectError("decimal expiry parser target drifted")
    require_instruction(ins_by_rva, 0x23E678, "imul", "0x3e8")
    require_instruction(ins_by_rva, 0x28B1ED, "movzx")
    require_instruction(ins_by_rva, 0x28B1F2, "add", "-0x30")
    require_instruction(ins_by_rva, 0x28B203, "lea")


    # Auth response fields and their separate ISO-ish expiry companions.
    for literal in (
        "authorizationTicket",
        "authorizationTicketExpiresAt",
        "packageKeyEnvelope",
        "packageKeyEnvelopeExpiresAt",
        "AUTHORIZATION_TICKET_INVALID",
        "AUTHORIZATION_TICKET_STORAGE_FAILED",
        "KEY_ENVELOPE_INVALID",
        "authorization.challenge",
        "authorization.ticket",
        "package-key.envelope",
        "build.manifest",
    ):
        find_unique_ascii(pe, data, literal)

    require_instruction(ins_by_rva, 0x23C80E, "mov", "r9d, 0x13")
    require_instruction(ins_by_rva, 0x23C82B, "mov", "r9d, 0x1c")
    require_instruction(ins_by_rva, 0x23C96A, "mov", "r9d, 0x12")
    require_instruction(ins_by_rva, 0x23C987, "mov", "r9d, 0x1b")
    for rva in (0x23C858, 0x23C9B4):
        require_instruction(ins_by_rva, rva, "call")
    # Invalid/expired artifacts are removed before returning their exact codes.
    for rva in (0x23C8FB, 0x23CA57):
        require_instruction(ins_by_rva, rva, "call")

    # Password challenge is pinned: version=1, iterations=600000, 16 decoded salt bytes.
    require_instruction(ins_by_rva, 0x23E258, "lea")
    require_instruction(ins_by_rva, 0x23E295, "lea")
    require_instruction(ins_by_rva, 0x23E2C7, "cmp", "0x927c0")
    require_instruction(ins_by_rva, 0x23E2DB, "lea")
    require_instruction(ins_by_rva, 0x23E3B4, "cmp", "0x10")
    for literal in ("passwordVersion", "passwordIterations", "passwordSalt"):
        find_unique_ascii(pe, data, literal)

    # Exact login request fields and endpoints.
    for literal in (
        "/api/password-challenge",
        "/api/login",
        "/api/register",
        "/api/renew",
        "/api/heartbeat",
        "/api/device-unbind/preview",
        "/api/device-unbind/confirm",
        "/api/unbind/check",
        "/api/watermark/lookup",
        "username",
        "passwordVerifier",
        "devicePublicKey",
        "launchNonce",
        "X-Device-Fingerprint",
        "X-Client-Build-Id",
        "X-Client-Auth-Policy",
        "https://auth.songunity.com",
        "LWBRIDGE_AUTH_URL",
    ):
        # devicePublicKey / launchNonce are constructed immediates in the login
        # future rather than guaranteed standalone rodata; they are verified
        # below by their exact construction.
        if literal not in {"devicePublicKey", "launchNonce"}:
            find_unique_ascii(pe, data, literal)

    # devicePublicKey (15 bytes) = "devicePu" + overlapping "ublicKey".
    require_instruction(ins_by_rva, 0xF26DD, "movabs", "0x79654b63696c6275")
    require_instruction(ins_by_rva, 0xF26EB, "movabs", "0x7550656369766564")
    require_instruction(ins_by_rva, 0xF26F8, "mov")
    # launchNonce (11 bytes) is constructed as an immediate field name.
    require_instruction(ins_by_rva, 0xF279A, "movabs", "0x6f4e68636e75616c")
    require_instruction(ins_by_rva, 0xF27A7, "mov")
    login_endpoint = require_instruction(ins_by_rva, 0xF287A, "lea")
    login_rva = rip_target(login_endpoint)
    if login_rva is None or ascii_at(pe, data, login_rva - base, 10) != "/api/login":
        raise InspectError("/api/login endpoint reference drifted")

    # Auth HTTP request helper adds JSON accept + three exact client headers.
    for rva, text, length in (
        (0xF60F7, "application/json", 16),
        (0xF6154, "X-Device-Fingerprint", 20),
        (0xF6186, "X-Client-Build-Id", 17),
        (0xF61C0, "X-Client-Auth-Policy", 20),
    ):
        ins = require_instruction(ins_by_rva, rva, "lea")
        target = rip_target(ins)
        if target is None:
            raise InspectError(f"HTTP literal at RVA 0x{rva:X} is not RIP-relative")
        if ascii_at(pe, data, target - base, length) != text:
            raise InspectError(f"HTTP literal {text!r} drifted")
    require_instruction(ins_by_rva, 0xF61B7, "mov", "1")
    # The value byte at the referenced static is ASCII "2".
    value_ref = require_instruction(ins_by_rva, 0xF61AB, "lea")
    value_rva = rip_target(value_ref)
    if value_rva is None or ascii_at(pe, data, value_rva - base, 1) != "2":
        raise InspectError("X-Client-Auth-Policy value drifted")

    # Heartbeat feeds a successful response back through auth-artifact ingest.
    hb_endpoint = require_instruction(ins_by_rva, 0xF66C6, "lea")
    hb_rva = rip_target(hb_endpoint)
    if hb_rva is None or ascii_at(pe, data, hb_rva - base, 14) != "/api/heartbeat":
        raise InspectError("/api/heartbeat endpoint reference drifted")
    ingest_calls = []
    # Enumerate direct calls to the artifact-ingest function from all .text.
    text_section = next(
        s for s in pe.sections if s.Name.rstrip(b"\0") == b".text"
    )
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    text_raw = int(text_section.PointerToRawData)
    text_size = int(text_section.SizeOfRawData)
    text_va = base + int(text_section.VirtualAddress)
    for ins in md.disasm(data[text_raw : text_raw + text_size], text_va):
        if call_target(ins) == base + FUNCTIONS["auth_artifact_ingest"][0]:
            ingest_calls.append(ins.address - base)
    if ingest_calls != [0xF4D3B, 0xF6980, 0x239A11]:
        raise InspectError(
            "auth artifact-ingest callsites drifted: "
            + ", ".join(f"0x{x:X}" for x in ingest_calls)
        )

    return {
        "findingId": "LWB-R8-004",
        "date": "2026-09-24",
        "scope": "original auth response/key-envelope transport grammar and device-key login binding",
        "evidenceLabel": "RECOVERED",
        "source": {
            "path": str(path),
            "sha256": sha,
            "imageBase": f"0x{base:X}",
        },
        "tools": {
            "python": "3.12.10",
            "pefile": pefile.__version__,
            "capstone": capstone.__version__,
        },
        "keyEnvelopeTransport": {
            "responseFields": [
                "packageKeyEnvelope",
                "packageKeyEnvelopeExpiresAt",
            ],
            "runtimeArtifact": "package-key.envelope",
            "acceptedPayloadMagic": key_magics,
            "tokenShape": "<canonical-url-safe-base64-first-segment>.<opaque-second-segment>",
            "segmentCount": 2,
            "firstSegmentDecodedShape": "LWKE1|<field1>|<expirySeconds>|...",
            "minimumDecodedFieldCount": 3,
            "magicFieldIndex": 0,
            "expirySecondsFieldIndex": 2,
            "expiryReturnUnit": "milliseconds",
            "secondSegmentHostValidation": "presence/exact-two-segment framing only in generic host expiry validator; cryptographic meaning is not established here",
            "base64": {
                "engineRva": f"0x{BASE64_ENGINE_RVA:X}",
                "configBytesHex": engine_prefix.hex(),
                "alphabet": BASE64_ALPHABET.decode("ascii"),
                "canonicalRoundTripRequired": True,
                "linkedCrate": "base64 0.22.1",
                "linkedSource": base64_source,
            },
        },
        "authorizationTicketTransport": {
            "responseFields": [
                "authorizationTicket",
                "authorizationTicketExpiresAt",
            ],
            "runtimeArtifact": "authorization.ticket",
            "acceptedPayloadMagics": auth_magics,
            "sameTwoSegmentPayloadGrammar": True,
        },
        "authApi": {
            "defaultBaseUrl": "https://auth.songunity.com",
            "override": "LWBRIDGE_AUTH_URL",
            "endpoints": [
                "/api/password-challenge",
                "/api/login",
                "/api/register",
                "/api/renew",
                "/api/heartbeat",
                "/api/unbind/check",
                "/api/device-unbind/preview",
                "/api/device-unbind/confirm",
                "/api/watermark/lookup",
            ],
            "headers": {
                "Accept": "application/json",
                "X-Device-Fingerprint": "runtime device fingerprint",
                "X-Client-Build-Id": "current build ID",
                "X-Client-Auth-Policy": "2",
            },
            "loginBodyFields": [
                "username",
                "passwordVerifier",
                "devicePublicKey",
                "launchNonce",
            ],
            "passwordChallenge": {
                "passwordVersion": 1,
                "passwordIterations": 600000,
                "passwordSaltDecodedBytes": 16,
            },
        },
        "result": [
            "The host receives packageKeyEnvelope/packageKeyEnvelopeExpiresAt and persists the envelope as package-key.envelope after validation.",
            "The key envelope is a signed/opaque two-segment token: exactly one dot, with a canonical URL-safe Base64 first segment and one opaque second segment.",
            "The decoded first segment is UTF-8 and pipe-delimited, contains at least three fields, requires field 0 = LWKE1, and parses field 2 as signed decimal expiry seconds multiplied by 1000.",
            "Authorization tickets use the same framing, with allowed first-field magics LWAT1 or LWAT2.",
            "The login body explicitly includes devicePublicKey and launchNonce, tying the auth exchange to device-key material before packageKeyEnvelope is returned.",
            "The exact envelope field1/remaining payload fields, second-segment signature/MAC algorithm, and the secure-proxy derivation of the 32-byte package key remain unrecovered.",
        ],
        "reproduction": [
            "python tools\\inspect_lwbridge_auth_key_envelope_contract.py ..\\LW\\lwbridge-0.3.1.exe",
            "python tools\\inspect_lwbridge_auth_key_envelope_contract.py ..\\LW\\lwbridge-0.3.1.exe --output evidence\\lwbridge-implementation\\2026-09-24-r8-004-auth-key-envelope-transport.json",
            "python -m py_compile tools\\inspect_lwbridge_auth_key_envelope_contract.py",
        ],
        "limits": {
            "staticOnly": True,
            "networkRequestsMade": False,
            "credentialsRead": False,
            "privateKeyRead": False,
            "notProven": [
                "exact semantic meaning/encoding of the login devicePublicKey value",
                "key-envelope decoded field1 or fields after expiry",
                "second token segment signature/MAC algorithm",
                "server ephemeral public-key location in the envelope",
                "exact envelope-to-agreement helper mapping inside the secure proxy",
                "32-byte package key or bridge-scripts.dat plaintext",
            ],
            "restrictionPreserved": "No secure-proxy instruction in historical SB-79 RVA 0x3F8E0-0x40A6D is inspected, decoded or rerouted.",
        },
        "implementationImpact": {
            "publicBehaviorEnabled": False,
            "closedGap": "host-side key-envelope token framing, canonical payload encoding, expiry/magic fields, API response/storage path and login device-key binding",
            "next": "recover the envelope's decoded key-agreement fields through independent permitted evidence or an existing original envelope artifact, then feed the recovered peer key into the already-proven agreement/KDF contract",
        },
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
    encoded = json.dumps(result, indent=2, ensure_ascii=False) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded, encoding="utf-8")
    else:
        print(encoded, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
