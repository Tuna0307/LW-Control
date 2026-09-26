#!/usr/bin/env python3
"""Verify bounded LWBridge 0.3.1 Dispatch/Ghost sort parser and SQL ordering assembly."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path

import pefile


EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
MAP_PANEL_SHA256 = "fc39de8154c54ee0937b6f9f9377d671e0dbad533ed2d9eda29a99d35e380f5e"
SHARED_SORTS = ["level", "quality", "completionTime", "updatedAt"]

FIXED_BYTES = {
    "qualityParserAndDispatchGhostGate": (
        0x140274C2C,
        "8b06b97175616c31c88b4e03ba6c69747931d109c10f85f1090000807c246c00741c"
        "488b4c2460488d1595cca0004d89f8e88e7b520085c00f84230400004983ff05753a"
        "488b4c24608b01ba67686f7331d00fb6490483f17409c10f8400040000488b4c2460"
        "8b01ba7472756331d00fb6490483f16b09c10f84fd040000",
    ),
    "dispatchGhostQualityBuilder": (
        0x14027508D,
        "41b85c00000031d2e8167d34004885c00f846e1c00004989c7bd5c00000041b85c000000"
        "4889c1488d159b66a100e8e06c5200b85c000000",
    ),
    "completionParserAndDispatchGhostGate": (
        0x140274E58,
        "488b0648b9636f6d706c6574694831c8488b4e0648ba74696f6e54696d654831d14809c1"
        "0f85b6070000807c246c007418488b4c2460488d155acaa0004d89f8e85379520085c07427"
        "4983ff050f858d070000488b4c24608b01ba67686f7331d00fb6490483f17409c10f8570070000",
    ),
    "completionExpressionRef": (
        0x140274EC8,
        "41b845000000488d8c2490000000488d150367a100",
    ),
    "levelParser": (
        0x140274D6A,
        "8b06b96c65766531c80fb64e0483f16c09c10f84b4010000",
    ),
    "levelLiteralBuilder": (
        0x140274F36,
        "41b80500000031d2e86d7e34004885c00f84081c00004989c7c640046cc7006c657665",
    ),
    "updatedAtParser": (
        0x140274C06,
        "488b0648b975706461746564414831c80fb64e084883f1744809c10f84bafeffff",
    ),
    "directionBranch": (
        0x14027525C,
        "488b8424100200004883f803752c",
    ),
    "ascBranch": (
        0x14027526A,
        "488b8424080200000fb70881f1617300000fb6400283f06341bf040000006609c8",
    ),
    "ordinaryDescBranch": (
        0x140275296,
        "4c89e94883f10841bf04000000488d2d5065a1004809c1",
    ),
    "innerNullLastMapper": (
        0x140207BB9,
        "4c8d2dc8c3e5ff488d1d4e3ca800",
    ),
    "pageNullLastMapper": (
        0x1402071A2,
        "4c8d2ddfcde5ff488d1d8f46a800",
    ),
    "recordKeyFallback": (
        0x140275850,
        "48b95f6b6579204153434889480648b97265636f72645f6b488908",
    ),
}

ASCII_AT_RAW = {
    "completionTimeExpression": (
        0x00C8A9E0,
        "NULLIF(CAST(json_extract(data_json,'$.completionTime') AS INTEGER),0)",
    ),
    "dispatchGhostQualityExpression": (
        0x00C8AB56,
        "CASE WHEN CAST(json_extract(data_json,'$.isSpecial') AS INTEGER)=1 THEN 100 ELSE quality END",
    ),
    "updatedAtExpression": (0x00C8A533, "updated_at AS sort_value_0"),
    "sortAliasPrefix": (0x00C8ABED, "sort_value_"),
    "asc": (0x00C8A530, "ASC"),
    "desc": (0x00C8ABFA, "DESC"),
}

FORMATTER_BYTES = {
    "innerNullLastDescriptor": (
        0x140C8B815,
        "0128c00f204953204e554c4c29204153432c20c800000120c000",
    ),
    "pageNullLastDescriptor": (
        0x140C8B83F,
        "0628706167652ec014204953204e554c4c29204153432c20706167652ec800000120c000",
    ),
}


class InspectError(ValueError):
    pass


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def raw_offset_for_va(pe: pefile.PE, va: int) -> int:
    return pe.get_offset_from_rva(va - pe.OPTIONAL_HEADER.ImageBase)


def require_bytes(blob: bytes, pe: pefile.PE, va: int, expected_hex: str, label: str) -> None:
    expected = bytes.fromhex(expected_hex)
    offset = raw_offset_for_va(pe, va)
    actual = blob[offset:offset + len(expected)]
    if actual != expected:
        raise InspectError(
            f"unexpected bytes for {label} at VA 0x{va:X}: expected {expected.hex()} got {actual.hex()}"
        )


def require_ascii(blob: bytes, raw_offset: int, text: str, label: str) -> None:
    expected = text.encode("ascii")
    actual = blob[raw_offset:raw_offset + len(expected)]
    if actual != expected:
        raise InspectError(
            f"unexpected ASCII for {label} at raw 0x{raw_offset:X}: expected {text!r} got {actual!r}"
        )


def extract_public_sorts(panel: str) -> dict[str, list[str]]:
    start = panel.find("function L(e,t,n,r,i,a){")
    end = panel.find("function tt(", start)
    if start < 0 or end < 0:
        raise InspectError("Map Data column function bounds were not found")
    columns = panel[start:end]
    begin = "]:[...e===`dispatch`?"
    finish = "]}"
    lo = columns.find(begin)
    hi = columns.find(finish, lo + len(begin))
    if lo < 0 or hi < 0:
        raise InspectError("Dispatch/Ghost frontend column segment was not found")
    segment = columns[lo:hi]
    positioned = [
        (match.start(), match.group(1))
        for match in re.finditer(r"sortBy:`([^`]+)`", segment)
    ]
    sorts = [name for _, name in sorted(positioned)]
    if "sortBy:`updatedAt`" not in segment:
        sorts.append("updatedAt")
    # The shipped frontend shares this exact column branch for Ghost.
    return {"dispatch": sorts, "ghost": list(sorts)}


def inspect(binary: Path, frontend_root: Path) -> dict[str, object]:
    blob = binary.read_bytes()
    digest = hashlib.sha256(blob).hexdigest()
    if digest != EXPECTED_SHA256:
        raise InspectError(f"unexpected LWBridge SHA-256: {digest}")

    panel_path = frontend_root / "assets" / "MapDataPanel-C1HVeNHr.js"
    panel_hash = sha256(panel_path)
    if panel_hash != MAP_PANEL_SHA256:
        raise InspectError(f"unexpected MapDataPanel SHA-256: {panel_hash}")
    panel = panel_path.read_text(encoding="utf-8")
    public_sorts = extract_public_sorts(panel)
    for kind in ("dispatch", "ghost"):
        if public_sorts[kind] != SHARED_SORTS:
            raise InspectError(
                f"unexpected {kind} public sorts: expected {SHARED_SORTS}, got {public_sorts[kind]}"
            )

    pe = pefile.PE(data=blob, fast_load=True)
    for label, (va, expected_hex) in FIXED_BYTES.items():
        require_bytes(blob, pe, va, expected_hex, label)
    for label, (raw_offset, value) in ASCII_AT_RAW.items():
        require_ascii(blob, raw_offset, value, label)
    for label, (va, expected_hex) in FORMATTER_BYTES.items():
        require_bytes(blob, pe, va, expected_hex, label)

    return {
        "findingId": "LWB-R7-053",
        "date": "2026-09-19",
        "scope": "Dispatch/Ghost Map Data alternate sort gates, expressions and shared null-order assembly",
        "evidenceStatus": "RECOVERED static / production implementation eligible",
        "sourceIdentity": {
            "lwbridge": {"path": str(binary), "sha256": digest},
            "mapPanel": {"path": str(panel_path), "sha256": panel_hash},
        },
        "frontend": {
            "dispatchSorts": SHARED_SORTS,
            "ghostSorts": SHARED_SORTS,
            "orderedMultiSort": True,
            "default": [{"sortBy": "updatedAt", "sortOrder": "desc"}],
        },
        "native": {
            "expressions": {
                "level": "level",
                "quality": "CASE WHEN CAST(json_extract(data_json,'$.isSpecial') AS INTEGER)=1 THEN 100 ELSE quality END",
                "completionTime": "NULLIF(CAST(json_extract(data_json,'$.completionTime') AS INTEGER),0)",
                "updatedAt": "updated_at",
            },
            "qualityGate": "Dispatch and Ghost route to the shared isSpecial=>100 quality expression",
            "completionTimeGate": "Dispatch and Ghost route to the same NULLIF(completionTime,0) expression",
            "alias": "sort_value_N in frontend sort-array order",
            "innerOrderFragment": "(sort_value_N IS NULL) ASC, sort_value_N <ASC|DESC>",
            "pageOrderFragment": "(page.sort_value_N IS NULL) ASC, page.sort_value_N <ASC|DESC>",
            "stableTieBreak": {"inner": "record_key ASC", "page": "page.record_key ASC"},
        },
        "implementationBoundary": {
            "enable": "Dispatch and Ghost public sort keys only, ordered unique multi-sort",
            "keepFailClosed": [
                "any non-public Dispatch/Ghost sort key"
            ],
        },
        "reproduction": [
            "python tools\\inspect_lwbridge_dispatch_ghost_sort_assembly.py ..\\LW\\lwbridge-0.3.1.exe evidence\\lwbridge-0.3.1\\frontend --json"
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=Path)
    parser.add_argument("frontend_root", type=Path)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        result = inspect(args.binary, args.frontend_root)
    except (InspectError, OSError, pefile.PEFormatError) as exc:
        parser.error(str(exc))
    print(json.dumps(result, indent=2) if args.json else f"finding={result['findingId']} status={result['evidenceStatus']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
