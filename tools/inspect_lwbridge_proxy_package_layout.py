#!/usr/bin/env python3
"""Verify LWBridge 0.3.1 LWBP2 package layout and package AES-GCM call contract.

This inspector is static and read-only. It hash-gates the verified reference
EXE, extracts the embedded secure proxy and bridge-scripts.dat, verifies the
unwind-bounded package function already identified in R6-045, and proves the
package field/AAD/AES argument layout without inspecting the historically
restricted package-key consumer RVA 0x3F8E0-0x40A6D.

It does not read the persisted private key, parse package-key.envelope, decrypt
bridge-scripts.dat, execute LWBridge/Last War, or inspect the SB-79 body.
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


EXPECTED_HOST_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
EXPECTED_PROXY_SHA256 = "481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400"
EXPECTED_PACKAGE_SHA256 = "a1e23419c25c76bee16942922a643381e0d38a857902569d927f22ef02c99c8d"
EXPECTED_IMAGE_BASE = 0x180000000

# These are outer-host RVAs, not raw offsets.
HOST_PACKAGE_RVA = 0x8699E0
HOST_PACKAGE_SIZE = 1_172_723
HOST_SECURE_PROXY_RVA = 0x987F74
HOST_SECURE_PROXY_SIZE = 612_352

PACKAGE_FUNCTION = (0x3D260, 0x3DCF5)
AES_GCM_FUNCTION = (0x3CB60, 0x3CFEA)
SHA256_FUNCTION = (0x3F5C0, 0x3F65D)


class InspectError(ValueError):
    pass


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def instructions(pe: pefile.PE, data: bytes, begin_rva: int, end_rva: int):
    offset = pe.get_offset_from_rva(begin_rva)
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    return list(
        md.disasm(
            data[offset : offset + (end_rva - begin_rva)],
            pe.OPTIONAL_HEADER.ImageBase + begin_rva,
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
    ins_by_rva: dict[int, Any],
    rva: int,
    mnemonic: str,
    contains: str | None = None,
):
    ins = ins_by_rva.get(rva)
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


def extract_host_asset(
    host_pe: pefile.PE,
    host: bytes,
    rva: int,
    size: int,
    expected_hash: str,
    label: str,
) -> tuple[bytes, int]:
    try:
        offset = int(host_pe.get_offset_from_rva(rva))
    except pefile.PEFormatError as exc:
        raise InspectError(f"{label} RVA 0x{rva:X} is not file-backed") from exc
    payload = host[offset : offset + size]
    if len(payload) != size:
        raise InspectError(f"{label} is truncated")
    digest = sha256(payload)
    if digest != expected_hash:
        raise InspectError(
            f"{label} SHA-256 {digest}; expected {expected_hash}"
        )
    return payload, offset


def parse_package(package: bytes) -> dict[str, Any]:
    if len(package) < 66 or package[:4] != b"LWBP":
        raise InspectError("bridge package does not start with LWBP")

    version, build_len = struct.unpack_from("<II", package, 4)
    if version != 2:
        raise InspectError(f"package version {version}; expected 2")
    if build_len != 22:
        raise InspectError(f"build ID length {build_len}; expected 22")

    build_start = 12
    build_end = build_start + build_len
    try:
        build_id = package[build_start:build_end].decode("ascii")
    except UnicodeDecodeError as exc:
        raise InspectError("build ID is not ASCII") from exc

    nonce_offset = build_end
    cipher_length_offset = nonce_offset + 12
    if cipher_length_offset + 4 > len(package):
        raise InspectError("package ends before ciphertext length")
    cipher_length = struct.unpack_from("<I", package, cipher_length_offset)[0]
    if not 1 <= cipher_length <= 0x1000000:
        raise InspectError(
            f"ciphertext length {cipher_length} is outside 1..0x1000000"
        )

    ciphertext_offset = cipher_length_offset + 4
    tag_offset = ciphertext_offset + cipher_length
    if tag_offset + 16 != len(package):
        raise InspectError(
            "package size is not header/build/nonce/length/ciphertext/16-byte-tag"
        )

    aad = b"LWBP2|" + build_id.encode("ascii")
    if len(aad) != 28:
        raise InspectError(f"AAD length {len(aad)}; expected 28")

    return {
        "magic": "LWBP",
        "version": version,
        "buildIdLength": build_len,
        "buildId": build_id,
        "header": {
            "magicOffset": 0,
            "versionOffset": 4,
            "buildIdLengthOffset": 8,
            "buildIdOffset": build_start,
            "nonceOffset": nonce_offset,
            "nonceLength": 12,
            "ciphertextLengthOffset": cipher_length_offset,
            "ciphertextOffset": ciphertext_offset,
            "ciphertextLength": cipher_length,
            "tagOffset": tag_offset,
            "tagLength": 16,
        },
        "aadAscii": aad.decode("ascii"),
        "aadLength": len(aad),
        "nonceHex": package[nonce_offset : nonce_offset + 12].hex(),
        "tagHex": package[tag_offset : tag_offset + 16].hex(),
        "totalSize": len(package),
    }


def verify_proxy(proxy: bytes) -> dict[str, Any]:
    pe = pefile.PE(data=proxy, fast_load=False)
    if pe.OPTIONAL_HEADER.ImageBase != EXPECTED_IMAGE_BASE:
        raise InspectError(
            f"secure proxy image base 0x{pe.OPTIONAL_HEADER.ImageBase:X}; "
            f"expected 0x{EXPECTED_IMAGE_BASE:X}"
        )

    pe.parse_data_directories(
        directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]]
    )
    unwind = {
        (int(entry.struct.BeginAddress), int(entry.struct.EndAddress))
        for entry in pe.DIRECTORY_ENTRY_EXCEPTION
    }
    for expected in (PACKAGE_FUNCTION, AES_GCM_FUNCTION, SHA256_FUNCTION):
        if expected not in unwind:
            raise InspectError(
                f"expected unwind range 0x{expected[0]:X}-0x{expected[1]:X} missing"
            )

    base = pe.OPTIONAL_HEADER.ImageBase
    package_ins = instructions(pe, proxy, *PACKAGE_FUNCTION)
    aes_ins = instructions(pe, proxy, *AES_GCM_FUNCTION)
    sha_ins = instructions(pe, proxy, *SHA256_FUNCTION)
    ins_by_rva = {
        ins.address - base: ins
        for ins in package_ins + aes_ins + sha_ins
    }

    # Package function signature ownership recovered from the prologue.
    require_instruction(ins_by_rva, 0x3D28A, "mov", "[rbp - 0x50], r9")
    require_instruction(ins_by_rva, 0x3D28E, "mov", "[rbp - 0x58], r8")
    require_instruction(ins_by_rva, 0x3D292, "mov", "r14, rdx")
    require_instruction(ins_by_rva, 0x3D299, "mov", "rdi, rcx")

    # Header/version and SHA-256 integrity path.
    require_instruction(ins_by_rva, 0x3D2DB, "lea")
    require_instruction(ins_by_rva, 0x3D314, "movzx", "[rdx + 7]")
    require_instruction(ins_by_rva, 0x3D333, "cmp", "ecx, 2")
    sha_call = require_instruction(ins_by_rva, 0x3D35E, "call")
    if call_target(sha_call) != base + SHA256_FUNCTION[0]:
        raise InspectError("package SHA-256 helper target drifted")
    require_instruction(ins_by_rva, 0x3D470, "lea")
    require_instruction(ins_by_rva, 0x3D4E3, "lea", "[r14 + 0x40]")

    # Build ID field and manifest/context comparison.
    require_instruction(ins_by_rva, 0x3D561, "movzx", "[rdx + 0xb]")
    require_instruction(ins_by_rva, 0x3D580, "cmp", "ecx, r13d")
    require_instruction(ins_by_rva, 0x3D5B7, "add", "rdx, 0xc")
    require_instruction(ins_by_rva, 0x3D5C8, "lea", "[r14 + 0x20]")
    require_instruction(ins_by_rva, 0x3D5EE, "cmp", "[r14 + 0x30]")


    # Exact LWBP2 binary slices: nonce [0x22,0x2E), uint32 length @0x2E,
    # ciphertext @0x32, and final 16-byte tag.
    require_instruction(ins_by_rva, 0x3D6E5, "sub", "0x22")
    require_instruction(ins_by_rva, 0x3D6FD, "lea", "[rax + 0x2e]")
    require_instruction(ins_by_rva, 0x3D70A, "lea", "[rax + 0x22]")

    require_instruction(ins_by_rva, 0x3D79D, "movzx", "[rcx + 0x31]")
    require_instruction(ins_by_rva, 0x3D7BC, "movzx", "[rcx + 0x2e]")
    require_instruction(ins_by_rva, 0x3D7D9, "sub", "0x42")
    require_instruction(ins_by_rva, 0x3D7DD, "cmp", "rcx, rax")
    require_instruction(ins_by_rva, 0x3D7EF, "lea", "[r14 + r13]")
    require_instruction(ins_by_rva, 0x3D85F, "lea", "[r13 + 0x32]")
    require_instruction(ins_by_rva, 0x3D87C, "add", "0x32")
    require_instruction(ins_by_rva, 0x3D88A, "lea", "[rdx + 0x10]")

    # AAD is the literal "LWBP2|" followed by the package build ID.
    aad_dword = require_instruction(ins_by_rva, 0x3DA00, "mov")
    aad_word = require_instruction(ins_by_rva, 0x3DA08, "movzx")
    for ins, expected in ((aad_dword, b"LWBP2|"), (aad_word, b"2|")):
        target = rip_target(ins)
        if target is None:
            raise InspectError("AAD literal reference is not RIP-relative")
        raw = pe.get_offset_from_rva(target - base)
        if not proxy[raw : raw + len(expected)].startswith(expected):
            raise InspectError(
                f"AAD literal at RVA 0x{ins.address-base:X} drifted"
            )
    require_instruction(ins_by_rva, 0x3DA13, "lea", "[rbx + 6]")
    require_instruction(ins_by_rva, 0x3DA1A, "mov", "rdx, r14")

    # Exact package-to-AES argument wiring.
    require_instruction(ins_by_rva, 0x3DA26, "lea", "[rsp + 0x50]")
    require_instruction(ins_by_rva, 0x3DA2B, "mov", "[rsp + 0x28], rax")
    require_instruction(ins_by_rva, 0x3DA30, "lea", "[rbp - 8]")
    require_instruction(ins_by_rva, 0x3DA34, "mov", "[rsp + 0x20], rax")
    require_instruction(ins_by_rva, 0x3DA39, "lea", "r9, [rsp + 0x68]")
    require_instruction(ins_by_rva, 0x3DA3E, "lea", "r8, [rbp - 0x80]")
    require_instruction(ins_by_rva, 0x3DA42, "lea", "rdx, [rbp - 0x48]")
    require_instruction(ins_by_rva, 0x3DA46, "mov", "[rbp - 0x58]")
    aes_call = require_instruction(ins_by_rva, 0x3DA4A, "call")
    if call_target(aes_call) != base + AES_GCM_FUNCTION[0]:
        raise InspectError("package AES-GCM helper target drifted")
    require_instruction(ins_by_rva, 0x3DAB7, "mov", "[rbp - 0x50]")

    # AES helper validates key/nonce/tag lengths and uses the fifth argument as
    # BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO AAD and sixth as plaintext output.
    require_instruction(ins_by_rva, 0x3CB93, "cmp", "0x20")
    require_instruction(ins_by_rva, 0x3CBA4, "cmp", "0xc")
    require_instruction(ins_by_rva, 0x3CBB5, "cmp", "0x10")
    require_instruction(ins_by_rva, 0x3CD74, "cmp", "[r15 + 0x18]")
    require_instruction(ins_by_rva, 0x3CD83, "mov", "[rbp - 0x31], rax")
    require_instruction(ins_by_rva, 0x3CD8B, "mov", "[rbp - 0x29], eax")
    require_instruction(ins_by_rva, 0x3CDB0, "mov", "rcx, r12")
    decrypt_call = require_instruction(ins_by_rva, 0x3CDFB, "call")
    if call_target(decrypt_call) != base + 0x450C2:
        raise InspectError("BCryptDecrypt import thunk target drifted")

    # SHA helper is explicitly SHA256.
    sha_literal_ins = require_instruction(ins_by_rva, 0x3F612, "lea")
    target = rip_target(sha_literal_ins)
    if target is None:
        raise InspectError("SHA256 literal reference is not RIP-relative")
    raw = pe.get_offset_from_rva(target - base)
    if proxy[raw : raw + len("SHA256".encode("utf-16le"))] != "SHA256".encode("utf-16le"):
        raise InspectError("SHA256 literal drifted")

    return {
        "packageFunction": {
            "beginRva": f"0x{PACKAGE_FUNCTION[0]:X}",
            "endRva": f"0x{PACKAGE_FUNCTION[1]:X}",
            "sha256HelperRva": f"0x{SHA256_FUNCTION[0]:X}",
            "aesGcmCallRva": "0x3DA4A",
            "aesGcmHelperRva": f"0x{AES_GCM_FUNCTION[0]:X}",
            "expectedBuildIdContextOffset": "0x20",
            "expectedBuildIdLengthContextOffset": "0x30",
            "expectedSha256HexContextOffset": "0x40",
        },
        "aesGcmArguments": {
            "rcx": "32-byte package key vector (original package-function R8)",
            "rdx": "12-byte package nonce vector",
            "r8": "ciphertext vector",
            "r9": "16-byte authentication-tag vector",
            "stackArg5": "AAD string: LWBP2| + buildId",
            "stackArg6": "plaintext output vector",
        },
        "packageFunctionOutputs": {
            "originalR9": "decrypted package bytes on success",
            "stackArg5": "error text on failure",
        },
        "sha256Algorithm": "SHA256",
    }


def inspect(path: Path) -> dict[str, Any]:
    host = path.read_bytes()
    host_hash = sha256(host)
    if host_hash != EXPECTED_HOST_SHA256:
        raise InspectError(
            f"unsupported LWBridge SHA-256 {host_hash}; expected {EXPECTED_HOST_SHA256}"
        )

    host_pe = pefile.PE(data=host, fast_load=False)
    package, package_raw = extract_host_asset(
        host_pe,
        host,
        HOST_PACKAGE_RVA,
        HOST_PACKAGE_SIZE,
        EXPECTED_PACKAGE_SHA256,
        "bridge-scripts.dat",
    )
    proxy, proxy_raw = extract_host_asset(
        host_pe,
        host,
        HOST_SECURE_PROXY_RVA,
        HOST_SECURE_PROXY_SIZE,
        EXPECTED_PROXY_SHA256,
        "xlua-proxy-secure.dll",
    )

    package_layout = parse_package(package)
    proxy_contract = verify_proxy(proxy)

    return {
        "findingId": "LWB-R8-003",
        "date": "2026-09-24",
        "scope": "LWBP2 bridge package binary layout, AAD and package AES-GCM argument ownership",
        "evidenceLabel": "RECOVERED",
        "source": {
            "path": str(path),
            "sha256": host_hash,
            "bridgePackage": {
                "hostRva": f"0x{HOST_PACKAGE_RVA:X}",
                "hostRawOffset": f"0x{package_raw:X}",
                "size": len(package),
                "sha256": sha256(package),
            },
            "secureProxy": {
                "hostRva": f"0x{HOST_SECURE_PROXY_RVA:X}",
                "hostRawOffset": f"0x{proxy_raw:X}",
                "size": len(proxy),
                "sha256": sha256(proxy),
            },
            "coordinateRule": "outer-host asset constants are RVAs; raw offsets are PE-mapped and reported separately",
        },
        "tools": {
            "python": "3.12.10",
            "pefile": pefile.__version__,
            "capstone": capstone.__version__,
        },
        "packageLayout": package_layout,
        "proxyContract": proxy_contract,
        "result": [
            "LWBP v2 layout is magic/version/build-length/build-id, then a 12-byte nonce, u32 little-endian ciphertext length, ciphertext, and a final 16-byte GCM tag.",
            "The verified 1,172,723-byte package has a 1,172,657-byte ciphertext and satisfies exact total-size relation 66+ciphertextLength.",
            "Package AAD is exactly ASCII LWBP2| followed by the 22-byte build ID.",
            "The package function compares SHA256(package) as lowercase hexadecimal against context +0x40 and compares the embedded build ID against context +0x20/+0x30 before decrypting.",
            "At RVA 0x3DA4A, AES-GCM receives the original third package-function argument as the 32-byte key, parsed nonce/ciphertext/tag, AAD, and a plaintext output vector.",
            "Successful plaintext is copied to the original fourth package-function argument; failure uses the fifth argument for error text.",
        ],
        "reproduction": [
            "python tools\\inspect_lwbridge_proxy_package_layout.py ..\\LW\\lwbridge-0.3.1.exe",
            "python tools\\inspect_lwbridge_proxy_package_layout.py ..\\LW\\lwbridge-0.3.1.exe --output evidence\\lwbridge-implementation\\2026-09-24-r8-003-lwbp2-package-layout.json",
            "python -m py_compile tools\\inspect_lwbridge_proxy_package_layout.py",
        ],
        "limits": {
            "staticOnly": True,
            "notProven": [
                "package-key.envelope field grammar",
                "how the 32-byte package key is derived from envelope fields",
                "which envelope field supplies the 65-byte peer public key to agreement helper RVA 0x3DD00",
                "bridge-scripts.dat plaintext or its post-decrypt container/entry structure",
                "protected bridge command handler plaintext",
                "plain-proxy instruction parity",
            ],
            "restrictionPreserved": "SB-79 is not replayed or rerouted: this inspector never decodes or queries RVA 0x3F8E0-0x40A6D.",
            "interpretationRule": "Only hash-locked package bytes and exact instructions in previously permitted unwind-bounded package/AES/SHA functions are treated as recovered.",
        },
        "implementationImpact": {
            "publicBehaviorEnabled": False,
            "closedGap": "exact LWBP2 package field layout plus package-function SHA/build checks, AAD and AES key/nonce/tag/ciphertext ownership",
            "next": "recover package-key.envelope grammar/key derivation through independent permitted evidence, then decrypt and preserve original script bytes",
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
