#!/usr/bin/env python3
"""Hash-lock and verify the original Treasure claim frontend/status contract.

This inspector deliberately stays outside the restricted protected-loader body.
It combines the verified 0.3.1 host string boundary with the extracted original
frontend assets that consume map_treasure_claim and map_treasure_claim_status.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

ORIGINAL_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
PANEL_SHA256 = "fc39de8154c54ee0937b6f9f9377d671e0dbad533ed2d9eda29a99d35e380f5e"
API_SHA256 = "062ebd2362885af05e07bf4f0de00ed833293a83fefa6b54c26975fa1db9df47"
PANEL_NAME = "MapDataPanel-C1HVeNHr.js"
API_NAME = "api-ClPPi2JT.js"

HOST_MARKERS = {
    0x8221D5: b"claimScope",
    0x8221EE: b"eligible",
    0x8221F6: b"queued",
    0x8221FC: b"skipped",
    0x822203: b"directQueued",
    0x82220F: b"scoutQueued",
    0x82221A: b"scoutDispatched",
    0x822229: b"claimed",
    0x822230: b"noScoutSkipped",
    0x82223E: b"otherAllianceSkipped",
    0x822252: b"failed",
    0x8222D4: b"prioritizeLuckySlots",
    0x822307: b"claimTreasures",
    0x822630: b"getTreasureClaimStatus",
    0x822660: b"playerUid",
    0x822669: b"allianceId",
    0x822673: b"states",
}

API_FRAGMENTS = [
    "function Ut(e,t,n,r=``){return U(`map_treasure_claim`,{serverId:e,claimScope:t,prioritizeLuckySlots:n,targetUuid:r})}",
    "function Kt(){return U(`map_treasure_claim_status`)}",
]

PANEL_FRAGMENTS = [
    "function Be(e,t){let n=Array.isArray(t)?t:Object.values(t),r=new Map(n.map(e=>[String(e.uuid||``),e]));return r.size===0?e:e.map(e=>{let t=r.get(String(k(e,`uuid`)||``));return t?{...e,...t}:e})}",
    "function T(e){return{charging:`map.treasureStateCharging`,claimable:`map.treasureStateClaimable`,depleted:`map.treasureStateDepleted`,expired:`map.treasureStateExpired`,verifying:`map.treasureStateVerifying`}[String(e||``)]||`map.treasureStateUnknown`}",
    "function E(e,t){return e===`claimed`?`map.treasurePlayerClaimed`:t===`other_alliance`?`map.treasurePlayerOtherAlliance`:t===`no_scout`||t===`no_squad`||t===`squad_reserved`?`map.treasurePlayerNoScout`:{unclaimed:`map.treasurePlayerUnclaimed`,dispatching:`map.treasurePlayerDispatching`,scouting:`map.treasurePlayerScouting`,digging:`map.treasurePlayerDigging`,claiming:`map.treasurePlayerClaiming`,claimed:`map.treasurePlayerClaimed`,failed:`map.treasurePlayerUnclaimed`,verifying:`map.treasurePlayerVerifying`}[String(e||``)]||`map.treasurePlayerUnknown`}",
    "je=`lwbridge.mapLuckyTreasurePriority`",
    "[J,Mn]=(0,b.useState)(()=>localStorage.getItem(je)!==`false`)",
    "(0,b.useEffect)(()=>{localStorage.setItem(je,String(J))},[J])",
    "if(Cn(C(`map.treasuresQueued`,n)),x(`treasure claims queued eligible=${n.eligible} queued=${n.queued} skipped=${n.skipped}`),n.queued>0)for(let e=0;e<1800;e+=1){await new Promise(e=>window.setTimeout(e,1e3));let e=await r();if(H(t=>Be(t,e.states)),e.batch&&(Cn(C(`map.treasureClaimSummary`,e.batch)),e.batch.state!==`running`)){hn(e=>e+1);break}}",
    "n=k(e,`worldClaimState`)||(a?`verifying`:`unknown`)",
    "E(k(e,`playerClaimState`)||(a?`verifying`:`unknown`),k(e,`claimBlockReason`))",
    "function he(e){let t=e,n=String(t.uuid||``).trim(),r=Number(t.suppliesType),i=r===1||r===3||r===4,a=String(t.playerClaimState||``);return(0,D.jsx)(`button`,{className:`map-schedule-button`,disabled:te||!n||n===`0`||!i&&t.complete!==!0||[`dispatching`,`scouting`,`digging`,`claiming`,`claimed`].includes(a)||t.worldClaimState===`depleted`||t.worldClaimState===`expired`||t.claimBlockReason===`other_alliance`",
    "onClick:()=>ar(`boxes`)",
    "onClick:()=>ar(`season`)",
    "onClaimTreasure:e=>ar(`single`,e)",
]


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def require(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit("FAIL: " + message)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=Path)
    parser.add_argument(
        "--frontend-dir",
        type=Path,
        default=Path("evidence/lwbridge-0.3.1/frontend/assets"),
    )
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    binary = args.binary.resolve()
    panel = (args.frontend_dir / PANEL_NAME).resolve()
    api = (args.frontend_dir / API_NAME).resolve()
    require(binary.is_file(), f"missing binary: {binary}")
    require(panel.is_file(), f"missing frontend panel: {panel}")
    require(api.is_file(), f"missing frontend API bundle: {api}")

    binary_hash = sha256(binary)
    panel_hash = sha256(panel)
    api_hash = sha256(api)
    require(binary_hash == ORIGINAL_SHA256, "original binary hash changed")
    require(panel_hash == PANEL_SHA256, "MapDataPanel hash changed")
    require(api_hash == API_SHA256, "API bundle hash changed")

    raw = binary.read_bytes()
    for offset, marker in HOST_MARKERS.items():
        require(raw[offset:offset + len(marker)] == marker, f"host marker mismatch at 0x{offset:X}")

    panel_text = panel.read_text(encoding="utf-8")
    api_text = api.read_text(encoding="utf-8")
    for fragment in API_FRAGMENTS:
        require(fragment in api_text, "original API claim/status wrapper changed")
    for fragment in PANEL_FRAGMENTS:
        require(fragment in panel_text, "original MapDataPanel Treasure contract changed: " + fragment[:80])

    result = {
        "ok": True,
        "findingId": "LWB-R7-087",
        "sources": {
            "originalBinary": {"sha256": binary_hash},
            "mapDataPanel": {"name": PANEL_NAME, "sha256": panel_hash},
            "apiBundle": {"name": API_NAME, "sha256": api_hash},
        },
        "hostBoundary": {
            "claimRuntime": "claimTreasures",
            "claimStatusRuntime": "getTreasureClaimStatus",
            "rpcTimeoutMilliseconds": 5000,
            "immediateCounters": [
                "eligible", "queued", "skipped", "directQueued", "scoutQueued",
                "scoutDispatched", "claimed", "noScoutSkipped",
                "otherAllianceSkipped", "failed",
            ],
            "statusIdentityMarkers": ["playerUid", "allianceId", "states"],
        },
        "frontend": {
            "claimScopes": ["boxes", "season", "single"],
            "luckyPriorityStorageKey": "lwbridge.mapLuckyTreasurePriority",
            "luckyPriorityDefault": True,
            "pollOnlyWhenQueuedPositive": True,
            "statusPollIntervalMilliseconds": 1000,
            "statusPollLimit": 1800,
            "statusPollMaximumSeconds": 1800,
            "batchOptional": True,
            "nonterminalBatchState": "running",
            "terminalRule": "when batch exists, any state other than exact 'running' stops polling",
            "stateMerge": "states accepts array or object values and overlays rows by String(uuid)",
            "rowStateFields": [
                "worldClaimState", "chargePercent", "playerClaimState", "claimBlockReason",
            ],
            "playerStateVocabulary": [
                "unclaimed", "dispatching", "scouting", "digging", "claiming",
                "claimed", "failed", "verifying",
            ],
            "claimBlockReasons": ["other_alliance", "no_scout", "no_squad", "squad_reserved"],
            "singleClaimDisabledWhen": [
                "global claim gate is disabled",
                "uuid is empty or '0'",
                "ordinary Treasure complete is not true",
                "player state is dispatching/scouting/digging/claiming/claimed",
                "world state is depleted/expired",
                "claimBlockReason is other_alliance",
            ],
            "supportedSuppliesTypesBypassOrdinaryCompleteGate": [1, 3, 4],
        },
        "limits": {
            "protectedScopeFilteringAndOrdering": "UNKNOWN/BLOCKED",
            "protectedLuckySlotPrioritization": "UNKNOWN/BLOCKED",
            "protectedScoutSlotSelectionAndReservation": "UNKNOWN/BLOCKED",
            "protectedBatchStateEnumeration": "UNKNOWN/BLOCKED except exact frontend nonterminal 'running' rule",
            "protectedLoaderBody": "not analyzed; SB-79 restriction preserved",
            "productionTreasureClaim": "BLOCKED/NOT_ROUTED",
        },
    }

    print("PASS: LWB-R7-087 original Treasure claim frontend/status contract")
    print("  poll=1000ms x 1800; only existing batch.state='running' is nonterminal")
    print("  lucky priority defaults on unless localStorage is exact 'false'")
    print("  single-row frontend gate and action-phase state vocabulary pinned")
    print("  protected claim executor remains UNKNOWN/BLOCKED; SB-79 preserved")
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
