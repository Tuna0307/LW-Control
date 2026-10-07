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
CURRENT_PACKAGE_SHA256 = "248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22"
ASSEMBLY_RDL = Path(r"C:\Users\chimw\AppData\Local\FunFly\Last War-Survival Game\Game\LastWar_Data\Assemblies\Assembly-CSharp.rdl")
ASSEMBLY_RDL_SHA256 = "bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e"
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


def checkpoint_b() -> dict[str, Any]:
    require(sha256(FRONTEND_INDEX) == FRONTEND_INDEX_SHA256, "frontend index hash changed")
    require(sha256(MAP_PANEL) == MAP_PANEL_SHA256, "Map panel hash changed")
    index = FRONTEND_INDEX.read_text(encoding="utf-8")
    panel = MAP_PANEL.read_text(encoding="utf-8")
    handlers = json.loads(HANDLER_DISCOVERY.read_text(encoding="utf-8"))

    schedule_wrapper = "function On(e,t=0){return N(" + BT + "map_dispatch_plunder_schedule" + BT
    require(schedule_wrapper in index, "dispatch/ghost schedule wrapper moved")
    require("!P(n.completionTime)||!P(n.plunderAt)" in panel,
            "frontend base completion/plunder selection gate moved")
    task_kind_expr = "taskKind:e===" + BT + "ghost" + BT + "?" + BT + "ghost" + BT + ":" + BT + "dispatch" + BT
    require(task_kind_expr in panel, "frontend Ghost taskKind projection moved")
    require("plunderAt:n+s*1e3" in index, "frontend random-delay ownership moved")
    require("maxRandomDelaySeconds:t,randomDelaySeconds:s" in index,
            "frontend random-delay evidence moved")

    marker = handlers["markers"]["map_dispatch_plunder_schedule"]["handlerCandidates"][0]
    require(marker["functionRva"] == "0x1336A9-0x1350DF", "schedule handler range changed")
    by_rva = {ins["rva"]: ins for ins in marker["instructions"]}
    require(
        any(ref["text"].startswith("prepareGhostPlunderTasks")
            for ref in by_rva["0x133CA8"].get("stringRefs", [])),
        "prepareGhostPlunderTasks locator changed",
    )
    require("0x1388" in by_rva["0x133CC1"]["opStr"], "Ghost prepare timeout changed")
    require(
        any(ref["text"] == "rows" for ref in by_rva["0x133F57"].get("stringRefs", [])),
        "prepared response rows locator changed",
    )
    require(
        any("ghost scheduling data unavailable" in ref["text"]
            for rva in ("0x133F8E", "0x134848")
            for ref in by_rva[rva].get("stringRefs", [])),
        "prepared Ghost missing-data error moved",
    )
    require(
        any(ref["text"].startswith("ownerServer") for ref in by_rva["0x1342E2"].get("stringRefs", [])),
        "Ghost ownerServer validation moved",
    )
    require(by_rva["0x1342EE"]["mnemonic"] == "test" and by_rva["0x1342F1"]["mnemonic"] == "jle",
            "positive ownerServer validation shape changed")
    # Expiry is conditional in the exact host: positive expiry at 0x134295 then
    # compare against plunderAt; zero/non-positive expiry bypasses that failure.
    require(by_rva["0x134295"]["mnemonic"] == "test", "expiry conditional moved")
    require(by_rva["0x134298"]["mnemonic"] == "setg", "expiry positive test moved")
    require(by_rva["0x13429B"]["mnemonic"] == "cmp", "expiry/plunder comparison moved")

    control_path = ROOT / "src/LWBridge.Map-0.3.17/MapActionControlPlane.cs"
    provider_path = ROOT / "src/LWBridge.Desktop/CurrentClientMap317ActionProvider.cs"
    ghost_source_path = ROOT / "src/LWBridge.Desktop/CurrentClientMapBlockSource.FastCity.cs"
    control = control_path.read_text(encoding="utf-8")
    provider = provider_path.read_text(encoding="utf-8")
    ghost_source = ghost_source_path.read_text(encoding="utf-8")

    method_start = control.index("public async ValueTask<IReadOnlyList<JsonElement>> ScheduleDispatchPlunderAsync")
    method_end = control.index("public void CancelDispatchPlunder", method_start)
    method = control[method_start:method_end]
    prepare_at = method.index("provider.PrepareGhostPlunderTasksAsync")
    index_at = method.index(".ToDictionary", prepare_at)
    normalize_at = method.index("NormalizeDispatchScheduleRow(source, item.TaskKind)", index_at)
    persist_at = method.index("store.ScheduleDispatchPlunderRow", normalize_at)
    require(prepare_at < index_at < normalize_at < persist_at,
            "current prepare -> index -> normalize -> persistence order changed")
    require('if (preparedGhost.Count != ghostInput.Length)' in method,
            "current prepared-row cardinality guard changed")

    public_fence = "PrepareGhostPlunderTasksAsync("
    fence_at = provider.index(public_fence)
    helper_at = provider.index("PrepareGhostPlunderRows", fence_at)
    require("terminal response identity is not source-proven" in provider[fence_at:helper_at],
            "public Ghost preparation fence rationale changed")
    helper = provider[helper_at:provider.index("GetMapPlunderServerDayStartAsync", helper_at)]
    for token in (
        'ReadInteger(row, "ownerServer")',
        'ReadInteger(row, "completionTime")',
        'ReadInteger(row, "protectTime")',
        'ReadInteger(row, "plunderAt")',
        'ReadInteger(row, "taskExpireTime")',
        'ReadInteger(row, "stealListCount")',
        'ReadInteger(row, "stealMaxTimes")',
        "stolenCount != stealListCount",
        "maxStealCount != stealMaxTimes",
        "completionTime + protectSeconds * 1000L",
    ):
        require(token in helper, f"current Ghost normalizer contract moved: {token}")

    for token in (
        'data["plunderAt"] = checked(completionTime + (long)protectSeconds * 1000L)',
        'data["stolenCount"] = stolenCount',
        'data["maxStealCount"] = maxStealCount',
    ):
        require(token in ghost_source, f"current Ghost source projection moved: {token}")

    return {
        "ok": True,
        "checkpoint": "B",
        "original": {
            "handlerRva": marker["functionRva"],
            "prepareMethodRva": "0x133CA8",
            "prepareTimeoutMilliseconds": 5000,
            "preparedResultRequiresRows": True,
            "postPrepareHostValidation": True,
            "persistenceOccursAfterPrepare": True,
            "laterExecutionIsSeparateWorker": True,
            "frontendRequiresBaseCompletionAndPlunderAtBeforeSchedule": True,
            "frontendOwnsRandomDelay": True,
        },
        "fieldClassification": {
            "uuid": {
                "originalObservable": "required positive decimal scheduling identity",
                "current": "preserved one-to-one",
                "classification": "EXACT_HOST_ADMISSION",
            },
            "ownerServer": {
                "originalObservable": "Ghost row requires positive ownerServer",
                "current": "required by current GhostReconSteal request and strict helper",
                "classification": "EXACT_HOST_ADMISSION_AND_CURRENT_OPERATION_INPUT",
            },
            "completionTime": {
                "originalObservable": "positive and <= plunderAt",
                "current": "source field",
                "classification": "EXACT_HOST_ADMISSION",
            },
            "plunderAt": {
                "originalObservable": "positive schedule time; frontend already requires it before prepare",
                "current": "projected as completionTime + protectTime*1000 before user random delay",
                "classification": "SOURCE_BACKED_BRIDGE_ADAPTATION",
            },
            "taskExpireTime": {
                "originalObservable": "optional/non-positive allowed; positive value must be after plunderAt",
                "current": "source field exists; strict helper currently requires >0",
                "classification": "STRICT_HELPER_IS_CONSERVATIVE_ADAPTATION_NOT_EXACT",
            },
            "stolenCount": {
                "originalObservable": "capacity guard when maxStealCount is positive",
                "current": "projected from stealListCount",
                "classification": "SOURCE_BACKED_BRIDGE_ADAPTATION",
            },
            "maxStealCount": {
                "originalObservable": "positive max gates full rows",
                "current": "projected from stealMaxTimes",
                "classification": "SOURCE_BACKED_BRIDGE_ADAPTATION",
            },
            "protectTime": {
                "originalObservable": "not a host schedule field; host consumes plunderAt",
                "current": "used to derive base plunderAt",
                "classification": "CURRENT_SOURCE_INPUT_TO_BRIDGE_ADAPTATION",
            },
            "aliasEquality": {
                "originalObservable": "not exposed",
                "current": "strict helper requires stolen/max aliases equal source counts",
                "classification": "CURRENT_CONSISTENCY_GUARD_NOT_ORIGINAL_PREPARER_SEMANTICS",
            },
        },
        "pipeline": [
            "frontend requires uuid/completionTime/plunderAt and adds bounded random delay",
            "host validates submitted rows",
            "Ghost subset only -> protected prepareGhostPlunderTasks",
            "provider result must expose rows",
            "prepared rows are matched/indexed by uuid",
            "host re-normalizes prepared Ghost rows",
            "durable row is persisted as ghost:<uuid>",
            "later worker arms and awaits separate terminal result",
        ],
        "disposition": {
            "terminalExecutionCorrelationBlocksPreparationByDefinition": False,
            "publicPreparationFenceReasonIsOverbroad": True,
            "firstMissingPreparationEdge": (
                "current Ghost source row -> exact protected preparer returned-row transformation/"
                "rejection semantics beyond host-visible postconditions"
            ),
            "runtimeHydrationRequiredByRecoveredPreparationContract": "UNKNOWN_NOT_PROVEN",
            "terminalStealConfirmationRequiredByPreparation": False,
        },
        "readyOfflineCandidate": (
            "separate preparation capability from execution capability; evaluate wiring the existing "
            "source-backed one-to-one normalizer as a preparation-only current adaptation while "
            "leaving Ghost arm/terminal execution fenced"
        ),
    }


