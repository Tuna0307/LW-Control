#!/usr/bin/env python3
"""Repeatable offline validator for LWB317-FUNCTION-MAP-PROVIDER-SEMANTICS-003."""
from __future__ import annotations

import hashlib
import importlib.util
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
INSPECTOR = ROOT / "tools" / "lwbridge317" / "inspect_map_provider_semantics.py"
_spec = importlib.util.spec_from_file_location("_provider_semantics", INSPECTOR)
if _spec is None or _spec.loader is None:
    raise RuntimeError("unable to load provider semantic inspector")
sem = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(sem)

REFERENCE = Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
REFERENCE_SHA256 = "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"

EXPECTED = {
    "Net/Msgs/RadarCenter/DetectEventClaimTreasureMessage.luac": {
        "decodedSha256": "ecceef817b99de8ce94518e910e3d4c773dbf1d0825a4878c87d00d59b0b4499",
        "methods": {
            "OnCreate": ("root/child[0]", [10, 22], ["uuid", "targetServer", "type"]),
            "HandleMessage": ("root/child[1]", [24, 71], ["errorCode", "OnGetDigTimesMsg"]),
        },
    },
    "Net/Msgs/RadarCenter/DetectEventGetTreasureClaimInfoMessage.luac": {
        "decodedSha256": "a8216497185f8a12ccf5d9608400c0070f6cae692d0a2007b2adf770f1f853ff",
        "methods": {
            "OnCreate": ("root/child[0]", [11, 23], ["uuid", "source", "targetServer"]),
            "HandleMessage": ("root/child[1]", [25, 32], ["errorCode", "GetDetectEventTreasureClaimInfo"]),
        },
    },
    "DataCenter/RadarCenterDataManager/DetectEventGetTreasureClaimInfo.luac": {
        "decodedSha256": "cb3df2e7ad2c34155cbb49c0d234e1d57a6320e6a64badb76b8af74825bafefe",
        "methods": {
            "InitData": ("root/child[3]", [37, 88], ["remainNum", "treasureClaimPlayerInfoList"]),
            "GetSelfClaimState": ("root/child[4]", [91, 104], ["DoubleClaim", "NormalClaim", "NoClaim"]),
        },
    },
    "DataCenter/ActMonopolyManager/ActDetectTreasureDataManager.luac": {
        "decodedSha256": "04a5b7f426b65bb9af1d1bed9855a37ccf0fe33acbdc0e50d02e2df79dfd3645",
        "methods": {
            "OnGetDigTimesMsg": ("root/child[4]", [70, 117], ["treasureRewardLimit"]),
            "CheckTreasureReachDailyLimit": ("root/child[5]", [119, 134], ["dailyGot"]),
            "OnPassDay": ("root/child[6]", [136, 138], ["dailyGot"]),
        },
    },
    "UI/UIWorldPoint/Component/UIWorldPointBtn.luac": {
        "decodedSha256": "d2f099d11172a80e7c8eda63fd410d3360fea965d8269691061be5a7d8f54d79",
        "methods": {
            "WorldSupplies": ("root/child[21]", [3432, 3451], ["SCOUT_SUPPLIES", "LaunchScout"]),
        },
    },
    "Util/MarchUtil.luac": {
        "decodedSha256": "80effa59728e223053bca2b99ddfdd963cc0c98d2ec420972a5100a5e23990b4",
        "methods": {
            "LaunchScout": ("root/child[59]", [1740, 1797], ["GetFreeScoutFormation", "StartMarch"]),
            "OnLaunchMarchSuccess": ("root/child[58]", [1709, 1737], ["IsScoutMarch"]),
        },
    },
    "DataCenter/ActivityListData/ActGhostrecon/ActGhostreconTaskTemplate.luac": {
        "decodedSha256": "015d156c9fc4a3d245718669c113afb23ce04e35b44b057e9abf52d405e865c8",
        "methods": {
            "CheckCanSteal": ("root/child[8]", [196, 199], ["protectTime", "GetServerTime"]),
        },
    },
    "Net/Msgs/Ghostrecon/GhostReconStealMessage.luac": {
        "decodedSha256": "db52842471d2d276dc374236f274214657b70d22770722f4b2883cf6a756a1a3",
        "methods": {
            "OnCreate": ("root/child[0]", [11, 15], ["uuid", "ownerServer"]),
            "HandleMessage": ("root/child[1]", [18, 29], ["errorCode", "GhostReconStealHandler"]),
        },
    },
    "Net/SFSNetwork.luac": {
        "decodedSha256": "f9c2762ef603df799da3b502a0c63a50454f97a5a8a6949a39f5948cf0a8fc36",
        "methods": {
            "SendMessage": ("root/child[1]", [25, 34], ["NewMessage", "SendLuaMessage"]),
            "HandleMessage": ("root/child[2]", [36, 54], ["xpcall"]),
        },
    },
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def main() -> int:
    require(REFERENCE.is_file(), f"reference missing: {REFERENCE}")
    require(sha256(REFERENCE) == REFERENCE_SHA256, "0.3.17 reference hash changed")

    entry_map, package = sem.load_package(Path(sem.probe.paths()["data"]))
    checks: list[dict[str, object]] = []
    for entry, expected in EXPECTED.items():
        row = sem.inspect_entry(entry_map, entry)
        require(row["decodedSha256"] == expected["decodedSha256"], f"decoded hash changed: {entry}")
        for method, (prototype, lines, constants) in expected["methods"].items():
            actual = row["methods"].get(method)
            require(actual is not None, f"missing method {entry}::{method}")
            require(actual["prototypePath"] == prototype, f"prototype moved: {entry}::{method}")
            require(actual["lines"] == lines, f"line range moved: {entry}::{method}")
            strings = {value for value in actual["constants"] if isinstance(value, str)}
            for constant in constants:
                require(constant in strings, f"missing constant {constant}: {entry}::{method}")
        checks.append({"entry": entry, "decodedSha256": row["decodedSha256"], "ok": True})

    ghost_handle = sem.inspect_entry(entry_map, "Net/Msgs/Ghostrecon/GhostReconStealMessage.luac")["methods"]["HandleMessage"]
    ghost_strings = {value for value in ghost_handle["constants"] if isinstance(value, str)}
    require("uuid" not in ghost_strings and "ownerServer" not in ghost_strings,
            "Ghost response handler unexpectedly gained a static identity field")

    network = sem.inspect_entry(entry_map, "Net/SFSNetwork.luac")
    incoming = network["methods"]["HandleMessage"]
    require(any(item["op"] == "CLOSURE" for item in incoming["instructions"]),
            "SFS incoming handler shape changed")
    decoded_network = sem.probe.decode_lenc(entry_map["Net/SFSNetwork.luac"])
    root = sem._dispatch.parse_chunk(decoded_network)
    handler = sem.root_methods(root)["HandleMessage"]
    require(len(handler["children"]) == 1, "SFS HandleMessage nested body shape changed")
    nested_strings = {x for x in handler["children"][0]["constants"] if isinstance(x, str)}
    require({"NewEmpty", "HandleMessage"} <= nested_strings,
            "SFS incoming path no longer proves fresh NewEmpty response instance")

    provider = (ROOT / "src/LWBridge.Desktop/CurrentClientMap317ActionProvider.cs").read_text(encoding="utf-8")
    require("current-client Treasure claim status provider is unavailable" in provider,
            "Treasure claim status fence was removed")
    require("current-client Treasure claim provider is unavailable" in provider,
            "Treasure claim fence was removed")
    # The later, accepted BLOCKER-BOUNDARIES-004 review separates missing
    # original preparation transformation from downstream Ghost execution
    # correlation. Retain BOTH explicit fail-closed dependencies; the old
    # single-message assertion was tied to the superseded rationale.
    ghost_method = provider.split("public ValueTask<IReadOnlyList<JsonElement>> PrepareGhostPlunderTasksAsync(", 1)
    require(len(ghost_method) == 2, "Ghost public method missing")
    ghost_fence = ghost_method[1].split("internal static IReadOnlyList<JsonElement> PrepareGhostPlunderRows", 1)[0]
    require("ValueTask.FromException<IReadOnlyList<JsonElement>>" in ghost_fence
            and "ProviderUnavailable(" in ghost_fence
            and "prepareGhostPlunderTasks row transformation/rejection semantics are incomplete" in ghost_fence
            and "downstream Ghost execution terminal-result correlation is separately unresolved" in ghost_fence,
            "Ghost public preparation fail-closed contract was removed")
    require("PrepareGhostPlunderRows" in provider, "Ghost normalization helper missing")

    fast_city = (ROOT / "src/LWBridge.Desktop/CurrentClientMapBlockSource.FastCity.cs").read_text(encoding="utf-8")
    for field in ('data["plunderAt"]', 'data["stolenCount"]', 'data["maxStealCount"]'):
        require(field in fast_city, f"Ghost projection missing {field}")

    overview = (ROOT / "tools/current_overview_bridge.lua").read_text(encoding="utf-8")
    guard = 'if request.taskKind == "ghost" then'
    require(guard in overview, "Ghost scheduled-execution fail-closed guard missing")
    guard_index = overview.index(guard)
    send_index = overview.index('safe_get(msg_defines, "DispatchSteal")', guard_index)
    require(guard_index < send_index, "Ghost guard no longer precedes DispatchSteal send")
    require("DISPATCH_PLUNDER_MANAGER_UNAVAILABLE" in overview[guard_index:send_index],
            "Ghost guard lost translated fail-closed result")

    result = {
        "ok": True,
        "schema": 1,
        "workItem": "LWB317-FUNCTION-MAP-PROVIDER-SEMANTICS-003",
        "referenceSha256": REFERENCE_SHA256,
        "package": package,
        "entries": checks,
        "publicMethods": {
            "GetTreasureClaimStatusAsync": "BLOCKED_GAME_PROVIDER_UNAVAILABLE",
            "ClaimTreasuresAsync": "BLOCKED_GAME_PROVIDER_UNAVAILABLE",
            "PrepareGhostPlunderTasksAsync": "BLOCKED_GAME_PROVIDER_UNAVAILABLE",
        },
        "implementedSubcontracts": [
            "current-v22 Ghost schedule-field projection",
            "strict Ghost row normalization helper",
            "Ghost scheduled-execution fail-closed guard before DispatchSteal",
        ],
        "liveProof": False,
    }
    print(json.dumps(result, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
