#!/usr/bin/env python3
"""Verify Map Data option/summary consumers and per-tab query emission."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


MAP_PANEL_SHA256 = "fc39de8154c54ee0937b6f9f9377d671e0dbad533ed2d9eda29a99d35e380f5e"
API_SHA256 = "062ebd2362885af05e07bf4f0de00ed833293a83fefa6b54c26975fa1db9df47"
INDEX_SHA256 = "4d18cbc036a417b1a13a2c9905f1abc5dda1427531ad1ab4f7b03f4b269704b3"


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
    index_path = frontend_root / "assets" / "index-sfL2sT3K.js"
    hashes = {
        "mapPanel": sha256(panel_path),
        "api": sha256(api_path),
        "index": sha256(index_path),
    }
    expected = {
        "mapPanel": MAP_PANEL_SHA256,
        "api": API_SHA256,
        "index": INDEX_SHA256,
    }
    for key, digest in hashes.items():
        if digest != expected[key]:
            raise InspectError(f"unexpected {key} SHA-256 {digest}")

    panel = panel_path.read_text(encoding="utf-8")
    api = api_path.read_text(encoding="utf-8")
    index = index_path.read_text(encoding="utf-8")

    map_options_wrapper = "U(`map_data_options`,{serverId:e})"
    map_summary_wrapper = "U(`map_summary`,Z(e))"
    options_consumer = (
        "le(L).then(t=>{if(e!==Ie.current)return;let n=t.serverId;if(n!==L){ct(n);return}"
        "ut(t.alliances),ft(t.names),mt(t.dispatchLevels),gt(t.counts),vt(!0),"
        "Ee.current(t.serverId,t.counts),bt(t.rewardItems),St(t.treasureTypes),"
        "wt(t.noAllianceCount),Tt(t.scanProgress)"
    )
    summary_consumer = (
        "async function Nt(e=u.selectedProfileId){let t=Xe.current+1;Xe.current=t;"
        "let n=await x(e);return t!==Xe.current||e!==D()?n:(He(n),Mt(n.scanState),n)}"
    )
    summary_count_update = (
        "He(n=>n?.serverId===e?{...n,counts:t}:ze?.serverId===e?"
        "{serverId:e,counts:t,scanState:ze}:n)"
    )
    stale_search_guard = (
        "async function er(e=z){if(F===`scheduledPlunder`)return;let t=E.current+1;"
        "E.current=t,Yt(!0);try{let n=$n(e),r=await i(F,n);if(t===E.current)"
    )

    locators = {
        "mapDataOptionsWrapperCharacterOffset": require(api, map_options_wrapper, "map_data_options wrapper"),
        "mapSummaryWrapperCharacterOffset": require(api, map_summary_wrapper, "map_summary wrapper"),
        "mapDataOptionsConsumerCharacterOffset": require(panel, options_consumer, "map_data_options consumer"),
        "mapSummaryConsumerCharacterOffset": require(index, summary_consumer, "map_summary consumer"),
        "mapSummaryCountUpdateCharacterOffset": require(index, summary_count_update, "map summary count update"),
        "staleSearchGuardCharacterOffset": require(panel, stale_search_guard, "map_search generation guard"),
    }
    require(panel, "finally{t===E.current&&Yt(!1)}", "map_search stale-finally guard")

    query_rules = {
        "city": [
            "alliance:n===`city`&&r?r:void 0",
            "withoutAlliance:n===`city`&&Et===`none`?!0:void 0",
            "markedOnly:n===`city`&&At?!0:void 0",
        ],
        "resource": ["resourceNameKey:n===`resource`?Ot.resource:void 0"],
        "monster": ["monsterNameKey:n===`monster`?Ot.monster:void 0"],
        "truckRailway": [
            "itemKey:a?It[a]:void 0",
            "reindeerOnly:o===`reindeer`?!0:void 0",
        ],
        "dispatchGhost": [
            "completionStatus:n===`dispatch`||n===`ghost`?Bt[n]:void 0",
            "specialOnly:o===`special`?!0:void 0",
        ],
        "plunderable": ["plunderableOnly:he(n,He(n)&&Ht[n]===!0)"],
        "treasure": [
            "treasureType:s?.treasureType",
            "suppliesType:s?.suppliesType",
            "includeForeignRadarTreasures:n===`treasure`?An:void 0",
            "luckyFirst:n===`treasure`?J:void 0",
            "viewerUid:n===`treasure`?Nn.playerUid:void 0",
            "viewerAllianceId:n===`treasure`?Nn.allianceId:void 0",
        ],
        "dispatchLevel": [
            "minLevel:n===`dispatch`&&Wt?Number(Wt):void 0",
            "maxLevel:n===`dispatch`&&Wt?Number(Wt):void 0",
        ],
    }
    for kind, snippets in query_rules.items():
        for snippet in snippets:
            require(panel, snippet, f"{kind} query rule")

    option_shape_snippets = [
        "[yt,bt]=(0,b.useState)({truck:[],railway:[]})",
        "t.alliances.some(e=>e.name===n)",
        "t.names.resource.some(t=>t.key===e.resource)",
        "t.names.monster.some(t=>t.key===e.monster)",
        "t.dispatchLevels.includes(Number(e))",
        "t.treasureTypes.some(t=>t.key===e)",
        "children:[e.name,` (`,e.count,`)`]",
        "children:[j(Xt,e.key,e.key),` (`,e.count,`)`]",
        "assetPath:o.iconPath,alt:o.name",
        "s=e=>We(i,n,e.treasureType,e.suppliesType,e.treasureNameKey)",
        "children:[s(e),` (`,e.count,`)`]",
        "R.serverId===L&&R.id===w.scanRunId&&R.status!==`running`",
        "R.createdAt",
        "R.updatedAt",
        "R.error",
    ]
    for snippet in option_shape_snippets:
        require(panel, snippet, "map_data_options nested consumer shape")

    return {
        "findingId": "LWB-R6-004",
        "date": "2026-09-08",
        "scope": "Map Data options/summary consumers, conditional per-tab query emission and stale-result suppression",
        "evidenceStatus": "RECOVERED",
        "sourceIdentity": {
            "mapPanel": {"path": str(panel_path), "sha256": hashes["mapPanel"]},
            "api": {"path": str(api_path), "sha256": hashes["api"]},
            "index": {"path": str(index_path), "sha256": hashes["index"]},
        },
        "locators": locators,
        "commands": {
            "mapDataOptions": {"name": "map_data_options", "payload": ["serverId"]},
            "mapSummary": {"name": "map_summary", "payload": ["profileId (implicit-capable wrapper)"]},
        },
        "mapDataOptionsResponse": {
            "topLevelFieldsConsumed": [
                "serverId", "alliances", "names", "dispatchLevels", "counts",
                "rewardItems", "treasureTypes", "noAllianceCount", "scanProgress",
            ],
            "nestedFieldsConsumed": {
                "alliances[]": ["name", "count"],
                "names.resource[]": ["key", "count"],
                "names.monster[]": ["key", "count"],
                "dispatchLevels[]": ["numeric value"],
                "rewardItems.truck[] / rewardItems.railway[]": ["key", "name", "iconPath"],
                "treasureTypes[]": ["key", "count", "treasureType", "suppliesType", "treasureNameKey"],
                "scanProgress": ["serverId", "id", "status", "createdAt", "updatedAt", "error (when present)"],
            },
        },
        "mapSummaryResponse": {
            "topLevelFieldsConsumed": ["serverId", "counts", "scanState"],
            "behavior": "the parent stores the summary and passes summary.scanState through the shared map scan state path; count refreshes replace counts only when server identity matches",
        },
        "queryEmission": {
            "rulesVerified": query_rules,
            "explicitFalseIsMeaningful": [
                "treasure includeForeignRadarTreasures is emitted for treasure even when false",
                "treasure luckyFirst is emitted for treasure even when false",
            ],
            "undefinedOmissionRules": [
                "city withoutAlliance and markedOnly are emitted only when true",
                "plunderableOnly is emitted only as true for truck/railway/dispatch when enabled",
                "dispatch minLevel/maxLevel are omitted when the level selector is empty",
            ],
        },
        "staleResultSuppression": {
            "mapSearch": "each request increments E.current; rows/total, error clearing and loading-finally effects are committed only when the captured generation still equals E.current",
            "mapDataOptions": "each options request increments Ie.current and ignores a response whose captured generation no longer matches",
            "mapSummary": "each summary request increments Xe.current and ignores replacement when the generation changed or selected profile changed",
        },
        "reproduction": [
            "python tools\\inspect_lwbridge_map_frontend_consumers.py evidence\\lwbridge-0.3.1\\frontend",
            "python tools\\inspect_lwbridge_map_frontend_consumers.py evidence\\lwbridge-0.3.1\\frontend --json",
            "dotnet run --project tests\\LWBridge.Desktop.Checks\\LWBridge.Desktop.Checks.csproj",
        ],
        "validationAndLimits": {
            "validation": "static-only against immutable recovered frontend assets",
            "liveProven": False,
            "unknownBlocked": [
                "complete map_data_options aggregation SQL and ordering/deduplication semantics",
                "map_summary backend server-selection and scan-state producer semantics",
                "exact original SQL predicate for nonempty query filters",
                "exact SQL expression for every non-updatedAt sort key",
                "city export native file-picker/workbook writer contract",
            ],
        },
        "implementationImpact": {
            "queryGate": "all eight real frontend tab envelope families plus false/zero/empty/omitted distinctions are offline regression-tested without weakening MAP_QUERY_UNRECOVERED",
            "options": "response shape is now pinned, but production map_data_options stays fail-closed until aggregation semantics are recovered",
            "summary": "response shape/stale-profile guard is pinned, but persisted rows must not be used to invent the active summary server",
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
