#!/usr/bin/env python3
"""Recover the package-key output dataflow around the opaque LWKE1 consumer.

Static/read-only and hash-gated to LWBridge 0.3.1. The inspector deliberately
does NOT disassemble or query the historically restricted secure-proxy body at
RVA 0x3F8E0-0x40A6D. It proves caller-side pointer ownership only:

- the envelope consumer receives a zero-initialized vector as its fourth arg;
- on success that exact vector address is passed as the package function's
  third arg;
- the package function forwards that third arg as AES-GCM key input;
- the AES helper requires the key vector length to be exactly 32 bytes.

This closes output ownership without recovering the restricted parser body.
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
from capstone.x86_const import X86_OP_IMM

EXPECTED_HOST_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
EXPECTED_PROXY_SHA256 = "481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400"
EXPECTED_PROXY_IMAGE_BASE = 0x180000000

HOST_SECURE_PROXY_RVA = 0x987F74
HOST_SECURE_PROXY_SIZE = 612_352

LOADER = (0x1C0A0, 0x1D46F)
OPAQUE_CONSUMER = (0x3F8E0, 0x40A6D)
PACKAGE_FUNCTION = (0x3D260, 0x3DCF5)
AES_FUNCTION = (0x3CB60, 0x3CFEA)


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


def inspect(path: Path) -> dict[str, Any]:
    host = path.read_bytes()
    host_hash = sha256(host)
    if host_hash != EXPECTED_HOST_SHA256:
        raise InspectError(
            f"unsupported LWBridge SHA-256 {host_hash}; expected {EXPECTED_HOST_SHA256}"
        )

    host_pe = pefile.PE(data=host, fast_load=False)
    try:
        proxy_raw = int(host_pe.get_offset_from_rva(HOST_SECURE_PROXY_RVA))
    except pefile.PEFormatError as exc:
        raise InspectError("secure proxy host RVA is not file-backed") from exc
    proxy = host[proxy_raw : proxy_raw + HOST_SECURE_PROXY_SIZE]
    if len(proxy) != HOST_SECURE_PROXY_SIZE:
        raise InspectError("embedded secure proxy is truncated")
    proxy_hash = sha256(proxy)
    if proxy_hash != EXPECTED_PROXY_SHA256:
        raise InspectError(
            f"secure proxy SHA-256 {proxy_hash}; expected {EXPECTED_PROXY_SHA256}"
        )

    pe = pefile.PE(data=proxy, fast_load=False)
    base = int(pe.OPTIONAL_HEADER.ImageBase)
    if base != EXPECTED_PROXY_IMAGE_BASE:
        raise InspectError(
            f"secure proxy image base 0x{base:X}; expected 0x{EXPECTED_PROXY_IMAGE_BASE:X}"
        )

    pe.parse_data_directories(
        directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]]
    )
    unwind = {
        (int(e.struct.BeginAddress), int(e.struct.EndAddress))
        for e in pe.DIRECTORY_ENTRY_EXCEPTION
    }
    for bounds in (LOADER, PACKAGE_FUNCTION, AES_FUNCTION, OPAQUE_CONSUMER):
        if bounds not in unwind:
            raise InspectError(
                f"expected unwind range 0x{bounds[0]:X}-0x{bounds[1]:X} missing"
            )

    # Important: do not disassemble OPAQUE_CONSUMER.
    ins_by_rva: dict[int, Any] = {}
    for bounds in (LOADER, PACKAGE_FUNCTION, AES_FUNCTION):
        for ins in disassemble(pe, proxy, *bounds):
            ins_by_rva[ins.address - base] = ins

    # Before the opaque envelope consumer, rsp+0x48 is a zero/empty vector.
    require_instruction(ins_by_rva, 0x1C838, "movdqu", "[rsp + 0x48]")
    require_instruction(ins_by_rva, 0x1C83E, "mov", "[rsp + 0x58]")

    # Envelope path is built, then the opaque consumer receives:
    # arg1 RCX = path
    # arg2 RDX = rbp-0x78
    # arg3 R8  = rbp+0x80 context
    # arg4 R9  = rsp+0x48 zero-initialized vector
    # arg5     = rbp+0x40 error/output string
    require_instruction(ins_by_rva, 0x1C8B8, "call")
    require_instruction(ins_by_rva, 0x1C8BE, "mov", "[rsp + 0x38], 1")
    require_instruction(ins_by_rva, 0x1C8C6, "lea", "[rbp + 0x40]")
    require_instruction(ins_by_rva, 0x1C8CA, "mov", "[rsp + 0x20], rcx")
    require_instruction(ins_by_rva, 0x1C8CF, "lea", "r9, [rsp + 0x48]")
    require_instruction(ins_by_rva, 0x1C8D4, "lea", "r8, [rbp + 0x80]")
    require_instruction(ins_by_rva, 0x1C8DB, "lea", "rdx, [rbp - 0x78]")
    require_instruction(ins_by_rva, 0x1C8DF, "mov", "rcx, rax")
    opaque_call = require_instruction(ins_by_rva, 0x1C8E2, "call")
    if call_target(opaque_call) != base + OPAQUE_CONSUMER[0]:
        raise InspectError("opaque envelope-consumer target drifted")
    require_instruction(ins_by_rva, 0x1C8E7, "test", "al, al")

    # On the success route the package call receives the exact same objects:
    # arg2 RDX = rbp+0x80 context (same as opaque arg3)
    # arg3 R8  = rsp+0x48 vector (same as opaque arg4)
    # arg5     = rbp+0x40 error string (same as opaque arg5)
    require_instruction(ins_by_rva, 0x1CC1D, "lea", "[rbp + 0x40]")
    require_instruction(ins_by_rva, 0x1CC21, "mov", "[rsp + 0x20], rax")
    require_instruction(ins_by_rva, 0x1CC26, "lea", "r9, [rbp - 0x60]")
    require_instruction(ins_by_rva, 0x1CC2A, "lea", "r8, [rsp + 0x48]")
    require_instruction(ins_by_rva, 0x1CC2F, "lea", "rdx, [rbp + 0x80]")
    require_instruction(ins_by_rva, 0x1CC36, "lea", "rcx, [rbp - 0x20]")
    package_call = require_instruction(ins_by_rva, 0x1CC3A, "call")
    if call_target(package_call) != base + PACKAGE_FUNCTION[0]:
        raise InspectError("package-function target drifted")
    require_instruction(ins_by_rva, 0x1CC3F, "test", "al, al")

    # Package function saves original arg3 (R8) then supplies it as AES key.
    require_instruction(ins_by_rva, 0x3D28E, "mov", "[rbp - 0x58], r8")
    require_instruction(ins_by_rva, 0x3DA46, "mov", "[rbp - 0x58]")
    aes_call = require_instruction(ins_by_rva, 0x3DA4A, "call")
    if call_target(aes_call) != base + AES_FUNCTION[0]:
        raise InspectError("package AES helper target drifted")

    # AES helper enforces 32-byte key length.
    require_instruction(ins_by_rva, 0x3CB93, "cmp", "0x20")

    return {
        "findingId": "LWB-R8-006",
        "date": "2026-09-24",
        "scope": "opaque package-key.envelope consumer output ownership into package AES key",
        "evidenceLabel": "RECOVERED",
        "source": {
            "path": str(path),
            "sha256": host_hash,
            "secureProxySha256": proxy_hash,
            "secureProxyHostRva": f"0x{HOST_SECURE_PROXY_RVA:X}",
            "secureProxyHostRawOffset": f"0x{proxy_raw:X}",
        },
        "callerDataflow": {
            "loaderRange": {
                "beginRva": f"0x{LOADER[0]:X}",
                "endRva": f"0x{LOADER[1]:X}",
            },
            "opaqueConsumer": {
                "beginRva": f"0x{OPAQUE_CONSUMER[0]:X}",
                "endRva": f"0x{OPAQUE_CONSUMER[1]:X}",
                "bodyInspected": False,
                "callRva": "0x1C8E2",
                "successTestRva": "0x1C8E7",
                "argumentOwnershipFromCaller": {
                    "arg1_rcx": "package-key.envelope path",
                    "arg2_rdx": "caller context at rbp-0x78; semantics not claimed",
                    "arg3_r8": "context object at rbp+0x80; later reused as package arg2",
                    "arg4_r9": "zero-initialized vector at rsp+0x48; later reused as package arg3/AES key",
                    "arg5_stack": "error/output string at rbp+0x40; later reused as package arg5",
                    "arg8_stack": 1,
                },
            },
            "packageCall": {
                "callRva": "0x1CC3A",
                "functionRva": "0x3D260",
                "sameContextPointer": "rbp+0x80: opaque arg3 -> package arg2",
                "sameKeyVectorPointer": "rsp+0x48: opaque arg4 -> package arg3",
                "sameErrorPointer": "rbp+0x40: opaque arg5 -> package arg5",
            },
        },
        "packageKey": {
            "producerBoundary": "successful opaque package-key.envelope consumer call at RVA 0x1C8E2",
            "consumerBoundary": "package function third argument at RVA 0x1CC3A",
            "aesKeyForwarding": "package arg3 is saved from R8 and forwarded as AES helper RCX at RVA 0x3DA4A",
            "requiredBytes": 32,
        },
        "result": [
            "The package-key.envelope consumer receives rsp+0x48 as its fourth argument after that vector is explicitly zero-initialized.",
            "After the consumer reports success, the enclosing loader passes the exact same rsp+0x48 address as the third argument to package function RVA 0x3D260.",
            "The package function forwards its original third argument to AES-GCM helper RVA 0x3CB60 as the key vector, and that helper requires a 32-byte key.",
            "Therefore the opaque package-key.envelope consumer is the producer boundary for the exact 32-byte package key used to decrypt bridge-scripts.dat.",
            "The same caller also carries rbp+0x80 from opaque arg3 directly into package arg2, preserving a shared context object across envelope and package validation; its internal field semantics are not inferred here.",
        ],
        "reproduction": [
            "python tools\\inspect_lwbridge_proxy_envelope_key_flow.py ..\\LW\\lwbridge-0.3.1.exe",
            "python tools\\inspect_lwbridge_proxy_envelope_key_flow.py ..\\LW\\lwbridge-0.3.1.exe --output evidence\\lwbridge-implementation\\2026-09-24-r8-006-envelope-key-output-flow.json",
            "python -m py_compile tools\\inspect_lwbridge_proxy_envelope_key_flow.py",
        ],
        "limits": {
            "staticOnly": True,
            "restrictedConsumerBodyInspected": False,
            "notProven": [
                "decoded LWKE1 field semantics",
                "peer public-key field ownership",
                "envelope nonce/tag/ciphertext layout",
                "internal ECDH/decrypt ordering inside the opaque consumer",
                "actual 32-byte package-key value",
                "bridge-scripts.dat plaintext",
            ],
            "restrictionPreserved": "The verifier never disassembles or reads instructions from secure-proxy RVA 0x3F8E0-0x40A6D; only the permitted enclosing caller and already recovered package/AES helpers are decoded.",
        },
        "implementationImpact": {
            "publicBehaviorEnabled": False,
            "closedGap": "exact opaque-consumer output pointer that becomes the 32-byte package AES key",
            "next": "obtain/recover the original LWKE1 envelope fields through independent evidence, then reproduce the already bounded key derivation without inspecting SB-79",
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
