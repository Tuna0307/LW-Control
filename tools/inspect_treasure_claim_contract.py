#!/usr/bin/env python3
"""Pin the original Treasure-claim host boundary and current-v19 direct claim authority."""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path

EXPECTED_EXE_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
EXPECTED_PACKAGE_SHA256 = "e7c5742a44d5f5862e4eb6c94944b4150969b6c4bd0a1c1cb9a337cfd1141fa2"

ORIGINAL_OFFSETS = {
    "claimScope": 0x8221D5,
    "eligible": 0x8221EE,
    "queued": 0x8221F6,
    "skipped": 0x8221FC,
    "directQueued": 0x822203,
    "scoutQueued": 0x82220F,
    "scoutDispatched": 0x82221A,
    "claimed": 0x822229,
    "noScoutSkipped": 0x822230,
    "otherAllianceSkipped": 0x82223E,
    "failed": 0x822252,
    "invalidScope": 0x822274,
    "prioritizeLuckySlots": 0x8222D4,
    "claimTreasures": 0x822307,
    "getTreasureClaimStatus": 0x822630,
    "claimCandidateSql": 0xC86F98,
    "seasonSuppliesSql": 0xC88230,
}

CLAIM_CANDIDATE_SQL = """SELECT data_json FROM map_records
                 WHERE kind='treasure' AND server_id=?1
                   AND (
                     COALESCE(CAST(json_extract(data_json,'$.suppliesType') AS INTEGER),0) IN (1,3,4)
                     OR (
                       COALESCE(CAST(json_extract(data_json,'$.suppliesType') AS INTEGER),0)=0
                       AND COALESCE(CAST(json_extract(data_json,'$.complete') AS INTEGER),0)=1
                     )
                   )
                   AND uuid IS NOT NULL AND TRIM(uuid)<>'' AND uuid<>'0'
                   AND (COALESCE(CAST(json_extract(data_json,'$.expireTime') AS INTEGER),0)<=0
                     OR CAST(json_extract(data_json,'$.expireTime') AS INTEGER)>?2)
                 ORDER BY point_index ASC"""

SEASON_SUPPLIES_SQL = """SELECT data_json FROM map_records
                 WHERE kind='treasure' AND server_id=?1
                   AND COALESCE(CAST(json_extract(data_json,'$.suppliesType') AS INTEGER),0) IN (1,3,4)
                   AND uuid IS NOT NULL AND TRIM(uuid)<>'' AND uuid<>'0'
                 ORDER BY point_index ASC"""

MODULES = {
    "claimMessage": (
        "Net/Msgs/RadarCenter/DetectEventClaimTreasureMessage.luac",
        "ecceef817b99de8ce94518e910e3d4c773dbf1d0825a4878c87d00d59b0b4499",
    ),
    "pushMessage": (
        "Net/Msgs/RadarCenter/PushDetectTreasureClaimMessage.luac",
        "e14bafd7b714c7703cd52d35ab60acc5b6f10aec3721184866ba1f590cc103ef",
    ),
    "claimInfoMessage": (
        "Net/Msgs/RadarCenter/DetectEventGetTreasureClaimInfoMessage.luac",
        "a8216497185f8a12ccf5d9608400c0070f6cae692d0a2007b2adf770f1f853ff",
    ),
    "claimInfoData": (
        "DataCenter/RadarCenterDataManager/DetectEventGetTreasureClaimInfo.luac",
        "cb3df2e7ad2c34155cbb49c0d234e1d57a6320e6a64badb76b8af74825bafefe",
    ),
    "uiUtil": (
        "Util/UIUtil.luac",
        "53cdc12c7bc7ee4172322b6945ca1e1f780b6e1263742a27a5e707cfb244c1a8",
    ),
    "worldButton": (
        "UI/UIWorldPoint/Component/UIWorldPointBtn.luac",
        "d2f099d11172a80e7c8eda63fd410d3360fea965d8269691061be5a7d8f54d79",
    ),
    "marchUtil": (
        "Util/MarchUtil.luac",
        "80effa59728e223053bca2b99ddfdd963cc0c98d2ec420972a5100a5e23990b4",
    ),
}


def fail(message: str) -> None:
    raise SystemExit(message)


