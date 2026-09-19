#!/usr/bin/env python3
"""Verify bounded LWBridge 0.3.1 Resource sort parser and SQL ordering assembly."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import pefile


EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
MAP_PANEL_SHA256 = "fc39de8154c54ee0937b6f9f9377d671e0dbad533ed2d9eda29a99d35e380f5e"
RESOURCE_SORTS = ["level", "updatedAt"]

FIXED_BYTES = {
    "levelParserToGenericExpression": (
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
            f"unexpected bytes for {label} at VA 0x{va:X}: "
            f"expected {expected.hex()} got {actual.hex()}"
        )


def require_ascii(blob: bytes, raw_offset: int, text: str, label: str) -> None:
    expected = text.encode("ascii")
    actual = blob[raw_offset:raw_offset + len(expected)]
    if actual != expected:
        raise InspectError(
            f"unexpected ASCII for {label} at raw 0x{raw_offset:X}: "
            f"expected {text!r} got {actual!r}"
        )


def extract_resource_sort_segment(panel: str) -> str:
    start = panel.find("]:e===`resource`?")
    end = panel.find("]:e===`monster`?", start)
    if start < 0 or end < 0:
        raise InspectError("Resource frontend column segment was not found")
    return panel[start:end]


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
    segment = extract_resource_sort_segment(panel)
    if "sortBy:`level`" not in segment:
        raise InspectError("frontend Resource level sort was not found")
    if "sortBy:`updatedAt`" not in panel:
        raise InspectError("shared frontend updatedAt column was not found")

    pe = pefile.PE(data=blob, fast_load=True)
    for label, (va, expected_hex) in FIXED_BYTES.items():
        require_bytes(blob, pe, va, expected_hex, label)
    for label, (raw_offset, text) in ASCII_AT_RAW.items():
        require_ascii(blob, raw_offset, text, label)
    for label, (va, expected_hex) in FORMATTER_BYTES.items():
        require_bytes(blob, pe, va, expected_hex, label)

    return {
        "findingId": "LWB-R7-051",
        "date": "2026-09-19",
        "scope": "Resource Map Data alternate sort parser, expressions and null-order assembly",
        "evidenceStatus": "RECOVERED static / production implementation eligible",
        "sourceIdentity": {
            "lwbridge": {"path": str(binary), "sha256": digest},
            "mapPanel": {"path": str(panel_path), "sha256": panel_hash},
        },
        "frontend": {
            "resourceSorts": RESOURCE_SORTS,
            "orderedMultiSort": True,
            "default": [{"sortBy": "updatedAt", "sortOrder": "desc"}],
        },
        "native": {
            "resourceExpressions": {
                "level": "level",
                "updatedAt": "updated_at",
            },
            "levelRule": "matching level jumps directly to the generic level expression builder; no kind-specific transform",
            "alias": "sort_value_N in public sort-array order",
            "innerOrderFragment": "(sort_value_N IS NULL) ASC, sort_value_N <ASC|DESC>",
            "pageOrderFragment": "(page.sort_value_N IS NULL) ASC, page.sort_value_N <ASC|DESC>",
            "stableTieBreak": {"inner": "record_key ASC", "page": "page.record_key ASC"},
            "direction": {"asc": "ASC", "desc": "DESC", "resourceOverride": None},
        },
        "implementationBoundary": {
            "enable": "Resource public level/updatedAt sorts only, preserving ordered unique multi-sort",
            "keepFailClosed": [
                "City alternate sorts",
                "Dispatch/Ghost alternate sorts",
                "any non-public Resource sort key",
            ],
        },
        "reproduction": [
            "python tools\\inspect_lwbridge_resource_sort_assembly.py ..\\LW\\lwbridge-0.3.1.exe evidence\\lwbridge-0.3.1\\frontend --json"
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
