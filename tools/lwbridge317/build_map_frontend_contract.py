#!/usr/bin/env python3
"""Build the exact-static LWBridge 0.3.17 Map frontend/host contract manifest.

This verifier consumes the recovered 0.3.17 frontend assets plus the native
surface-discovery manifest.  It validates source snippets that establish the
payload/result/default contracts used by the Map UI and records only those
facts.  Native handler semantics beyond string/xref discovery are deliberately
left to the state-machine/storage inspectors.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any


INDEX_SHA256 = "44c4e4043825b7db296b64171951b27176f8850ff8d7d337cc991df4765524c6"
MAP_PANEL_SHA256 = "ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089"

SCAN_TYPES = ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"]
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


class ContractError(ValueError):
    pass


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def require(text: str, needle: str, label: str) -> int:
    pos = text.find(needle)
    if pos < 0:
        raise ContractError(f"missing {label}: {needle}")
    return pos


def loc(text: str, needle: str) -> str:
    return f"0x{require(text, needle, needle):X}"


def native_functions(surface: dict[str, Any], marker: str) -> list[str]:
    entry = surface["native"]["markers"].get(marker, {})
    result: list[str] = []
    for xref in entry.get("xrefs", []):
        fn = xref.get("functionRva")
        if fn and fn not in result:
            result.append(fn)
    return result


def marker_presence(surface: dict[str, Any], marker: str) -> dict[str, Any]:
    entry = surface["native"]["markers"].get(marker, {})
    return {
        "present": bool(entry.get("occurrences")),
        "occurrences": len(entry.get("occurrences", [])),
        "functions": native_functions(surface, marker),
    }


def build(index_path: Path, panel_path: Path, surface_path: Path) -> dict[str, Any]:
    if sha(index_path) != INDEX_SHA256:
        raise ContractError("unexpected index asset SHA-256")
    if sha(panel_path) != MAP_PANEL_SHA256:
        raise ContractError("unexpected MapDataPanel asset SHA-256")
    index = index_path.read_text(encoding="utf-8")
    panel = panel_path.read_text(encoding="utf-8")
    surface_bytes = surface_path.read_bytes()
    surface_digest = hashlib.sha256(surface_bytes).hexdigest()
    surface = json.loads(surface_bytes.decode("utf-8"))

    # Exact source anchors for the profile-aware Tauri wrapper.
    bridge_helper = "function N(e,t){if(Ne()){let n=A(),r=t&&typeof t==`object`&&!Array.isArray(t)?{...t}:t==null?{}:{value:t};return n&&!(`profileId`in r)&&(r.profileId=n),C(e,{payload:r})"
    listener_helper = "function Pe(e,t){if(!Ne())throw Error(`desktop bridge unavailable`)"
    require(index, bridge_helper, "profile-aware invoke wrapper")
    require(index, listener_helper, "profile-aware event wrapper")

    # Map panel/query/default anchors.
    require(panel, "je=50", "default Map page size")
    require(panel, "lwbridge.mapScanMode", "manual scan speed storage key")
    require(panel, "lwbridge.mapIncludeForeignRadarTreasures", "treasure foreign-radar storage key")
    require(panel, "lwbridge.mapLuckyTreasurePriority", "treasure priority storage key")
    require(panel, "function nr(", "query builder")
    for field in QUERY_FIELDS:
        require(panel, field + ":", f"query field {field}")
    for sort in SORT_KEYS:
        if sort == "itemCount":
            require(panel, "`itemCount`", "itemCount sort")
        else:
            require(panel, f"sortBy:`{sort}`", f"sort key {sort}")
    require(panel, "nr(1,200)", "city export query page size")
    require(panel, "Math.ceil(r.total/je)", "total-based pagination")
    require(panel, "rows:H,total:en", "tab cache rows/total")
    require(index, "resume:!1", "auto scan explicit fresh-start flag")

    scan_state_default = {
        "serverId": 0,
        "serverIdSource": "none",
        "scanRunId": "",
        "isReading": False,
        "phase": "idle",
        "selectedTypes": SCAN_TYPES,
        "totalBlocks": 0,
        "readBlocks": 0,
        "unreadBlocks": 0,
        "failedBlocks": 0,
        "inflightBlocks": 0,
        "scanMode": "normal",
        "concurrency": 8,
        "retryCount": 2,
        "scanRate": 0,
        "progressPercent": 0,
        "nativeCaptureReady": False,
        "nativePendingRecords": 0,
        "nativeDroppedRecords": 0,
    }

    commands = {
        "map_scan_start": {
            "request": {"selectedTypes": "array<scanType>", "scanMode": "normal|fast", "resume": "optional boolean"},
            "frontendManual": "{selectedTypes,scanMode}",
            "frontendAuto": "{selectedTypes,scanMode,resume:false}",
            "result": "MapScanState",
        },
        "map_scan_status": {"request": {}, "result": "MapScanState"},
        "map_scan_stop": {"request": {}, "result": "MapScanState"},
        "map_scan_clear": {"request": {"serverId": "integer"}, "result": "MapScanState"},
        "map_summary": {"request": {"profileId": "profile-scoped optional/injected"}, "result": {"serverId": "integer", "counts": "MapCounts", "scanState": "MapScanState"}},
        "map_data_options": {"request": {"serverId": "integer"}, "result": {"serverId": "integer", "alliances": "array", "names": {"resource": "array", "monster": "array"}, "dispatchLevels": "array<number>", "counts": "MapCounts", "rewardItems": "object", "treasureTypes": "array", "noAllianceCount": "number", "scanProgress": "object|null"}},
        "map_search": {"request": {"kind": "scanType", "query": QUERY_FIELDS}, "result": {"rows": "array", "total": "number"}},
        "map_city_export": {"request": {"query": QUERY_FIELDS, "headers": "array<string>", "sheetName": "string", "yesLabel": "string", "noLabel": "string"}, "result": {"canceled": "boolean", "rowCount": "number", "path": "string"}},
        "map_coordinate_jump": {"request": {"serverId": "integer", "x": "integer>=1", "y": "integer>=1"}, "result": {"serverId": "integer", "x": "integer", "y": "integer"}},
        "map_march_follow": {"request": {"serverId": "integer", "marchUuid": "string"}, "result": {"serverId": "integer", "marchUuid": "string"}},
        "map_player_mark_set": {"request": {"row": "map row", "marked": "boolean"}, "result": "not consumed by frontend"},
        "server_jump": {"request": {"serverId": "integer 1..99999"}, "result": {"changed": "boolean", "previousServerId": "integer", "serverId": "integer"}},
        "server_jump_history_set": {"request": {"history": "unique array<int> max 5", "profileId": "profile scoped"}, "result": "normalized history array"},
        "server_jump_history_import": {"request": {"history": "legacy localStorage history", "profileId": "profile scoped"}, "result": "normalized history array"},
        "map_treasure_state_refresh": {"request": {"serverId": "integer", "records": "rows"}, "result": {"playerUid": "string", "allianceId": "string", "states": "array|object"}},
        "map_treasure_state_refresh_all": {"request": {"serverId": "integer"}, "result": {"playerUid": "string", "allianceId": "string", "states": "array|object"}},
        "map_treasure_claim": {"request": {"serverId": "integer", "claimScope": "single|boxes|season", "prioritizeLuckySlots": "boolean", "targetUuid": "string"}, "result": {"eligible": "number", "queued": "number", "skipped": "number"}},
        "map_treasure_claim_status": {"request": {}, "result": {"states": "array|object", "batch": "optional object with state"}},
        "map_dispatch_share_alliance": {"request": {"rows": ["uuid", "serverId", "x", "y", "cfgId", "ownerName", "allianceAbbr"]}, "result": {"sharedUuids": "array<string>", "shared": "number", "failed": "number"}},
        "map_plunder_jobs_list": {"request": {}, "result": {"dispatchJobs": "array", "truckJobs": "array"}},
        "map_dispatch_plunder_schedule": {"request": {"rows": "selected dispatch/ghost rows with computed plunderAt/maxRandomDelaySeconds/randomDelaySeconds"}, "result": "not otherwise consumed"},
        "map_dispatch_plunder_cancel": {"request": {"serverId": "integer", "taskUuid": "uuid or ghost:<uuid>"}, "result": "not otherwise consumed"},
        "map_dispatch_plunder_clear": {"request": {"before": "epoch milliseconds", "taskKind": "dispatch|ghost"}, "result": "not otherwise consumed"},
        "map_truck_plunder_schedule": {"request": {"rows": "selected truck rows with executeAt=max(now,protectTime)"}, "result": "not otherwise consumed"},
        "map_truck_plunder_cancel": {"request": {"serverId": "integer", "trainUuid": "string"}, "result": "not otherwise consumed"},
        "map_truck_plunder_clear": {"request": {"before": "epoch milliseconds"}, "result": "not otherwise consumed"},
    }

    for command in commands:
        if command not in surface["frontend"]["commandLiterals"]:
            raise ContractError(f"command literal missing from recovered frontend: {command}")

    legacy_markers = {
        "query.alliance": "alliance_name = ?",
        "query.withoutAlliance": "(alliance_name IS NULL OR alliance_name = '')",
        "query.resourceNameKey": "CAST(json_extract(data_json,'$.resourceNameKey') AS TEXT) = ?",
        "query.monsterNameKey": "CAST(json_extract(data_json,'$.monsterNameKey') AS TEXT) = ?",
        "query.itemKeyExists": "itemKey EXISTS (SELECT 1 FROM json_each(",
        "query.itemKeyValue": "CAST(json_extract(good.value,'$.key') AS TEXT) = ?",
        "scan.duplicateCode": "SCAN_RUNNING",
        "scan.duplicateMessage": "map scan already running",
        "scan.connectionCode": "GAME_CONNECTION_UNAVAILABLE",
        "scan.connectionMessage": "game connection unavailable",
        "scan.resumeInput": "resumeAvailable",
        "scan.selectedTypes": "selectedTypes",
        "scan.nativeCaptureReady": "nativeCaptureReady",
        "scan.nativePendingRecords": "nativePendingRecords",
        "scan.nativeDroppedRecords": "nativeDroppedRecords",
        "provider.enterWorldMap": "enterWorldMap",
        "provider.startMapScan": "startMapScan",
        "provider.stopMapScan": "stopMapScan",
        "provider.nativeStart": "XluaBridgeNativeStart",
        "provider.captureRun": "__XLUA_BRIDGE_NATIVE_WORLD_CAPTURE_RUN",
        "provider.captureFunction": "__XluaBridgeNativeWorldCapture",
        "provider.scanTick": "XluaBridgeMapScanTick",
        "provider.nativeUpdate": "XluaBridgeNativeUpdate",
        "provider.poll": "XluaBridgePoll",
        "source.commandsMap": "src\\commands\\map.rs",
        "source.mapScan": "src\\services\\map_scan.rs",
        "source.mapStore": "src\\services\\map_store.rs",
    }
    legacy_revalidation = {}
    for key, marker in legacy_markers.items():
        evidence = marker_presence(surface, marker)
        legacy_revalidation[key] = {
            "marker": marker,
            "classification": "UNCHANGED_MARKER_PRESENT" if evidence["present"] else "ABSENT_OR_CHANGED",
            **evidence,
        }

    return {
        "schema": 1,
        "findingId": "LWB317-RE-MAP-001-FRONTEND-HOST",
        "evidenceState": "EXACT_BYTES_STATIC_CONTRACT",
        "sources": {
            "index": {"path": str(index_path), "sha256": sha(index_path)},
            "mapPanel": {"path": str(panel_path), "sha256": sha(panel_path)},
            "surfaceDiscovery": {"path": str(surface_path), "sha256": surface_digest},
            "referenceSha256": surface["native"]["sha256"],
        },
        "bridge": {
            "invoke": "Tauri invoke payload envelope is {payload:<args>}; active profileId is injected into object args when absent",
            "errors": "object rejection with code becomes Error(code) with original fields assigned",
            "events": "profile-tagged {profileId,payload} events are filtered to the active profile; untagged events pass through",
            "locators": {"invokeHelper": loc(index, bridge_helper), "eventHelper": loc(index, listener_helper)},
        },
        "commands": commands,
        "events": surface["frontend"]["events"],
        "scanTypes": SCAN_TYPES,
        "scanStateDefault": scan_state_default,
        "query": {
            "fields": QUERY_FIELDS,
            "defaultPageSize": 50,
            "cityExportPageSize": 200,
            "defaultSorts": {kind: [{"sortBy": "updatedAt", "sortOrder": "desc"}] for kind in SCAN_TYPES},
            "sortKeys": SORT_KEYS,
            "sortClickCycle": "absent -> prepend desc; desc -> asc; asc -> remove; array order is priority",
            "pagination": "totalPages=max(1,ceil(total/50)); requested page above totalPages is clamped by setting page to last page",
            "locators": {"builder": loc(panel, "function nr("), "pageSize": loc(panel, "je=50"), "exportPageSize": loc(panel, "nr(1,200)")},
        },
        "preferences": {
            "manualScanMode": {"key": "lwbridge.mapScanMode", "accepted": ["normal", "fast"]},
            "includeForeignRadarTreasures": {"key": "lwbridge.mapIncludeForeignRadarTreasures", "default": False},
            "luckyTreasurePriority": {"key": "lwbridge.mapLuckyTreasurePriority", "default": True},
        },
        "autoScan": {
            "storageKey": "lwbridge.mapAutoScan.${profileId}",
            "default": {"enabled": False, "intervalMinutes": 60, "serverIds": [], "selectedTypes": ["truck", "railway", "dispatch", "ghost", "treasure"], "scanMode": "fast", "returnToOriginalServer": True, "nextRunAt": 0},
            "normalization": {"intervalMinutes": [20, 1440], "serverIdRange": [1, 99999], "serverIdLimit": 20, "scanModeFallback": "fast", "selectedTypeFallback": ["truck", "railway", "dispatch", "ghost", "treasure"]},
            "dueGate": "enabled && online && !scanActive && !autoWorkerBusy && now>=nextRunAt",
            "schedulerCadenceMs": 5000,
            "completionPollMs": 2000,
            "completionTimeoutMs": 2700000,
            "cycle": "capture current server; use configured servers or current; server_jump each; map_scan_start(resume=false); poll status until not reading; optionally return original; schedule next run",
        },
        "frontendRefresh": {
            "scanProgressRowThrottleMs": 1000,
            "scanComplete": "refresh rows and options when isReading transitions true->false",
            "events": ["bridge://player-mark-changed", "bridge://dispatch-plunder-changed", "bridge://truck-plunder-changed", "bridge://map-scan-status"],
        },
        "nativeSurface": {
            command: {"functions": native_functions(surface, command), "occurrences": marker_presence(surface, command)["occurrences"]}
            for command in commands
        },
        "legacyRevalidation": legacy_revalidation,
        "limits": [
            "UNCHANGED_MARKER_PRESENT means exact marker text survived in 0.3.17; it does not alone prove full 0.3.1 control flow is unchanged.",
            "Native handler/state-machine/storage semantics are promoted only by the subsequent 0.3.17 native inspectors.",
            "Live behavior is not claimed by this static contract.",
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("index", type=Path)
    parser.add_argument("map_panel", type=Path)
    parser.add_argument("surface", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = build(args.index, args.map_panel, args.surface)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
