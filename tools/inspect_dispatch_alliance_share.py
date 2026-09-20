#!/usr/bin/env python3
"""Hash-lock and inspect the original + current-v19 Dispatch alliance-share contract."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import inspect_lwbridge_map_scan as original
import inspect_current_dispatch_plunder_sources as lua
import run_live_resource_probe as probe


EXPECTED_ORIGINAL_SHA256 = (
    "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
)
EXPECTED_FRONTEND_SHA256 = (
    "062ebd2362885af05e07bf4f0de00ed833293a83fefa6b54c26975fa1db9df47"
)
EXPECTED_MAP_PANEL_SHA256 = (
    "fc39de8154c54ee0937b6f9f9377d671e0dbad533ed2d9eda29a99d35e380f5e"
)
EXPECTED_PACKAGE_SHA256 = (
    "e7c5742a44d5f5862e4eb6c94944b4150969b6c4bd0a1c1cb9a337cfd1141fa2"
)

MODULES = {
    "chat_command": (
        "Chat/NetMessage/ChatHeroDispatchShareCommand.luac",
        "ad3f495a7bc87e55c05a3493d70b8cf680d8a72c3982e2d4079becfccf71909f",
    ),
    "chat_controller": (
        "Chat/Controller/ChatController.luac",
        "0ba3628a5bba07fafc317fd21b6297b4295c74b3a61d6135a4dbbf2e823be434",
    ),
    "chat_defines": (
        "Chat/WebMessage/Config/ChatMsgDefines.luac",
        "ad7fd478b0925445240d49fac251ca172003052043f1591a716abbd01298e976",
    ),
    "share_encode": (
        "Chat/Other/ShareEncode.luac",
        "9d379f701734bf4c20f1c29c57ea194dd6fb3b68f0cca712969edba5da562591",
    ),
    "share_decode": (
        "Chat/Other/ShareDecode.luac",
        "fe63553ea832a893ffecb22e1c9a458bbc28cba14b289191a43833bd12fe1ac6",
    ),
    "position_share_ctrl": (
        "UI/UIPositionShare/Controller/UIPositionShareCtrl.luac",
        "bce752b65980dee4f82f70767087133bb23e1578ffd77dd85648838c394082f0",
    ),
    "world_point_ctrl": (
        "UI/UIWorldPoint/Controller/UIWorldPointCtrl.luac",
        "4e738cdd076dde56239461d3f011703885d1d4b55facf1c3aa283abcd8675f0e",
    ),
}


class InspectError(RuntimeError):
    pass


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def require_text(label: str, haystack: str, needles: list[str]) -> None:
    missing = [needle for needle in needles if needle not in haystack]
    if missing:
        raise InspectError(f"{label} missing: {missing}")


def require_proto(label: str, root: dict, constants: list[str]) -> dict:
    proto = lua.find_proto(root, constants)
    return lua.require_constants(label, proto, constants)


def require_chat_define_mapping(
    root: dict,
    key: str,
    command: str,
    module: str,
) -> dict[str, object]:
    constants = root["constants"]
    key_index = constants.index(key)
    command_index = constants.index(command)
    module_index = constants.index(module)
    code = root["code"]

    direct_pc = None
    for pc, instruction in enumerate(code):
        if (
            lua.opcode(instruction) == "SETTABLE"
            and lua.instruction_a(instruction) == 0
            and lua.instruction_b(instruction) == 0x100 + key_index
            and lua.instruction_c(instruction) == 0x100 + command_index
        ):
            direct_pc = pc
            break
    if direct_pc is None:
        raise InspectError(
            f"ChatMsgDefines did not directly assign {key!r} -> {command!r}"
        )

    map_pc = None
    for pc in range(len(code) - 3):
        get_defines, get_command, load_module, assign = code[pc:pc + 4]
        if (
            lua.opcode(get_defines) == "GETTABUP"
            and lua.instruction_a(get_defines) == 2
            and lua.instruction_c(get_defines) == 0x100
            and lua.opcode(get_command) == "GETTABLE"
            and lua.instruction_a(get_command) == 2
            and lua.instruction_b(get_command) == 2
            and lua.instruction_c(get_command) == 0x100 + key_index
            and lua.opcode(load_module) == "LOADK"
            and lua.instruction_a(load_module) == 3
            and lua.instruction_bx(load_module) == module_index
            and lua.opcode(assign) == "SETTABLE"
            and lua.instruction_a(assign) == 1
            and lua.instruction_b(assign) == 2
            and lua.instruction_c(assign) == 3
        ):
            map_pc = pc
            break
    if map_pc is None:
        raise InspectError(
            f"ChatMsgMap did not map {key!r} to {module!r}"
        )

    return {
        "line": root["line"],
        "lastLine": root["lastLine"],
        "verifiedConstants": [key, command, module],
        "directAssignmentPc": direct_pc,
        "messageMapPc": map_pc,
    }


def inspect_all(
    binary: Path,
    frontend: Path,
    map_panel: Path,
    package_path: Path,
) -> dict[str, object]:
    if sha256(binary) != EXPECTED_ORIGINAL_SHA256:
        raise InspectError("original lwbridge hash mismatch")
    if sha256(frontend) != EXPECTED_FRONTEND_SHA256:
        raise InspectError("recovered frontend hash mismatch")
    if sha256(map_panel) != EXPECTED_MAP_PANEL_SHA256:
        raise InspectError("recovered MapDataPanel hash mismatch")
    if sha256(package_path) != EXPECTED_PACKAGE_SHA256:
        raise InspectError("current LWScripts.data hash mismatch")

    frontend_text = frontend.read_text(encoding="utf-8")
    require_text(
        "frontend Dispatch share wrapper",
        frontend_text,
        [
            "map_dispatch_share_alliance",
            "uuid:String(e.uuid",
            "serverId:e.serverId",
            "x:e.x",
            "y:e.y",
            "cfgId:e.cfgId",
            "ownerName:e.ownerName",
            "allianceAbbr:e.allianceAbbr",
        ],
    )
    map_panel_text = map_panel.read_text(encoding="utf-8")
    require_text(
        "frontend Dispatch share result consumer",
        map_panel_text,
        [
            "sharedUuids",
            "map.shareAlliancePartial",
            "map.shareAllianceSuccess",
        ],
    )

    original_report = original.inspect(binary, (), dump_rva=0x163EF2)
    require_text(
        "original Dispatch share handler",
        original_report,
        [
            "function 0x163EF2-0x1651C5",
            "'rows'",
            "'uuidserverIduid'",
            "'cfgIdselected dispatch task cannot be sharedselect between 1 and 200 dispatch tasksshared'",
            "'shareDispatchTaskToAlliance'",
            "mov     qword ptr [rbx + 0x1000], 0x1388",
            "'shared'",
            "movabs  rcx, 0x7555646572616873",
            "mov     dword ptr [rax + 7], 0x73646975",
            "movabs  rcx, 0x755564656c696166",
        ],
    )
    binary_bytes = binary.read_bytes()
    for required in (
        "selected dispatch task cannot be shared",
        "select between 1 and 200 dispatch tasks",
        "shareDispatchTaskToAlliance",
    ):
        if required.encode("ascii") not in binary_bytes:
            raise InspectError(f"original binary missing {required!r}")

    file_version, content_version, entries = probe.read_lwlf(package_path)
    if (file_version, content_version) != (3, 19):
        raise InspectError(
            f"unexpected package version {file_version}/{content_version}"
        )
    entry_map = {
        name.replace("\\", "/"): body
        for name, body in entries
    }

    roots: dict[str, dict] = {}
    module_hashes: dict[str, str] = {}
    for key, (name, expected_hash) in MODULES.items():
        if name not in entry_map:
            raise InspectError(f"current package missing {name}")
        decoded = probe.decode_lenc(entry_map[name])
        digest = hashlib.sha256(decoded).hexdigest()
        if digest != expected_hash:
            raise InspectError(
                f"{name} hash {digest} != expected {expected_hash}"
            )
        roots[key] = lua.parse_chunk(decoded)
        module_hashes[key] = digest

    command_methods = lua.root_methods(roots["chat_command"])
    command_create = lua.require_constants(
        "ChatHeroDispatchShareCommand.OnCreate",
        command_methods["OnCreate"],
        [
            "PutLong",
            "uuid",
            "PutInt",
            "targetServer",
            "type",
            "post",
            "PutUtfString",
            "lang",
            "msg",
        ],
    )
    command_handle = lua.require_constants(
        "ChatHeroDispatchShareCommand.HandleMessage",
        command_methods["HandleMessage"],
        [
            "errorCode",
            "ShowErrorCodeTips",
            "noShowChat",
            "extraShareInfo",
            "dispatch_quick_mark_success_tips",
        ],
    )

    chat_controller = lua.require_constants(
        "ChatController.OnChatShare",
        lua.root_methods(roots["chat_controller"])["OnChatShare"],
        [
            "ChatShareChannel",
            "TO_ALLIANCE",
            "ChatShareAlliance",
            "Text_PointShare",
            "dispatch",
            "uuid",
            "sid",
            "targetServer",
            "ChatHeroDispatchShare",
            "SendSFSMessage",
        ],
    )
    chat_define = require_chat_define_mapping(
        roots["chat_defines"],
        "ChatHeroDispatchShare",
        "hero.dispatch.share.chat",
        "Chat.NetMessage.ChatHeroDispatchShareCommand",
    )

    share_encode = require_proto(
        "ShareEncode Text_PointShare encoder",
        roots["share_encode"],
        ["x", "y", "sid", "uname", "abbr", "dispatch", "cfgId", "uuid"],
    )
    share_decode = require_proto(
        "ShareDecode Dispatch point-share branch",
        roots["share_decode"],
        ["x", "y", "dispatch", "456288", "sid", "POSITION_COORDINATE_CROSS"],
    )
    position_normalize = require_proto(
        "UIPositionShare coordinate normalization",
        roots["position_share_ctrl"],
        ["ShareType", "Pos", "pos", "IndexToTilePos", "World", "x", "y"],
    )
    world_dispatch = require_proto(
        "UIWorldPoint Dispatch share construction",
        roots["world_point_ctrl"],
        [
            "DispatchTask",
            "GetPointInfo",
            "dispatch",
            "cfgId",
            "PlayerInfoDataManager",
            "alAbbr",
            "name",
            "UIPositionShare",
        ],
    )

    return {
        "ok": True,
        "originalSha256": EXPECTED_ORIGINAL_SHA256,
        "frontendSha256": EXPECTED_FRONTEND_SHA256,
        "mapPanelSha256": EXPECTED_MAP_PANEL_SHA256,
        "packageSha256": EXPECTED_PACKAGE_SHA256,
        "fileVersion": file_version,
        "contentVersion": content_version,
        "original": {
            "handlerRva": "0x163EF2-0x1651C5",
            "rowCount": "1..200",
            "rowFields": [
                "uuid",
                "serverId",
                "x",
                "y",
                "cfgId",
                "ownerName",
                "allianceAbbr",
            ],
            "invalidRow": "selected dispatch task cannot be shared",
            "invalidCount": "select between 1 and 200 dispatch tasks",
            "perRowBridgeCommand": "shareDispatchTaskToAlliance",
            "perRowTimeoutMilliseconds": 5000,
            "successPredicate": "result.shared == true",
            "resultFields": [
                "shared",
                "failed",
                "sharedUuids",
                "failedUuids",
            ],
        },
        "currentV19": {
            "moduleHashes": module_hashes,
            "chatCommand": command_create,
            "chatResult": command_handle,
            "chatController": chat_controller,
            "chatDefine": chat_define,
            "shareEncode": share_encode,
            "shareDecode": share_decode,
            "positionNormalize": position_normalize,
            "worldDispatch": world_dispatch,
            "postType": "Text_PointShare",
            "channel": "TO_ALLIANCE",
            "command": "hero.dispatch.share.chat",
            "dispatchLabelKey": "456288",
            "pointPayload": [
                "x",
                "y",
                "sid",
                "dispatch=1",
                "cfgId",
                "uuid",
                "optional uname/abbr",
            ],
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "binary",
        nargs="?",
        type=Path,
        default=Path("../LW/lwbridge-0.3.1.exe"),
    )
    parser.add_argument(
        "--frontend",
        type=Path,
        default=Path(
            "evidence/lwbridge-0.3.1/frontend/assets/api-ClPPi2JT.js"
        ),
    )
    parser.add_argument(
        "--map-panel",
        type=Path,
        default=Path(
            "evidence/lwbridge-0.3.1/frontend/assets/MapDataPanel-C1HVeNHr.js"
        ),
    )
    parser.add_argument(
        "--package",
        type=Path,
        default=Path(probe.paths()["data"]),
    )
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    try:
        result = inspect_all(
            args.binary.resolve(),
            args.frontend.resolve(),
            args.map_panel.resolve(),
            args.package.resolve(),
        )
    except (OSError, ValueError, InspectError, lua.InspectError) as exc:
        print(f"FAIL Dispatch alliance-share inspection: {exc}")
        return 1

    if args.json:
        print(json.dumps(result, indent=2, sort_keys=True))
    else:
        print("PASS Dispatch alliance-share original/current-v19 inspection")
        print(f"originalSha256={result['originalSha256']}")
        print(f"frontendSha256={result['frontendSha256']}")
        print(f"mapPanelSha256={result['mapPanelSha256']}")
        print(f"packageSha256={result['packageSha256']}")
        print("originalRows=1..200; invalid-row/count strings pinned")
        print("originalPerRow=shareDispatchTaskToAlliance timeout=5000ms")
        print("originalResult=shared,failed,sharedUuids,failedUuids")
        print(
            "currentV19=Text_PointShare -> TO_ALLIANCE -> "
            "hero.dispatch.share.chat"
        )
        print(
            "pointPayload=x,y,sid,dispatch=1,cfgId,uuid,"
            "optional uname/abbr; labelKey=456288"
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
