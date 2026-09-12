#!/usr/bin/env python3
"""Verify the secure-proxy package validation/AES boundary for LWBridge 0.3.1.

This build-specific inspector is read-only. It hash-gates lwbridge-0.3.1.exe,
extracts the embedded secure xLua proxy, verifies one unwind-delimited package
validation/decrypt function and its direct call into the already recovered
AES-GCM helper, and pins the package diagnostics referenced by that function.
It does not locate or parse package-key.envelope, identify the file caller of
the package function, decrypt bridge-scripts.dat, or execute LWBridge/Last War.
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
PROXY_OFFSET = 0x987374
PROXY_SIZE = 612_352
PROXY_SHA256 = "481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400"
EXPECTED_IMAGE_BASE = 0x180000000

PACKAGE_FUNCTION = (0x3D260, 0x3DCF5)
AES_GCM_HELPER = 0x3CB60

RAW_LITERALS = {
    "package integrity invalid": (0x72F08, "ascii"),
    "package build mismatch": (0x72F28, "ascii"),
    "LWBP2|": (0x72F40, "ascii"),
}

EXPECTED_LITERAL_REFS = {
    0x3D53F: "package integrity invalid",
    0x3D65B: "package build mismatch",
    0x3DA00: "LWBP2|",
    0x3DBAD: "package integrity invalid",
    0x3DC17: "package build mismatch",
    0x3DC81: "package integrity invalid",
}


class InspectError(ValueError):
    pass


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


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


def _instructions(pe: pefile.PE, data: bytes, begin_rva: int, end_rva: int):
    offset = pe.get_offset_from_rva(begin_rva)
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    return list(
        md.disasm(
            data[offset : offset + (end_rva - begin_rva)],
            pe.OPTIONAL_HEADER.ImageBase + begin_rva,
        )
    )


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

    pe.parse_data_directories(
        directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]]
    )
    unwind_ranges = {
        (entry.struct.BeginAddress, entry.struct.EndAddress)
        for entry in pe.DIRECTORY_ENTRY_EXCEPTION
    }
    if PACKAGE_FUNCTION not in unwind_ranges:
        raise InspectError(
            "package function unwind range drifted: expected "
            f"0x{PACKAGE_FUNCTION[0]:X}-0x{PACKAGE_FUNCTION[1]:X}"
        )

    literal_vas: dict[str, int] = {}
    literal_rows: dict[str, dict[str, str]] = {}
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

    begin_rva, end_rva = PACKAGE_FUNCTION
    rows = _instructions(pe, proxy, begin_rva, end_rva)
    by_rva = {ins.address - base: ins for ins in rows}

    reference_rows: list[dict[str, str]] = []
    for rva, literal in EXPECTED_LITERAL_REFS.items():
        ins = by_rva.get(rva)
        if ins is None:
            raise InspectError(f"missing package instruction at RVA 0x{rva:X}")
        target = _rip_target(ins)
        if target != literal_vas[literal]:
            raise InspectError(
                f"RVA 0x{rva:X} target {target!r}; expected {literal!r} at 0x{literal_vas[literal]:X}"
            )
        reference_rows.append(
            {
                "instructionRva": f"0x{rva:X}",
                "mnemonic": ins.mnemonic,
                "operands": ins.op_str,
                "literal": literal,
            }
        )

    aes_call_rva = 0x3DA4A
    aes_call = by_rva.get(aes_call_rva)
    if aes_call is None:
        raise InspectError(f"missing AES helper call at RVA 0x{aes_call_rva:X}")
    if _call_target(aes_call) != base + AES_GCM_HELPER:
        raise InspectError(
            f"RVA 0x{aes_call_rva:X} call target drifted; expected 0x{base + AES_GCM_HELPER:X}"
        )

    return {
        "findingId": "LWB-R6-045",
        "date": "2026-09-09",
        "scope": "PM7-B secure-proxy package validation/decrypt to AES-GCM boundary",
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
        "packageFunction": {
            "beginRva": f"0x{begin_rva:X}",
            "endRva": f"0x{end_rva:X}",
            "unwindDelimited": True,
            "aesGcmCallRva": f"0x{aes_call_rva:X}",
            "aesGcmTargetRva": f"0x{AES_GCM_HELPER:X}",
            "literalReferences": reference_rows,
        },
        "literals": literal_rows,
        "result": [
            "Secure-proxy unwind function RVA 0x3D260-0x3DCF5 directly calls the recovered AES-GCM helper RVA 0x3CB60 at callsite RVA 0x3DA4A.",
            "The same function directly references the exact package marker LWBP2| plus package integrity invalid and package build mismatch diagnostics at the verified instruction RVAs.",
            "This establishes a package validation/decrypt consumer of the R6-043 AES-GCM primitive and separates that package-side boundary from the still-unrecovered package-key.envelope reader/parser and agreement caller.",
        ],
        "reproduction": [
            "python tools\\inspect_lwbridge_proxy_package_crypto_boundary.py ..\\LW\\lwbridge-0.3.1.exe",
            "python tools\\inspect_lwbridge_proxy_package_crypto_boundary.py ..\\LW\\lwbridge-0.3.1.exe --output evidence\\lwbridge-implementation\\2026-09-09-r6-proxy-package-crypto-boundary.json",
            "python -m py_compile tools\\inspect_lwbridge_proxy_package_crypto_boundary.py",
        ],
        "limits": {
            "staticOnly": True,
            "secureProxyOnly": True,
            "notProven": [
                "the file/path caller that supplies bytes to package function RVA 0x3D260-0x3DCF5",
                "whether that caller is bridge-scripts.dat or another protected package",
                "the package-key.envelope open/read function, grammar and parsed field ownership",
                "the caller that links package-key.envelope fields into agreement helper RVA 0x3DD00",
                "which exact values at the package callsite provide the AES-GCM key, nonce, tag and ciphertext",
                "plain-proxy instruction parity",
                "protected getWorldMapState/getCurrentServerId handler plaintext and authoritative readiness/error mapping",
                "live current-client behavior",
            ],
            "interpretationRule": "Only the hash-locked unwind boundary, exact RIP-relative package diagnostics/marker and direct AES helper call are treated as recovered. No enclosing file-loader or envelope ownership is inferred from function adjacency.",
        },
        "implementationImpact": {
            "publicBehaviorEnabled": False,
            "closedGap": "identifies a package validation/decrypt consumer of the recovered AES-GCM helper",
            "pm7B": "continue from the independent package-key.envelope open/read + agreement caller seam; package-side AES consumption is now source-attributed",
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
