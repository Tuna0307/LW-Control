#!/usr/bin/env python3
"""Static 0.3.17 original bridge package/envelope contract inspector.

Hash-gated against the fixed reference EXE and exact secure proxy. This tool
does not read runtime key stores, authorization state, or protected services and
does not attempt decryption.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

ROOT = Path(__file__).resolve().parents[2]
REFERENCE = Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
REFERENCE_SHA256 = "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
SECURE_RAW = 0xA4919B
SECURE_SIZE = 619_008
SECURE_SHA256 = "ae8bba866df80e9c924923d1f59d49e824c784c8ba9b47780fe2f58305e00a86"

FUNCTIONS = {
    "moduleTable": (0x127D0, 0x13620),
    "aesGcm": (0x3E100, 0x3E58A),
    "package": (0x3E800, 0x3F295),
    "base64Url": (0x3E590, 0x3E800),
    "ecdh": (0x3F2A0, 0x3F5BE),
    "hashHmac": (0x3FE60, 0x4020A),
    "hkdf": (0x40210, 0x40360),
    "sha256Bytes": (0x40B60, 0x40BFD),
    "splitPipe": (0x40C00, 0x40DAC),
    "validateBuild": (0x40DC0, 0x40E30),
    "validateShaHex": (0x40E30, 0x40E80),
    "envelope": (0x40E80, 0x4200D),
    "verifyTokenSignature": (0x43E50, 0x440D8),
    "exportDevicePublic": (0x44660, 0x44860),
    "openDeviceKey": (0x44860, 0x44920),
}

THUNKS = {
    "BCryptOpenAlgorithmProvider": 0x46614,
    "BCryptGetProperty": 0x4661A,
    "BCryptCloseAlgorithmProvider": 0x46620,
    "BCryptCreateHash": 0x46626,
    "BCryptHashData": 0x4662C,
    "BCryptFinishHash": 0x46632,
    "BCryptDestroyHash": 0x46638,
    "BCryptImportKeyPair": 0x4663E,
    "BCryptDestroyKey": 0x46644,
    "BCryptVerifySignature": 0x4664A,
    "BCryptSetProperty": 0x46656,
    "BCryptGenerateSymmetricKey": 0x4665C,
    "BCryptDecrypt": 0x46662,
    "NCryptOpenStorageProvider": 0x46668,
    "NCryptImportKey": 0x4666E,
    "NCryptFreeObject": 0x46674,
    "NCryptSecretAgreement": 0x4667A,
    "NCryptDeriveKey": 0x46680,
    "NCryptOpenKey": 0x46686,
    "NCryptExportKey": 0x4668C,
}

TOKEN_VERIFY_X = "00b0c33896bc67481de5fdc412d9b1549e70d736d9a5f7c1a3dbfeeccd8e135f"
TOKEN_VERIFY_Y = "7ae6887c7189787df7edaf33f52f3cac0849f1382b24e8d7aa6116152b425659"


class InspectError(RuntimeError):
    pass


def require(condition: bool, message: str) -> None:
    if not condition:
        raise InspectError(message)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def load_proxy() -> tuple[bytes, pefile.PE]:
    host = REFERENCE.read_bytes()
    require(sha256(host) == REFERENCE_SHA256, "reference EXE identity changed")
    proxy = host[SECURE_RAW : SECURE_RAW + SECURE_SIZE]
    require(sha256(proxy) == SECURE_SHA256, "secure proxy identity changed")
    return proxy, pefile.PE(data=proxy, fast_load=False)


def disassemble(proxy: bytes, pe: pefile.PE, start: int, end: int):
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    offset = pe.get_offset_from_rva(start)
    return list(
        md.disasm(
            proxy[offset : offset + (end - start)],
            int(pe.OPTIONAL_HEADER.ImageBase) + start,
        )
    )


def call_targets(insns, image_base: int) -> set[int]:
    out: set[int] = set()
    for ins in insns:
        if ins.mnemonic != "call" or not ins.operands:
            continue
        op = ins.operands[0]
        if op.type == X86_OP_IMM:
            out.add(int(op.imm) - image_base)
    return out


def ins_at(insns) -> dict[int, Any]:
    image_base = int(insns[0].address) - (
        int(insns[0].address) & 0xFFFFFFFF
    ) if insns else 0
    # Caller uses absolute address subtraction separately; map populated below.
    return {}


def text_at(proxy: bytes, pe: pefile.PE, rva: int, size: int) -> bytes:
    return proxy[pe.get_offset_from_rva(rva) : pe.get_offset_from_rva(rva) + size]


def has_call(proxy: bytes, pe: pefile.PE, fn: str, target: int) -> bool:
    start, end = FUNCTIONS[fn]
    image_base = int(pe.OPTIONAL_HEADER.ImageBase)
    return target in call_targets(disassemble(proxy, pe, start, end), image_base)


def instruction_map(proxy: bytes, pe: pefile.PE, fn: str) -> dict[int, Any]:
    start, end = FUNCTIONS[fn]
    base = int(pe.OPTIONAL_HEADER.ImageBase)
    return {
        int(ins.address) - base: ins
        for ins in disassemble(proxy, pe, start, end)
    }


def require_mnemonic(rows: dict[int, Any], rva: int, mnemonic: str, contains: str | None = None):
    require(rva in rows, f"instruction missing at {rva:#x}")
    ins = rows[rva]
    require(ins.mnemonic == mnemonic, f"mnemonic changed at {rva:#x}: {ins.mnemonic}")
    if contains is not None:
        require(contains.lower() in ins.op_str.lower(),
                f"operand changed at {rva:#x}: {ins.op_str}")


def inspect() -> dict[str, Any]:
    proxy, pe = load_proxy()
    image_base = int(pe.OPTIONAL_HEADER.ImageBase)

    aes = instruction_map(proxy, pe, "aesGcm")
    require_mnemonic(aes, 0x3E133, "cmp", "0x20")
    require_mnemonic(aes, 0x3E144, "cmp", "0xc")
    require_mnemonic(aes, 0x3E155, "cmp", "0x10")
    for name in (
        "BCryptOpenAlgorithmProvider",
        "BCryptSetProperty",
        "BCryptGenerateSymmetricKey",
        "BCryptDecrypt",
    ):
        require(has_call(proxy, pe, "aesGcm", THUNKS[name]),
                f"AES helper lost {name}")
    require(text_at(proxy, pe, 0x75AC8, 8).decode("utf-16le").startswith("AES"),
            "AES algorithm literal moved")
    require(
        text_at(proxy, pe, 0x75AD0, 34).decode("utf-16le").startswith("ChainingModeGCM"),
        "GCM property literal moved",
    )

    package = instruction_map(proxy, pe, "package")
    require_mnemonic(package, 0x3E8D3, "cmp", "2")
    require_mnemonic(package, 0x3EBA5, "cmp", "0x16")
    require_mnemonic(package, 0x3EC85, "sub", "0x22")
    require_mnemonic(package, 0x3EC9D, "lea", "0x2e")
    require_mnemonic(package, 0x3ED63, "lea", "- 1")
    require_mnemonic(package, 0x3ED67, "cmp", "0xffffff")
    require(has_call(proxy, pe, "package", FUNCTIONS["sha256Bytes"][0]),
            "package whole-file SHA-256 call moved")
    require(has_call(proxy, pe, "package", FUNCTIONS["aesGcm"][0]),
            "package AES-GCM call moved")
    require(
        text_at(proxy, pe, 0x75C60, 7) == b"LWBP2|\x00",
        "package AAD prefix changed",
    )

    ecdh = instruction_map(proxy, pe, "ecdh")
    require_mnemonic(ecdh, 0x3F2DD, "cmp", "0x41")
    require_mnemonic(ecdh, 0x3F2E7, "cmp", "4")
    require_mnemonic(ecdh, 0x3F44D, "cmp", "0x20")
    for name in (
        "NCryptOpenStorageProvider",
        "NCryptImportKey",
        "NCryptSecretAgreement",
        "NCryptDeriveKey",
    ):
        require(has_call(proxy, pe, "ecdh", THUNKS[name]),
                f"ECDH helper lost {name}")
    require(
        text_at(proxy, pe, 0x75BA0, 18).decode("utf-16le").startswith("TRUNCATE"),
        "TRUNCATE KDF literal moved",
    )

    hkdf = instruction_map(proxy, pe, "hkdf")
    require_mnemonic(hkdf, 0x40250, "mov", "edx, 8")
    require_mnemonic(hkdf, 0x40333, "mov", "edx, 8")
    require_mnemonic(hkdf, 0x40309, "mov", "1")
    require(has_call(proxy, pe, "hkdf", FUNCTIONS["hashHmac"][0]),
            "HKDF helper no longer uses hash/HMAC helper")
    hmac = instruction_map(proxy, pe, "hashHmac")
    require_mnemonic(hmac, 0x3FEAE, "mov", "edx")
    require(has_call(proxy, pe, "hashHmac", THUNKS["BCryptCreateHash"]),
            "HMAC helper lost BCryptCreateHash")
    require(has_call(proxy, pe, "hashHmac", THUNKS["BCryptHashData"]),
            "HMAC helper lost BCryptHashData")
    require(has_call(proxy, pe, "hashHmac", THUNKS["BCryptFinishHash"]),
            "HMAC helper lost BCryptFinishHash")
    require(text_at(proxy, pe, 0x71E10, 14).decode("utf-16le").startswith("SHA256"),
            "SHA256 literal moved")

    envelope = instruction_map(proxy, pe, "envelope")
    require_mnemonic(envelope, 0x411F0, "cmp", "0xe")
    decoded_lengths = {
        8: (0x413A0, 0x20),
        9: (0x413B5, 0x41),
        10: (0x413D0, 0x20),
        11: (0x413E2, 0x0C),
        12: (0x413F4, 0x20),
        13: (0x41406, 0x10),
    }
    for idx, (rva, length) in decoded_lengths.items():
        require_mnemonic(envelope, rva, "cmp", hex(length))
    require_mnemonic(envelope, 0x413BF, "cmp", "4")
    require_mnemonic(envelope, 0x414B3, "cmp", "0x4b0")
    require(has_call(proxy, pe, "envelope", FUNCTIONS["ecdh"][0]),
            "envelope ECDH call moved")
    require(has_call(proxy, pe, "envelope", FUNCTIONS["hkdf"][0]),
            "envelope HKDF call moved")
    require(has_call(proxy, pe, "envelope", FUNCTIONS["aesGcm"][0]),
            "envelope AES call moved")
    require(has_call(proxy, pe, "envelope", FUNCTIONS["openDeviceKey"][0]),
            "envelope device-key call moved")
    require(has_call(proxy, pe, "envelope", FUNCTIONS["exportDevicePublic"][0]),
            "envelope device-public export call moved")
    require(has_call(proxy, pe, "envelope", FUNCTIONS["sha256Bytes"][0]),
            "envelope device-public SHA call moved")
    require(text_at(proxy, pe, 0x75BFC, 2) == b"|\x00",
            "envelope canonical delimiter moved")

    sig = instruction_map(proxy, pe, "verifyTokenSignature")
    require_mnemonic(sig, 0x43E69, "cmp", "0x40")
    require(has_call(proxy, pe, "verifyTokenSignature", THUNKS["BCryptImportKeyPair"]),
            "token verifier lost public-key import")
    require(has_call(proxy, pe, "verifyTokenSignature", THUNKS["BCryptVerifySignature"]),
            "token verifier lost signature verification")
    require(
        text_at(proxy, pe, 0x73488, 24).decode("utf-16le").startswith("ECDSA_P256"),
        "ECDSA_P256 literal moved",
    )
    require(
        text_at(proxy, pe, 0x734A0, 28).decode("utf-16le").startswith("ECCPUBLICBLOB"),
        "ECCPUBLICBLOB literal moved",
    )
    key_bytes = text_at(proxy, pe, 0x72040, 64)
    require(key_bytes[:32].hex() == TOKEN_VERIFY_X, "token verify X coordinate changed")
    require(key_bytes[32:].hex() == TOKEN_VERIFY_Y, "token verify Y coordinate changed")

    require(
        text_at(proxy, pe, 0x75B30, 80).decode("utf-16le").startswith(
            "Microsoft Software Key Storage Provider"
        ),
        "device CNG provider changed",
    )
    require(
        text_at(proxy, pe, 0x75DB0, 78).decode("utf-16le").startswith(
            "{2D337A4D-7E6C-49EF-9486-54F0A00D8A41}"
        ),
        "device CNG key name changed",
    )

    module = instruction_map(proxy, pe, "moduleTable")
    require_mnemonic(module, 0x1286E, "cmp", "0x3ff")
    require_mnemonic(module, 0x12D14, "cmp", "9")
    require(text_at(proxy, pe, 0x736B0, 10).startswith(b"bootstrap"),
            "bootstrap name literal moved")
    require(text_at(proxy, pe, 0x73738, 21).startswith(b"trailing package data"),
            "trailing-data rejection literal moved")
    require(text_at(proxy, pe, 0x73750, 18).startswith(b"bootstrap missing"),
            "bootstrap-required literal moved")
    require(
        text_at(proxy, pe, 0x73658, 41).startswith(b"local __bridge_preload = package.preload\n"),
        "preload wrapper prefix changed",
    )

    return {
        "ok": True,
        "findingId": "LWB317-FUNCTION-ORIGINAL-LUA-RECOVERY-006-B",
        "referenceSha256": REFERENCE_SHA256,
        "secureProxySha256": SECURE_SHA256,
        "functionRvas": {
            name: [f"0x{start:X}", f"0x{end:X}"]
            for name, (start, end) in FUNCTIONS.items()
        },
        "package": {
            "format": {
                "magic": "LWBP",
                "version": 2,
                "buildIdLength": 22,
                "nonceLength": 12,
                "tagLength": 16,
                "cipherLengthEncoding": "little-endian u32",
                "buildOffset": 12,
                "nonceOffset": 0x22,
                "cipherLengthOffset": 0x2E,
                "ciphertextOffset": 0x32,
            },
            "integrity": "SHA-256 over complete encrypted package, lowercase hex compared to envelope-derived expected SHA",
            "buildBinding": "22-byte package build id compared to envelope-derived expected build",
            "aad": "ASCII 'LWBP2|' + exact package build id",
            "cipher": "AES-256-GCM",
            "compression": "NONE: AES plaintext is consumed directly by module-table parser",
        },
        "envelope": {
            "outerEncoding": "exactly two canonical Base64URL segments separated by one '.'",
            "signature": {
                "segment": 2,
                "decodedLength": 64,
                "algorithm": "ECDSA_P256",
                "digest": "SHA-256(decoded segment 1)",
                "publicKeyX": TOKEN_VERIFY_X,
                "publicKeyY": TOKEN_VERIFY_Y,
            },
            "payload": {
                "segment": 1,
                "fieldDelimiter": "|",
                "fieldCount": 14,
                "fields": {
                    "0": "literal LWKE1",
                    "1": "decimal u64 timestamp-1; must be <= currentTime+300",
                    "2": "decimal u64 timestamp-2; must be > currentTime, > field1, delta <=1200, and within caller ceiling",
                    "3": "22-character build id",
                    "4": "64-character lowercase SHA-256 hex",
                    "5": "nonzero decimal u64 caller-bound context value A",
                    "6": "nonzero decimal u64 caller-bound context value B",
                    "7": "nonzero decimal u64 context value C",
                    "8": "Base64URL -> 32-byte SHA-256 binding of local exported device public material",
                    "9": "Base64URL -> 65-byte uncompressed P-256 peer point, first byte 0x04",
                    "10": "Base64URL -> 32-byte HKDF salt",
                    "11": "Base64URL -> 12-byte AES-GCM nonce",
                    "12": "Base64URL -> 32-byte encrypted package key",
                    "13": "Base64URL -> 16-byte AES-GCM tag",
                },
            },
            "deviceKey": {
                "provider": "Microsoft Software Key Storage Provider",
                "persistedKeyName": "{2D337A4D-7E6C-49EF-9486-54F0A00D8A41}",
                "privateKeyReadOrExportedByThisTool": False,
            },
            "agreement": {
                "curve": "P-256",
                "peerPointEncoding": "0x04 || X32 || Y32",
                "primitive": "NCryptSecretAgreement",
                "rawDerivedLength": 32,
                "ncryptKdf": "TRUNCATE",
            },
            "keyDerivation": {
                "algorithm": "HKDF-SHA256, one expand block",
                "extract": "HMAC-SHA256(key=field10 salt, data=32-byte ECDH output)",
                "expand": "HMAC-SHA256(key=PRK, data=canonicalInfo || 0x01)",
                "outputLength": 32,
                "canonicalInfoAndAadFieldOrder": [0, 3, 5, 6, 7, 2, 8],
                "canonicalSeparator": "|",
            },
            "keyCipher": {
                "algorithm": "AES-256-GCM",
                "key": "32-byte HKDF output",
                "nonce": "field11",
                "ciphertext": "field12",
                "tag": "field13",
                "aad": "same canonical envelope info string",
                "plaintextLength": 32,
                "plaintextMeaning": "package AES-256-GCM key",
            },
            "packageContextOutput": {
                "buildId": "field3 copied to context +0x20",
                "packageSha256": "field4 copied to context +0x40",
            },
        },
        "moduleTable": {
            "compression": "none",
            "prefix": "little-endian u32 entry count",
            "entryCountRange": [1, 1024],
            "entryShape": "u32 nameLength, u32 sourceLength, name bytes, source bytes",
            "nameAlphabet": "A-Z a-z 0-9 . _",
            "bootstrap": "exactly one entry named 'bootstrap' required",
            "trailingBytesAllowed": False,
            "loaderConstruction": [
                "starts generated source with 'local __bridge_preload = package.preload\\n'",
                "non-bootstrap entries are wrapped into package.loaded/package.preload source scaffolding",
                "bootstrap source is retained separately and appended/executed by loader",
            ],
            "bytecodeObservation": "plaintext entries are treated as source byte strings; no compression or separate bytecode container is present at this layer",
        },
        "historical031Revalidation": {
            "LWBPv2Layout": "CONFIRMED_SHAPE, 0.3.17 identity differs",
            "packageAadLWBP2Build": "CONFIRMED",
            "packageWholeSha256Binding": "CONFIRMED",
            "packageBuildBinding": "CONFIRMED",
            "AES256GCM": "CONFIRMED",
            "signedTwoSegmentEnvelope": "CONFIRMED_AND_EXPANDED",
            "deviceBoundP256Agreement": "CONFIRMED_AND_EXPANDED",
            "hkdfSha256": "CONFIRMED",
            "moduleTableAfterDecrypt": "CONFIRMED; no compression stage",
        },
        "requiredInputs": {
            "encryptedPackage": "present in supplied 0.3.17 EXE",
            "signedEnvelope": "not supplied in bounded artifact roots",
            "persistedLocalPrivateKey": "required by protocol; owner-state CNG key not read/exported",
            "validContextBindings": "envelope embeds/validates time/build/hash/context/device bindings",
        },
        "safeDisposition": {
            "canExtractEncryptedPackage": True,
            "canDecryptFromSuppliedInputs": False,
            "missingSuppliedArtifact": "package-key.envelope",
            "additionalRequiredOwnerState": "matching persisted CNG private key; not accessed",
            "bypassAttempted": False,
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        result = inspect()
    except (InspectError, OSError, pefile.PEFormatError) as exc:
        parser.error(str(exc))
    encoded = json.dumps(result, indent=2) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded, encoding="utf-8")
    else:
        print(encoded, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
