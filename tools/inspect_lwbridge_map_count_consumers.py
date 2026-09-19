#!/usr/bin/env python3
"""Verify recovered Map Data count/no-alliance/summary frontend semantics."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


MAP_PANEL_SHA256 = "fc39de8154c54ee0937b6f9f9377d671e0dbad533ed2d9eda29a99d35e380f5e"
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
    index_path = frontend_root / "assets" / "index-sfL2sT3K.js"
    panel_hash = sha256(panel_path)
    index_hash = sha256(index_path)
    if panel_hash != MAP_PANEL_SHA256:
        raise InspectError(f"unexpected MapDataPanel SHA-256 {panel_hash}")
    if index_hash != INDEX_SHA256:
        raise InspectError(f"unexpected index SHA-256 {index_hash}")

    panel = panel_path.read_text(encoding="utf-8")
    index = index_path.read_text(encoding="utf-8")

    count_defaults = "var ve={city:0,resource:0,monster:0,truck:0,railway:0,dispatch:0,ghost:0,treasure:0};"
    summary_scope = "function ye(e,t){return t>0&&e?.serverId===t?e.counts:null}"
    summary_seed = "let e=ye(p,L);e?(gt(e),vt(!0)):(gt({}),vt(!1))"
    options_propagation = (
        "gt(t.counts),vt(!0),Ee.current(t.serverId,t.counts),bt(t.rewardItems),"
        "St(t.treasureTypes),wt(t.noAllianceCount),Tt(t.scanProgress)"
    )
    no_alliance_fallback = "Dt(e=>{if(e===`none`)return t.noAllianceCount>0?e:`all`;"
    clear_reset = "gt(ve),vt(!0),Ee.current(e.serverId,ve),bt({truck:[],railway:[]}),St([]),wt(0),Tt(null)"
    parent_count_update = (
        "function Pt(e,t){He(n=>n?.serverId===e?{...n,counts:t}:"
        "ze?.serverId===e?{serverId:e,counts:t,scanState:ze}:n)}"
    )
    summary_fetch = (
        "async function Nt(e=u.selectedProfileId){let t=Xe.current+1;Xe.current=t;"
        "let n=await x(e);return t!==Xe.current||e!==D()?n:(He(n),Mt(n.scanState),n)}"
    )

    locators = {
        "countDefaultsCharacterOffset": require(panel, count_defaults, "eight-kind count defaults"),
        "summaryCountScopeCharacterOffset": require(panel, summary_scope, "summary count server scope"),
        "summaryCountSeedCharacterOffset": require(panel, summary_seed, "summary count seed"),
        "optionsCountPropagationCharacterOffset": require(panel, options_propagation, "options count propagation"),
        "noAllianceFallbackCharacterOffset": require(panel, no_alliance_fallback, "no-alliance filter fallback"),
        "clearCountResetCharacterOffset": require(panel, clear_reset, "clear count/no-alliance reset"),
        "parentCountUpdateCharacterOffset": require(index, parent_count_update, "parent summary count update"),
        "summaryFetchCharacterOffset": require(index, summary_fetch, "summary stale-profile guard"),
    }

    return {
        "schemaVersion": 1,
        "date": "2026-09-09",
        "findingId": "LWB-R6-016",
        "scope": "Map Data count/no-alliance/summary frontend consumer semantics",
        "evidenceStatus": "RECOVERED static",
        "sourceIdentity": {
            "mapPanel": {"path": str(panel_path), "sha256": panel_hash},
            "index": {"path": str(index_path), "sha256": index_hash},
        },
        "locators": locators,
        "recovered": {
            "countKeys": ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"],
            "defaultCounts": "all eight recovered kind counters are zero",
            "summaryCountScope": "MapDataPanel consumes summary.counts only when summary.serverId equals the selected data server and that server is positive",
            "optionsCountPropagation": "a non-stale map_data_options response stores counts locally and forwards the same serverId/counts pair to the parent summary state",
            "parentCountUpdate": "the parent replaces summary counts only for the same server; if no summary exists, it may construct one only when the current map scan state has the same serverId",
            "noAllianceFilter": "the selected city no-alliance filter remains selected only while noAllianceCount is greater than zero; otherwise it falls back to all",
            "clearReset": "successful map clear resets the eight counts to zero and noAllianceCount to zero before refreshing rows/options",
            "staleSummaryGuard": "map_summary replacement remains guarded by request generation and selected profile identity",
        },
        "reproduction": [
            "python tools\\inspect_lwbridge_map_count_consumers.py evidence\\lwbridge-0.3.1\\frontend",
            "python tools\\inspect_lwbridge_map_count_consumers.py evidence\\lwbridge-0.3.1\\frontend --json",
        ],
        "validationAndLimits": {
            "validation": "hash-locked static verification against immutable recovered frontend assets",
            "liveProven": False,
            "unknownBlocked": [
                "backend SQL/source used to compute each count",
                "backend SQL/source and null/empty policy used to compute noAllianceCount",
                "original map_data_options persisted-vs-run source selection",
                "authoritative map_summary profile-to-server selection and scan-state producer",
            ],
        },
        "implementationImpact": {
            "counts": "pins exact frontend count keys and server-scoped replacement/reset rules without inventing backend aggregation SQL",
            "noAlliance": "pins UI fallback behavior but does not authorize deriving noAllianceCount from alliance option rows",
            "summary": "pins count propagation and stale/server guards; public map_summary remains fail-closed until its backend server/scan-state producer is recovered",
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
