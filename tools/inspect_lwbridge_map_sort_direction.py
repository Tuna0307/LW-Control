#!/usr/bin/env python3
"""Verify the bounded LWBridge Map Data sort-direction branch."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import pefile


EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
MAP_PANEL_SHA256 = "fc39de8154c54ee0937b6f9f9377d671e0dbad533ed2d9eda29a99d35e380f5e"

ASC_RAW_OFFSET = 0x00C8A530
DESC_RAW_OFFSET = 0x00C8ABFA

FIXED_BRANCH_BYTES = {
    "directionLengthThreeGate": (
        0x14027525C,
        "488b8424100200004883f803752c",
    ),
    "ascComparisonAndReference": (
        0x14027526A,
        "488b8424080200000fb70881f1617300000fb6400283f06341bf040000006609c8488d2d6865a100742aeb35",
    ),
    "descReferenceAndValidation": (
        0x140275296,
        "4c89e94883f10841bf04000000488d2d5065a1004809c1751c",
    ),
    "distanceDescOverride": (
        0x1402752AF,
        "48b864697374616e6365483906750d41bf03000000488d2d655ea100",
    ),
}


class InspectError(ValueError):
    pass


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def raw_offset_for_va(pe: pefile.PE, va: int) -> int:
    return pe.get_offset_from_rva(va - pe.OPTIONAL_HEADER.ImageBase)


def require_fixed_bytes(blob: bytes, pe: pefile.PE, va: int, expected_hex: str, name: str) -> None:
    expected = bytes.fromhex(expected_hex)
    offset = raw_offset_for_va(pe, va)
    actual = blob[offset:offset + len(expected)]
    if actual != expected:
        raise InspectError(
            f"unexpected bytes for {name} at VA 0x{va:X}: expected {expected.hex()} got {actual.hex()}"
        )


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
    for key, (va, expected_hex) in FIXED_BRANCH_BYTES.items():
        require_fixed_bytes(blob, pe, va, expected_hex, key)

    if blob[ASC_RAW_OFFSET:ASC_RAW_OFFSET + 3] != b"ASC":
        raise InspectError("ASC literal is not present at the recovered raw offset")
    if blob[DESC_RAW_OFFSET:DESC_RAW_OFFSET + 4] != b"DESC":
        raise InspectError("DESC literal is not present at the recovered raw offset")

    toggle = (
        "function _e(e,t){let n=e.findIndex(e=>e.sortBy===t);"
        "if(n<0)return[{sortBy:t,sortOrder:`desc`},...e];let r=e[n];"
        "return r.sortOrder===`desc`?[{...r,sortOrder:`asc`},...e.filter((e,t)=>t!==n)]:"
        "e.filter((e,t)=>t!==n)}"
    )
    if toggle not in panel:
        raise InspectError("frontend sort-order toggle was not found")

    rdata = next((section for section in pe.sections if section.Name.rstrip(b"\0") == b".rdata"), None)
    if rdata is None:
        raise InspectError(".rdata section is missing")
    if (rdata.PointerToRawData, rdata.VirtualAddress) != (0x7B8400, 0x7B9000):
        raise InspectError("unexpected .rdata raw/RVA mapping")

    return {
        "findingId": "LWB-R6-027",
        "date": "2026-09-09",
        "scope": "Map Data native sort direction literals and distance-desc override",
        "evidenceStatus": "RECOVERED",
        "sourceIdentity": {
            "lwbridge": {"path": str(binary), "sha256": digest},
            "mapPanel": {"path": str(panel_path), "sha256": panel_hash},
        },
        "locators": {
            "fixedBranchBytes": {
                key: {"preferredImageVa": hex(va), "expectedHex": expected_hex}
                for key, (va, expected_hex) in FIXED_BRANCH_BYTES.items()
            },
            "ascLiteral": {"rawPeOffset": hex(ASC_RAW_OFFSET), "text": "ASC", "length": 3},
            "descLiteral": {"rawPeOffset": hex(DESC_RAW_OFFSET), "text": "DESC", "length": 4},
            "rdataMapping": {
                "rawPeOffset": hex(rdata.PointerToRawData),
                "rva": hex(rdata.VirtualAddress),
            },
        },
        "publicFrontendOrders": ["desc", "asc"],
        "recoveredResult": {
            "asc": "the public asc path selects the three-byte ASC literal",
            "ordinaryDesc": "the public desc path selects the four-byte DESC literal",
            "distanceDesc": (
                "after the desc literal is selected, an exact inline comparison against distance "
                "overrides the direction to the three-byte ASC literal"
            ),
        },
        "implementationImpact": {
            "productionChange": "none",
            "reason": (
                "the distance expression/value source and complete ordered multi-sort/null-order assembly "
                "remain unrecovered, so the recovered physical direction rule is not sufficient to enable alternate sorts"
            ),
            "failClosed": "non-updatedAt production sorts remain MAP_QUERY_UNRECOVERED",
        },
        "validationAndLimits": {
            "validation": "hash-locked fixed-byte and immutable-frontend verification",
            "liveProven": False,
            "unknownBlocked": [
                "reason/mechanism for distance physical ASC beyond the recovered branch itself",
                "distance computed expression/value source",
                "complete ordered multi-sort/null-order/tie-break assembly",
            ],
        },
        "reproduction": [
            "python tools\\inspect_lwbridge_map_sort_direction.py ..\\LW\\lwbridge-0.3.1.exe evidence\\lwbridge-0.3.1\\frontend --json"
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
