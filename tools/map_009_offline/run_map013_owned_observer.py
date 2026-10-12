#!/usr/bin/env python3
"""One real task-owned passive native-event probe with exact owned Stop.

Runs *existing* Overview helper start/stop (including its original-script
backup/recovery journal). No keyboard, mouse, foreground or Chrome access.
The private Lua observer adds subscribers without replacing game handlers.
Only raw records in ignored artifacts; no automatic candidate acceptance.
"""
from __future__ import annotations

import hashlib
import argparse
from dataclasses import dataclass
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import secrets
import shutil
import sys
import threading
import time
import uuid

REPO = Path(__file__).resolve().parents[2]
TOOLS = REPO / "tools"
sys.path.insert(0, str(TOOLS))
import run_overview_bridge_current as admitted
overview = admitted.base  # shim applies current-client v24 hash/compatibility gate

BASE = REPO / "artifacts" / "map-013-live"
TRACE_SOURCE = Path(__file__).with_name("map013_wrapper_trace.lua")
GAME_ROOT = Path.home() / "AppData/Local/FunFly/Last War-Survival Game"


def publish_command(path: Path, values: dict[str, str]) -> int:
    if path.exists():
        raise RuntimeError(f"owned command already pending: {path.name}")
    path.parent.mkdir(parents=True, exist_ok=True)
    text = "".join(f"{k}={v}\n" for k, v in values.items())
    temp = path.with_name(path.name + "." + uuid.uuid4().hex + ".tmp")
    temp.write_text(text, encoding="utf-8")
    published_at_ns = time.time_ns()
    os.replace(temp, path)
    return published_at_ns


class ReplyValidationError(RuntimeError):
    """Response exists, but cannot be attributed to the exact admitted owner."""


@dataclass(frozen=True)
class ExpectedReply:
    kind: str  # world, region, or trace (three distinct actual emitters)
    request_id: str
    profile_id: str
    session_id: str
    game_pid: int
    challenge: str
    published_at_ns: int
    server_id: int | None = None
    world_id: int | None = None
    request_mode: str | None = None


def _exact_int(value: object, *, minimum: int = 0) -> bool:
    return type(value) is int and value >= minimum


