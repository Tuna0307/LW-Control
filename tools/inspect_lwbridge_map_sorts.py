#!/usr/bin/env python3
"""Verify bounded LWBridge 0.3.1 Map Data sort evidence without broad disassembly."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path

import pefile


EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
MAP_PANEL_SHA256 = "fc39de8154c54ee0937b6f9f9377d671e0dbad533ed2d9eda29a99d35e380f5e"
MAP_SEARCH_RANGE = (0x140271864, 0x140276D7B)
SORT_MAPPING_RANGE = (0x1402749A7, 0x1402755F0)


PARSER_PREFIXES = {
    "remainingLootCountCandidate": (
        0x140274B1D,
        "f30f6f06660f74c60fb74610660f6ec8660f74cf660fdbc8660fd7c13dffff0000",
    ),
    "distance": (0x140274B68, "48b864697374616e6365483906"),
    "itemCount": (0x140274BE5, "488b0648b96974656d436f756e4831c80fb64e084883f174"),
    "updatedAt": (0x140274C06, "488b0648b975706461746564414831c80fb64e084883f174"),
    "quality": (0x140274C2C, "8b06b97175616c31c88b4e03ba6c69747931d109c1"),
    "shield": (0x140274CDF, "8b06b97368696531c80fb74e0481f16c64000009c1"),
    "health": (0x140274CFA, "8b06b96865616c31c80fb74e0481f17468000009c1"),
    "level": (0x140274D6A, "8b06b96c65766531c80fb64e0483f16c09c1"),
    "power": (0x140274D82, "8b06b9706f776531c80fb64e0483f17209c1"),
    "arriveTime": (0x140274DC2, "488b0648b961727269766554694831c80fb74e084881f16d6500004809c1"),
    "protectTime": (0x140274E0A, "488b0648b970726f74656374544831c8488b4e0348ba7465637454696d654831d14809c1"),
    "completionTime": (0x140274E58, "488b0648b9636f6d706c6574694831c8488b4e0648ba74696f6e54696d654831d14809c1"),
}


EXPRESSION_STRINGS = {
    "updatedAt": (0x00C8A533, "updated_at AS sort_value_0"),
    "distanceLiteral": (0x00C8A918, "distance"),
    "powerLiteral": (0x00C8A920, "power"),
    "itemCountDescriptor": (0x00C8A925, "V"),
    "itemCount": (
        0x00C8A926,
        "COALESCE((SELECT SUM(CAST(json_extract(good.value,'$.count') AS REAL)) FROM json_each(",
    ),
    "completionTime": (
        0x00C8A9E0,
        "NULLIF(CAST(json_extract(data_json,'$.completionTime') AS INTEGER),0)",
    ),
    "remainingLootCount": (
        0x00C8AA25,
        "COALESCE(CAST(json_extract(data_json,'$.remainingLootCount') AS INTEGER),0)",
    ),
    "protectTime": (
        0x00C8AA70,
        "NULLIF(CAST(json_extract(data_json,'$.protectTime') AS INTEGER),0)",
    ),
    "arriveTime": (
        0x00C8AAB2,
        "NULLIF(CAST(json_extract(data_json,'$.arriveTs') AS INTEGER),0)",
    ),
    "truckQuality": (
        0x00C8AAF1,
        "CASE WHEN CAST(json_extract(data_json,'$.isSpecialURQuality') AS INTEGER)=1 THEN 100 ELSE quality END",
    ),
    "dispatchGhostQuality": (
        0x00C8AB56,
        "CASE WHEN CAST(json_extract(data_json,'$.isSpecial') AS INTEGER)=1 THEN 100 ELSE quality END",
    ),
    "health": (
        0x00C8ABB2,
        "NULLIF(CAST(json_extract(data_json,'$.health') AS REAL),0)",
    ),
    "sortValuePrefix": (0x00C8ABED, "sort_value_"),
    "descendingLiteral": (0x00C8ABFA, "DESC"),
    "nullOrderFragment": (0x00C8AC19, " IS NULL) ASC, "),
    "pageNullOrderPrefix": (0x00C8AC40, "(page."),
    "pageNullOrderFragment": (0x00C8AC48, " IS NULL) ASC, page."),
}


DIRECT_REFERENCE_PREFIXES = {
    "completionTime": (0x140274ED6, "488d150367a100"),
    "arriveTime": (0x140275039, "488d157266a100"),
    "protectTime": (0x140275081, "488d15e865a100"),
    "itemCount": (0x140275191, "488d158d63a100"),
}


PUBLIC_SORTS_BY_KIND = {
    "city": ["level", "health", "shield", "updatedAt"],
    "resource": ["level", "updatedAt"],
    "monster": ["level", "distance", "updatedAt"],
    "truck": ["quality", "power", "itemCount", "remainingLootCount", "arriveTime", "updatedAt"],
    "railway": ["quality", "power", "itemCount", "protectTime", "updatedAt"],
    "dispatch": ["level", "quality", "completionTime", "updatedAt"],
    "ghost": ["level", "quality", "completionTime", "updatedAt"],
    "treasure": ["updatedAt"],
}


class InspectError(ValueError):
    pass


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def raw_offset_for_va(pe: pefile.PE, va: int) -> int:
    return pe.get_offset_from_rva(va - pe.OPTIONAL_HEADER.ImageBase)


def require_bytes(blob: bytes, pe: pefile.PE, va: int, expected_hex: str, description: str) -> None:
    expected = bytes.fromhex(expected_hex)
    offset = raw_offset_for_va(pe, va)
    actual = blob[offset:offset + len(expected)]
    if actual != expected:
        raise InspectError(
            f"unexpected bytes for {description} at VA 0x{va:X}: "
            f"expected {expected.hex()} got {actual.hex()}"
        )


def require_ascii(blob: bytes, raw_offset: int, text: str, description: str) -> None:
    expected = text.encode("ascii")
    actual = blob[raw_offset:raw_offset + len(expected)]
    if actual != expected:
        raise InspectError(
            f"unexpected ASCII for {description} at raw 0x{raw_offset:X}: "
            f"expected {text!r} got {actual!r}"
        )


def extract_public_sorts(panel: str) -> dict[str, list[str]]:
    start = panel.find("function L(e,t,n,r,i,a){")
    end = panel.find("function tt(", start)
    if start < 0 or end < 0:
        raise InspectError("Map Data column function bounds were not found")
    columns = panel[start:end]

    markers = {
        "city": ("return e===`city`?", "]:e===`resource`?"),
        "resource": ("]:e===`resource`?", "]:e===`monster`?"),
        "monster": ("]:e===`monster`?", "]:e===`truck`?"),
        "truck": ("]:e===`truck`?", "]:e===`railway`?"),
        "railway": ("]:e===`railway`?", "]:e===`treasure`?"),
        "treasure": ("]:e===`treasure`?", "]:[...e===`dispatch`?"),
        "dispatchGhost": ("]:[...e===`dispatch`?", "]}"),
    }

    result: dict[str, list[str]] = {}
    for kind, (begin, finish) in markers.items():
        lo = columns.find(begin)
        hi = columns.find(finish, lo + len(begin))
        if lo < 0 or hi < 0:
            raise InspectError(f"missing frontend column segment for {kind}")
        segment = columns[lo:hi]
        positioned = [(match.start(), match.group(1)) for match in re.finditer(r"sortBy:`([^`]+)`", segment)]
        item_count_at = segment.find("sortBy:i?`itemCount`:void 0")
        if item_count_at >= 0:
            positioned.append((item_count_at, "itemCount"))
        sorts = [name for _, name in sorted(positioned)]
        if "sortBy:`updatedAt`" not in segment:
            sorts.append("updatedAt")
        result[kind] = sorts

    result["dispatch"] = result.pop("dispatchGhost")
    result["ghost"] = list(result["dispatch"])
    return result


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

    pe = pefile.PE(data=blob, fast_load=True)
    for key, (va, expected_hex) in PARSER_PREFIXES.items():
        require_bytes(blob, pe, va, expected_hex, f"sort parser {key}")
    for key, (raw_offset, text) in EXPRESSION_STRINGS.items():
        require_ascii(blob, raw_offset, text, f"sort expression {key}")
    for key, (va, expected_hex) in DIRECT_REFERENCE_PREFIXES.items():
        require_bytes(blob, pe, va, expected_hex, f"sort expression reference {key}")

    toggle = (
        "function _e(e,t){let n=e.findIndex(e=>e.sortBy===t);"
        "if(n<0)return[{sortBy:t,sortOrder:`desc`},...e];let r=e[n];"
        "return r.sortOrder===`desc`?[{...r,sortOrder:`asc`},...e.filter((e,t)=>t!==n)]:"
        "e.filter((e,t)=>t!==n)}"
    )
    if toggle not in panel:
        raise InspectError("frontend ordered multi-sort toggle function was not found")

    defaults = (
        "Re={city:[{sortBy:`updatedAt`,sortOrder:`desc`}],"
        "resource:[{sortBy:`updatedAt`,sortOrder:`desc`}],"
        "monster:[{sortBy:`updatedAt`,sortOrder:`desc`}],"
        "truck:[{sortBy:`updatedAt`,sortOrder:`desc`}],"
        "railway:[{sortBy:`updatedAt`,sortOrder:`desc`}],"
        "dispatch:[{sortBy:`updatedAt`,sortOrder:`desc`}],"
        "ghost:[{sortBy:`updatedAt`,sortOrder:`desc`}],"
        "treasure:[{sortBy:`updatedAt`,sortOrder:`desc`}] }"
    ).replace("] }", "]}")
    if defaults not in panel:
        raise InspectError("frontend per-kind updatedAt defaults were not found")

    public_sorts = extract_public_sorts(panel)
    for kind, expected in PUBLIC_SORTS_BY_KIND.items():
        actual = public_sorts.get(kind)
        if actual != expected:
            raise InspectError(f"unexpected public sort columns for {kind}: expected {expected}, got {actual}")

    return {
        "findingId": "LWB-R6-026",
        "date": "2026-09-09",
        "scope": "Map Data native sort parser/expression inventory and frontend ordered multi-sort state",
        "evidenceStatus": "RECOVERED",
        "sourceIdentity": {
            "lwbridge": {"path": str(binary), "sha256": digest},
            "mapPanel": {"path": str(panel_path), "sha256": panel_hash},
        },
        "locators": {
            "mapSearchPreferredImageVaRange": [hex(MAP_SEARCH_RANGE[0]), hex(MAP_SEARCH_RANGE[1])],
            "sortMappingPreferredImageVaRange": [hex(SORT_MAPPING_RANGE[0]), hex(SORT_MAPPING_RANGE[1])],
            "parserComparisons": {
                key: {"preferredImageVa": hex(va), "expectedPrefixHex": expected_hex}
                for key, (va, expected_hex) in PARSER_PREFIXES.items()
            },
            "expressionStrings": {
                key: {"rawPeOffset": hex(offset), "text": text}
                for key, (offset, text) in EXPRESSION_STRINGS.items()
            },
            "directExpressionReferences": {
                key: {"preferredImageVa": hex(va), "expectedPrefixHex": expected_hex}
                for key, (va, expected_hex) in DIRECT_REFERENCE_PREFIXES.items()
            },
        },
        "frontendSortState": {
            "default": [{"sortBy": "updatedAt", "sortOrder": "desc"}],
            "toggle": [
                "missing key: prepend key desc and preserve existing ordered sorts",
                "existing desc key: change to asc and move it to the front",
                "existing asc key: remove it",
            ],
            "publicSortsByKind": public_sorts,
            "itemCountVisibleOnlyWhenItemKeyIsPresent": True,
        },
        "recoveredNativeSubset": {
            "parserKeysPinnedByScalarImmediateComparisons": [
                "distance", "itemCount", "updatedAt", "quality", "shield", "health",
                "level", "power", "arriveTime", "protectTime", "completionTime",
            ],
            "directExpressionReferences": ["itemCount", "arriveTime", "protectTime", "completionTime"],
            "exactExpressionInventory": list(EXPRESSION_STRINGS),
            "remainingLootCountCandidate": (
                "a 18-byte vector comparison is pinned at 0x140274B1D, but this checkpoint "
                "does not claim the compared constant until its producer is independently pinned"
            ),
        },
        "implementationImpact": {
            "productionChange": "none",
            "reason": (
                "frontend can emit ordered multi-sort arrays, while complete native multi-sort/null-order "
                "assembly and several key-to-expression/kind mappings are still incomplete"
            ),
            "failClosed": "non-updatedAt production sorts remain MAP_QUERY_UNRECOVERED",
        },
        "unknownBlocked": [
            "remainingLootCount vector-compare constant producer and exact parser-key proof",
            "shield prebuilt CASE expression data-flow",
            "distance exact computed expression/value source",
            "railway quality expression branch",
            "complete per-kind native gates for every alternate sort",
            "complete ordered multi-sort SQL assembly, null ordering and tie-break behavior",
        ],
        "reproduction": [
            "python tools\\inspect_lwbridge_map_sorts.py ..\\LW\\lwbridge-0.3.1.exe evidence\\lwbridge-0.3.1\\frontend --json",
        ],
        "validationAndLimits": {
            "validation": "hash-locked static verification of immutable reference/frontend bytes",
            "liveProven": False,
            "broadDisassemblyRequired": False,
        },
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