def require(condition: bool, message: str) -> None:
    if not condition:
        fail(message)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=Path)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    blob = args.binary.read_bytes()
    digest = hashlib.sha256(blob).hexdigest()
    require(digest == EXPECTED_EXE_SHA256, f"unexpected LWBridge SHA-256: {digest}")

    markers = {
        "claimScope": b"claimScope",
        "eligible": b"eligible",
        "queued": b"queued",
        "skipped": b"skipped",
        "directQueued": b"directQueued",
        "scoutQueued": b"scoutQueued",
        "scoutDispatched": b"scoutDispatched",
        "claimed": b"claimed",
        "noScoutSkipped": b"noScoutSkipped",
        "otherAllianceSkipped": b"otherAllianceSkipped",
        "failed": b"failed",
        "invalidScope": b"treasure claim scope is invalid",
        "prioritizeLuckySlots": b"prioritizeLuckySlots",
        "claimTreasures": b"claimTreasures",
        "getTreasureClaimStatus": b"getTreasureClaimStatus",
        "claimCandidateSql": CLAIM_CANDIDATE_SQL.encode("ascii"),
        "seasonSuppliesSql": SEASON_SUPPLIES_SQL.encode("ascii"),
    }
    for name, expected in ORIGINAL_OFFSETS.items():
        value = markers[name]
        require(
            blob[expected : expected + len(value)] == value,
            f"original marker {name} moved from 0x{expected:X}",
        )

    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import inspect_lwbridge_map_scan as original
    import inspect_current_dispatch_plunder_sources as lua
    import run_live_resource_probe as package

    report = original.inspect(args.binary, (), dump_rva=0x1686E4)
    for fragment in (
        "0x65786f62",
        "0x73616573",
        "0x6e6f",
        "0x676e6973",
        "0x656c",
        "test    rdx, rdx",
        "'claimTreasures'",
        "0x1388",
    ):
        require(fragment in report, f"original claim wrapper missing {fragment}")

    package_path = Path(package.paths()["data"])
    package_digest = hashlib.sha256(package_path.read_bytes()).hexdigest()
    require(
        package_digest == EXPECTED_PACKAGE_SHA256,
        f"unexpected current LWScripts.data SHA-256: {package_digest}",
    )
    file_version, content_version, entries = package.read_lwlf(package_path)
    entry_map = {name.replace("\\", "/"): body for name, body in entries}

    parsed: dict[str, dict] = {}
    module_rows = {}
    for key, (name, expected_hash) in MODULES.items():
        require(name in entry_map, f"current package missing {name}")
        decoded = package.decode_lenc(entry_map[name])
        module_hash = hashlib.sha256(decoded).hexdigest()
        require(module_hash == expected_hash, f"unexpected decoded SHA-256 for {name}")
        parsed[key] = lua.parse_chunk(decoded)
        module_rows[key] = {"path": name, "sha256": module_hash}

    claim_children = parsed["claimMessage"]["children"]
    require(len(claim_children) >= 2, "claim message prototypes missing")
    create_strings = lua.string_constants(claim_children[0])
    response_strings = lua.string_constants(claim_children[1])
    for value in ("PutLong", "uuid", "PutInt", "targetServer", "type"):
        require(value in create_strings, f"claim OnCreate missing {value}")
    for value in (
        "errorCode",
        "reward",
        "RewardManager",
        "AddRewardsAndRes",
        "ActDetectTreasureDataManager",
        "OnGetDigTimesMsg",
    ):
        require(value in response_strings, f"claim response missing {value}")

    push_strings = lua.string_constants(parsed["pushMessage"]["children"][1])
    for value in ("errorCode", "uuid", "WorldBuildTopBubbleTreasureGet"):
        require(value in push_strings, f"push claim response missing {value}")

    info_methods = lua.root_methods(parsed["claimInfoData"])
    self_state = info_methods.get("GetSelfClaimState")
    require(self_state is not None, "GetSelfClaimState missing")
    self_state_strings = lua.string_constants(self_state)
    for value in ("treasureClaimPlayerInfoList", "DoubleClaim", "NormalClaim", "NoClaim"):
        require(value in self_state_strings, f"claim-info self state missing {value}")

    ui_protos = parsed["uiUtil"]["children"]
    require(len(ui_protos) > 119, "UIUtil Treasure prototypes missing")
    direct_strings = lua.string_constants(ui_protos[116])
    click_strings = lua.string_constants(ui_protos[119])
    for value in ("IsHaveGetReward", "DetectEventClaimTreasure", "uuid", "serverId"):
        require(value in direct_strings, f"direct Treasure helper missing {value}")
    for value in (
        "CheckInteractAuthority",
        "IsHaveGetReward",
        "IsReceiveAllReward",
        "DetectEventGetTreasureClaimInfo",
        "max_reward_times",
    ):
        require(value in click_strings, f"Treasure click gate missing {value}")

    world_methods = lua.root_methods(parsed["worldButton"])
    supplies = world_methods.get("WorldSupplies")
    require(supplies is not None, "UIWorldPointBtn.WorldSupplies missing")
    supplies_strings = lua.string_constants(supplies)
    for value in ("CheckBtnState", "LaunchScout", "SCOUT_SUPPLIES"):
        require(value in supplies_strings, f"Supplies claim path missing {value}")

    march = lua.root_methods(parsed["marchUtil"]).get("LaunchScout")
    require(march is not None, "MarchUtil.LaunchScout missing")
    march_strings = lua.string_constants(march)
    for value in ("GetFreeScoutFormation", "StartMarch"):
        require(value in march_strings, f"LaunchScout missing {value}")

    result = {
        "schemaVersion": 1,
        "findingId": "LWB-R7-086",
        "date": "2026-09-20",
        "status": "RECOVERED/OFFLINE-CONTRACT",
        "original": {
            "sha256": digest,
            "claimServiceRva": "0x1686E4-0x16A011",
            "claimScopes": ["boxes", "season", "single"],
            "prioritizeLuckySlotsDefault": True,
            "claimCommand": "claimTreasures",
            "claimCommandTimeoutMs": 5000,
            "statusCommand": "getTreasureClaimStatus",
            "statusCommandTimeoutMs": 5000,
            "immediateResultCounters": [
                "eligible", "queued", "skipped", "directQueued", "scoutQueued",
                "scoutDispatched", "claimed", "noScoutSkipped",
                "otherAllianceSkipped", "failed",
            ],
            "claimCandidateSqlOffset": "0x00C86F98",
            "seasonSupplySqlOffset": "0x00C88230",
        },
        "currentV19": {
            "packageSha256": package_digest,
            "fileVersion": file_version,
            "contentVersion": content_version,
            "modules": module_rows,
            "directClaim": {
                "command": "detect.event.claim.treasure",
                "request": ["uuid:PutLong", "targetServer:PutInt", "type:optional PutInt"],
                "ordinaryUiSend": ["uuid", "serverId"],
                "duplicateGate": "IsHaveGetReward(localUid)",
                "success": "response has no errorCode; reward is optional",
                "successMutations": [
                    "RewardManager.AddRewardsAndRes",
                    "ActDetectTreasureDataManager.OnGetDigTimesMsg",
                ],
                "pushSignal": "WorldBuildTopBubbleTreasureGet(uuid)",
            },
            "claimInfo": {
                "authoritativePlayerMembership": "treasureClaimPlayerInfoList",
                "selfStates": ["DoubleClaim", "NormalClaim", "NoClaim"],
            },
            "supplies": {
                "route": "UIWorldPointBtn.WorldSupplies -> CheckBtnState -> MarchUtil.LaunchScout(...SCOUT_SUPPLIES) -> StartMarch",
            },
        },
        "unknownBlocked": [
            "protected claimTreasures scope-specific candidate filtering/order",
            "protected lucky-slot prioritization formula and duplicate suppression",
            "protected scout-slot selection/reservation and batch state transition schema",
            "exact mapping from bridge status batch to all queued/direct/scout terminal states",
        ],
        "safety": "No claim, scout march, collection, or other state-changing game action was executed.",
    }

    if args.json:
        print(json.dumps(result, indent=2))
    else:
        print("PASS Treasure claim host/current-v19 contract inspection")
        print(f"binarySha256={digest}")
        print(f"packageSha256={package_digest}")
        print("claimScopes=boxes,season,single")
        print("claimTreasuresTimeoutMs=5000 statusTimeoutMs=5000")
        print("directClaim=detect.event.claim.treasure uuid:PutLong targetServer:PutInt")
        print("supplies=CheckBtnState -> LaunchScout(SCOUT_SUPPLIES) -> StartMarch")
        print("protectedClaimTreasuresOrchestration=UNKNOWN/BLOCKED")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
