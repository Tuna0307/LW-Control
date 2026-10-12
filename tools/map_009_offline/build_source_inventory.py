#!/usr/bin/env python3
"""Reproduce the compact committed v24 source locator inventory (no raw bodies)."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

import compact_lua53 as lua
from inspect_current import (ASSEMBLY_SHA256, PACKAGE_SHA256, load_entries,
                             probe, root_methods)
from inspect_managed import DEFAULT as ASSEMBLY, MetadataImage


# These module/prototype locators are obtained from the current v24 root routers.
# A locator is not a claim that its alternative source is a complete census.
SELECTION: dict[str, tuple[str, ...]] = {
    "Chat/Other/ShareDecode.luac": ("root/0", "Decode"),
    "Chat/Other/ShareExecute.luac": ("root/0", "Execute"),
    "Chat/Controller/ChatController.luac": ("OnExecuteChatEvent",),
    "UI/UIChatNew/Component/ChatItem/ChatItem.luac": ("ExecuteChatEvent",),
    "Util/SceneUtils.luac": ("TileXYToIndex", "IndexToTilePos", "EncodeWorldPos", "DecodeWorldPos", "TileToWorld", "ChangeToWorld"),
    "Util/GoToUtil.luac": ("GotoWorldPos", "GotoDragonPos", "GoToServerPreCheck"),
    "Util/CrossServerUtil.luac": ("JumpToServerByServerId",),
    "Net/Msgs/Railway/GetMarchPosMessage.luac": ("OnCreate", "HandleMessage"),
    "Net/Msgs/GetUserInfoMultiMessage.luac": ("OnCreate",),
    "Net/Msgs/GetNewUserInfoMessage.luac": ("OnCreate",),
    "Net/Msgs/WorldGetDetailMessage.luac": ("OnCreate", "HandleMessage"),
    "Net/Msgs/GetWorldMarchCurPosMessage.luac": ("OnCreate", "HandleMessage"),
    "Net/Msgs/FindEnemyPointMessage.luac": ("OnCreate", "HandleMessage"),
    "Net/Msgs/FindMonsterMessage.luac": ("OnCreate", "HandleMessage"),
    "Net/Msgs/FindResourceMessage.luac": ("OnCreate", "HandleMessage"),
    "Net/Msgs/FindResourcePointMessage.luac": ("OnCreate", "HandleMessage"),
    "Net/Msgs/DispatchTask/DispatchFindNearestPointMessage.luac": ("OnCreate", "HandleMessage"),
    "DataCenter/LWRailway/Train/LWTrainDataManager.luac": ("TryGetTrainList", "OnTrainListGet"),
    "DataCenter/LWDoomsday/LWDoomsdayManager.luac": ("__init",),
    "DataCenter/WorldMarchDataManager/WorldMarchDataManager.luac": ("__init",),
    "Net/Msgs/Ghostrecon/GhostReconGetAllianceTaskListMessage.luac": ("OnCreate", "HandleMessage"),
    "Net/Msgs/Ghostrecon/GhostreconGetTaskListMessage.luac": ("OnCreate", "HandleMessage"),
    "DataCenter/ActivityListData/ActGhostrecon/ActGhostreconManager.luac": ("GhostreconGetTaskListHandler", "InitBubblePosInfos"),
    "Net/Msgs/RadarCenter/DetectEventGetTreasureClaimInfoMessage.luac": ("OnCreate", "HandleMessage"),
    "Net/Msgs/DispatchTreasure/DispatchTreasureGetSelfInfoMessage.luac": ("OnCreate", "HandleMessage"),
    "Net/Msgs/DispatchTreasure/DispatchTreasureGetALInfoMessage.luac": ("OnCreate", "HandleMessage"),
}

MANAGED_METHODS = ("UpdateLWAoi_Normal", "SendAoiRequest", "UpdateViewRequest",
                   "SendViewRequest", "StartViewRequest", "HandleViewPointsReply",
                   "HandleViewUpdateNotify", "UpdateLWAoi", "PositionToAoiBlock",
                   "AoiBlockToIndex", "IndexToAoiBlock", "AoiBlockToTilePos")


def method_body(root: dict, path: str) -> dict:
    body = root
    for segment in path.split("/")[1:]:
        body = body["children"][int(segment)]
    return body


def manifest() -> dict:
    entries = load_entries(probe.paths()["data"])
    modules = {}
    for name, wanted in sorted(SELECTION.items()):
        encoded = entries[name]
        decoded = probe.decode_lenc(encoded)
        root = lua.parse_chunk(decoded)
        paths = root_methods(root)
        chosen = {}
        for method in wanted:
            path = paths.get(method, method)
            body = method_body(root, path)
            instructions = b"".join(word.to_bytes(4, "little") for word in body["code"])
            chosen[method] = {"path": path, "sourceLines": [body["line"], body["lastLine"]],
                              "instructions": len(body["code"]),
                              "instructionSha256": hashlib.sha256(instructions).hexdigest()}
        modules[name] = {"encodedSha256": hashlib.sha256(encoded).hexdigest(),
                         "decodedSha256": hashlib.sha256(decoded).hexdigest(), "methods": chosen}
    with ASSEMBLY.open("rb") as file:
        assembly_hash = hashlib.file_digest(file, "sha256").hexdigest()
    if assembly_hash != ASSEMBLY_SHA256:
        raise ValueError("current Assembly-CSharp.rdl differs from lead inventory")
    image = MetadataImage.load(ASSEMBLY)
    managed = {}
    for type_index, type_row in enumerate(image.tables.TypeDef.rows, 1):
        if image.type_name("TypeDef", type_index) != "WorldPointManager":
            continue
        for method_row in type_row.MethodList:
            row = method_row.row
            if str(row.Name) not in MANAGED_METHODS:
                continue
            _, parameters = image.decode_method_signature(bytes(row.Signature.value))
            managed[str(row.Name)] = {"methodToken": f"0x{0x06000000 | method_row.row_index:08x}",
                                      "parameters": parameters}
    if set(managed) != set(MANAGED_METHODS):
        raise ValueError("managed AOI native type/methods changed")
    return {
        "schema": 1,
        "source": "installed-client-v24-read-only",
        "luaPackage": {"sha256": PACKAGE_SHA256, "lwlfFormatVersion": 3,
                       "lwlfContentVersion": 24, "entryCount": len(entries)},
        "managedAssembly": {"relativePath": "Game/LastWar_Data/Assemblies/Assembly-CSharp.rdl",
                            "sha256": assembly_hash, "WorldPointManager": managed},
        "luaModules": modules,
        "reproduce": "python tools/map_009_offline/build_source_inventory.py",
        "limitations": "Code locators and serializer signatures only; native server acceptance, whole-world completeness and latency not established",
    }


def main():
    out = Path(__file__).resolve().parents[2] / "docs/proof/MAP-009-OFFLINE-NATIVE-SOURCE-LOCATORS-2026-10-12.json"
    out.write_text(json.dumps(manifest(), indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print("current v24 source inventory:", len(SELECTION), "Lua modules,", len(MANAGED_METHODS),
          "managed methods ->", out)


if __name__ == "__main__":
    main()
