#!/usr/bin/env python3
"""Hash-gated static audit for LWB317-FUNCTION-MAP-BLOCKER-BOUNDARIES-004."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[2]
REFERENCE_EXE = Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
REFERENCE_SHA256 = "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
FRONTEND_INDEX = ROOT / "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js"
FRONTEND_INDEX_SHA256 = "44c4e4043825b7db296b64171951b27176f8850ff8d7d337cc991df4765524c6"
MAP_PANEL = ROOT / "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js"
MAP_PANEL_SHA256 = "ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089"
HANDLER_DISCOVERY = ROOT / "evidence/lwbridge-0.3.17/map/action-handler-discovery.json"
BT = chr(96)

def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()

def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)

def find_all(text: str, needle: str) -> list[int]:
    positions: list[int] = []
    start = 0
    while True:
        found = text.find(needle, start)
        if found < 0:
            return positions
        positions.append(found)
        start = found + len(needle)

def checkpoint_a() -> dict[str, Any]:
    require(REFERENCE_EXE.is_file(), f"reference EXE missing: {REFERENCE_EXE}")
    require(sha256(REFERENCE_EXE) == REFERENCE_SHA256, "reference EXE hash changed")
    require(sha256(FRONTEND_INDEX) == FRONTEND_INDEX_SHA256, "frontend index hash changed")
    require(sha256(MAP_PANEL) == MAP_PANEL_SHA256, "Map panel hash changed")

    index = FRONTEND_INDEX.read_text(encoding="utf-8")
    panel = MAP_PANEL.read_text(encoding="utf-8")
    handlers = json.loads(HANDLER_DISCOVERY.read_text(encoding="utf-8"))

    status_wrapper = "function En(){return N(" + BT + "map_treasure_claim_status" + BT + ")}"
    require(status_wrapper in index, "exact no-feature-payload status wrapper moved")

    profile_injection = "n&&!(" + BT + "profileId" + BT + "in r)&&(r.profileId=n)"
    require(profile_injection in index, "selected-profile injection contract moved")

    merge = (
        "function He(e,t){let n=Array.isArray(t)?t:Object.values(t),r=new Map(n.map(e=>[String(e.uuid||"
        + BT + BT + "),e]));return r.size===0?e:e.map(e=>{let t=r.get(String(k(e,"
        + BT + "uuid" + BT + ")||" + BT + BT + "));return t?{...e,...t}:e})}"
    )
    require(merge in panel, "Treasure state merge helper changed")

    poll_tokens = {
        "queuedGate": "r.queued>0",
        "loop": "for(let e=0;e<1800;e+=1)",
        "sleep": "window.setTimeout(e,1e3)",
        "mergeStates": "U(t=>He(t,e.states))",
        "stop": "e.batch.state!==" + BT + "running" + BT,
    }
    for label, token in poll_tokens.items():
        require(token in panel, f"Treasure claim polling contract moved: {label}")

    idle_status = (
        "let n=await t();if(e)return;if(Ln({playerUid:n.playerUid,allianceId:n.allianceId}),"
        "U(e=>He(e,n.states))"
    )
    require(idle_status in panel, "idle Treasure status snapshot contract moved")

    marker = handlers["markers"]["map_treasure_claim_status"]["handlerCandidates"][0]
    require(marker["functionRva"] == "0x115C5D-0x1166BE", "status handler range changed")
    provider_refs: list[str] = []
    timeout_proof = False
    for ins in marker["instructions"]:
        refs = [ref["text"] for ref in ins.get("stringRefs", [])]
        if "getTreasureClaimStatus" in refs:
            provider_refs.append(ins["rva"])
        if ins["rva"] == "0x1160E6" and ins["mnemonic"] == "mov" and "0x1388" in ins["opStr"]:
            timeout_proof = True
    require(provider_refs == ["0x1160CD"], "status provider locator changed")
    require(timeout_proof, "status 5000 ms timeout locator changed")

    claim_wrapper = (
        "function Cn(e,t,n,r=" + BT + BT + "){return N(" + BT + "map_treasure_claim" + BT
        + ",{serverId:e,claimScope:t,prioritizeLuckySlots:n,targetUuid:r})}"
    )
    require(claim_wrapper in index, "Treasure claim wrapper changed")

    return {
        "ok": True,
        "checkpoint": "A",
        "referenceExeSha256": REFERENCE_SHA256,
        "frontend": {
            "index": {"path": str(FRONTEND_INDEX.relative_to(ROOT)), "sha256": FRONTEND_INDEX_SHA256},
            "mapPanel": {"path": str(MAP_PANEL.relative_to(ROOT)), "sha256": MAP_PANEL_SHA256},
        },
        "status": {
            "wrapperOffsets": find_all(index, status_wrapper),
            "profileInjectionOffsets": find_all(index, profile_injection),
            "idleSnapshotOffsets": find_all(panel, idle_status),
            "claimQueuedGateOffsets": find_all(panel, poll_tokens["queuedGate"]),
            "pollLoopOffsets": find_all(panel, poll_tokens["loop"]),
            "pollSleepOffsets": find_all(panel, poll_tokens["sleep"]),
            "pollStateMergeOffsets": find_all(panel, poll_tokens["mergeStates"]),
            "pollStopOffsets": find_all(panel, poll_tokens["stop"]),
            "mergeHelperOffsets": find_all(panel, merge),
            "hostHandlerRva": marker["functionRva"],
            "providerStringRva": provider_refs[0],
            "providerTimeoutMilliseconds": 5000,
            "requiredIdleFields": ["playerUid", "allianceId", "states"],
            "optionalObservedField": "batch",
            "pollStartsWhen": "claim result queued > 0",
            "pollContinuesWhen": "batch missing OR batch.state == running",
            "pollStopsWhen": "batch exists AND batch.state != running",
            "maxPollIterations": 1800,
            "pollIntervalMilliseconds": 1000,
        },
        "ownership": {
            "frontendInvocationProfile": "currently selected profileId is injected on every bridge call",
            "pollProfilePinned": False,
            "providerBatchCanBePriorOperationState": True,
            "providerBatchLocationOrLifecycleRecovered": False,
            "idleSnapshotRequiresActiveBatch": False,
        },
        "disposition": {
            "missingUuidArgumentDisprovesPerTargetStatus": False,
            "cacheControllerHypothesis": "PARTIALLY_SUPPORTED",
            "firstMissingEdge": (
                "provider-owned claim/batch controller storage + transition/reset semantics -> "
                "exact idle/active status snapshot"
            ),
        },
    }

def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--checkpoint", choices=["A"], default="A")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = checkpoint_a()
    encoded = json.dumps(result, indent=2, ensure_ascii=False) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded, encoding="utf-8")
    print(encoded, end="")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
