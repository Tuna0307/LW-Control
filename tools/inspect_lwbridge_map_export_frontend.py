#!/usr/bin/env python3
"""Verify the recovered frontend contract for Map Data city export."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


MAP_PANEL_SHA256 = "fc39de8154c54ee0937b6f9f9377d671e0dbad533ed2d9eda29a99d35e380f5e"
API_SHA256 = "062ebd2362885af05e07bf4f0de00ed833293a83fefa6b54c26975fa1db9df47"


class InspectError(ValueError):
    pass


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def require(text: str, needle: str, description: str) -> int:
    offset = text.find(needle)
    if offset < 0:
        raise InspectError(f"missing {description}: {needle}")
    return offset


def inspect(frontend_root: Path) -> dict[str, object]:
    panel_path = frontend_root / "assets" / "MapDataPanel-C1HVeNHr.js"
    api_path = frontend_root / "assets" / "api-ClPPi2JT.js"
    panel_hash = sha256(panel_path)
    api_hash = sha256(api_path)
    if panel_hash != MAP_PANEL_SHA256:
        raise InspectError(f"unexpected MapDataPanel SHA-256 {panel_hash}")
    if api_hash != API_SHA256:
        raise InspectError(f"unexpected API SHA-256 {api_hash}")

    panel = panel_path.read_text(encoding="utf-8")
    api = api_path.read_text(encoding="utf-8")

    wrapper = "U(`map_city_export`,{query:e,...t})"
    export_start = "async function lr(){if(!(F!==`city`||L<=0)){En(!0),K(``);try{"
    call = (
        "let e=await o($n(1,200),{headers:[C(`map.server`),`X`,`Y`,C(`map.player`),"
        "`UID`,`UUID`,C(`map.alliance`),C(`map.level`),`HP`,C(`automation.shieldEnds`),"
        "C(`map.marked`),C(`map.updatedAt`)],sheetName:C(`map.city`),"
        "yesLabel:C(`common.yes`),noLabel:C(`common.no`)})"
    )
    result = (
        "e.canceled||(K(C(`map.exportExcelSuccess`,{count:e.rowCount,path:e.path})),"
        "x(`exported ${e.rowCount} city rows to ${e.path}`))"
    )
    failure = "catch(e){K(_(C,e)),x(`city export error `+String(e))}finally{En(!1)}}}"
    button = (
        "F===`city`&&(0,D.jsx)(`button`,{disabled:Tn||L<=0||w.isReading,onClick:lr,"
        "children:C(Tn?`map.exportingExcel`:`map.exportExcel`)})"
    )

    locators = {
        "wrapperCharacterOffset": require(api, wrapper, "map_city_export wrapper"),
        "exportFunctionCharacterOffset": require(panel, export_start, "city export function"),
        "exportCallCharacterOffset": require(panel, call, "city export call/options"),
        "exportResultCharacterOffset": require(panel, result, "city export result handling"),
        "exportFailureCharacterOffset": require(panel, failure, "city export failure/finally handling"),
        "exportButtonCharacterOffset": require(panel, button, "city export button gate"),
    }

    return {
        "schemaVersion": 1,
        "date": "2026-09-09",
        "findingId": "LWB-R6-017",
        "scope": "Map Data city export frontend request/result contract",
        "evidenceStatus": "RECOVERED static",
        "sourceIdentity": {
            "mapPanel": {"path": str(panel_path), "sha256": panel_hash},
            "api": {"path": str(api_path), "sha256": api_hash},
        },
        "locators": locators,
        "recovered": {
            "command": "map_city_export",
            "apiPayload": "{query,...options}",
            "eligibility": "frontend offers export only on the city tab; selected data server must be positive; button is disabled while a map scan is reading or export is already busy",
            "query": {
                "source": "the same current Map Data query builder used by search",
                "page": 1,
                "pageSize": 200,
                "implication": "current city keyword/alliance/no-alliance/marked/sort state is serialized by the shared query builder when present",
            },
            "options": {
                "headers": [
                    "map.server", "X", "Y", "map.player", "UID", "UUID",
                    "map.alliance", "map.level", "HP", "automation.shieldEnds",
                    "map.marked", "map.updatedAt"
                ],
                "sheetName": "map.city",
                "yesLabel": "common.yes",
                "noLabel": "common.no",
                "localization": "translation keys are resolved by the frontend before invocation; literal X/Y/UID/UUID/HP remain literal",
            },
            "result": {
                "fieldsConsumed": ["canceled", "rowCount", "path"],
                "canceled": "suppresses success text/log when true",
                "success": "uses rowCount and path in the visible success message and log",
                "failure": "surfaces the thrown error through the panel error formatter and city-export log",
            },
        },
        "reproduction": [
            "python tools\\inspect_lwbridge_map_export_frontend.py evidence\\lwbridge-0.3.1\\frontend",
            "python tools\\inspect_lwbridge_map_export_frontend.py evidence\\lwbridge-0.3.1\\frontend --json",
        ],
        "validationAndLimits": {
            "validation": "hash-locked static verification against immutable recovered frontend assets",
            "liveProven": False,
            "unknownBlocked": [
                "native file picker contract and default filename/path",
                "backend row-to-column value mapping and type coercion",
                "whether the backend internally paginates beyond the frontend query pageSize=200",
                "workbook library/format details, column widths/styles and exact cancellation/error codes",
                "large UID/UUID preservation and reopen behavior in the original writer",
            ],
        },
        "implementationImpact": {
            "frontendEnvelope": "exact request options and consumed result fields are now pinned",
            "backend": "map_city_export must remain fail-closed until writer scope/row mapping/pagination/file-dialog semantics are recovered; the 200-row frontend query must not be mistaken for proof of an export row limit",
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("frontend_root", type=Path)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        result = inspect(args.frontend_root)
    except (InspectError, OSError) as exc:
        parser.error(str(exc))
    print(json.dumps(result, indent=2) if args.json else f"finding={result['findingId']} status={result['evidenceStatus']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
