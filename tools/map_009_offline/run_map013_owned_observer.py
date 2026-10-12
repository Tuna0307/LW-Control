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


def publish_command(path: Path, values: dict[str, str]) -> None:
    if path.exists():
        raise RuntimeError(f"owned command already pending: {path.name}")
    path.parent.mkdir(parents=True, exist_ok=True)
    text = "".join(f"{k}={v}\n" for k, v in values.items())
    temp = path.with_name(path.name + "." + uuid.uuid4().hex + ".tmp")
    temp.write_text(text, encoding="utf-8")
    os.replace(temp, path)


def wait_for(path: Path, request_id: str, seconds: float) -> dict:
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        if path.is_file():
            try:
                result = json.loads(path.read_text(encoding="utf-8"))
                if result.get("requestId") == request_id:
                    return result
            except (OSError, ValueError):
                pass
        time.sleep(0.2)
    raise TimeoutError(f"{path.name} did not contain a matching exact-owner result within bound")


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
    else:
        os.environ.pop("LWBRIDGE_MAP013_PRIVATE_TRACE", None)
        os.environ["LWBRIDGE_MAP013_WRAPPER_TRACE"] = str(private_source)
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
        if not options.unmodified_control and not options.baseline_region:
            publish_command(live_root / "map013-native-trace.txt", {
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
        publish_command(bridge_root / "world-ready.txt", world_command)
        try:
            world = wait_for(bridge_root / "world-ready-result.json", world_id, 21)
            (attempt / "world-status.json").write_text(json.dumps(world, indent=2), encoding="utf-8")
            print("WORLD_STATUS", world.get("state"), world.get("serverId"), world.get("error"), flush=True)
        except Exception as world_error:
            print("WORLD_OBSERVATION_FAILED", repr(world_error), flush=True)
            (attempt / "world-status-error.txt").write_text(repr(world_error), encoding="utf-8")
        if options.unmodified_control:
            return 0
        if options.baseline_region:
            if not isinstance(world, dict) or world.get("state") != "proven" or world.get("serverId", 0) <= 0 or world.get("worldId") != 0:
                raise RuntimeError("cannot issue first-region native request without verified world owner")
            publish_command(live_root / "map013-native-trace.txt", {
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
            publish_command(live_root / "bulk-aoi-diagnostic.txt", region_command)
            print("NATIVE_FIRST_REGION_REQUESTED", world["serverId"], "455,425", flush=True)
            try:
                region = wait_for(live_root / "bulk-aoi-diagnostic-result.json", region_id, 28)
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
            trace = wait_for(live_root / "map013-native-trace-result.json", trace_id, 12)
            (attempt / "trace-status.json").write_text(json.dumps(trace, indent=2), encoding="utf-8")
            print("LIVE_EVENT_CAPTURE", trace.get("state"),
                  "registrations", trace.get("registrations"),
                  "events", len(trace.get("events") or []),
                  "samples", len(trace.get("samples") or []), flush=True)
        except Exception as trace_error:
            print("LIVE_TRACE_RESULT_FAILED", repr(trace_error), flush=True)
            (attempt / "trace-status-error.txt").write_text(repr(trace_error), encoding="utf-8")
            raise
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
