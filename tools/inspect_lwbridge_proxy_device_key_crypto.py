#!/usr/bin/env python3
"""Verify the recovered LWBridge proxy device-key and crypto contract.

This build-specific inspector is read-only. It hash-gates lwbridge-0.3.1.exe,
extracts the embedded secure xLua proxy, validates the exact provider/key/export
and crypto literals, and checks the bounded instruction sites recovered in
LWB-R6-043. It does not decrypt bridge-scripts.dat, read a persisted private
key, execute LWBridge/Last War, or infer protected handler semantics.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

import pefile
import capstone
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP


EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
PROXY_OFFSET = 0x987374
PROXY_SIZE = 612_352
PROXY_SHA256 = "481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400"
EXPECTED_IMAGE_BASE = 0x180000000

RAW_LITERALS = {
    "ECCPUBLICBLOB": (0x708A0, "utf-16le"),
    "AES": (0x72DA8, "utf-16le"),
    "ChainingModeGCM": (0x72DB0, "utf-16le"),
    "ChainingMode": (0x72DD0, "utf-16le"),
    "device key missing": (0x72DF0, "ascii"),
    "Microsoft Software Key Storage Provider": (0x72E10, "utf-16le"),
    "key envelope agreement failed": (0x72E60, "ascii"),
    "TRUNCATE": (0x72E80, "utf-16le"),
    "device key unavailable": (0x72EB0, "ascii"),
    "device key mismatch": (0x72EC8, "ascii"),
    "key envelope decrypt failed": (0x72EE0, "ascii"),
    "{2D337A4D-7E6C-49EF-9486-54F0A00D8A41}": (0x73090, "utf-16le"),
    "DEVICE_KEY_PROVIDER": (0x730E0, "ascii"),
    "DEVICE_KEY_EXPORT": (0x730F8, "ascii"),
    "DEVICE_KEY_FORMAT": (0x73110, "ascii"),
    "DEVICE_KEY_MISSING": (0x73128, "ascii"),
}

FUNCTIONS = {
    "aes_gcm_decrypt": (0x3CB60, 0x3CFEA),
    "agreement_derive": (0x3DD00, 0x3E01E),
    "export_device_public_key": (0x430C0, 0x432BE),
    "open_persisted_device_key": (0x432C0, 0x43380),
}

IMPORT_THUNKS = {
    "BCryptOpenAlgorithmProvider": 0x45074,
    "BCryptSetProperty": 0x450B6,
    "BCryptGenerateSymmetricKey": 0x450BC,
    "BCryptDecrypt": 0x450C2,
    "NCryptOpenStorageProvider": 0x450C8,
    "NCryptImportKey": 0x450CE,
    "NCryptSecretAgreement": 0x450DA,
    "NCryptDeriveKey": 0x450E0,
    "NCryptOpenKey": 0x450E6,
    "NCryptExportKey": 0x450EC,
}


class InspectError(ValueError):
    pass


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _decode_instructions(pe: pefile.PE, data: bytes, begin_rva: int, end_rva: int):
    offset = pe.get_offset_from_rva(begin_rva)
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    return list(md.disasm(data[offset : offset + (end_rva - begin_rva)], pe.OPTIONAL_HEADER.ImageBase + begin_rva))


def _rip_target(ins) -> int | None:
    for op in ins.operands:
        if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
            return ins.address + ins.size + op.mem.disp
    return None


def _call_target(ins) -> int | None:
    if ins.mnemonic != "call" or not ins.operands:
        return None
    op = ins.operands[0]
    return op.imm if op.type == X86_OP_IMM else None


def _require_instruction(ins_by_rva: dict[int, Any], rva: int, mnemonic: str, contains: str | None = None):
    ins = ins_by_rva.get(rva)
    if ins is None:
        raise InspectError(f"missing instruction at RVA 0x{rva:X}")
    if ins.mnemonic != mnemonic:
        raise InspectError(f"RVA 0x{rva:X} mnemonic {ins.mnemonic!r}; expected {mnemonic!r}")
    if contains is not None and contains not in ins.op_str:
        raise InspectError(f"RVA 0x{rva:X} operands {ins.op_str!r}; expected fragment {contains!r}")
    return ins


def inspect(path: Path) -> dict[str, Any]:
    host = path.read_bytes()
    digest = _sha256(host)
    if digest != EXPECTED_SHA256:
        raise InspectError(f"unsupported lwbridge SHA-256 {digest}; expected {EXPECTED_SHA256}")

    proxy = host[PROXY_OFFSET : PROXY_OFFSET + PROXY_SIZE]
    proxy_digest = _sha256(proxy)
    if proxy_digest != PROXY_SHA256:
        raise InspectError(f"embedded secure proxy SHA-256 {proxy_digest}; expected {PROXY_SHA256}")

    pe = pefile.PE(data=proxy, fast_load=False)
    base = pe.OPTIONAL_HEADER.ImageBase
    if base != EXPECTED_IMAGE_BASE:
        raise InspectError(f"secure proxy image base 0x{base:X}; expected 0x{EXPECTED_IMAGE_BASE:X}")

    literal_rows: dict[str, dict[str, str]] = {}
    literal_vas: dict[str, int] = {}
    for value, (raw_offset, encoding) in RAW_LITERALS.items():
        encoded = value.encode(encoding)
        if proxy[raw_offset : raw_offset + len(encoded)] != encoded:
            raise InspectError(f"literal {value!r} missing at raw offset 0x{raw_offset:X}")
        rva = pe.get_rva_from_offset(raw_offset)
        va = base + rva
        literal_vas[value] = va
        literal_rows[value] = {
            "proxyRawOffset": f"0x{raw_offset:X}",
            "proxyRva": f"0x{rva:X}",
            "proxyVa": f"0x{va:X}",
            "encoding": encoding,
        }

    decoded: dict[str, list[Any]] = {}
    ins_by_rva: dict[int, Any] = {}
    for name, (begin_rva, end_rva) in FUNCTIONS.items():
        rows = _decode_instructions(pe, proxy, begin_rva, end_rva)
        decoded[name] = rows
        for ins in rows:
            ins_by_rva[ins.address - base] = ins

    # Persisted device-key open path.
    if _rip_target(_require_instruction(ins_by_rva, 0x432E7, "lea")) != literal_vas["Microsoft Software Key Storage Provider"]:
        raise InspectError("open_persisted_device_key provider reference drifted")
    if _call_target(_require_instruction(ins_by_rva, 0x432F3, "call")) != base + IMPORT_THUNKS["NCryptOpenStorageProvider"]:
        raise InspectError("NCryptOpenStorageProvider call target drifted")
    if _rip_target(_require_instruction(ins_by_rva, 0x432FC, "lea")) != literal_vas["DEVICE_KEY_PROVIDER"]:
        raise InspectError("DEVICE_KEY_PROVIDER diagnostic branch drifted")
    if _rip_target(_require_instruction(ins_by_rva, 0x4332D, "lea")) != literal_vas["{2D337A4D-7E6C-49EF-9486-54F0A00D8A41}"]:
        raise InspectError("persisted device-key name reference drifted")
    if _call_target(_require_instruction(ins_by_rva, 0x4333C, "call")) != base + IMPORT_THUNKS["NCryptOpenKey"]:
        raise InspectError("NCryptOpenKey call target drifted")
    if _rip_target(_require_instruction(ins_by_rva, 0x43345, "lea")) != literal_vas["DEVICE_KEY_MISSING"]:
        raise InspectError("DEVICE_KEY_MISSING diagnostic branch drifted")

    # Public-key export and exact accepted format.
    if _rip_target(_require_instruction(ins_by_rva, 0x430FE, "lea")) != literal_vas["ECCPUBLICBLOB"]:
        raise InspectError("first ECCPUBLICBLOB reference drifted")
    if _call_target(_require_instruction(ins_by_rva, 0x43107, "call")) != base + IMPORT_THUNKS["NCryptExportKey"]:
        raise InspectError("first NCryptExportKey call target drifted")
    if _rip_target(_require_instruction(ins_by_rva, 0x431BA, "lea")) != literal_vas["ECCPUBLICBLOB"]:
        raise InspectError("second ECCPUBLICBLOB reference drifted")
    if _call_target(_require_instruction(ins_by_rva, 0x431C6, "call")) != base + IMPORT_THUNKS["NCryptExportKey"]:
        raise InspectError("second NCryptExportKey call target drifted")
    _require_instruction(ins_by_rva, 0x431D8, "cmp", "0x314b4345")
    _require_instruction(ins_by_rva, 0x431E0, "cmp", "0x20")
    _require_instruction(ins_by_rva, 0x431E6, "cmp", "0x48")
    _require_instruction(ins_by_rva, 0x431F5, "mov", "0x41")
    _require_instruction(ins_by_rva, 0x43205, "mov", "byte ptr [rax], 4")
    if _rip_target(_require_instruction(ins_by_rva, 0x431CF, "lea")) != literal_vas["DEVICE_KEY_EXPORT"]:
        raise InspectError("DEVICE_KEY_EXPORT diagnostic branch drifted")
    if _rip_target(_require_instruction(ins_by_rva, 0x43230, "lea")) != literal_vas["DEVICE_KEY_FORMAT"]:
        raise InspectError("DEVICE_KEY_FORMAT diagnostic branch drifted")

    # Device-key agreement and 32-byte TRUNCATE derivation.
    _require_instruction(ins_by_rva, 0x3DD3D, "cmp", "0x41")
    _require_instruction(ins_by_rva, 0x3DD47, "cmp", "byte ptr [rdx], 4")
    if _call_target(_require_instruction(ins_by_rva, 0x3DD76, "call")) != base + FUNCTIONS["open_persisted_device_key"][0]:
        raise InspectError("device-key open helper call target drifted")
    _require_instruction(ins_by_rva, 0x3DDDD, "mov", "0x314b4345")
    _require_instruction(ins_by_rva, 0x3DDE3, "mov", "0x20")
    if _rip_target(_require_instruction(ins_by_rva, 0x3DE11, "lea")) != literal_vas["Microsoft Software Key Storage Provider"]:
        raise InspectError("agreement provider reference drifted")
    if _call_target(_require_instruction(ins_by_rva, 0x3DE1C, "call")) != base + IMPORT_THUNKS["NCryptOpenStorageProvider"]:
        raise InspectError("agreement NCryptOpenStorageProvider target drifted")
    if _rip_target(_require_instruction(ins_by_rva, 0x3DE47, "lea")) != literal_vas["ECCPUBLICBLOB"]:
        raise InspectError("agreement ECCPUBLICBLOB reference drifted")
    if _call_target(_require_instruction(ins_by_rva, 0x3DE54, "call")) != base + IMPORT_THUNKS["NCryptImportKey"]:
        raise InspectError("agreement NCryptImportKey target drifted")
    if _call_target(_require_instruction(ins_by_rva, 0x3DE6F, "call")) != base + IMPORT_THUNKS["NCryptSecretAgreement"]:
        raise InspectError("agreement NCryptSecretAgreement target drifted")
    for rva in (0x3DE99, 0x3DEDB):
        if _rip_target(_require_instruction(ins_by_rva, rva, "lea")) != literal_vas["TRUNCATE"]:
            raise InspectError(f"TRUNCATE reference drifted at RVA 0x{rva:X}")
    for rva in (0x3DEA4, 0x3DEE6):
        if _call_target(_require_instruction(ins_by_rva, rva, "call")) != base + IMPORT_THUNKS["NCryptDeriveKey"]:
            raise InspectError(f"NCryptDeriveKey target drifted at RVA 0x{rva:X}")
    _require_instruction(ins_by_rva, 0x3DEAD, "cmp", "0x20")
    _require_instruction(ins_by_rva, 0x3DEEF, "cmp", "0x20")

    # Symmetric decrypt input shape and AES-GCM configuration.
    _require_instruction(ins_by_rva, 0x3CB93, "cmp", "0x20")
    _require_instruction(ins_by_rva, 0x3CBA4, "cmp", "0xc")
    _require_instruction(ins_by_rva, 0x3CBB5, "cmp", "0x10")
    if _rip_target(_require_instruction(ins_by_rva, 0x3CBF9, "lea")) != literal_vas["AES"]:
        raise InspectError("AES algorithm reference drifted")
    if _call_target(_require_instruction(ins_by_rva, 0x3CC05, "call")) != base + IMPORT_THUNKS["BCryptOpenAlgorithmProvider"]:
        raise InspectError("BCryptOpenAlgorithmProvider target drifted")
    if _rip_target(_require_instruction(ins_by_rva, 0x3CC1C, "lea")) != literal_vas["ChainingModeGCM"]:
        raise InspectError("ChainingModeGCM reference drifted")
    if _rip_target(_require_instruction(ins_by_rva, 0x3CC23, "lea")) != literal_vas["ChainingMode"]:
        raise InspectError("ChainingMode property reference drifted")
    if _call_target(_require_instruction(ins_by_rva, 0x3CC2F, "call")) != base + IMPORT_THUNKS["BCryptSetProperty"]:
        raise InspectError("BCryptSetProperty target drifted")
    if _call_target(_require_instruction(ins_by_rva, 0x3CD18, "call")) != base + IMPORT_THUNKS["BCryptGenerateSymmetricKey"]:
        raise InspectError("BCryptGenerateSymmetricKey target drifted")
    if _call_target(_require_instruction(ins_by_rva, 0x3CDFB, "call")) != base + IMPORT_THUNKS["BCryptDecrypt"]:
        raise InspectError("BCryptDecrypt target drifted")

    return {
        "findingId": "LWB-R6-043",
        "date": "2026-09-09",
        "scope": "PM7-B secure-proxy device-key identity, public-key format, KDF and AES-GCM contract",
        "evidenceLabel": "RECOVERED",
        "source": {
            "path": str(path),
            "sha256": digest,
            "embeddedProxy": "xlua-proxy-secure.dll",
            "embeddedProxySha256": proxy_digest,
            "embeddedProxyHostRawOffset": f"0x{PROXY_OFFSET:X}",
            "coordinateSystem": "secure-proxy preferred-image RVAs/VAs plus raw offsets",
        },
        "tools": {
            "python": "3.12.10",
            "pefile": pefile.__version__,
            "capstone": capstone.__version__,
        },
        "functions": {
            name: {"beginRva": f"0x{begin:X}", "endRva": f"0x{end:X}"}
            for name, (begin, end) in FUNCTIONS.items()
        },
        "literals": literal_rows,
        "result": [
            "DEVICE_KEY_PROVIDER, DEVICE_KEY_EXPORT and DEVICE_KEY_FORMAT are diagnostic labels on concrete failure/validation branches, not runtime configuration-variable names.",
            "The secure proxy opens Microsoft Software Key Storage Provider and then opens persisted key {2D337A4D-7E6C-49EF-9486-54F0A00D8A41}; provider-open failure maps to DEVICE_KEY_PROVIDER and key-open failure maps to DEVICE_KEY_MISSING.",
            "The proxy exports ECCPUBLICBLOB, requires magic ECK1 (0x314B4345), cbKey=32 and total blob size 72 bytes, then emits a 65-byte public-key representation beginning with 0x04 followed by the 64 coordinate bytes. Export failure maps to DEVICE_KEY_EXPORT and structural mismatch maps to DEVICE_KEY_FORMAT.",
            "The agreement path accepts only the same 65-byte 0x04-prefixed public-key form, reconstructs an ECK1/32-byte ECCPUBLICBLOB, imports it, performs NCryptSecretAgreement, and derives exactly 32 bytes using NCryptDeriveKey with KDF name TRUNCATE.",
            "The symmetric decrypt helper requires a 32-byte key, 12-byte nonce and 16-byte authentication tag, opens AES, sets ChainingMode to ChainingModeGCM, creates the symmetric key and calls BCryptDecrypt.",
        ],
        "reproduction": [
            "python tools\\inspect_lwbridge_proxy_device_key_crypto.py ..\\LW\\lwbridge-0.3.1.exe",
            "python tools\\inspect_lwbridge_proxy_device_key_crypto.py ..\\LW\\lwbridge-0.3.1.exe --output evidence\\lwbridge-implementation\\2026-09-09-r6-proxy-device-key-crypto.json",
            "python -m py_compile tools\\inspect_lwbridge_proxy_device_key_crypto.py",
        ],
        "limits": {
            "staticOnly": True,
            "secureProxyOnly": True,
            "notProven": [
                "the host instruction-level provisioning path that creates/finalizes the persisted key",
                "whether the plain proxy uses instruction-identical code for these helpers",
                "the exact package-key.envelope open/read caller and envelope field layout",
                "which recovered symmetric-decrypt callsite consumes the package key versus the protected script package",
                "bridge-scripts.dat plaintext and getWorldMapState/getCurrentServerId protected handler implementation",
                "authoritative transport/readiness error propagation and live current-client behavior",
            ],
            "interpretationRule": "Only direct literals, bounded function control flow and imported-call targets in the hash-locked secure proxy are treated as recovered. Caller ownership outside these functions remains open.",
        },
        "implementationImpact": {
            "publicBehaviorEnabled": False,
            "supersedesR6042Interpretation": "DEVICE_KEY_PROVIDER/EXPORT/FORMAT are diagnostic labels rather than configuration-variable names",
            "pm7B": "device-key provider/name/export/KDF/AES-GCM prerequisites recovered for the secure proxy; continue with package-key envelope open/read caller and protected handler/readiness linkage",
            "mapSummary": "remains fail-closed",
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
