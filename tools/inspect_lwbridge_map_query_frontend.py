#!/usr/bin/env python3
"""Verify the recovered LWBridge Map Data frontend query/presentation contract."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path


MAP_PANEL_SHA256 = "fc39de8154c54ee0937b6f9f9377d671e0dbad533ed2d9eda29a99d35e380f5e"
API_SHA256 = "062ebd2362885af05e07bf4f0de00ed833293a83fefa6b54c26975fa1db9df47"

QUERY_FIELDS = [
    "serverId", "keyword", "resourceNameKey", "monsterNameKey", "treasureType",
    "suppliesType", "alliance", "withoutAlliance", "markedOnly", "page", "pageSize",
    "sorts", "quality", "specialOnly", "reindeerOnly", "itemKey", "completionStatus",
    "plunderableOnly", "includeForeignRadarTreasures", "luckyFirst", "viewerUid",
    "viewerAllianceId", "minLevel", "maxLevel",
]

SORT_KEYS = [
    "updatedAt", "level", "health", "shield", "distance", "quality", "power",
    "remainingLootCount", "arriveTime", "protectTime", "completionTime", "itemCount",
]


class InspectError(ValueError):
    pass


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def require(text: str, needle: str, description: str) -> None:
    if needle not in text:
        raise InspectError(f"missing {description}: {needle}")


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
    map_search_wrapper = "U(`map_search`,{kind:e,query:t})"
    map_options_wrapper = "U(`map_data_options`,{serverId:e})"
    map_export_wrapper = "U(`map_city_export`,{query:e,...t})"
    require(api, map_search_wrapper, "map_search wrapper")
    require(api, map_options_wrapper, "map_data_options wrapper")
    require(api, map_export_wrapper, "map_city_export wrapper")

    start = panel.find("function $n(")
    end = panel.find("async function er", start)
    if start < 0 or end < 0:
        raise InspectError("Map Data query builder function bounds were not found")
    query_builder = panel[start:end]
    for field in QUERY_FIELDS:
        require(query_builder, field + ":", f"query field {field}")

    found_sorts = set(re.findall(r"sortBy:`([^`]+)`", panel))
    if "sortBy:i?`itemCount`:void 0" in panel:
        found_sorts.add("itemCount")
    missing_sorts = [item for item in SORT_KEYS if item not in found_sorts]
    if missing_sorts:
        raise InspectError("missing sort keys: " + ", ".join(missing_sorts))

    require(panel, "Oe=50", "default Map Data page size")
    require(panel, "$n(1,200)", "city export page-size override")
    require(panel, "Math.ceil(r.total/Oe)", "map_search total-based pagination")
    require(panel, "H(r.rows),U(r.total)", "map_search rows/total result consumption")

    return {
        "findingId": "LWB-R6-003",
        "date": "2026-09-08",
        "scope": "Map Data frontend query builder, sort vocabulary and city export envelope",
        "evidenceStatus": "RECOVERED",
        "sourceIdentity": {
            "mapPanel": {"path": str(panel_path), "sha256": panel_hash},
            "api": {"path": str(api_path), "sha256": api_hash},
        },
        "locators": {
            "queryBuilderCharacterRange": [start, end],
            "mapSearchWrapperCharacterOffset": api.find(map_search_wrapper),
            "mapDataOptionsWrapperCharacterOffset": api.find(map_options_wrapper),
            "mapCityExportWrapperCharacterOffset": api.find(map_export_wrapper),
            "defaultPageSizeCharacterOffset": panel.find("Oe=50"),
            "cityExportPageSizeCharacterOffset": panel.find("$n(1,200)"),
        },
        "commands": {
            "mapSearch": {"name": "map_search", "payload": ["kind", "query"]},
            "mapDataOptions": {"name": "map_data_options", "payload": ["serverId"]},
            "mapCityExport": {"name": "map_city_export", "payloadShape": "{query,...options}"},
        },
        "queryFields": QUERY_FIELDS,
        "sortKeys": SORT_KEYS,
        "pagination": {"defaultPageSize": 50, "cityExportPageSize": 200, "resultFields": ["rows", "total"]},
        "reproduction": [
            "python tools\\inspect_lwbridge_map_query_frontend.py evidence\\lwbridge-0.3.1\\frontend",
            "python tools\\inspect_lwbridge_map_query_frontend.py evidence\\lwbridge-0.3.1\\frontend --json",
        ],
        "validationAndLimits": {
            "validation": "static-only against immutable recovered frontend assets",
            "liveProven": False,
            "unknownBlocked": [
                "exact original SQL predicate for nonempty keyword/alliance/special/item/plunder filters",
                "exact SQL expression for every non-updatedAt sort key",
                "complete map_data_options aggregation SQL",
                "city export native file-picker/workbook writer contract",
            ],
        },
        "implementationImpact": {
            "defaultIndexedSearch": "implemented offline in MapDataStore.SearchIndexed",
            "supportedSlice": [
                "page/pageSize LIMIT/OFFSET",
                "updatedAt asc/desc with record_key ASC tie-breaker",
                "city marked state and markedOnly through recovered player_marks join",
                "rows/total backend envelope",
            ],
            "implementationPolicy": "unrecovered filter values other than omitted/null/empty-string fields, and non-updatedAt sorts, fail with MAP_QUERY_UNRECOVERED",
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
