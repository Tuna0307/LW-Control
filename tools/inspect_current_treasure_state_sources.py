#!/usr/bin/env python3
"""Pin current-v19 Treasure/Supplies read-only state source anchors."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

import run_live_resource_probe as probe

EXPECTED_PACKAGE_SHA256 = "e7c5742a44d5f5862e4eb6c94944b4150969b6c4bd0a1c1cb9a337cfd1141fa2"
EXPECTED_FILE_VERSION = 3
EXPECTED_CONTENT_VERSION = 19

MODULES = {
    "DataCenter/WorldPointDetail/WorldSuppliesPointData.luac": {
        "encoded": "42295ef48ea4067f538dd010bde8e5a9f4ba708716d646b2683e75107f6f69b3",
        "decoded": "b51e78625e58221ef536a880a71d2b7e5bef883049e85df0563086818ba3fa4f",
        "anchors": ["CheckBtnState", "HasPlayer", "AlreadyGet", "reward_lucky_num"],
    },
    "DataCenter/WorldPointDetail/WorldChargeData.luac": {
        "encoded": "30dcd64d2a8a0866e82f19305992e340e3706f40de53d7ab0263d2f3696e7346",
        "decoded": "b5bed9517de2381242c0fea7796baac190d332d2e0692e12bbe92ebd1addeb21",
        "anchors": ["GetPercent", "HasPlayer", "getReward"],
    },
    "DataCenter/SeasonManager/Activity/SeasonSuppliesShareDataManager.luac": {
        "encoded": "b5ef00c90b08ec2f12a80c6b628925cf0920af3be7c3113f519091bd1190c4b8",
        "decoded": "301f040aa7fa336a1e991ce6fd1e69930db781bdb5336702a9629b41cd88660d",
        "anchors": [
            "GetActivityInfo",
            "IsSeasonActivityOpen",
            "GetServerCurrentSeasonConfig",
            "lw_supplies_refresh",
            "supplies_para",
        ],
    },
    "UI/UIWorldPoint/Controller/UIWorldPointCtrl.luac": {
        "encoded": "7298256e4a5181a335cfd2ce8c89c9454452211b2feab68eac57827e0465af6d",
        "decoded": "4e738cdd076dde56239461d3f011703885d1d4b55facf1c3aa283abcd8675f0e",
        "anchors": ["WorldGetSuppliesPointDetail", "GetWorldSuppliesPointDetailData", "IsHaveWorking"],
    },
    "UI/UIWorldPoint/Component/UIWorldPointBtn.luac": {
        "encoded": "31fa9297595702c199e5bd23c90e8aa727236e371d7c461f3eb6639f5c580748",
        "decoded": "d2f099d11172a80e7c8eda63fd410d3360fea965d8269691061be5a7d8f54d79",
        "anchors": ["WorldPointBtnType", "GetWorldSuppliesPointDetailData"],
    },
    "DataCenter/RadarCenterDataManager/DetectEventGetTreasureClaimInfo.luac": {
        "encoded": "e954090f9f975acf4130acb9a2b311437b991685298ea6661a12402bf9762218",
        "decoded": "cb3df2e7ad2c34155cbb49c0d234e1d57a6320e6a64badb76b8af74825bafefe",
        "anchors": ["treasureClaimPlayerInfo", "remainNum"],
    },
    "DataCenter/RadarCenterDataManager/DetectEventTreasureClaimPlayerInfo.luac": {
        "encoded": "7d918668f89c356c31cdb3689159de22a1864a505c3846c8ef93d9963e94a420",
        "decoded": "6d4588297ba79ae9da9b9f7d135f5cab7aaa1f8276a748c62101d483892aac95",
        "anchors": ["uid", "isBigReward", "bigRewardMultiple", "hasLuckSiphonbuff"],
    },
}


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def main() -> int:
    paths = probe.paths()
    package_path = Path(paths["data"])
    package = package_path.read_bytes()
    package_sha = digest(package)
    if package_sha != EXPECTED_PACKAGE_SHA256:
        raise SystemExit(f"unexpected LWScripts.data SHA-256: {package_sha}")

    file_version, content_version, entries = probe.read_lwlf(package_path)
    if file_version != EXPECTED_FILE_VERSION or content_version != EXPECTED_CONTENT_VERSION:
        raise SystemExit(
            f"unexpected package version: file={file_version}, content={content_version}"
        )
    entry_map = dict(entries)

    observed_modules: dict[str, object] = {}
    for name, expected in MODULES.items():
        raw = entry_map.get(name)
        if raw is None:
            raise SystemExit(f"missing current-v19 Lua entry: {name}")
        decoded = probe.decode_lenc(raw)
        encoded_sha = digest(raw)
        decoded_sha = digest(decoded)
        if encoded_sha != expected["encoded"]:
            raise SystemExit(
                f"unexpected encoded SHA-256 for {name}: {encoded_sha}"
            )
        if decoded_sha != expected["decoded"]:
            raise SystemExit(
                f"unexpected decoded SHA-256 for {name}: {decoded_sha}"
            )
        missing = [
            anchor for anchor in expected["anchors"]
            if anchor.encode("utf-8") not in decoded
        ]
        if missing:
            raise SystemExit(f"missing source anchors in {name}: {missing}")
        observed_modules[name] = {
            "encodedSha256": encoded_sha,
            "decodedSha256": decoded_sha,
            "decodedBytes": len(decoded),
            "anchors": expected["anchors"],
        }

    result = {
        "schemaVersion": 1,
        "findingId": "LWB-R7-069",
        "date": "2026-09-20",
        "package": {
            "path": str(package_path),
            "sha256": package_sha,
            "fileVersion": file_version,
            "contentVersion": content_version,
            "entryCount": len(entries),
        },
        "modules": observed_modules,
        "sourceBackedFacts": [
            "ordinary Treasure UI uses TreasurePointInfo.IsHaveWorking(playerUid)",
            "Supplies detail is requested with WorldGetSuppliesPointDetail and read via GetWorldSuppliesPointDetailData",
            "WorldSuppliesPointData.CheckBtnState owns CanGet/AlreadyGet/Limit/Over eligibility",
            "WorldSuppliesPointData.HasPlayer delegates to chargeData.HasPlayer for charging supplies",
            "WorldChargeData exposes per-player getReward plus charge percentage",
            "SeasonSuppliesShareDataManager resolves current-server season config and Supplies activity state through GetServerCurrentSeasonConfig/GetActivityInfo/IsSeasonActivityOpen",
            "Season Supplies configuration consumes lw_supplies_refresh and supplies_para from the current server season config",
        ],
        "limits": [
            "This inspector pins package/module identity and source anchors; it does not infer unrecovered claimPriority.",
            "State-changing claim/scout/march behavior is outside this read-only checkpoint.",
        ],
    }
    print(json.dumps(result, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
