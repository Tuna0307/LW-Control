"""Read-only, source-extracted MAP-014 single-probe vs native-group boundary audit.

No transport connection, native execution, camera command or receipt write.
Positive shape checks describe *controlled* future envelopes, NEVER a native
authentication result. The checked-in Lua writer is the source of current
file-transport keys; no extra keys may be credited to that producer.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
from datetime import datetime, timezone
import json
from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[2]
LUA = ROOT / "tools/current_live_resource_probe.lua"
MANAGED = ROOT / "src/LWBridge.Desktop/CurrentClientMapBlockSource.FastCity.cs"
BASE = ROOT / "src/LWBridge.Desktop/CurrentClientMapBlockSource.cs"
RECEIVER = ROOT / "src/LWBridge.Desktop/Map014NativeSharedReceiptSource.cs"
OVERVIEW = ROOT / "tools/current_overview_bridge.lua"
BACKEND = ROOT / "src/LWBridge.Desktop/LWBridgeBackend.cs"
GAME_PIPE = ROOT / "src/LWBridge.GamePipeAdapter/PipeClientAdapter.cs"
PROVIDER = ROOT / "src/LWBridge.Desktop/CurrentClientMap317ScanProvider.cs"
PIPE_PROTOCOL = ROOT / "src/LWBridge.Desktop/LWBridgeControlPipeProtocol.cs"
PIPE_CALL_REGISTRY = ROOT / "src/LWBridge.Desktop/LWBridgeControlPipeCallRegistry.cs"
PIPE_SESSION_TRANSPORT = ROOT / "src/LWBridge.Desktop/LWBridgeControlPipeRpcSessionTransport.cs"
DESKTOP_SOURCE_DIR = ROOT / "src/LWBridge.Desktop"
ARCHIVES = (
    ROOT / "artifacts/map-013-live/native-causal-20261014-004239",
    ROOT / "artifacts/map-013-live/native-causal-20261014-004916",
)

GROUP_ONLY_KEYS = frozenset({
    "groupGeneration", "regionOrdinal", "worldId", "scanRunId",
    "nativeReplyRequestId", "nativeReplyAtUnixMs", "requestStartedAtUnixMs",
    "homeRestoreRequestId", "homeNativeReplyAtUnixMs", "homeNativeReplyRequestId",
    "homeNativeReplyEvidence", "nativeAoiProofs", "kindBlocks",
})
OWNER_FIELDS = ("profileId", "launchSessionId", "challenge", "gamePid")
# src/LWBridge.Desktop/Map014NativeSharedReceiptSource.cs
# ValidateReceivedEnvelope: the pinned v24 native point request maximum is 160.
SOURCE_NATIVE_REPLY_LIMIT = 160


class BoundaryError(ValueError):
    """Missing authentic producer evidence or inconsistent controlled receipt."""


def section(source: str, beginning: str, end: str) -> str:
    start = source.find(beginning)
    stop = source.find(end, start + len(beginning))
    if start < 0 or stop < 0:
        raise BoundaryError(f"source contract changed near {beginning!r}")
    return source[start:stop]


def audit_lua_pipe_argument_boundary(
    *, overview: str | None = None, backend: str | None = None,
) -> dict:
    """Source-lock a *different* trust boundary: Lua's raw-text JSON scan.

    The backend's own getStatus API demands an empty JSON object, but the
    game-side Lua receiver does not parse the args object: it searches the
    entire inbound text for one literal. A synthetic JSON input may contain
    nonempty payload.args AND a nested decoy args={}, satisfying that literal.
    This is a parser-semantic limit, not a proven external/authenticated attack
    or any kind of native group ingress.
    """
    overview = OVERVIEW.read_text(encoding="utf-8-sig") if overview is None else overview
    backend = BACKEND.read_text(encoding="utf-8-sig") if backend is None else backend
    parser = section(overview, "local function pipe_json_string(text, name)",
                     "local function write_pipe_result(")
    require("return string.match(" in parser and
            """'"' .. tostring(name) .. '":"([^"]*)"'""" in parser and
            'return tonumber(value)' in parser,
            "Lua pipe field text parser changed: requalify JSON grammar")
    inbound = section(overview, "local function process_pipe_inbound(control)",
                      "local RECOVERY_WINDOWS")
    require(re.search(
        r'local\s+empty_args\s*=\s*string\.find\(\s*text,\s*'
        r'\'"args":\{\}\',\s*1,\s*true\)\s*~=\s*nil', inbound) is not None and
        'kind ~= "call" or function_name ~= "getStatus" or' in inbound and
        'not empty_args then' in inbound,
        "Lua pipe argument parsing boundary changed: requalify getStatus policy")
    backend_call = section(backend, "private async Task<JsonElement?> CallLuaAsync(",
                           "// Original set_automation handler")
    require('"getStatus"' in backend_call and
            re.search(r'args\.ValueKind\s*!=\s*JsonValueKind\.Object', backend_call) and
            re.search(r'using\s+JsonElement\.ObjectEnumerator\s+properties\s*='
                      r'\s*args\.EnumerateObject\(\);\s*'
                      r'if\s*\(properties\.MoveNext\(\)\)', backend_call) is not None,
            "backend getStatus empty-object guard changed")
    return {
        "luaArgumentCheck": "unscoped.literal.substring",
        "luaParsedArgsObject": False,
        "backendPublicGetStatusRequiresEmptyObject": True,
        "independentNativeGroupCommandOrReply": False,
    }


def classify_controlled_pipe_arguments(raw_json: str) -> dict:
    """Compare literal Lua args test with JSON payload object, never run Lua.

    No proof that a malformed frame can originate on the authenticated pipe:
    controlled bytes only, no connection/session/packet authentication.
    """
    def unique_keys(items: list[tuple[str, object]]) -> dict:
        out: dict = {}
        for name, value in items:
            require(name not in out, "duplicate JSON property in controlled pipe fixture")
            out[name] = value
        return out

    require(isinstance(raw_json, str), "invalid controlled pipe JSON fixture")
    try:
        frame = json.loads(raw_json, object_pairs_hook=unique_keys)
    except BoundaryError:
        raise
    except (TypeError, ValueError) as exc:
        raise BoundaryError("invalid controlled pipe JSON fixture") from exc
    require(isinstance(frame, dict) and frame.get("type") == "command" and
            isinstance(frame.get("payload"), dict) and
            frame["payload"].get("fn") == "getStatus",
            "controlled pipe fixture is not getStatus command")
    args = frame["payload"].get("args")
    strictly_empty = isinstance(args, dict) and len(args) == 0
    loose_match = '"args":{}' in raw_json
    return {
        "luaLiteralEmptyArgsMatched": loose_match,
        "actualPayloadArgsEmpty": strictly_empty,
        "luaLiteralFalseAcceptance": loose_match and not strictly_empty,
        "safeToAttestNativeGroup": False,
    }


def audit_pipe_result_correlation_source(
    *, protocol: str | None = None, registry: str | None = None,
    transport: str | None = None, overview: str | None = None,
) -> dict:
    """Inspect the actual authenticated RPC Result path, NOT native replies.

    The original R7-097 host deliberately leaves the outer Result envelope's
    requestId unchecked; the registry matches *payload.id* within the session's
    authenticated profile/instance route. Both this limited positive and its
    explicit missing native packet binding matter. This function source-gates
    that conclusion without starting any .NET, Lua, pipe, app or game process.
    """
    protocol = PIPE_PROTOCOL.read_text(encoding="utf-8-sig") if protocol is None else protocol
    registry = PIPE_CALL_REGISTRY.read_text(encoding="utf-8-sig") if registry is None else registry
    transport = (PIPE_SESSION_TRANSPORT.read_text(encoding="utf-8-sig")
                 if transport is None else transport)
    overview = OVERVIEW.read_text(encoding="utf-8-sig") if overview is None else overview

    parsed = section(protocol, "public static LWBridgeCallResult ParseCallResult(",
                     "// IMPLEMENTATION POLICY: compare only values")
    require('string id = RequireString(body, "id")' in parsed and
            'bool ok = RequireBoolean(body, "ok")' in parsed and
            'envelope.ProfileId,' in parsed and 'envelope.InstanceId,' in parsed and
            'envelope.RequestId,' in parsed and 'envelope.Timestamp,' in parsed and
            re.search(r'\breturn new\(\s*envelope.Version,\s*envelope.Type,', parsed) is not None,
            "pipe Result parser body id/outer envelope contract changed")
    require('string.Equals(envelope.Type, ResultType, StringComparison.Ordinal)' in parsed,
            "pipe Result message type check changed")

    inbound = section(transport, "private async Task InboundLoopAsync(",
                      "private async Task<InboundFrame?> TryReadFrameAsync(")
    require(re.search(
        r'LWBridgeControlPipeProtocol\.ParseCallResult\(\s*frame\.Payload\s*\).*?'
        r'LWBridgeAuthenticatedConnection\s+connection\s*=\s*session\.Connection;\s*'
        r'if\s*\(calls\.TryCompleteResult\(\s*connection\.ProfileId,\s*'
        r'connection\.InstanceId,\s*result\)\)', inbound, re.S) is not None and
        'Interlocked.Increment(ref unknownResults)' in inbound,
        "pipe Result authenticated session routing changed")

    completion = section(registry, "internal bool TryCompleteResult(",
                         "internal bool TryFailPending(")
    require(re.search(
        r'if\s*\(!pending\.TryGetValue\(result\.Id,\s*out entry\)\)\s*'
        r'return false;', completion) is not None and
        re.search(r'pending\.Remove\(result\.Id\);\s*entry\.Timeout\.Cancel\(\);',
                  completion) is not None,
        "pipe result pending lookup/removal contract changed")
    require(re.search(
        r'if\s*\(!string\.Equals\(\s*entry\.ProfileId,\s*'
        r'authenticatedProfileId,\s*StringComparison\.Ordinal\)\s*\|\|\s*'
        r'!string\.Equals\(\s*entry\.InstanceId,\s*authenticatedInstanceId,\s*'
        r'StringComparison\.Ordinal\)\)\s*\{\s*return false;\s*\}', completion) is not None,
        "pipe result route owner guard changed")
    require(not re.search(r'\bresult\.(?:RequestId|ProfileId|InstanceId|Timestamp)\b',
                          completion),
            "pipe Result outer envelope validation changed: requalify correlation claim")

    writer = section(overview, "local function write_pipe_result(control, request_id, result)",
                     "-- CURRENT-CLIENT EQUIVALENT IMPLEMENTATION:")
    require(re.search(
        r'type\s*=\s*"result",\s*profileId\s*=\s*control\.profileId,\s*'
        r'instanceId\s*=\s*control\.sessionId,\s*requestId\s*=\s*request_id,'
        r'\s*timestamp\s*=.*?payload\s*=\s*\{\s*id\s*=\s*request_id,\s*'
        r'ok\s*=\s*true,\s*result\s*=\s*result,', writer, re.S) is not None,
        "Lua pipe Result writer's authenticated route/payload ID contract changed")
    return {
        "pendingLookup": "result.payload.id",
        "ownerBinding": "authenticated.pipe.profileId+instanceId",
        "luaProducerEmitsMatchingOuterAndPayloadIds": True,
        "outerEnvelopeRequestIdCorrelated": False,
        "outerEnvelopeProfileAndInstanceCorrelated": False,
        "nativeGroupReceiptAvailable": False,
        "expiredUnknownOrDuplicatePayloadIdCorrelated": False,
    }


def correlate_pipe_status_result_only(
    pending: dict[str, tuple[str, str]],
    authenticated_route: tuple[str, str],
    result: dict,
) -> dict:
    """Controlled C# correlation *subset*: no frame/packet authentication.

    Deliberately reflect the original host policy of ignoring outer Result
    identity when matching a pending call. The modeled authenticated route is
    supplied by the caller, never inferred from JSON or a self-asserted field.
    Consume the pending ID only when the exact route owns it, as registry does.
    A positive value remains ONLY a pending Lua getStatus result.
    """
    body = result.get("payload")
    require(result.get("version") == 1 and result.get("type") == "result" and
            isinstance(body, dict) and isinstance(body.get("id"), str) and
            bool(body["id"]) and type(body.get("ok")) is bool and
            all(isinstance(result.get(name), str) for name in (
                "profileId", "instanceId", "requestId")) and
            type(result.get("timestamp")) in (int, float),
            "unsupported synthetic current pipe Result envelope")
    matched = pending.get(body["id"]) == authenticated_route
    if matched:
        pending.pop(body["id"])
    outer_matches = (result["profileId"], result["instanceId"]) == authenticated_route and (
        result["requestId"] == body["id"])
    return {
        "pendingCallCorrelated": matched,
        "outerEnvelopeMatches": outer_matches,
        "nativeGroupReceiptProven": False,
    }


@dataclass(frozen=True)
class ProducerContract:
    modes: frozenset[str]
    output_keys: frozenset[str]
    producer_version: str
    command_path: str
    result_path: str

    @classmethod
    def read_source(cls, *, lua: str | None = None,
                    managed: str | None = None, base: str | None = None,
                    receiver: str | None = None) -> "ProducerContract":
        lua = LUA.read_text(encoding="utf-8-sig") if lua is None else lua
        managed = MANAGED.read_text(encoding="utf-8-sig") if managed is None else managed
        base = BASE.read_text(encoding="utf-8-sig") if base is None else base
        receiver = RECEIVER.read_text(encoding="utf-8-sig") if receiver is None else receiver
        reader = section(lua, "local function read_bulk_aoi_diagnostic(now)",
                         "local function collection_contains_int")
        writer = section(lua, "local function write_bulk_aoi_result(",
                         "local function try_recover_zoom_aoi_geometry")
        desktop = section(managed, "private async Task<FastCityBatchObservation> ProbeFastCityBatchAsync(",
                          "internal static void ValidateNativeMarchTelemetry")
        output = section(writer, "write_json(bulk_aoi_diagnostic_result_path, {", "\n    })")
        # Lua table fields are top-level; strip comments and find name = value
        # at start of line or after a comma (including two assignments per line).
        output = "\n".join(line.split("--", 1)[0] for line in output.splitlines())
        keys = set(re.findall(r"(?:^\s*|,\s*)([A-Za-z_]\w*)\s*=", output, re.M))
        modes = set(re.findall(r'request\.requestMode\s*~=\s*"([^"]+)"', reader))
        version = re.search(r'ProbeVersion\s*=\s*"([^"]+)"', base)
        lua_version = re.search(r'local M = \{ VERSION = "([^"]+)" \}', lua)
        commands = re.search(r'string commandPath = Path.Combine\(probeRuntimeRoot, "([^"]+)"\)', desktop)
        results = re.search(r'string resultPath = Path.Combine\(probeRuntimeRoot, "([^"]+)"\)', desktop)
        required = (
            'read_kv_file(bulk_aoi_diagnostic_path, 4096)' in reader,
            'pcall(os.remove, bulk_aoi_diagnostic_path)' in reader,
            'request.profileId ~= identity.profileId' in reader,
            'request.launchSessionId ~= identity.sessionId' in reader,
            'request.challenge ~= identity.challenge' in reader,
            'request.gamePid ~= identity.gamePid' in reader,
            'request.requestMode == "coverage"' in lua,
            'indices = { target_aoi_index }' in lua,
            'MatchesString(root.Value, "requestId", requestId)' in desktop,
            'ValidateFastCityBatchResult(' in desktop,
            'await WriteCommandAsync(commandPath, command, cancellationToken)' in desktop,
            '"requestMode=coverage"' in desktop,
            'WriteCommandAsync(' in base,
            'RequireFreshCaptureTime(' in base,
            'NO production implementation of this interface' in receiver,
            re.search(r'const int MaximumNativePointAoisPerReply\s*=\s*(\d+)', receiver) is not None,
        )
        if (not all(required) or not version or not lua_version or
                not commands or not results or lua_version.group(1) != version.group(1)):
            raise BoundaryError("source-backed single-file protocol anchors changed")
        # Mere presence of four identity strings is not an ownership guard.
        # Changing ONE disjunction to a conjunction would admit foreign
        # individual probes, while leaving all previous token checks green.
        owner_guard = (
            r'\bif\s+request\.profileId\s*~=\s*identity\.profileId\s+or\s+'
            r'request\.launchSessionId\s*~=\s*identity\.sessionId\s+or\s+'
            r'request\.challenge\s*~=\s*identity\.challenge\s+or\s+'
            r'request\.gamePid\s*~=\s*identity\.gamePid\s+then\s+'
            r'request\.error\s*=\s*"bulk_aoi_diagnostic_identity_mismatch"\s+end'
        )
        if re.search(owner_guard, reader) is None:
            raise BoundaryError("bulk AOI owner guard changed: requalify producer identity")
        if modes != {"native", "expanded", "direct", "coverage", "anchor", "edge", "zoom"}:
            raise BoundaryError(f"Lua request-mode whitelist changed: {sorted(modes)}")
        limit = re.search(r'const int MaximumNativePointAoisPerReply\s*=\s*(\d+)', receiver)
        if limit is None or int(limit.group(1)) != SOURCE_NATIVE_REPLY_LIMIT:
            raise BoundaryError("native AOI reply cohort contract changed")
        if not {"schemaVersion", "probeVersion", "requestId", "profileId",
                "launchSessionId", "challenge", "gamePid", "state", "requestedIndices",
                "nativeCurrentSetCount", "point_records", "capturedAt",
                "cameraGeometryRestored"}.issubset(keys):
            raise BoundaryError("Lua result writer's one-probe field schema changed")
        if GROUP_ONLY_KEYS.intersection(keys):
            raise BoundaryError("Lua writer has new group fields: reevaluate transport boundary")
        if commands.group(1) != "bulk-aoi-diagnostic.txt" or results.group(1) != "bulk-aoi-diagnostic-result.json":
            raise BoundaryError("desktop changed its one-command/one-result transport")
        return cls(frozenset(modes), frozenset(keys), version.group(1),
                   commands.group(1), results.group(1))


def audit_other_source_transports(
    *, overview: str | None = None, backend: str | None = None,
    adapter: str | None = None, probe: str | None = None,
    provider: str | None = None, receiver: str | None = None,
    desktop_sources: dict[str, str] | None = None,
) -> dict:
    """Source contract of the *other* two actual Lua producers/transport routes.

    A generic authenticated RPC pipe is real and useful for Home operations;
    its Lua consumer currently supports only getStatus({}). Unknown changes to
    that consumer, ownership pump or Map317 source wiring are REVIEW REQUIRED,
    never silently counted as proof that only per-probe transport exists.
    """
    overview = OVERVIEW.read_text(encoding="utf-8-sig") if overview is None else overview
    backend = BACKEND.read_text(encoding="utf-8-sig") if backend is None else backend
    adapter = GAME_PIPE.read_text(encoding="utf-8-sig") if adapter is None else adapter
    probe = LUA.read_text(encoding="utf-8-sig") if probe is None else probe
    provider = PROVIDER.read_text(encoding="utf-8-sig") if provider is None else provider
    receiver = RECEIVER.read_text(encoding="utf-8-sig") if receiver is None else receiver
    pipe = section(overview, "local function process_pipe_inbound(control)",
                   "local RECOVERY_WINDOWS")
    # Check actual short-circuit rejects, not just the names of their fields.
    # An altered 'or' can preserve every old string anchor and still admit a
    # mixed owner or a command other than the sole getStatus({}) route.
    owner_reject = (
        r'\bif\s+version\s*~=\s*1\s+or\s+'
        r'profile_id\s*~=\s*control\.profileId\s+or\s+'
        r'instance_id\s*~=\s*control\.sessionId\s+or\s+'
        r'request_id\s*==\s*nil\s+then\s+'
        r'pipe_runtime\.state\s*=\s*"error"\s+'
        r'pipe_runtime\.error\s*=\s*"pipe_inbound_identity_invalid"\s+return'
    )
    require(re.search(owner_reject, pipe) is not None,
            "named-pipe owner rejection guard changed")
    command_reject = (
        r'\bif\s+request_id\s*==\s*""\s+or\s+'
        r'payload_id\s*~=\s*request_id\s+or\s+'
        r'kind\s*~=\s*"call"\s+or\s+'
        r'function_name\s*~=\s*"getStatus"\s+or\s+'
        r'not\s+empty_args\s+then\s+'
        r'pipe_runtime\.state\s*=\s*"error"\s+'
        r'pipe_runtime\.error\s*=\s*"pipe_command_unsupported"\s+return'
    )
    require(re.search(command_reject, pipe) is not None,
            "named-pipe command rejection guard changed")
    # The Lua receiver is the enforcement boundary, irrespective of generic
    # desktop RPC framing and internal callers for catalog options.
    handled_types = set(re.findall(
        r'(?:if|elseif)\s+message_type\s*==\s*"([^"]+)"', pipe))
    allowed_lua = set(re.findall(r'function_name\s*~=\s*"([^"]+)"', pipe))
    require(handled_types == {"hello.ack", "command"} and
            allowed_lua == {"getStatus"} and
            'kind ~= "call"' in pipe and
            'not empty_args' in pipe and
            'pipe_command_unsupported' in pipe and
            'write_pipe_result(' in pipe,
            "named-pipe Lua receiver changed: requalify other transport before no-group claim")
    result_text = section(pipe, "local result = {", "\n            }")
    status_keys = set(re.findall(r'^\s*([A-Za-z_]\w*)\s*=', result_text, re.M))
    require(status_keys == {"bridgeVersion", "profileId", "instanceId",
                            "gameStateObserved", "gameReady", "loggedIn",
                            "connected", "connecting", "gameUid", "serverId",
                            "worldPos"},
            "named-pipe Lua result payload changed: requalify actual native source")
    exposed_lua = section(backend,
                          "private async Task<JsonElement?> CallLuaAsync(",
                          "// Original set_automation handler")
    require('"getStatus"' in exposed_lua and
            'Only call_lua(getStatus,{})' in exposed_lua and
            '"getTradeStationCatalog"' in backend and
            '"getMonsterCatalogOptions"' in backend and
            '"getSquads"' in backend,
            "managed pipe RPC routing changed: requalify supported Lua calls")
    require('PipeClientAdapter' in adapter and
            '"pipe-inbound-" + sequence.ToString("D8")' in adapter and
            '"pipe-outbound-" + sequence.ToString("D8")' in adapter and
            'pipe_mailbox_path("inbound", sequence)' in pipe,
            "pipe adapter/mailbox mechanism changed")
    dispatch = section(probe,
                       "function M._sharedRuntimeOwnership.PumpVerified(now)",
                       "function M.Pump()")
    discovered_pumps = set(re.findall(
        r'\b(pump_[a-z_]+|asset_image_runtime\.pump|'
        r'M\._treasureStateRuntime\.pump|resource_scan_detail_runtime\.pump|'
        r'train_list_runtime\.pump)\s*\(', dispatch))
    expected_pumps = {
        "asset_image_runtime.pump", "M._treasureStateRuntime.pump",
        "pump_monster_protection_queue", "pump_monster_protection_detail",
        "pump_resource_detail_diagnostic", "resource_scan_detail_runtime.pump",
        "train_list_runtime.pump", "pump_bulk_aoi_diagnostic",
        "pump_runtime_diagnostic", "pump_aoi_diagnostic",
    }
    require(discovered_pumps == expected_pumps and
            'read_command()' in dispatch and
            'if active_request_id == nil then' in dispatch,
            "Lua probe dispatch changed: requalify any new source/consumer lane")
    require("createSourceForAcceptedRun = null" in provider and
            "No production caller" in provider and
            "NO production implementation of this interface" in receiver and
            "interface IMap014ReceivedNativeSharedGroupIngress" in receiver,
            "production Map317 group ingress or selector declaration changed")
    require("Map014NativeSharedReceiptSource(" in receiver and
            "AcknowledgeAlreadyReceivedHomeAsync(" in receiver and
            "ReceiveAlreadyCompletedAsync(" in receiver,
            "future received-only native group contract changed")
    # The provider's default null factory is insufficient by itself: a new
    # composition root or ingress implementation could be added elsewhere.
    # Inspect *all* production desktop .cs files, including nested folders,
    # but not generated bin/obj outputs or separate test fixtures. Conservative
    # references (even comments) force requalification, never a fake positive.
    if desktop_sources is None:
        desktop_sources = {
            str(path.relative_to(DESKTOP_SOURCE_DIR)): path.read_text(encoding="utf-8-sig")
            for path in DESKTOP_SOURCE_DIR.rglob("*.cs")
            if path.is_file() and not {"bin", "obj"}.intersection(
                path.relative_to(DESKTOP_SOURCE_DIR).parts)
        }
    actual_group_references = sorted(name for name, source in desktop_sources.items()
                                     if name != RECEIVER.name and
                                     ("Map014NativeSharedReceiptSource" in source or
                                      "IMap014ReceivedNativeSharedGroupIngress" in source))
    require(not actual_group_references,
            "production native group ingress or wiring changed: " +
            ", ".join(actual_group_references[:8]))
    return {
        "namedPipeTransportExists": True,
        "namedPipeLuaFunctions": sorted(allowed_lua),
        "namedPipeAcceptedEnvelopeTypes": sorted(handled_types),
        "internalBackendPipeFunctionsWithoutCurrentLuaHandler": [
            "getMonsterCatalogOptions", "getSquads", "getTradeStationCatalog",
        ],
        "pipeNativeGroupProducer": False,
        "producerOtherPumps": sorted(discovered_pumps - {"pump_bulk_aoi_diagnostic"}),
        "producerNativeGroupPump": False,
        "Map317ProductionGroupFactoryConfigured": False,
        "futureGroupIngressImplementationPresent": False,
        "otherProductionGroupReferences": actual_group_references,
    }


def require(condition: bool, reason: str) -> None:
    if not condition:
        raise BoundaryError(reason)


def instant(value: str) -> datetime:
    require(isinstance(value, str), "invalid capture timestamp")
    try:
        dt = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exc:
        raise BoundaryError("invalid capture timestamp") from exc
    require(dt.tzinfo is not None, "unqualified capture timezone")
    return dt.astimezone(timezone.utc)


def audit_one_probe_correlation(command: dict, result: dict,
                                started: datetime, now: datetime,
                                contract: ProducerContract) -> None:
    """Mirror the *correlation subset* of the existing file protocol.

    Acceptance means only a correlated *single* file probe, not native AOI
    completeness, record accuracy or live group authentication.
    """
    require(command.get("schema") == "1" and
            command.get("probeVersion") == contract.producer_version,
            "unknown single-probe request schema")
    require(command.get("requestMode") in contract.modes, "unsupported producer request mode")
    require(bool(command.get("scanRunId")) and bool(command.get("requestId")),
            "missing original per-probe identity")
    require(all(command.get(k) for k in OWNER_FIELDS), "missing request owner")
    require(result.get("schemaVersion") == 1 and
            result.get("probeVersion") == contract.producer_version,
            "result schema/version mismatch")
    require(result.get("requestId") == command["requestId"], "old or foreign per-probe result file")
    require(all(result.get(k) == command[k] for k in OWNER_FIELDS),
            "per-probe owner/profile/session mismatch")
    require(result.get("requestMode") == command["requestMode"] and
            result.get("state") == "proven", "per-probe mode or result failure")
    stamp = instant(result.get("capturedAt"))
    require(started.tzinfo is not None and now.tzinfo is not None, "unqualified caller clock")
    require((stamp - started).total_seconds() >= -2 and
            (stamp - now).total_seconds() <= 5,
            "stale or future per-probe result")


def audit_current_producer_group_request(request: dict, contract: ProducerContract) -> None:
    """Current mode whitelist and writer cannot create a group request/response."""
    require(request.get("requestMode") in contract.modes, "unsupported producer group command")
    # A selected stage echo on a legal single request has no native group
    # ownership or reply semantics. Unknown file keys are not producer output.
    require(not any(name in request for name in (
        "groupGeneration", "selectedStage", "selectedStageEcho", "regionOrdinal")),
        "forged group/stage labels on one-shot producer command")
    raise BoundaryError("one-shot producer mode has no authenticated A/B/C group transport")


def audit_current_producer_group_result(result: dict, contract: ProducerContract) -> None:
    """Only declared Lua writer fields can be attributed to this producer."""
    invented = set(result).difference(contract.output_keys)
    require(not invented, "receipt contains fields that Lua producer cannot emit: " +
            ",".join(sorted(invented)[:8]))
    raise BoundaryError("one-probe result lacks 3 native stages, 10000 AOI proofs, 2500 kind blocks and home ACK")


def audit_future_shape_only(claim: dict, *, selected_kind: str = "city") -> None:
    """Adversarial sanity check; a pass is NEVER independent native authentication."""
    owner = claim.get("owner")
    require(isinstance(owner, dict), "missing pinned group owner")
    for key in ("scanRunId", "profileId", "launchSessionId", "challenge", "gamePid",
                "serverId", "worldId", "groupGeneration"):
        require(key in owner and owner[key] is not None and owner[key] != "",
                f"missing group owner {key}")
    require(selected_kind == "city", "only City future shape under present readiness")
    stages = claim.get("stages")
    require(isinstance(stages, list) and len(stages) == 3, "need exactly 3 native A/B/C stages")
    ack = claim.get("homeAck")
    require(isinstance(ack, dict), "native final home ACK missing")
    home_id = ack.get("homeRestoreRequestId")
    seen_ids: set[str] = set()
    last_reply = 0
    for ordinal, stage in enumerate(stages):
        require(isinstance(stage, dict), "missing native stage")
        require(all(stage.get(k) == owner[k] for k in owner), "mixed stage profile/session/run")
        request_id = stage.get("requestId")
        reply_id = stage.get("nativeReplyRequestId")
        require(bool(request_id) and request_id == reply_id and request_id not in seen_ids and
                stage.get("nativeReplyState") == "matched", "missing, duplicated or foreign native stage reply")
        seen_ids.add(request_id)
        require(stage.get("regionOrdinal") == ordinal, "out-of-order A/B/C region")
        began = stage.get("requestStartedAtUnixMs", 0)
        ended = stage.get("nativeReplyAtUnixMs", 0)
        require(type(began) is int and type(ended) is int and
                began > last_reply and 0 < ended - began <= 20000,
                "stale or overlapping native stage reply")
        last_reply = ended
    aois = claim.get("aoiProofs")
    require(isinstance(aois, list) and len(aois) == 10000, "10000 per-AOI native proofs absent")
    found: set[int] = set()
    per_block: dict[int, tuple[str, int]] = {}
    reply_cohorts: dict[str, tuple[int, int]] = {}
    for proof in aois:
        require(isinstance(proof, dict) and
                all(proof.get(k) == owner[k] for k in owner),
                "mixed native AOI owner/profile/session")
        idx = proof.get("nativeAoiIndex")
        require(type(idx) is int and 0 <= idx < 10000 and idx not in found,
                "duplicate, missing or invalid native AOI")
        require(proof.get("nativeSource") == "WorldPointManager._curViewIndex",
                "missing or foreign native AOI source")
        found.add(idx)
        col, row = idx % 100, idx // 100
        block = row // 2 * 50 + col // 2
        reply = (proof.get("nativeReplyRequestId"), proof.get("nativeReplyAtUnixMs"))
        require(reply[0] != home_id, "home reply reused as native AOI evidence")
        require(bool(reply[0]) and type(reply[1]) is int and
                stages[0]["requestStartedAtUnixMs"] <= reply[1] < ack.get("homeNativeReplyAtUnixMs", 0),
                "stale/missing native AOI response")
        if reply[0] in reply_cohorts:
            cohort_time, cohort_count = reply_cohorts[reply[0]]
            require(cohort_time == reply[1] and cohort_count < SOURCE_NATIVE_REPLY_LIMIT,
                    "native AOI reply cohort time or 160-AOI bound violated")
            reply_cohorts[reply[0]] = (cohort_time, cohort_count + 1)
        else:
            reply_cohorts[reply[0]] = (reply[1], 1)
        require(block not in per_block or per_block[block] == reply,
                "four AOIs in a logical block lack one correlated native response")
        per_block[block] = reply
    require(len(found) == 10000 and len(per_block) == 2500,
            "incomplete native AOI coverage")
    kinds = claim.get("kindBlocks")
    require(isinstance(kinds, list) and len(kinds) == 2500,
            "2500 type-enriched logical blocks absent")
    seen_blocks: set[int] = set()
    for typed in kinds:
        idx = typed.get("blockIndex")
        require(type(idx) is int and 0 <= idx < 2500 and idx not in seen_blocks and
                typed.get("kind") == selected_kind and
                (typed.get("nativeReplyRequestId"), typed.get("nativeReplyAtUnixMs")) == per_block[idx] and
                typed.get("source") == "WorldPointManager._pointInfos",
                "logical block type/source/reply missing, duplicated or mismatched")
        seen_blocks.add(idx)
    require(all(ack.get(k) == owner[k] for k in owner), "final home owner changed")
    require(bool(home_id) and home_id not in seen_ids and
            ack.get("homeNativeReplyRequestId") == home_id and
            ack.get("nativeHomeReplyState") == "matched" and
            ack.get("originalCameraRestored") is True and
            type(ack.get("homeNativeReplyAtUnixMs")) is int and
            ack["homeNativeReplyAtUnixMs"] > last_reply,
            "one independently correlated native final home reply absent")


def archive_rejection(path: Path, contract: ProducerContract) -> dict:
    """Read archived stage files. Do not upgrade their native diagnostic success."""
    snapshot_lua = path / "experimental-resource-probe.lua"
    # The historical experiment added native-observer *diagnostic* fields
    # beyond today's checked-in base writer. Compare those exact archival
    # producer bytes before deciding a stage contains an invented field.
    historic_contract = (ProducerContract.read_source(
        lua=snapshot_lua.read_text(encoding="utf-8-sig"))
        if snapshot_lua.exists() else contract)
    regions = [json.loads((path / f"sequence-region-{i}.json").read_text(encoding="utf-8-sig"))
               for i in (1, 2, 3)]
    reasons = []
    for idx, record in enumerate(regions, 1):
        try:
            audit_current_producer_group_result(record, historic_contract)
        except BoundaryError as exc:
            reasons.append(f"{idx}: {exc}")
    require(len(reasons) == 3, "archived stage erroneously passed producer-group boundary")
    # Read only the leading numeric summary; never use the large native rows as
    # substitute authenticated per-AOI reply receipts.
    summary_path = path / "full-sequence-result.json"
    if summary_path.exists():
        with summary_path.open(encoding="utf-8-sig") as stream:
            summary = stream.read(300)
    else:
        summary = ""
    fields = {k: float(v) for k, v in
              re.findall(r'"(nativeCoverage|nativeTotal|probes|elapsedSeconds)"\s*:\s*([\d.]+)', summary)}
    return {"archive": str(path.relative_to(ROOT)),
            "producerSnapshot": snapshot_lua.name if snapshot_lua.exists() else "not available",
            "sourceResultKeys": len(historic_contract.output_keys),
            "stageCount": 3,
            "groupQualified": False, "summary": fields, "reasons": reasons}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", action="append", type=Path, help="read-only sequence archive")
    args = parser.parse_args()
    contract = ProducerContract.read_source()
    other = audit_other_source_transports()
    pipe_result = audit_pipe_result_correlation_source()
    pipe_arguments = audit_lua_pipe_argument_boundary()
    outcomes = [archive_rejection(path, contract)
                for path in (args.archive or ARCHIVES) if path.exists()]
    print(json.dumps({
        "producerVersion": contract.producer_version,
        "command": contract.command_path, "result": contract.result_path,
        "singleProbeModes": sorted(contract.modes),
        "sourceResultKeys": len(contract.output_keys),
        "unemittableGroupKeys": sorted(GROUP_ONLY_KEYS.difference(contract.output_keys)),
        "alternateRoutes": other,
        "actualPipeResultCorrelation": pipe_result,
        "luaPipeArgumentBoundary": pipe_arguments,
        "authenticatedGroupTransport": False, "archived": outcomes,
        "qualification": "CURRENT PRODUCER SINGLE-PROBE ONLY; NATIVE GROUP NOT PROVEN",
    }, indent=2))


if __name__ == "__main__":
    main()
