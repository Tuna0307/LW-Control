#!/usr/bin/env python3
"""Verify LWBridge shared scan-state server/source fallback branches.

This build-specific inspector is read-only and hash-gated. It checks the
already-recovered shared scan-state producer in lwbridge-0.3.1.exe and pins
the exact current/home/state server tests, isInWorld/isReading boolean guards,
the two serverIdSource="live" writes, and the serverIdSource="none" fallback.
It does not inspect protected bridge-script plaintext or discover new xrefs.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import struct
from typing import Any

import capstone
import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP


EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
EXPECTED_IMAGE_BASE = 0x140000000
PRODUCER_BEGIN_RVA = 0x0F7F47
PRODUCER_END_RVA = 0x0F9333

HELPERS = {
    "extract_signed": 0x345030,
    "lookup_value": 0x5A2051,
    "insert_value": 0x5A48D3,
    "string_value": 0x2ADBC0,
    "current_server_changed_error": 0x33FBE8,
}


class InspectError(ValueError):
    pass


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _decode(pe: pefile.PE, data: bytes) -> tuple[dict[int, Any], list[Any]]:
    offset = pe.get_offset_from_rva(PRODUCER_BEGIN_RVA)
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    rows = list(
        md.disasm(
            data[offset : offset + (PRODUCER_END_RVA - PRODUCER_BEGIN_RVA)],
            pe.OPTIONAL_HEADER.ImageBase + PRODUCER_BEGIN_RVA,
        )
    )
    return {ins.address - pe.OPTIONAL_HEADER.ImageBase: ins for ins in rows}, rows


def _require(ins_by_rva: dict[int, Any], rva: int, mnemonic: str, contains: str | None = None):
    ins = ins_by_rva.get(rva)
    if ins is None:
        raise InspectError(f"missing instruction at RVA 0x{rva:X}")
    if ins.mnemonic != mnemonic:
        raise InspectError(f"RVA 0x{rva:X} mnemonic {ins.mnemonic!r}; expected {mnemonic!r}")
    if contains is not None and contains not in ins.op_str:
        raise InspectError(
            f"RVA 0x{rva:X} operands {ins.op_str!r}; expected fragment {contains!r}"
        )
    return ins


def _direct_target(ins) -> int | None:
    if not ins.operands:
        return None
    op = ins.operands[-1] if ins.mnemonic.startswith("j") else ins.operands[0]
    return int(op.imm) if op.type == X86_OP_IMM else None


def _rip_target(ins) -> int | None:
    for op in ins.operands:
        if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
            return ins.address + ins.size + op.mem.disp
    return None


def _ascii_at_va(pe: pefile.PE, data: bytes, va: int, length: int) -> str | None:
    rva = va - pe.OPTIONAL_HEADER.ImageBase
    offset = pe.get_offset_from_rva(rva)
    raw = data[offset : offset + length]
    try:
        return raw.decode("ascii")
    except UnicodeDecodeError:
        return None


def _literal_text(pe: pefile.PE, data: bytes, va: int, minimum_length: int) -> str | None:
    direct = _ascii_at_va(pe, data, va, minimum_length)
    if direct is not None and all(0x20 <= ord(ch) <= 0x7E for ch in direct):
        return direct
    rva = va - pe.OPTIONAL_HEADER.ImageBase
    offset = pe.get_offset_from_rva(rva)
    if offset + 16 > len(data):
        return None
    ptr, length = struct.unpack_from("<QQ", data, offset)
    if length < minimum_length or length > 512:
        return None
    text = _ascii_at_va(pe, data, ptr, minimum_length)
    if text is None or not all(0x20 <= ord(ch) <= 0x7E for ch in text):
        return None
    return text


def _require_literal(pe: pefile.PE, data: bytes, ins, text: str) -> None:
    target = _rip_target(ins)
    if target is None:
        raise InspectError(f"instruction at 0x{ins.address:X} has no RIP-relative target")
    actual = _literal_text(pe, data, target, len(text))
    if actual != text:
        raise InspectError(
            f"instruction at 0x{ins.address:X} targets {actual!r}; expected {text!r}"
        )


def _require_call(ins_by_rva: dict[int, Any], rva: int, helper_rva: int) -> None:
    ins = _require(ins_by_rva, rva, "call")
    expected = EXPECTED_IMAGE_BASE + helper_rva
    if _direct_target(ins) != expected:
        raise InspectError(
            f"call at RVA 0x{rva:X} targets {_direct_target(ins)!r}; expected 0x{expected:X}"
        )


def _require_jump(ins_by_rva: dict[int, Any], rva: int, mnemonic: str, target_rva: int) -> None:
    ins = _require(ins_by_rva, rva, mnemonic)
    expected = EXPECTED_IMAGE_BASE + target_rva
    if _direct_target(ins) != expected:
        raise InspectError(
            f"branch at RVA 0x{rva:X} targets {_direct_target(ins)!r}; expected 0x{expected:X}"
        )


def inspect(path: Path) -> dict[str, Any]:
    data = path.read_bytes()
    digest = _sha256(data)
    if digest != EXPECTED_SHA256:
        raise InspectError(f"unsupported lwbridge SHA-256 {digest}; expected {EXPECTED_SHA256}")

    pe = pefile.PE(data=data, fast_load=False)
    if pe.OPTIONAL_HEADER.ImageBase != EXPECTED_IMAGE_BASE:
        raise InspectError(
            f"image base 0x{pe.OPTIONAL_HEADER.ImageBase:X}; expected 0x{EXPECTED_IMAGE_BASE:X}"
        )
    pe.parse_data_directories(
        directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]]
    )
    ranges = {
        (int(entry.struct.BeginAddress), int(entry.struct.EndAddress))
        for entry in getattr(pe, "DIRECTORY_ENTRY_EXCEPTION", [])
    }
    if (PRODUCER_BEGIN_RVA, PRODUCER_END_RVA) not in ranges:
        raise InspectError("shared scan-state producer runtime-function range drifted")

    ins_by_rva, _ = _decode(pe, data)

    # Current-server ownership and world-state boolean normalization input.
    _require_literal(pe, data, _require(ins_by_rva, 0x0F81BE, "lea"), "serverId")
    _require_call(ins_by_rva, 0x0F81D3, HELPERS["extract_signed"])
    _require(ins_by_rva, 0x0F81D8, "mov", "r13, rax")
    _require_literal(pe, data, _require(ins_by_rva, 0x0F8225, "lea"), "isInWorld")
    _require_call(ins_by_rva, 0x0F8232, HELPERS["lookup_value"])
    _require(ins_by_rva, 0x0F8240, "cmp", "byte ptr [rax], 1")
    _require(ins_by_rva, 0x0F8249, "mov", "bpl, byte ptr [rax + 1]")
    _require(ins_by_rva, 0x0F825A, "test", "bpl, bpl")
    _require(ins_by_rva, 0x0F825D, "sete", "al")
    _require(ins_by_rva, 0x0F8260, "mov", "dword ptr [rsp + 0xe8], eax")
    _require(ins_by_rva, 0x0F8328, "mov", "dword ptr [rsp + 0xe8], 0")

    # Positive home-server value is retained in r12 for the exact-false branch.
    _require_literal(pe, data, _require(ins_by_rva, 0x0F835F, "lea"), "homeServerId")
    _require_call(ins_by_rva, 0x0F8374, HELPERS["extract_signed"])
    _require(ins_by_rva, 0x0F8379, "mov", "r12, rax")
    _require(ins_by_rva, 0x0F837F, "mov", "qword ptr [rsp + 0x48], r13")
    _require(ins_by_rva, 0x0F87E5, "mov", "r13, qword ptr [rsp + 0x48]")

    # e8 is one only for an exact boolean false isInWorld; all other cases jump
    # to the current-server branch at F8978.
    _require(ins_by_rva, 0x0F87EE, "cmp", "byte ptr [rsp + 0xe8], 0")
    _require_jump(ins_by_rva, 0x0F87F6, "je", 0x0F8978)

    # Exact isInWorld=false fallback: use positive homeServerId only when the
    # state serverId is not already positive, then mark the source live.
    _require_literal(pe, data, _require(ins_by_rva, 0x0F87FC, "lea"), "serverId")
    _require_call(ins_by_rva, 0x0F880C, HELPERS["extract_signed"])
    _require(ins_by_rva, 0x0F8811, "test", "r12, r12")
    _require_jump(ins_by_rva, 0x0F8814, "jle", 0x0F9051)
    _require(ins_by_rva, 0x0F881A, "test", "rax, rax")
    _require_jump(ins_by_rva, 0x0F881D, "jg", 0x0F9051)
    _require(ins_by_rva, 0x0F8851, "mov", "qword ptr [r15 + 0x10], r12")
    _require_literal(pe, data, _require(ins_by_rva, 0x0F8855, "lea"), "live")
    _require_call(ins_by_rva, 0x0F8864, HELPERS["string_value"])
    _require_literal(pe, data, _require(ins_by_rva, 0x0F8891, "lea"), "serverIdSource")
    _require_call(ins_by_rva, 0x0F88A0, HELPERS["insert_value"])

    # Current-server branch. A positive current server replaces state serverId
    # and marks it live unless an active read reports a different positive
    # serverId, which constructs the exact current-server-changed error.
    _require(ins_by_rva, 0x0F8978, "test", "r13, r13")
    _require_jump(ins_by_rva, 0x0F897B, "jle", 0x0F89B2)
    _require_literal(pe, data, _require(ins_by_rva, 0x0F897D, "lea"), "serverId")
    _require_call(ins_by_rva, 0x0F898D, HELPERS["extract_signed"])
    _require(ins_by_rva, 0x0F8992, "mov", "r12, rax")
    _require_literal(pe, data, _require(ins_by_rva, 0x0F899E, "lea"), "isReading")
    _require_call(ins_by_rva, 0x0F89AB, HELPERS["lookup_value"])
    _require(ins_by_rva, 0x0F89E6, "cmp", "byte ptr [rax], 1")
    _require(ins_by_rva, 0x0F89EB, "cmp", "byte ptr [rax + 1], 0")
    _require(ins_by_rva, 0x0F89F1, "test", "r12, r12")
    _require(ins_by_rva, 0x0F89F4, "setg", "al")
    _require(ins_by_rva, 0x0F89F7, "cmp", "r12, r13")
    _require(ins_by_rva, 0x0F89FA, "setne", "cl")
    _require(ins_by_rva, 0x0F89FD, "test", "al, cl")
    _require_jump(ins_by_rva, 0x0F89FF, "je", 0x0F8A3F)
    _require_literal(
        pe,
        data,
        _require(ins_by_rva, 0x0F8A18, "lea"),
        "current server changed during map scan",
    )
    _require_call(ins_by_rva, 0x0F8A2A, HELPERS["current_server_changed_error"])
    _require(ins_by_rva, 0x0F8A76, "mov", "qword ptr [r12 + 0x10], r13")
    _require_literal(pe, data, _require(ins_by_rva, 0x0F8ACE, "lea"), "live")
    _require_call(ins_by_rva, 0x0F8ADD, HELPERS["string_value"])
    _require_literal(pe, data, _require(ins_by_rva, 0x0F8B07, "lea"), "serverIdSource")
    _require_call(ins_by_rva, 0x0F8B16, HELPERS["insert_value"])

    # No-current-server branch. Exact boolean true isReading preserves the
    # current state. Otherwise a positive state serverId is preserved; only the
    # remaining nonpositive case writes serverId=0 and source="none".
    _require_literal(pe, data, _require(ins_by_rva, 0x0F89BF, "lea"), "isReading")
    _require_call(ins_by_rva, 0x0F89CC, HELPERS["lookup_value"])
    _require(ins_by_rva, 0x0F8C53, "cmp", "byte ptr [rcx], 1")
    _require(ins_by_rva, 0x0F8C58, "cmp", "byte ptr [rcx + 1], 0")
    _require_jump(ins_by_rva, 0x0F8C5C, "jne", 0x0F9051)
    _require_literal(pe, data, _require(ins_by_rva, 0x0F8C62, "lea"), "serverId")
    _require_call(ins_by_rva, 0x0F8C72, HELPERS["extract_signed"])
    _require(ins_by_rva, 0x0F8C77, "test", "rax, rax")
    _require_jump(ins_by_rva, 0x0F8C7A, "jg", 0x0F9051)
    _require(ins_by_rva, 0x0F8CA2, "mov", "byte ptr [r15], 2")
    _require(ins_by_rva, 0x0F8CA6, "xorps", "xmm6, xmm6")
    _require(ins_by_rva, 0x0F8CA9, "movups", "xmmword ptr [r15 + 8], xmm6")
    _require_literal(pe, data, _require(ins_by_rva, 0x0F8CAE, "lea"), "none")
    _require_call(ins_by_rva, 0x0F8CBD, HELPERS["string_value"])
    _require_literal(pe, data, _require(ins_by_rva, 0x0F8CE6, "lea"), "serverIdSource")
    _require_call(ins_by_rva, 0x0F8CF5, HELPERS["insert_value"])
    _require_literal(pe, data, _require(ins_by_rva, 0x0F8D1E, "lea"), "tileWidth")
    _require_literal(pe, data, _require(ins_by_rva, 0x0F8D48, "lea"), "tileHeight")

    return {
        "findingId": "LWB-R6-044",
        "date": "2026-09-09",
        "scope": "PM7-B shared scan-state serverIdSource fallback and reading-mismatch branches",
        "evidenceLabel": "RECOVERED",
        "source": {
            "path": str(path),
            "sha256": digest,
            "imageBase": f"0x{EXPECTED_IMAGE_BASE:X}",
            "producerPreferredVaRange": [
                f"0x{EXPECTED_IMAGE_BASE + PRODUCER_BEGIN_RVA:X}",
                f"0x{EXPECTED_IMAGE_BASE + PRODUCER_END_RVA:X}",
            ],
            "coordinateSystem": "preferred-image virtual addresses/RVAs from the verified PE",
        },
        "tools": {
            "python": "3.12.10",
            "pefile": pefile.__version__,
            "capstone": capstone.__version__,
        },
        "result": [
            "The producer extracts getCurrentServerId.serverId into r13 and homeServerId into r12, and distinguishes an exact boolean false isInWorld from true/missing/non-boolean input before selecting its server fallback branch.",
            "On the exact isInWorld=false branch, a positive homeServerId replaces a nonpositive state serverId and the producer writes serverIdSource=live; an absent/nonpositive homeServerId or already-positive state serverId bypasses that fallback.",
            "On the current-server branch, a positive current serverId is written back with serverIdSource=live. If isReading is exactly boolean true and the existing state serverId is positive but differs from the current serverId, the producer constructs the exact error text current server changed during map scan instead of silently switching servers.",
            "When current serverId is nonpositive, exact boolean true isReading preserves the existing state; otherwise an already-positive state serverId is also preserved. Only the remaining nonpositive state-server case writes numeric serverId=0, serverIdSource=none, tileWidth=0 and tileHeight=0.",
        ],
        "locators": {
            "currentServerIdExtract": "0x1400F81BE-0x1400F81D8",
            "isInWorldLookupAndExplicitFalseFlag": "0x1400F8225-0x1400F8260",
            "homeServerIdExtract": "0x1400F835F-0x1400F8379",
            "exactFalseBranch": "0x1400F87EE-0x1400F88C4",
            "currentServerBranch": "0x1400F8978-0x1400F8B3C",
            "readingMismatchError": "0x1400F89E6-0x1400F8A2F",
            "noneFallback": "0x1400F89B2-0x1400F8D72",
        },
        "reproduction": [
            "python tools\\inspect_lwbridge_scan_state_source_selection.py ..\\LW\\lwbridge-0.3.1.exe",
            "python tools\\inspect_lwbridge_scan_state_source_selection.py ..\\LW\\lwbridge-0.3.1.exe --output evidence\\lwbridge-implementation\\2026-09-09-r6-scan-state-source-selection.json",
            "python tools\\inspect_lwbridge_map_scan.py ..\\LW\\lwbridge-0.3.1.exe --dump-rva 0x0F7F47",
            "python tools\\inspect_lwbridge_map_scan.py ..\\LW\\lwbridge-0.3.1.exe --dump-rva 0x151509",
            "python -m py_compile tools\\inspect_lwbridge_scan_state_source_selection.py",
        ],
        "limits": {
            "staticOnly": True,
            "publicBehaviorEnabled": False,
            "notProven": [
                "the protected getWorldMapState/getCurrentServerId handler implementations and their current-client managed linkage",
                "the common bridge dispatcher's exact failure/timeout error identities",
                "the exact code/condition that emits the separate map scan state is unavailable message",
                "the package-key.envelope open/read parser and parsed-field ownership into the recovered crypto helpers",
                "plain-proxy parity for the R6-043 crypto helpers",
                "live current-client correlation",
            ],
            "interpretationRule": "Only branch conditions, typed JSON-value checks, literal writes and helper calls visible in the hash-locked shared producer are promoted. The separate unavailable literal is not assigned to this producer without a direct recovered reference.",
        },
        "implementationImpact": {
            "publicSummary": "remains fail-closed with MAP_INDEX_UNAVAILABLE",
            "publicStatus": "remains fail-closed until authoritative bridge/readiness integration is recovered",
            "closedGap": "R6-036 serverIdSource live/none fallback and active-read server-mismatch behavior",
            "pm7B": "continue protected handler/readiness mapping and package-key loader ownership; do not infer map scan state is unavailable from nearby vocabulary",
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