def validate_reply(value: object, expected: ExpectedReply, *, file_mtime_ns: int) -> str:
    """Return pending or terminal. Reject wrong/missing owner *before* state.

    World's sessionId and probe's launchSessionId are different schemas.
    A copied serverId from an outgoing request is never a native provenance
    witness. The rules here validate only diagnostic file ownership/context.
    """
    if expected.kind not in ("world", "region", "trace"):
        raise ValueError("unsupported diagnostic reply kind")
    if not isinstance(value, dict):
        raise ReplyValidationError("reply must be an object")
    if value.get("requestId") != expected.request_id:
        raise ReplyValidationError("reply requestId mismatch")
    if type(value.get("schemaVersion")) is not int or value["schemaVersion"] != 1:
        raise ReplyValidationError("reply schemaVersion absent or invalid")
    version_field = "bridgeVersion" if expected.kind == "world" else "probeVersion"
    version_value = (overview.BRIDGE_VERSION if expected.kind == "world"
                     else "lwbridge-live-resource-probe-2")
    if value.get(version_field) != version_value:
        raise ReplyValidationError(f"reply {version_field} absent or mismatched")
    session_field = "sessionId" if expected.kind == "world" else "launchSessionId"
    for name, correct in (("profileId", expected.profile_id),
                          (session_field, expected.session_id),
                          ("gamePid", expected.game_pid),
                          ("challenge", expected.challenge)):
        supplied = value.get(name)
        if (name == "gamePid" and
                (type(supplied) is not int or supplied != correct)) or (
                name != "gamePid" and
                (not isinstance(supplied, str) or supplied != correct)):
            raise ReplyValidationError(f"reply {name} absent or mismatched")
    # mtime is an independent filesystem-side bound, not owner provenance.
    # One-second tolerance covers FAT/portable timestamp granularity.
    if file_mtime_ns + 1_000_000_000 < expected.published_at_ns:
        raise ReplyValidationError("reply file predates request publication")
    captured = value.get("capturedAt")
    if not isinstance(captured, str):
        raise ReplyValidationError("reply capturedAt absent")
    try:
        captured_dt = datetime.strptime(captured, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=timezone.utc)
    except ValueError as error:
        raise ReplyValidationError("reply capturedAt malformed") from error
    captured_ns = int(captured_dt.timestamp() * 1e9)
    if captured_ns + 2_000_000_000 < expected.published_at_ns:
        raise ReplyValidationError("reply capture time predates request publication")
    if captured_ns > time.time_ns() + 5_000_000_000:
        raise ReplyValidationError("reply future capture time")
    state = value.get("state")
    allowed_states = ("proven", "failed", "pending") if expected.kind != "trace" else (
        "observed", "observer_error", "pending")
    if state not in allowed_states:
        raise ReplyValidationError("reply state absent or invalid")
    if expected.kind == "world":
        if state == "proven":
            if not _exact_int(value.get("serverId"), minimum=1):
                raise ReplyValidationError("world reply lacks actual positive serverId")
            if not _exact_int(value.get("worldId")):
                raise ReplyValidationError("world reply lacks actual worldId")
            if expected.server_id is not None and value["serverId"] != expected.server_id:
                raise ReplyValidationError("world reply serverId mismatched")
            if expected.world_id is not None and value["worldId"] != expected.world_id:
                raise ReplyValidationError("world reply worldId mismatched")
            for field in ("tileWidth", "tileHeight"):
                if not _exact_int(value.get(field), minimum=1):
                    raise ReplyValidationError(f"world reply {field} invalid")
            for field, bound in (("playerTileX", value["tileWidth"]),
                                 ("playerTileY", value["tileHeight"])):
                # current_map_context() explicitly allows a missing player
                # point until the native manager is populated. Never invent
                # a coordinate, but validate any coordinate it did emit.
                if value.get(field) is not None and (
                    not _exact_int(value[field]) or value[field] >= bound
                ):
                    raise ReplyValidationError(f"world reply {field} invalid")
            if value.get("error") is not None:
                raise ReplyValidationError("proven world reply contains error")
        elif state == "failed" and (not isinstance(value.get("error"), str) or
                                    not value["error"]):
            raise ReplyValidationError("failed world reply lacks error")
    elif expected.kind == "region":
        if value.get("requestMode") != expected.request_mode:
            raise ReplyValidationError("region requestMode absent or mismatched")
        # This output copies the source request's serverId into no response
        # field at all. It cannot independently attest remote provenance.
        if state == "proven":
            for field in ("targetTileX", "targetTileY", "targetAoiIndex"):
                if not _exact_int(value.get(field)):
                    raise ReplyValidationError(f"region reply {field} absent")
        elif state == "failed" and (not isinstance(value.get("error"), str) or
                                    not value["error"]):
            raise ReplyValidationError("failed region reply lacks error")
    elif expected.kind == "trace":
        if state == "observed" and (not isinstance(value.get("events"), list) or
                                     not isinstance(value.get("registrations"), list)):
            raise ReplyValidationError("observed trace arrays absent")
        if state == "observer_error" and not isinstance(value.get("errors"), list):
            raise ReplyValidationError("error trace diagnostics absent")
    return state


def wait_for(path: Path, expected: ExpectedReply, seconds: float) -> dict:
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        if path.is_file():
            try:
                result = json.loads(path.read_text(encoding="utf-8"))
                state = validate_reply(result, expected, file_mtime_ns=path.stat().st_mtime_ns)
                if state != "pending":
                    return result
            except (OSError, ValueError, UnicodeError):
                pass
        time.sleep(0.2)
    raise TimeoutError(f"{path.name} did not contain a validated terminal owner reply within bound")


