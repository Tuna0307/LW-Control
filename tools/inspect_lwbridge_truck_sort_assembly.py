#!/usr/bin/env python3
"""Verify bounded LWBridge 0.3.1 Truck sort parsing and SQL ordering assembly."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import pefile


EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
MAP_PANEL_SHA256 = "fc39de8154c54ee0937b6f9f9377d671e0dbad533ed2d9eda29a99d35e380f5e"

TRUCK_SORTS = [
    "quality",
    "power",
    "itemCount",
    "remainingLootCount",
    "arriveTime",
    "updatedAt",
]

# Fixed bytes are intentionally limited to the verified map_search sort region and
# the two tiny mapper helpers reached from that region.
FIXED_BYTES = {
    "remainingLootLiteralLoad": (
        0x140274A22,
        "660f6f35b61f5c00660f6e3dbe1f5c00",
    ),
    "remainingLootParserAndTruckGate": (
        0x140274B1D,
        "f30f6f06660f74c60fb74610660f6ec8660f74cf660fdbc8660fd7c13dffff00000f85f40a00004983ff05",
    ),
    "itemCountParser": (
        0x140274BE5,
        "488b0648b96974656d436f756e4831c80fb64e084883f1744809c10f8462030000",
    ),
    "qualityParser": (
        0x140274C2C,
        "8b06b97175616c31c88b4e03ba6c69747931d109c10f85f1090000",
    ),
    "updatedAtParser": (
        0x140274C06,
        "488b0648b975706461746564414831c80fb64e084883f1744809c10f84bafeffff",
    ),
    "powerParser": (
        0x140274D82,
        "8b06b9706f776531c80fb64e0483f17209c10f859e080000",
    ),
    "arriveTimeParser": (
        0x140274DC2,
        "488b0648b961727269766554694831c80fb74e084881f16d6500004809c10f8552080000",
    ),
    "remainingLootExpressionRef": (
        0x140274FE3,
        "41b84b000000488d8c2490000000488d152d66a100",
    ),
    "arriveTimeExpressionRef": (
        0x14027502B,
        "41b83f000000488d8c2490000000488d157266a100",
    ),
    "truckQualityExpressionRef": (
        0x1402751A7,
        "41b865000000488d8c2490000000488d153565a100",
    ),
    "itemCountExpressionRef": (
        0x140275189,
        "488d8c2490000000488d158d63a1004c8d842470010000",
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
    "itemCountExpression": (
        0x00C8A926,
        "COALESCE((SELECT SUM(CAST(json_extract(good.value,'$.count') AS REAL)) FROM json_each(",
    ),
    "remainingLootExpression": (
        0x00C8AA25,
        "COALESCE(CAST(json_extract(data_json,'$.remainingLootCount') AS INTEGER),0)",
    ),
    "arriveTimeExpression": (
        0x00C8AAB2,
        "NULLIF(CAST(json_extract(data_json,'$.arriveTs') AS INTEGER),0)",
    ),
    "truckQualityExpression": (
        0x00C8AAF1,
        "CASE WHEN CAST(json_extract(data_json,'$.isSpecialURQuality') AS INTEGER)=1 THEN 100 ELSE quality END",
    ),
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

REMAINING_LOOT_LITERAL_VA = 0x1408369E0


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


def extract_truck_sort_segment(panel: str) -> str:
    start = panel.find("]:e===`truck`?")
    end = panel.find("]:e===`railway`?", start)
    if start < 0 or end < 0:
        raise InspectError("Truck frontend column segment was not found")
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
    truck_segment = extract_truck_sort_segment(panel)
    for key in TRUCK_SORTS:
        if key == "updatedAt":
            continue
        token = f"sortBy:`{key}`"
        if key == "itemCount":
            token = "sortBy:i?`itemCount`:void 0"
        if token not in truck_segment:
            raise InspectError(f"frontend Truck sort {key!r} was not found")
    if "sortBy:`updatedAt`" not in panel:
        raise InspectError("shared frontend updatedAt column was not found")
    if "itemKey:A(F)&&It[F]||``" not in panel:
        raise InspectError("frontend item-key query binding was not found")

    pe = pefile.PE(data=blob, fast_load=True)
    for label, (va, expected_hex) in FIXED_BYTES.items():
        require_bytes(blob, pe, va, expected_hex, label)
    for label, (raw_offset, text) in ASCII_AT_RAW.items():
        require_ascii(blob, raw_offset, text, label)
    for label, (va, expected_hex) in FORMATTER_BYTES.items():
        require_bytes(blob, pe, va, expected_hex, label)

    remaining_offset = raw_offset_for_va(pe, REMAINING_LOOT_LITERAL_VA)
    if blob[remaining_offset:remaining_offset + len("remainingLootCount")] != b"remainingLootCount":
        raise InspectError("remainingLootCount SIMD literal changed")

    return {
        "findingId": "LWB-R7-049",
        "date": "2026-09-19",
        "scope": "Truck Map Data alternate sort parser, expression and null-order assembly",
        "evidenceStatus": "RECOVERED static / production implementation eligible",
        "sourceIdentity": {
            "lwbridge": {"path": str(binary), "sha256": digest},
            "mapPanel": {"path": str(panel_path), "sha256": panel_hash},
        },
        "frontend": {
            "truckSorts": TRUCK_SORTS,
            "orderedMultiSort": True,
            "itemCountRequiresItemKey": True,
            "default": [{"sortBy": "updatedAt", "sortOrder": "desc"}],
        },
        "native": {
            "truckExpressions": {
                "quality": "CASE WHEN CAST(json_extract(data_json,'$.isSpecialURQuality') AS INTEGER)=1 THEN 100 ELSE quality END",
                "power": "power",
                "itemCount": "COALESCE((SELECT SUM(CAST(json_extract(good.value,'$.count') AS REAL)) FROM json_each(data_json,'$.currentGoods') AS good WHERE CAST(json_extract(good.value,'$.key') AS TEXT) = ?),0)",
                "remainingLootCount": "COALESCE(CAST(json_extract(data_json,'$.remainingLootCount') AS INTEGER),0)",
                "arriveTime": "NULLIF(CAST(json_extract(data_json,'$.arriveTs') AS INTEGER),0)",
                "updatedAt": "updated_at",
            },
            "alias": "sort_value_N in public sort-array order",
            "selectFragment": "<expression> AS sort_value_N",
            "innerOrderFragment": "(sort_value_N IS NULL) ASC, sort_value_N <ASC|DESC>",
            "pageOrderFragment": "(page.sort_value_N IS NULL) ASC, page.sort_value_N <ASC|DESC>",
            "stableTieBreak": {
                "inner": "record_key ASC",
                "page": "page.record_key ASC",
            },
            "direction": {
                "asc": "ASC",
                "desc": "DESC",
                "truckOverride": None,
            },
            "remainingLootParserProof": "SIMD literal is exactly remainingLootCount",
            "itemCountGate": "native branch requires the item-key value before the Truck/Railway itemCount expression is admitted",
            "treasureLuckyFirstLeadingSort": "separate Treasure-only path; not present for Truck",
        },
        "implementationBoundary": {
            "enable": "Truck public sort keys only, preserving ordered unique frontend multi-sort and itemCount/itemKey coupling",
            "keepFailClosed": [
                "Railway alternate sorts",
                "City alternate sorts",
                "Resource alternate sorts",
                "Dispatch/Ghost alternate sorts",
                "any non-public Truck sort key",
            ],
        },
        "reproduction": [
            "python tools\\inspect_lwbridge_truck_sort_assembly.py ..\\LW\\lwbridge-0.3.1.exe evidence\\lwbridge-0.3.1\\frontend --json"
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
