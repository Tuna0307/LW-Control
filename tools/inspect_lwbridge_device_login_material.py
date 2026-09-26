#!/usr/bin/env python3
"""Recover original LWBridge 0.3.1 login device-public-key and launch-nonce format.

Static, hash-gated and read-only. The inspector proves the original Windows
CNG device-key identity, exported public-point shape, URL-safe no-padding
Base64 encoding used by the login devicePublicKey field, and the 32-byte
authorization.challenge/launchNonce generation + reuse contract.

It does not read/export a live private key, authenticate, send network
requests, decrypt package-key.envelope, execute LWBridge, or inspect the
historically restricted secure-proxy SB-79 body.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

import capstone
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
EXPECTED_IMAGE_BASE = 0x140000000

DEVICE_PROVIDER = "Microsoft Software Key Storage Provider"
DEVICE_KEY_NAME = "{2D337A4D-7E6C-49EF-9486-54F0A00D8A41}"
DEVICE_ALGORITHM = "ECDH_P256"
PUBLIC_BLOB_TYPE = "ECCPUBLICBLOB"
URLSAFE_ALPHABET = b"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_"

FUNCTIONS = {
    "device_key_open_or_create": (0x39ECBD, 0x39ED6B),
    "device_key_create": (0x39ED6B, 0x39EE94),
    "device_public_export": (0x39EE94, 0x39F03D),
    "device_key_delete": (0x39F03D, 0x39F0CF),
    "device_key_open": (0x39F0CF, 0x39F135),
    "challenge_read_validate": (0x23A111, 0x23A24C),
    "challenge_get_or_create": (0x23B408, 0x23B8B0),
    "login_future": (0xF1C9E, 0xF2FBD),
    "base64_encode": (0x401E98, 0x401FC4),
}

DEVICE_B64_ENGINE_RVA = 0xCA294E
CHALLENGE_B64_ENGINE_RVA = 0x836BF0


class InspectError(ValueError):
    pass


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def disassemble(pe: pefile.PE, data: bytes, begin: int, end: int):
    off = pe.get_offset_from_rva(begin)
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    return list(
        md.disasm(
            data[off : off + (end - begin)],
            pe.OPTIONAL_HEADER.ImageBase + begin,
        )
    )


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


def read_utf16z(pe: pefile.PE, data: bytes, va: int) -> str:
    rva = va - pe.OPTIONAL_HEADER.ImageBase
    off = pe.get_offset_from_rva(rva)
    chars: list[str] = []
    while off + 1 < len(data):
        code = data[off] | (data[off + 1] << 8)
        off += 2
        if code == 0:
            break
        chars.append(chr(code))
    return "".join(chars)


def verify_engine(pe: pefile.PE, data: bytes, rva: int) -> dict[str, Any]:
    off = pe.get_offset_from_rva(rva)
    prefix = data[off : off + 3]
    alphabet = data[off + 3 : off + 3 + len(URLSAFE_ALPHABET)]
    if prefix != bytes([0, 0, 2]):
        raise InspectError(
            f"Base64 engine RVA 0x{rva:X} config {prefix.hex()}; expected 000002"
        )
    if alphabet != URLSAFE_ALPHABET:
        raise InspectError(f"Base64 engine RVA 0x{rva:X} alphabet drifted")
    return {
        "rva": f"0x{rva:X}",
        "configBytesHex": prefix.hex(),
        "alphabet": alphabet.decode("ascii"),
        "padding": False,
    }


def inspect(path: Path) -> dict[str, Any]:
    data = path.read_bytes()
    digest = sha256(data)
    if digest != EXPECTED_SHA256:
        raise InspectError(
            f"unsupported LWBridge SHA-256 {digest}; expected {EXPECTED_SHA256}"
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
        (int(e.struct.BeginAddress), int(e.struct.EndAddress))
        for e in pe.DIRECTORY_ENTRY_EXCEPTION
    }
    for name, bounds in FUNCTIONS.items():
        if name == "base64_encode":
            # This Rust helper has a normal unwind entry in this build too.
            pass
        if bounds not in unwind:
            raise InspectError(
                f"{name} unwind range 0x{bounds[0]:X}-0x{bounds[1]:X} missing"
            )

    ins_by_rva: dict[int, Any] = {}
    for begin, end in FUNCTIONS.values():
        for ins in disassemble(pe, data, begin, end):
            ins_by_rva[ins.address - base] = ins

    # Device-key provider / algorithm / persisted key identity.
    for rva, expected in (
        (0x39ECD3, DEVICE_PROVIDER),
        (0x39ED81, DEVICE_PROVIDER),
        (0x39EDF1, DEVICE_ALGORITHM),
        (0x39EDF8, DEVICE_KEY_NAME),
        (0x39F050, DEVICE_PROVIDER),
        (0x39F0EE, DEVICE_KEY_NAME),
        (0x39EEC8, PUBLIC_BLOB_TYPE),
        (0x39EF3D, PUBLIC_BLOB_TYPE),
    ):
        ins = require_instruction(ins_by_rva, rva, "lea")
        target = rip_target(ins)
        if target is None:
            raise InspectError(f"RVA 0x{rva:X} string ref is not RIP-relative")
        actual = read_utf16z(pe, data, target)
        if actual != expected:
            raise InspectError(
                f"RVA 0x{rva:X} string {actual!r}; expected {expected!r}"
            )

    # Public CNG blob must be ECK1, cbKey=32, total 72 bytes.
    require_instruction(ins_by_rva, 0x39EF67, "cmp", "0x48")
    require_instruction(ins_by_rva, 0x39EF71, "cmp", "0x314b4345")
    require_instruction(ins_by_rva, 0x39EF7D, "cmp", "0x20")

    # Convert ECK1 blob into SEC1 uncompressed form: 0x04 || X(32) || Y(32).
    require_instruction(ins_by_rva, 0x39EF83, "mov", "0x41")
    require_instruction(ins_by_rva, 0x39EFA9, "mov", "byte ptr [rax], 4")
    require_instruction(ins_by_rva, 0x39EFB4, "lea", "[rdi + 8]")
    require_instruction(ins_by_rva, 0x39EFB8, "add", "0x48")

    # URL-safe no-padding Base64 engine used for devicePublicKey.
    device_engine_ref = require_instruction(ins_by_rva, 0x39EFDE, "lea")
    if rip_target(device_engine_ref) != base + DEVICE_B64_ENGINE_RVA:
        raise InspectError("devicePublicKey Base64 engine target drifted")
    device_engine = verify_engine(pe, data, DEVICE_B64_ENGINE_RVA)
    device_b64_call = require_instruction(ins_by_rva, 0x39EFE8, "call")
    if call_target(device_b64_call) != base + FUNCTIONS["base64_encode"][0]:
        raise InspectError("devicePublicKey Base64 encoder target drifted")

    # Encoder explicitly checks engine byte 0; zero takes the unpadded length path.
    require_instruction(ins_by_rva, 0x401EE1, "cmp", "byte ptr [r14], 0")

    # Login explicitly uses the recovered value as devicePublicKey.
    require_instruction(ins_by_rva, 0xF26C1, "mov", "r15d, 0xf")
    require_instruction(ins_by_rva, 0xF26DD, "movabs", "0x79654b63696c6275")
    require_instruction(ins_by_rva, 0xF26EB, "movabs", "0x7550656369766564")
    require_instruction(ins_by_rva, 0xF2710, "mov", "[rdi + 0x88]")

    # launchNonce comes from challenge_get_or_create.
    challenge_call = require_instruction(ins_by_rva, 0xF250F, "call")
    if call_target(challenge_call) != base + FUNCTIONS["challenge_get_or_create"][0]:
        raise InspectError("launchNonce challenge generator call drifted")
    require_instruction(ins_by_rva, 0xF2543, "mov", "[rdi + 0xc8]")
    require_instruction(ins_by_rva, 0xF279A, "movabs", "0x6f4e68636e75616c")
    require_instruction(ins_by_rva, 0xF27A7, "mov")
    require_instruction(ins_by_rva, 0xF2793, "lea", "[rdi + 0xc8]")

    # Existing challenge is accepted only at exactly 43 URL-safe characters.
    existing_call = require_instruction(ins_by_rva, 0x23B429, "call")
    if call_target(existing_call) != base + FUNCTIONS["challenge_read_validate"][0]:
        raise InspectError("existing challenge reader target drifted")
    require_instruction(ins_by_rva, 0x23A1E6, "cmp", "0x2b")
    challenge_charset_call = require_instruction(ins_by_rva, 0x23A1FC, "call")
    if call_target(challenge_charset_call) != base + 0x20DA92:
        raise InspectError("challenge URL-safe charset validator target drifted")

    # New challenge: fill exactly 32 bytes then URL-safe/no-padding Base64 encode.
    require_instruction(ins_by_rva, 0x23B4C2, "mov", "r8d, 0x20")
    challenge_engine_ref = require_instruction(ins_by_rva, 0x23B4FD, "lea")
    if rip_target(challenge_engine_ref) != base + CHALLENGE_B64_ENGINE_RVA:
        raise InspectError("challenge Base64 engine target drifted")
    require_instruction(ins_by_rva, 0x23B50C, "mov", "r9d, 0x20")
    challenge_b64_call = require_instruction(ins_by_rva, 0x23B515, "call")
    if call_target(challenge_b64_call) != base + FUNCTIONS["base64_encode"][0]:
        raise InspectError("challenge Base64 encoder target drifted")
    challenge_engine = verify_engine(pe, data, CHALLENGE_B64_ENGINE_RVA)

    # 43-char validator itself permits only ASCII alnum, '_' and '-'.
    # It is a small leaf without an unwind entry, so assert exact opcodes by bytes.
    validator_off = pe.get_offset_from_rva(0x20DA92)
    validator = data[validator_off : validator_off + 0x50]
    # Immediate comparisons include '_' (0x5f) and '-' (0x2d).
    if bytes([0x3C, 0x5F]) not in validator and b"\x83\xf8\x5f" not in validator:
        # Capstone form in this exact build is cmp eax,0x5f => 83 f8 5f.
        raise InspectError("challenge validator '_' comparison missing")
    if b"\x83\xf8\x2d" not in validator:
        raise InspectError("challenge validator '-' comparison missing")

    return {
        "findingId": "LWB-R8-005",
        "date": "2026-09-24",
        "scope": "original devicePublicKey and launchNonce/auth-challenge encoding contract",
        "evidenceLabel": "RECOVERED",
        "source": {
            "path": str(path),
            "sha256": digest,
            "imageBase": f"0x{base:X}",
        },
        "deviceKey": {
            "provider": DEVICE_PROVIDER,
            "algorithm": DEVICE_ALGORITHM,
            "persistedKeyName": DEVICE_KEY_NAME,
            "exportBlobType": PUBLIC_BLOB_TYPE,
            "exportBlob": {
                "requiredBytes": 72,
                "magic": "ECK1",
                "cbKey": 32,
            },
            "loginPublicPoint": {
                "binaryShape": "0x04 || X[32] || Y[32]",
                "binaryBytes": 65,
                "encoding": "URL-safe Base64 without padding",
                "encodedChars": 87,
                "base64Engine": device_engine,
                "loginField": "devicePublicKey",
            },
        },
        "launchNonce": {
            "runtimeArtifact": "authorization.challenge",
            "reuseContract": {
                "acceptedEncodedChars": 43,
                "allowedCharacters": "0-9 A-Z a-z _ -",
            },
            "generation": {
                "randomBytes": 32,
                "encoding": "URL-safe Base64 without padding",
                "encodedChars": 43,
                "base64Engine": challenge_engine,
            },
            "loginField": "launchNonce",
        },
        "result": [
            "The original host persists an ECDH_P256 key named {2D337A4D-7E6C-49EF-9486-54F0A00D8A41} in Microsoft Software Key Storage Provider.",
            "devicePublicKey is the ECDH public point exported as a 72-byte ECCPUBLICBLOB (ECK1, cbKey=32), converted to 65-byte SEC1 uncompressed form 0x04||X||Y, then URL-safe Base64 encoded without padding, yielding 87 characters.",
            "launchNonce is the authorization.challenge value. A valid existing value is reused only when it is exactly 43 URL-safe characters; otherwise a fresh 32-byte challenge is generated and URL-safe Base64 encoded without padding to 43 characters.",
            "The login JSON uses those values under the exact devicePublicKey and launchNonce field names.",
            "This closes the R8-004 uncertainty around the devicePublicKey encoding but does not identify the decoded LWKE1 peer-key/encrypted-key fields returned by the server.",
        ],
        "reproduction": [
            "python tools\\inspect_lwbridge_device_login_material.py ..\\LW\\lwbridge-0.3.1.exe",
            "python tools\\inspect_lwbridge_device_login_material.py ..\\LW\\lwbridge-0.3.1.exe --output evidence\\lwbridge-implementation\\2026-09-24-r8-005-device-login-material.json",
            "python -m py_compile tools\\inspect_lwbridge_device_login_material.py",
        ],
        "limits": {
            "staticOnly": True,
            "networkRequestsMade": False,
            "livePrivateKeyReadOrExported": False,
            "notProven": [
                "decoded LWKE1 field1/remaining field semantics",
                "server peer public-key field location",
                "envelope nonce/tag/ciphertext field ownership",
                "32-byte package key",
                "bridge-scripts.dat plaintext",
            ],
            "restrictionPreserved": "No secure-proxy SB-79 RVA 0x3F8E0-0x40A6D instruction is inspected or rerouted.",
        },
        "implementationImpact": {
            "publicBehaviorEnabled": False,
            "closedGap": "exact client ECDH public-key/login encoding and launchNonce challenge contract",
            "next": "recover the returned LWKE1 peer/agreement material from an independent original artifact or non-restricted contract, then derive the package key through the already-proven ECDH/TRUNCATE helper",
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
