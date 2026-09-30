#!/usr/bin/env python3
"""Read-only current-client Map compatibility evidence for LWB317-RE-MAP-001.

The legacy current-v19 inspectors stay immutable and fail closed on a changed
package hash. This Phase-2 tool instead validates the installed client through
the current compatibility gate first, then fingerprints the specific Map Lua
modules used by Dispatch/Treasure and records whether their decoded contracts
remain byte-identical to the previously recovered v19 modules.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / "run_live_resource_probe.py"
COMPAT = ROOT / "current_client_compat.py"
OVERVIEW = ROOT / "run_overview_bridge.py"


HISTORICAL_MODULES: dict[str, dict[str, Any]] = {
    "Net/Msgs/DispatchTask/DispatchStealMessage.luac": {
        "decodedSha256": "301b51900f73a95429823cdaf257f5f361f062ff3b7d5d1ac8cba2e7c3daba1a",
        "anchors": ["OnCreate", "PutLong", "uuid", "PutInt", "targetServer", "HandleMessage", "reward"],
    },
    "DataCenter/ActivityListData/ActDispatchTaskDataManager.luac": {
        "decodedSha256": "21cf16b7fe6da46f468a4d6f1f259ae6fd972a100ad9398c57a2e701abd8162d",
        "anchors": ["IsOpenCrossSteal", "GetTodayStealNum", "UpdateSteal"],
    },
    "UI/UIWorldPoint/Component/UIWorldPointBtn.luac": {
        "decodedSha256": "d2f099d11172a80e7c8eda63fd410d3360fea965d8269691061be5a7d8f54d79",
        "anchors": ["DispatchSteal", "GetWorldSuppliesPointDetailData", "WorldPointBtnType"],
    },
    "DataCenter/WorldPointDetail/WorldSuppliesPointData.luac": {
        "decodedSha256": "b51e78625e58221ef536a880a71d2b7e5bef883049e85df0563086818ba3fa4f",
        "anchors": ["CheckBtnState", "HasPlayer", "AlreadyGet", "reward_lucky_num"],
    },
    "DataCenter/WorldPointDetail/WorldChargeData.luac": {
        "decodedSha256": "b5bed9517de2381242c0fea7796baac190d332d2e0692e12bbe92ebd1addeb21",
        "anchors": ["GetPercent", "HasPlayer", "getReward"],
    },
    "DataCenter/SeasonManager/Activity/SeasonSuppliesShareDataManager.luac": {
        "decodedSha256": "301f040aa7fa336a1e991ce6fd1e69930db781bdb5336702a9629b41cd88660d",
        "anchors": ["GetActivityInfo", "IsSeasonActivityOpen", "GetServerCurrentSeasonConfig", "lw_supplies_refresh", "supplies_para"],
    },
    "UI/UIWorldPoint/Controller/UIWorldPointCtrl.luac": {
        "decodedSha256": "4e738cdd076dde56239461d3f011703885d1d4b55facf1c3aa283abcd8675f0e",
        "anchors": ["WorldGetSuppliesPointDetail", "GetWorldSuppliesPointDetailData", "IsHaveWorking"],
    },
    "Chat/NetMessage/ChatHeroDispatchShareCommand.luac": {
        "decodedSha256": "ad3f495a7bc87e55c05a3493d70b8cf680d8a72c3982e2d4079becfccf71909f",
        "anchors": ["PutLong", "uuid", "PutInt", "targetServer", "HandleMessage", "errorCode"],
    },
    "Chat/Controller/ChatController.luac": {
        "decodedSha256": "0ba3628a5bba07fafc317fd21b6297b4295c74b3a61d6135a4dbbf2e823be434",
        "anchors": ["TO_ALLIANCE", "Text_PointShare", "dispatch", "uuid", "sid", "ChatHeroDispatchShare", "SendSFSMessage"],
    },
    "Chat/WebMessage/Config/ChatMsgDefines.luac": {
        "decodedSha256": "ad7fd478b0925445240d49fac251ca172003052043f1591a716abbd01298e976",
        "anchors": ["ChatHeroDispatchShare", "hero.dispatch.share.chat", "Chat.NetMessage.ChatHeroDispatchShareCommand"],
    },
    "Chat/Other/ShareEncode.luac": {
        "decodedSha256": "9d379f701734bf4c20f1c29c57ea194dd6fb3b68f0cca712969edba5da562591",
        "anchors": ["x", "y", "sid", "uname", "abbr", "dispatch", "cfgId", "uuid"],
    },
    "Chat/Other/ShareDecode.luac": {
        "decodedSha256": "fe63553ea832a893ffecb22e1c9a458bbc28cba14b289191a43833bd12fe1ac6",
        "anchors": ["x", "y", "dispatch", "456288", "sid", "POSITION_COORDINATE_CROSS"],
    },
    "UI/UIPositionShare/Controller/UIPositionShareCtrl.luac": {
        "decodedSha256": "bce752b65980dee4f82f70767087133bb23e1578ffd77dd85648838c394082f0",
        "anchors": ["ShareType", "Pos", "IndexToTilePos", "World", "x", "y"],
    },
    "DataCenter/RadarCenterDataManager/DetectEventGetTreasureClaimInfo.luac": {
        "decodedSha256": "cb3df2e7ad2c34155cbb49c0d234e1d57a6320e6a64badb76b8af74825bafefe",
        "anchors": ["treasureClaimPlayerInfo", "remainNum"],
    },
    "DataCenter/RadarCenterDataManager/DetectEventTreasureClaimPlayerInfo.luac": {
        "decodedSha256": "6d4588297ba79ae9da9b9f7d135f5cab7aaa1f8276a748c62101d483892aac95",
        "anchors": ["uid", "isBigReward", "bigRewardMultiple", "hasLuckSiphonbuff"],
    },
    "DataCenter/LWRailway/Util/RailwayUtil.luac": {
        "decodedSha256": "59bafe773cec053e4616094625a1ee0b71972f2391d8b0c79bea85ea5c64b42f",
        "anchors": ["ClickAttackTrain", "TruckRob", "isTruckQuickRob"],
    },
    "DataCenter/LWRailway/Train/LWTrainDataManager.luac": {
        "decodedSha256": "77f4dbbd6d0f60980ad66aa9245464a2ea88f066e28da44de228bb8eb9f2f019",
        "anchors": ["marchUid", "uuid"],
    },
    "DataCenter/LWRailway/Station/LWMyStationDataManager.luac": {
        "decodedSha256": "94133ab998308e926ccbbae975376406dff3f0e61f035a7a1b4dee1ee4ae6865",
        "anchors": ["GetRobFormation", "TrySaveTruckFormation", "TryAttackTrain", "dailyRobCount"],
    },
    "DataCenter/LWFakePVPBattle/FakePVPLogic.luac": {
        "decodedSha256": "d7a0ed1d9bc43d916154ade556a73cec0dcb747ed00f10ea00e082c7a4e081e6",
        "anchors": ["OnGetBattleData", "JumpToEnd", "attackTrainReward"],
    },
    "DataCenter/LWBattle/LWBattleManager.luac": {
        "decodedSha256": "b3fd779bd480c9a8cfb4b0154e03c56b8f85eede233e63550cfada0733068543",
        "anchors": ["FakePVP"],
    },
    "Net/Msgs/Railway/AttackTrainMessage.luac": {
        "decodedSha256": "9f7e6bdcf200ec91ffe5d37b72343957553bb9a0f1be9fe2ac355c5139614066",
        "anchors": ["TrainSkirmishDataReceived", "TrainAttackReceived", "dailyRobCount"],
    },
    "UI/UILWHero/UIHeroFakePVPFormationPanel/View/UIHeroFakePVPFormationPanelView.luac": {
        "decodedSha256": "3f9e704543194be7d056c00871c32ab7c2c19b65afd8a51184865d64cc2294b1",
        "anchors": ["TrySaveTruckFormation", "TryAttackTrain"],
    },
}


def load(path: Path, name: str):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"could not load {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def normalize_verified_overview_wrapper(probe, compat, overview, paths, installed_identity, entries):
    """Return an in-memory official-package view for a known current Overview install.

    The installed Overview bridge deliberately preserves the official LuaEntry and
    adds one packaged probe module. Static compatibility must be able to inspect
    that state without rewriting the user's installed package. Any unrecognized
    wrapper state remains fail-closed.
    """
    if installed_identity.get("ok") is True:
        return installed_identity, list(entries), {
            "state": "official_package",
            "normalized": False,
        }

    expected_problems = {
        "script package already contains the LWBridge preserved-entry marker",
        "critical Lua entry changed: DataCenter/Global/LuaEntry.luac",
    }
    problems = set(installed_identity.get("problems", []))
    if problems != expected_problems:
        raise RuntimeError(
            "current client identity gate failed outside the recognized Overview wrapper state: "
            + "; ".join(installed_identity.get("problems", []))
        )

    entry_map = dict(entries)
    wrapped = entry_map.get(probe.LUA_ENTRY)
    original = entry_map.get(probe.ORIGINAL_LUA_ENTRY)
    packaged_probe = entry_map.get(overview.RESOURCE_PROBE_ENTRY)
    if wrapped is None or original is None or packaged_probe is None:
        raise RuntimeError("recognized Overview wrapper entries are incomplete")

    wrapper_plain = probe.decode_lenc(wrapped)
    probe_plain = probe.decode_lenc(packaged_probe)
    expected_wrapper = overview.wrapper_source()
    expected_probe = overview.packaged_resource_probe_source()
    if wrapper_plain != expected_wrapper:
        raise RuntimeError("installed LuaEntry wrapper does not match the current Overview bridge source")
    if probe_plain != expected_probe:
        raise RuntimeError("installed packaged resource probe does not match the current Overview bridge source")

    expected_lua_entry = compat.CRITICAL_ENTRIES[probe.LUA_ENTRY]
    original_hash = sha256(original)
    if original_hash != expected_lua_entry:
        raise RuntimeError(
            "preserved official LuaEntry does not match the recovered supported source: " + original_hash
        )

    normalized_entries: list[tuple[str, bytes]] = []
    for name, raw in entries:
        if name in (probe.ORIGINAL_LUA_ENTRY, overview.RESOURCE_PROBE_ENTRY):
            continue
        if name == probe.LUA_ENTRY:
            raw = original
        normalized_entries.append((name, raw))

    file_version, content_version, _ = probe.read_lwlf(paths["data"])
    with tempfile.TemporaryDirectory() as directory:
        normalized_path = Path(directory) / "LWScripts.data"
        probe.write_lwlf(normalized_path, file_version, content_version, normalized_entries)
        normalized_package = {
            "packageSha256": probe.sha256_file(normalized_path),
            "packageSize": normalized_path.stat().st_size,
            "packageCrc32": probe.crc32_file(normalized_path),
            "fileVersion": file_version,
            "contentVersion": content_version,
            "entryCount": len(normalized_entries),
        }

    installed_observed = dict(installed_identity.get("observed", {}))
    critical_entries = dict(installed_observed.get("criticalEntries", {}))
    critical_entries[probe.LUA_ENTRY] = original_hash
    normalized_observed = {
        **normalized_package,
        "gameSha256": installed_observed.get("gameSha256"),
        "xluaSha256": installed_observed.get("xluaSha256"),
        "assemblyCSharpSha256": installed_observed.get("assemblyCSharpSha256"),
        "luaEntrySha256": original_hash,
        "criticalEntries": critical_entries,
        "version": installed_observed.get("version"),
        "versionMarker": installed_observed.get("versionMarker"),
        "versionMarkerMatchesContentVersion": installed_observed.get("versionMarkerMatchesContentVersion"),
    }
    normalized_identity = {
        "ok": True,
        "compatibilityPolicy": compat.POLICY,
        "observed": normalized_observed,
        "problems": [],
    }
    wrapper = {
        "state": "verified_current_overview_wrapper",
        "normalized": True,
        "installedPackageSha256": installed_observed.get("packageSha256"),
        "installedPackageSize": installed_observed.get("packageSize"),
        "installedEntryCount": installed_observed.get("entryCount"),
        "preservedOfficialLuaEntrySha256": original_hash,
        "wrapperPlaintextSha256": sha256(wrapper_plain),
        "packagedProbePlaintextSha256": sha256(probe_plain),
        "normalizedOfficialPackage": normalized_package,
    }
    return normalized_identity, normalized_entries, wrapper


def compare_baseline(probe, baseline_path: Path, current_entries: list[tuple[str, bytes]]) -> dict[str, Any]:
    file_version, content_version, baseline_entries = probe.read_lwlf(baseline_path)
    baseline_map = dict(baseline_entries)
    current_map = dict(current_entries)
    baseline_names = set(baseline_map)
    current_names = set(current_map)
    added = sorted(current_names - baseline_names)
    removed = sorted(baseline_names - current_names)
    changed = sorted(
        name for name in baseline_names & current_names
        if sha256(baseline_map[name]) != sha256(current_map[name])
    )
    return {
        "path": str(baseline_path),
        "packageSha256": probe.sha256_file(baseline_path),
        "fileVersion": file_version,
        "contentVersion": content_version,
        "entryCount": len(baseline_entries),
        "added": added,
        "removed": removed,
        "changed": changed,
        "addedCount": len(added),
        "removedCount": len(removed),
        "changedCount": len(changed),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--baseline-package", type=Path)
    parser.add_argument("--require-content-version", type=int)
    parser.add_argument("--finding-id", default="LWB317-RE-MAP-006-CURRENT-CLIENT-STATIC")
    args = parser.parse_args()

    probe = load(BASE, "lwb317_current_probe")
    compat = load(COMPAT, "lwb317_current_compat")
    overview = load(OVERVIEW, "lwb317_current_overview")
    paths = probe.paths()
    file_version, content_version, entries = probe.read_lwlf(paths["data"])
    installed_identity = compat.inspect_current(probe, paths)
    try:
        identity, normalized_entries, package_state = normalize_verified_overview_wrapper(
            probe, compat, overview, paths, installed_identity, entries)
    except RuntimeError as exc:
        raise SystemExit(str(exc)) from exc
    if args.require_content_version is not None and content_version != args.require_content_version:
        raise SystemExit(
            f"current client content version {content_version} != required {args.require_content_version}"
        )

    entry_map = dict(normalized_entries)
    modules: dict[str, Any] = {}
    for name, expected in HISTORICAL_MODULES.items():
        raw = entry_map.get(name)
        if raw is None:
            modules[name] = {"present": False, "historicalDecodedSha256": expected["decodedSha256"]}
            continue
        decoded = probe.decode_lenc(raw)
        digest = sha256(decoded)
        missing = [anchor for anchor in expected["anchors"] if anchor.encode("utf-8") not in decoded]
        modules[name] = {
            "present": True,
            "encodedSha256": sha256(raw),
            "decodedSha256": digest,
            "decodedBytes": len(decoded),
            "historicalDecodedSha256": expected["decodedSha256"],
            "byteIdenticalToHistoricalModule": digest == expected["decodedSha256"],
            "anchors": expected["anchors"],
            "missingAnchors": missing,
            "anchorsPresent": not missing,
        }

    modules_compatible = all(
        module.get("present") is True
        and module.get("byteIdenticalToHistoricalModule") is True
        and module.get("anchorsPresent") is True
        for module in modules.values()
    )
    baseline = None
    if args.baseline_package is not None:
        baseline_path = args.baseline_package.resolve()
        if not baseline_path.is_file():
            raise SystemExit(f"baseline package does not exist: {baseline_path}")
        baseline = compare_baseline(probe, baseline_path, normalized_entries)

    result = {
        "schema": 1,
        "findingId": args.finding_id,
        "evidenceState": "CURRENT_CLIENT_STATIC_HEADLESS",
        "installedIdentity": installed_identity,
        "identity": identity,
        "packageState": package_state,
        "package": {
            "path": str(paths["data"]),
            "fileVersion": file_version,
            "contentVersion": content_version,
            "entryCount": len(entries),
        },
        "baseline": baseline,
        "modules": modules,
        "classification": {
            "trackedModulesCompatible": modules_compatible,
            "worldAoiRuntime": "Assembly-CSharp.rdl identity is covered by the current compatibility policy; structural WorldPointManager/WorldGetBlock checks remain a separate read-only check",
            "dispatchTreasureTruckSources": "per-module exact current fingerprint above; historical equality is comparison evidence only",
            "liveProofRequired": True,
        },
        "limits": [
            "This tool is read-only and does not launch or control Last War or rewrite its installed package.",
            "A verified current Overview wrapper is normalized only in memory by restoring its preserved official LuaEntry and excluding the repository-owned packaged probe entry.",
            "Byte-identical historical modules establish current source compatibility for those modules, not original LWBridge 0.3.17 provider implementation identity.",
            "Navigation, server switching, acquisition completion and state-changing Treasure/Plunder actions still require bounded live proof where current game state permits.",
        ],
    }
    text = json.dumps(result, indent=2) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(text, encoding="utf-8")
    else:
        print(text, end="")
    return 0 if identity.get("ok") is True and modules_compatible else 2


if __name__ == "__main__":
    raise SystemExit(main())