def snapshot_worldready_stages(
    bridge_root: Path, *, profile: str, session: str, challenge: str,
    game_pid: int, request_id: str,
) -> dict:
    """Read-only exact-owner boundary snapshot; no credentials are exported.

    This cannot prove a native packet. Without opt-in Lua stage evidence,
    disappearance of a command file cannot identify who consumed it.
    """
    request_path = bridge_root / "world-ready.txt"
    result_path = bridge_root / "world-ready-result.json"
    command = overview.read_kv(request_path)
    control = overview.read_kv(bridge_root / "control.txt")
    lease = overview.read_kv(bridge_root / "lease.txt")
    stages = []
    stage_path = bridge_root / "map013-worldready-lifecycle.txt"
    if stage_path.is_file():
        try:
            # Hard bound private log replay: no raw PID, token or secret data.
            for line in stage_path.read_text(encoding="utf-8")[-20000:].splitlines():
                parts = line.split("|", 3)
                if len(parts) == 4 and (parts[2] in (request_id, "-")):
                    stages.append({"event": parts[1], "forRequest": parts[2] == request_id,
                                   "epoch": int(parts[0]) if parts[0].isdigit() else None})
            stages = stages[-96:]
        except (OSError, UnicodeError):
            stages = []
    return {
        "commandFilePresent": request_path.is_file(),
        "commandReadable": command is not None,
        "commandIdentityMatches": bool(command is not None
            and command.get("schema") == "1"
            and command.get("bridgeVersion") == overview.BRIDGE_VERSION
            and command.get("requestId") == request_id
            and command.get("profileId") == profile
            and command.get("sessionId") == session
            and command.get("challenge") == challenge
            and command.get("gamePid") == str(game_pid)),
        "controlFilePresent": (bridge_root / "control.txt").is_file(),
        "controlIdentityMatches": bool(control is not None
            and control.get("schema") == "1"
            and control.get("bridgeVersion") == overview.BRIDGE_VERSION
            and control.get("profileId") == profile
            and control.get("sessionId") == session
            and control.get("challenge") == challenge
            and control.get("gamePid") == str(game_pid)),
        "leaseFilePresent": (bridge_root / "lease.txt").is_file(),
        "leaseMatchesAndIsFresh": bool(lease is not None
            and lease.get("schema") == "1"
            and lease.get("bridgeVersion") == overview.BRIDGE_VERSION
            and lease.get("sessionId") == session
            and lease.get("challenge") == challenge
            and lease.get("updatedAt", "").isdigit()
            and -5 <= int(time.time()) - int(lease["updatedAt"]) <= 5),
        "resultFilePresent": result_path.is_file(),
        "sourceTraceStages": stages,
        "interpretation": (
            "Source stages distinguish consumed/rejected/pending/written only "
            "when an exact request-bound stage is present; file absence alone "
            "does not establish consumption or native response identity."
        ),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--unmodified-control", action="store_true",
                        help="distinguish accepted original probe startup from private trace extension")
    parser.add_argument("--baseline-region", action="store_true",
                        help="one bounded owner-scoped historical-positive Resource/City first-region control")
    options = parser.parse_args()
    if os.name != "nt":
        raise RuntimeError("live helper supports only owned Windows game")
    BASE.mkdir(exist_ok=True)
    prefix = "original-probe-control-" if options.unmodified_control else "native-causal-"
    attempt = BASE / (prefix + time.strftime("%Y%m%d-%H%M%S"))
    if attempt.exists():
        raise RuntimeError("fresh immutable attempt directory already exists")
    attempt.mkdir()
    private_source = attempt / "map013-wrapper-trace.lua"
    shutil.copyfile(TRACE_SOURCE, private_source)
    profile = "map013native" + secrets.token_hex(5)
    session = "map013" + secrets.token_hex(12)
    challenge = secrets.token_hex(32)
    os.environ["LWBRIDGE_REBUILD_DATA_ROOT"] = str(attempt)
    if options.unmodified_control:
        os.environ.pop("LWBRIDGE_MAP013_PRIVATE_TRACE", None)
        os.environ.pop("LWBRIDGE_MAP013_WRAPPER_TRACE", None)
        os.environ.pop("LWBRIDGE_MAP013_WORLD_TRACE", None)
    else:
        os.environ.pop("LWBRIDGE_MAP013_PRIVATE_TRACE", None)
        os.environ["LWBRIDGE_MAP013_WRAPPER_TRACE"] = str(private_source)
        os.environ["LWBRIDGE_MAP013_WORLD_TRACE"] = "1"
    readme = attempt / "attempt.json"
    readme.write_text(json.dumps({
        "kind": "MAP-013 owned passive native event subscriber test",
        "profileId": profile, "sessionId": session,
        "challengeSha256": hashlib.sha256(challenge.encode()).hexdigest(),
        "privateSourceSha256": hashlib.sha256(private_source.read_bytes()).hexdigest(),
        "gameRoot": str(GAME_ROOT), "status": "CREATED",
    }, indent=2), encoding="utf-8")
    owned = None
    exception = None
    stop_result = None
    trace = None
    world = None
    region = None
    lease_stop = threading.Event()
    lease_thread = None
    try:
        # Admission includes actual original scripts, no other selected game,
        # selected launcher and candidate LENC source size/integrity.
        admission = overview.run_check_only(GAME_ROOT)
        (attempt / "admission.json").write_text(json.dumps(admission, indent=2), encoding="utf-8")
        if admission.get("ok") is not True:
            raise RuntimeError("private injected candidate failed official check-only")
        print("ADMISSION_OK", admission["candidate"]["resourceProbeLencSize"], flush=True)
        owned = overview.run_start(profile, session, challenge, 165, GAME_ROOT)
        if owned.get("ok") is not True or not owned.get("gameRunning"):
            raise RuntimeError("existing Overview helper failed exact owner start")
        (attempt / "owned-start.json").write_text(json.dumps(
            {k: v for k, v in owned.items() if k not in ("ready", "restore")},
            indent=2), encoding="utf-8")
        pid = int(owned["gamePid"])
        print("OWNED_GAME_READY", pid, "session", session, flush=True)
        def refresh_exact_lease():
            # Existing app normally performs this for its exact game owner.
            # Standalone diagnostic must do the same; no foreign lease write.
            while not lease_stop.is_set():
                try:
                    overview.write_lease(overview.overview_paths(GAME_ROOT), session, challenge)
                except Exception as err:
                    print("OWNED_LEASE_REFRESH_FAILED", repr(err), flush=True)
                    lease_stop.set()
                    break
                lease_stop.wait(1)
        lease_thread = threading.Thread(target=refresh_exact_lease, name="MAP013-owned-lease", daemon=True)
        lease_thread.start()
        bridge_root = attempt / "overview-bridge"
        live_root = attempt / "live-resource"
        common = {"schema": "1", "profileId": profile, "sessionId": session,
                  "challenge": challenge, "gamePid": str(pid)}
        # Start passive event observation *before* WorldReady so failure in
        # world-scene transition cannot prevent diagnosis of the hook itself.
        trace_id = "map013trace" + secrets.token_hex(8)
        trace_published = None
        if not options.unmodified_control and not options.baseline_region:
            trace_published = publish_command(live_root / "map013-native-trace.txt", {
                "schema": "1", "probeVersion": "lwbridge-live-resource-probe-2",
                "requestId": trace_id, "profileId": profile,
                "launchSessionId": session, "challenge": challenge, "gamePid": str(pid),
            })
            # Probe Pump is invoked after bridge.Pump in the ordinary LuaEntry.
            # A brief source-supported offset ensures the observer has sampled
            # at least one bridge callback before WorldReady is published.
            time.sleep(2)
        world_id = "map013world" + secrets.token_hex(8)
        world_command = {
            **common, "bridgeVersion": overview.BRIDGE_VERSION, "requestId": world_id,
        }
        (attempt / "world-command-metadata.json").write_text(json.dumps({
            "requestId": world_id, "schema": world_command["schema"],
            "bridgeVersion": world_command["bridgeVersion"],
            "sessionMatchesOwner": world_command["sessionId"] == session,
            "profileMatchesOwner": world_command["profileId"] == profile,
            "gamePidMatchesOwner": world_command["gamePid"] == str(pid),
        }, indent=2), encoding="utf-8")
        world_published = publish_command(bridge_root / "world-ready.txt", world_command)
        (attempt / "world-published-state.json").write_text(json.dumps(
            snapshot_worldready_stages(bridge_root, profile=profile,
                                       session=session, challenge=challenge,
                                       game_pid=pid, request_id=world_id), indent=2),
            encoding="utf-8")
        try:
            world = wait_for(bridge_root / "world-ready-result.json",
                ExpectedReply("world", world_id, profile, session, pid, challenge,
                              world_published, world_id=0), 21)
            (attempt / "world-status.json").write_text(json.dumps(world, indent=2), encoding="utf-8")
            print("WORLD_STATUS", world.get("state"), world.get("serverId"), world.get("error"), flush=True)
        except Exception as world_error:
            print("WORLD_OBSERVATION_FAILED", repr(world_error), flush=True)
            (attempt / "world-status-error.txt").write_text(repr(world_error), encoding="utf-8")
        finally:
            (attempt / "world-after-wait-stages.json").write_text(json.dumps(
                snapshot_worldready_stages(bridge_root, profile=profile,
                                           session=session, challenge=challenge,
                                           game_pid=pid, request_id=world_id), indent=2),
                encoding="utf-8")
        if options.unmodified_control:
            if not isinstance(world, dict) or world.get("state") != "proven":
                raise RuntimeError("unmodified wrapper control lacks verified WorldScene result")
            return 0
        if options.baseline_region:
            if not isinstance(world, dict) or world.get("state") != "proven" or world.get("serverId", 0) <= 0 or world.get("worldId") != 0:
                raise RuntimeError("cannot issue first-region native request without verified world owner")
            trace_published = publish_command(live_root / "map013-native-trace.txt", {
                "schema": "1", "probeVersion": "lwbridge-live-resource-probe-2",
                "requestId": trace_id, "profileId": profile,
                "launchSessionId": session, "challenge": challenge, "gamePid": str(pid),
            })
            time.sleep(1)
            region_id = "map013first" + secrets.token_hex(8)
            region_command = {
                "schema": "1", "probeVersion": "lwbridge-live-resource-probe-2",
                "requestId": region_id, "profileId": profile,
                "launchSessionId": session, "challenge": challenge, "gamePid": str(pid),
                "serverId": str(world["serverId"]), "scanRunId": region_id,
                "viewLevel": "-1", "requestMode": "native",
                "targetTileX": "455", "targetTileY": "425",
                "requestedCount": "8", "holdMilliseconds": "0",
                "homeTileX": str(world.get("playerTileX") or -1),
                "homeTileY": str(world.get("playerTileY") or -1),
                "includeCity": "true", "includeResource": "true",
                "includeMonster": "false", "includeMonsterProtection": "false",
                "includeTrain": "false", "includeDispatch": "false",
                "includeGhost": "false", "includeTreasure": "false",
                "includeResourceDetails": "false", "deferRestoreUntilResponse": "true",
            }
            region_published = publish_command(live_root / "bulk-aoi-diagnostic.txt", region_command)
            print("NATIVE_FIRST_REGION_REQUESTED", world["serverId"], "455,425", flush=True)
            try:
                region = wait_for(live_root / "bulk-aoi-diagnostic-result.json",
                    ExpectedReply("region", region_id, profile, session, pid, challenge,
                                  region_published, server_id=world["serverId"],
                                  world_id=world["worldId"], request_mode="native"), 28)
                (attempt / "native-first-region.json").write_text(
                    json.dumps(region, indent=2), encoding="utf-8")
                print("NATIVE_FIRST_REGION_REPLY", region.get("state"), region.get("error"),
                      "pointRows", len(region.get("point_records") or []),
                      "matched", region.get("matchedCount"),
                      "cameraStable", region.get("cameraTileStable"),
                      flush=True)
            except Exception as region_error:
                print("NATIVE_FIRST_REGION_UNCONFIRMED", repr(region_error), flush=True)
                (attempt / "native-first-region-error.txt").write_text(
                    repr(region_error), encoding="utf-8")
        try:
            trace = wait_for(live_root / "map013-native-trace-result.json",
                ExpectedReply("trace", trace_id, profile, session, pid, challenge,
                              trace_published), 12)
            (attempt / "trace-status.json").write_text(json.dumps(trace, indent=2), encoding="utf-8")
            print("LIVE_EVENT_CAPTURE", trace.get("state"),
                  "registrations", trace.get("registrations"),
                  "events", len(trace.get("events") or []),
                  "samples", len(trace.get("samples") or []), flush=True)
            if trace["state"] != "observed":
                raise RuntimeError("owned diagnostic observer ended in non-observed state")
        except Exception as trace_error:
            print("LIVE_TRACE_RESULT_FAILED", repr(trace_error), flush=True)
            (attempt / "trace-status-error.txt").write_text(repr(trace_error), encoding="utf-8")
            raise
        if not isinstance(world, dict) or world.get("state") != "proven":
            raise RuntimeError("WorldReady has no verified positive world scene")
        # Fail closed: even successful listener subscription is not a new
        # positive baseline until actual raw payloads can be correlated.
        return 0
    except Exception as error:
        exception = f"{type(error).__name__}: {error}"
        print("LIVE_OBSERVER_ERROR", exception, flush=True)
        return 2
    finally:
        lease_stop.set()
        if lease_thread is not None:
            lease_thread.join(timeout=3)
        if owned is not None and "gamePid" in owned:
            try:
                stop_result = overview.run_stop(
                    profile, session, int(owned["gamePid"]),
                    str(owned["gamePath"]), GAME_ROOT, str(owned["gameStartedAtUtc"]), challenge,
                )
                print("EXACT_OWNED_STOP", stop_result.get("ok"), stop_result.get("installedFilesChanged"), flush=True)
            except Exception as stop_error:
                print("STOP_REQUIRES_RECOVERY", repr(stop_error), flush=True)
                exception = (exception or "") + " STOP_ERROR:" + repr(stop_error)
        (attempt / "final.json").write_text(json.dumps({
            "state": "PARTIAL" if exception or (trace is None and not options.unmodified_control) else "OBSERVATION_ONLY",
            "error": exception, "ownedGamePid": owned.get("gamePid") if owned else None,
            "exactStopSucceeded": bool(stop_result and stop_result.get("ok")),
            "originalScriptsRestored": bool(stop_result and stop_result.get("installedFilesChanged") is False),
            "nativeEventsCaptured": len(trace.get("events", [])) if trace else 0,
            "nativeRegionQualified": False,
            "regionState": region.get("state") if isinstance(region, dict) else None,
            "regionPointRecords": len(region.get("point_records") or []) if isinstance(region, dict) else None,
            "observedTraceDoesNotClaimNativeAttribution": True,
        }, indent=2), encoding="utf-8")
        print("RAW_ATTEMPT", str(attempt), flush=True)


if __name__ == "__main__":
    raise SystemExit(main())