def checkpoint_c() -> dict[str, Any]:
    import importlib.util
    import sys

    require(ASSEMBLY_RDL.is_file(), f"current RDL missing: {ASSEMBLY_RDL}")
    require(sha256(ASSEMBLY_RDL) == ASSEMBLY_RDL_SHA256, "current Assembly-CSharp.rdl hash changed")

    sys.path.insert(0, str(ROOT / "tools"))
    import run_live_resource_probe as probe  # type: ignore

    sem_spec = importlib.util.spec_from_file_location(
        "boundary_semantics_c", ROOT / "tools/lwbridge317/inspect_map_provider_semantics.py"
    )
    if sem_spec is None or sem_spec.loader is None:
        raise RuntimeError("unable to load semantic inspector")
    sem = importlib.util.module_from_spec(sem_spec)
    sys.modules[sem_spec.name] = sem
    sem_spec.loader.exec_module(sem)

    entry_map, package = sem.load_package(Path(probe.paths()["data"]))
    require(package["sha256"] == CURRENT_PACKAGE_SHA256, "current v22 package hash changed")

    def methods(entry: str):
        root = sem._dispatch.parse_chunk(probe.decode_lenc(entry_map[entry]))
        return sem.root_methods(root)

    direct_handle = methods("Net/Msgs/Ghostrecon/GhostReconStealMessage.luac")["HandleMessage"]
    direct_strings = {x for x in direct_handle["constants"] if isinstance(x, str)}
    require({"errorCode", "GhostReconStealHandler"} <= direct_strings,
            "direct Ghost handler contract moved")
    require("uuid" not in direct_strings and "ownerServer" not in direct_strings,
            "direct Ghost handler began statically consuming request identity")

    ghost_manager = methods(
        "DataCenter/ActivityListData/ActGhostrecon/ActGhostreconManager.luac"
    )["GhostReconStealHandler"]
    manager_strings = {x for x in ghost_manager["constants"] if isinstance(x, str)}
    require({"stealTimes", "reward"} <= manager_strings,
            "Ghost manager success fields moved")
    require(not ({"uuid", "ownerServer", "pointId"} & manager_strings),
            "Ghost manager began statically consuming terminal identity")

    push = methods("Net/Msgs/Ghostrecon/PushGhostReconStealMessage.luac")["HandleMessage"]
    push_strings = {x for x in push["constants"] if isinstance(x, str)}
    require("PushHeroDispatchMissionStealHandler" in push_strings,
            "Ghost push routing moved")
    push_manager = methods(
        "DataCenter/ActivityListData/ActDispatchTaskDataManager.luac"
    )["PushHeroDispatchMissionStealHandler"]
    push_manager_strings = {x for x in push_manager["constants"] if isinstance(x, str)}
    require({"serverId", "pointId", "playerInfo"} <= push_manager_strings,
            "Ghost push location identity fields moved")
    require("uuid" not in push_manager_strings,
            "Ghost push manager began statically consuming task UUID")

    network = methods("Net/SFSNetwork.luac")
    send_method = network["SendMessage"]
    send_strings = {x for x in send_method["constants"] if isinstance(x, str)}
    require({"NewMessage", "ToBinary", "SendLuaMessage"} <= send_strings,
            "SFS send path moved")
    require(send_method["upvalueNames"][:2] == ["GetMsgType", "Network"],
            "SFS send GetMsgType/Network upvalues moved")
    incoming = network["HandleMessage"]
    require(incoming["upvalueNames"] and incoming["upvalueNames"][0] == "GetMsgType",
            "SFS receive GetMsgType upvalue moved")
    require(len(incoming["children"]) == 1, "SFS incoming closure shape changed")
    incoming_strings = {x for x in incoming["children"][0]["constants"] if isinstance(x, str)}
    require({"NewEmpty", "HandleMessage"} <= incoming_strings,
            "SFS fresh incoming message path moved")

    relevant_entries = [
        "Net/SFSNetwork.luac",
        "Net/Msgs/Ghostrecon/GhostReconStealMessage.luac",
        "Net/Msgs/Ghostrecon/PushGhostReconStealMessage.luac",
        "DataCenter/ActivityListData/ActGhostrecon/ActGhostreconManager.luac",
        "DataCenter/ActivityListData/ActDispatchTaskDataManager.luac",
    ]
    require(
        all(b"getFutureManager" not in probe.decode_lenc(entry_map[name]) for name in relevant_entries),
        "Ghost/SFS Lua path began using managed FutureManager directly",
    )

    defines = probe.decode_lenc(entry_map["Net/Config/MsgDefines.luac"])
    require(b"ghost.recon.steal" in defines and b"push.ghost.recon.steal" in defines,
            "Ghost direct/push command names moved")

    rdl_spec = importlib.util.spec_from_file_location(
        "boundary_rdl_c", ROOT / "tools/inspect_lastwar_rdl_metadata.py"
    )
    if rdl_spec is None or rdl_spec.loader is None:
        raise RuntimeError("unable to load RDL inspector")
    rdl = importlib.util.module_from_spec(rdl_spec)
    sys.modules[rdl_spec.name] = rdl
    rdl_spec.loader.exec_module(rdl)
    image = rdl.MetadataImage.load(ASSEMBLY_RDL)
    method_owners, _ = image.owner_maps()

    def find_method(owner: str, name: str, arg_fragment: str | None = None):
        matches = []
        for index, row in enumerate(image.tables.MethodDef.rows, 1):
            if method_owners.get(index) != owner or str(row.Name) != name:
                continue
            ret, args = image.decode_method_signature(bytes(row.Signature.value))
            rendered = ",".join(args)
            if arg_fragment is None or arg_fragment in rendered:
                matches.append((index, row, ret, args))
        require(len(matches) == 1, f"expected one method {owner}::{name} {arg_fragment}: {len(matches)}")
        return matches[0]

    _, send_lua_row, send_ret, send_args = find_method(
        "NetworkManager", "SendLuaMessage", "string,uint8[]"
    )
    require(send_ret == "void" and send_args == ["string", "uint8[]"],
            "NetworkManager.SendLuaMessage signature changed")
    _, _, _, ext_args = find_method(
        "NetworkManager", "OnExtensionResponse", "string,Sfs2X.Entities.Data.SFSObject"
    )
    require(ext_args == ["string", "Sfs2X.Entities.Data.SFSObject"],
            "NetworkManager extension response signature changed")

    network_types = [
        (idx, row, full)
        for idx, row, full in rdl._matching_types(image, "NetworkManager")
        if full == "NetworkManager"
    ]
    require(len(network_types) == 1, "NetworkManager type changed")
    _, network_row, _ = network_types[0]
    network_fields = {str(ref.row.Name) for ref in network_row.FieldList}
    require("_futureManager" in network_fields, "NetworkManager FutureManager field moved")

    future_types = [
        (idx, row, full)
        for idx, row, full in rdl._matching_types(image, "Main.Scripts.Network.FutureManager")
        if full == "Main.Scripts.Network.FutureManager"
    ]
    require(len(future_types) == 1, "FutureManager type changed")
    _, future_row, _ = future_types[0]
    future_fields = {str(ref.row.Name) for ref in future_row.FieldList}
    future_methods = {str(ref.row.Name) for ref in future_row.MethodList}
    require({"_sendInfos", "_futureId"} <= future_fields,
            "FutureManager pending telemetry fields moved")
    require({"getFutureId", "onSendRequest", "onServerMsgCome"} <= future_methods,
            "FutureManager correlation methods moved")

    msg_types = [
        (idx, row, full)
        for idx, row, full in rdl._matching_types(image, "Main.Scripts.Network.msgSendInfo")
        if full == "Main.Scripts.Network.msgSendInfo"
    ]
    require(len(msg_types) == 1, "msgSendInfo type changed")
    _, msg_row, _ = msg_types[0]
    msg_fields = {str(ref.row.Name) for ref in msg_row.FieldList}
    require({"_futureId", "_msgId", "_sendTime"} <= msg_fields,
            "msgSendInfo telemetry payload changed")

    raw_rdl = ASSEMBLY_RDL.read_bytes()
    require(raw_rdl.count(b"fuid") == 1, "RDL ASCII fuid evidence changed")
    require(raw_rdl.count("fuid".encode("utf-16le")) == 1,
            "RDL user-string fuid evidence changed")

    send_il = image.disassemble(send_lua_row)
    future_receive = find_method(
        "Main.Scripts.Network.FutureManager", "onServerMsgCome", "int32,int32"
    )[1]
    receive_il = image.disassemble(future_receive)

    overview = (ROOT / "tools/current_overview_bridge.lua").read_text(encoding="utf-8")
    guard_at = overview.index('if request.taskKind == "ghost" then')
    dispatch_send_at = overview.index('safe_get(msg_defines, "DispatchSteal")', guard_at)
    require(guard_at < dispatch_send_at, "Ghost safety guard no longer precedes Dispatch send")

    provider = (ROOT / "src/LWBridge.Desktop/CurrentClientMap317ActionProvider.cs").read_text(
        encoding="utf-8"
    )
    require("current-client Ghost plunder preparation remains unavailable" in provider,
            "Ghost public preparation fence removed")

    return {
        "ok": True,
        "checkpoint": "C",
        "currentPackageSha256": CURRENT_PACKAGE_SHA256,
        "assemblyRdlSha256": ASSEMBLY_RDL_SHA256,
        "directResponse": {
            "staticallyConsumedByMessageHandler": sorted(direct_strings),
            "staticallyConsumedByManager": sorted(manager_strings),
            "uuidConsumed": False,
            "ownerServerConsumed": False,
            "rawSchemaDefinesUuid": "UNKNOWN",
            "extraFieldsSurviveLuaHandler": "PROVEN_BY_DISTINGUISHING_ORACLE",
        },
        "pushResponse": {
            "distinctCommand": "push.ghost.recon.steal",
            "consumedLocationIdentity": ["serverId", "pointId", "playerInfo"],
            "taskUuidConsumed": False,
            "equivalenceToDirectTerminalAck": "UNKNOWN_NOT_PROVEN",
        },
        "luaTransport": {
            "directCommand": "ghost.recon.steal",
            "dispatchKey": "cmd -> GetMsgType(cmd)",
            "incomingCreatesFreshMessageInstance": True,
            "pathSpecificPendingQueue": False,
            "pathSpecificFutureManagerCall": False,
            "strictSingleFlightOrOrderingGuarantee": "UNKNOWN_NOT_PROVEN",
        },
        "managedTransport": {
            "sendSignature": "void SendLuaMessage(string msgId, byte[] sfsObjBinary)",
            "receiveSignature": "void OnExtensionResponse(string cmd, SFSObject so)",
            "futureManager": {
                "networkManagerOwnsFutureManager": True,
                "fields": sorted(future_fields),
                "methods": sorted(future_methods),
                "msgSendInfoFields": sorted(msg_fields),
                "fuidLiteralPinnedInRdl": True,
                "sendMethodIl": send_il,
                "onServerMsgComeIl": receive_il,
                "classification": (
                    "SOURCE_PROVEN_MANAGED_FUTURE/PENDING_MECHANISM; modified CIL tokens "
                    "prevent promoting an end-to-end Ghost application correlation contract"
                ),
            },
            "futureIdExposedToLuaRawResponse": "UNKNOWN",
            "ghostDirectPathUsesFutureIdForTerminalResult": "UNKNOWN_NOT_PROVEN",
        },
        "disposition": {
            "handlerDoesNotReadUuidImpliesSchemaHasNoUuid": False,
            "correlationImpossible": False,
            "safeApplicationCorrelationRecovered": False,
            "state": "PARTIAL_UNKNOWN",
            "firstMissingEdge": (
                "server/managed raw direct Ghost response -> Lua response table identity exposure "
                "(uuid/fuid/point/task key), plus ordering/single-flight semantics usable by bridge"
            ),
        },
        "fences": {
            "publicProviderFencesPreserved": True,
            "scheduledGhostSafetyGuardPreserved": True,
        },
    }

def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--checkpoint", choices=["A", "B", "C"], default="A")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = {"A": checkpoint_a, "B": checkpoint_b, "C": checkpoint_c}[args.checkpoint]()
    encoded = json.dumps(result, indent=2, ensure_ascii=False) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded, encoding="utf-8")
    print(encoded, end="")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
