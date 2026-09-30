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
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / "run_live_resource_probe.py"
COMPAT = ROOT / "current_client_compat.py"


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


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    probe = load(BASE, "lwb317_current_probe")
    compat = load(COMPAT, "lwb317_current_compat")
    paths = probe.paths()
    identity = compat.inspect_current(probe, paths)
    if identity.get("ok") is not True:
        raise SystemExit("current client identity gate failed: " + "; ".join(identity.get("problems", [])))

    file_version, content_version, entries = probe.read_lwlf(paths["data"])
    entry_map = dict(entries)
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

    result = {
        "schema": 1,
        "findingId": "LWB317-RE-MAP-006-CURRENT-CLIENT-STATIC",
        "evidenceState": "CURRENT_CLIENT_STATIC_HEADLESS",
        "identity": identity,
        "package": {
            "path": str(paths["data"]),
            "fileVersion": file_version,
            "contentVersion": content_version,
            "entryCount": len(entries),
        },
        "modules": modules,
        "classification": {
            "worldAoiRuntime": "compatible when check_current_client_runtime_contract.py passes",
            "dispatchTreasureTruckSources": "per-module exact current fingerprint above; historical equality is comparison evidence only",
            "liveProofRequired": True,
        },
        "limits": [
            "This tool is read-only and does not launch or control Last War.",
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
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
