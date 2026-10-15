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


def retire_owned_reply(path: Path, request_id: str, profile: str, session: str,
                       pid: int, challenge: str) -> None:
    """Remove only a previously archived reply of this exact diagnostic owner."""
    value = json.loads(path.read_text(encoding="utf-8"))
    if (value.get("requestId") != request_id or value.get("profileId") != profile or
            value.get("launchSessionId") != session or value.get("gamePid") != pid or
            value.get("challenge") != challenge or value.get("state") not in ("observed", "proven", "failed")):
        raise RuntimeError("cannot retire a foreign or unexpected diagnostic reply")
    path.unlink()


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
    target_tile_x: int | None = None
    target_tile_y: int | None = None


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
            if (expected.target_tile_x is None or expected.target_tile_y is None or
                    not _exact_int(expected.target_tile_x) or not _exact_int(expected.target_tile_y)):
                raise ReplyValidationError("region expected target geometry absent")
            for field in ("targetTileX", "targetTileY", "targetAoiIndex"):
                if not _exact_int(value.get(field)):
                    raise ReplyValidationError(f"region reply {field} absent")
            if (value["targetTileX"] != expected.target_tile_x or
                    value["targetTileY"] != expected.target_tile_y):
                raise ReplyValidationError("region reply target coordinates mismatch request")
            # Actual probe source choose_bulk_aoi_indices derives the target
            # AOI from the target tile and native _lwAoiBlockSize/Count:
            # index = (y // size) * count + (x // size), bounded to the grid.
            size, count = value.get("blockSize"), value.get("blockCount")
            if not _exact_int(size, minimum=1) or not _exact_int(count, minimum=1):
                raise ReplyValidationError("region native block geometry absent")
            if expected.target_tile_x >= size * count or expected.target_tile_y >= size * count:
                raise ReplyValidationError("region request outside native AOI geometry")
            target_index = (expected.target_tile_y // size) * count + expected.target_tile_x // size
            if value["targetAoiIndex"] != target_index:
                raise ReplyValidationError("region reply target AOI mismatches request geometry")
            indices = value.get("requestedIndices")
            if (not isinstance(indices, list) or not indices or
                    any(not _exact_int(idx) or idx >= count * count for idx in indices) or
                    len(indices) != len(set(indices)) or
                    target_index not in indices):
                raise ReplyValidationError("region selected AOIs omit or corrupt requested target")
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


def observe_scene_gate(
    live_root: Path, attempt: Path, *, profile: str, session: str,
    challenge: str, game_pid: int, seconds: float = 90,
) -> dict:
    """Wait for the source's actual transition gate, not a startup sleep.

    Each observation is an exact-owner passive sample. No world request is
    submitted here. Preserve each terminal trace before another command so
    its old fixed-path reply cannot be mistaken for the next observation.
    """
    deadline = time.monotonic() + seconds
    index = 0
    while time.monotonic() < deadline:
        index += 1
        request_id = "map013scene" + secrets.token_hex(8)
        published = publish_command(live_root / "map013-native-trace.txt", {
            "schema": "1", "probeVersion": "lwbridge-live-resource-probe-2",
            "requestId": request_id, "profileId": profile,
            "launchSessionId": session, "challenge": challenge,
            "gamePid": str(game_pid), "sceneOnly": "true",
        })
        result_path = live_root / "map013-native-trace-result.json"
        result = wait_for(result_path,
            ExpectedReply("trace", request_id, profile, session, game_pid,
                          challenge, published),
            min(24, max(0.1, deadline - time.monotonic())))
        preserved = attempt / f"scene-gate-{index:03d}.json"
        if preserved.exists():
            raise RuntimeError("scene observation evidence already exists")
        os.replace(result_path, preserved)
        if result.get("state") != "observed":
            raise RuntimeError("scene observer failed before transition admission")
        samples = result.get("samples")
        view = samples[-1].get("view", {}) if isinstance(samples, list) and samples else {}
        city = view.get("sceneUtilsIsInCity") is True
        world = view.get("sceneUtilsIsInWorld") is True
        print("NATIVE_SCENE_GATE", index, "city", city, "world", world,
              "scene", view.get("currentSceneId"), flush=True)
        if city or world:
            return {"requestId": request_id, "observation": index,
                    "city": city, "world": world,
                    "sceneId": view.get("currentSceneId"),
                    "evidence": preserved.name}
    raise TimeoutError("native scene never admitted ChangeToWorld within bounded observation")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--unmodified-control", action="store_true",
                        help="distinguish accepted original probe startup from private trace extension")
    parser.add_argument("--baseline-region", action="store_true",
                        help="one bounded owner-scoped historical-positive Resource/City first-region control")
    parser.add_argument("--region-mode", choices=("coverage", "native"), default="coverage",
                        help="production coverage control, or explicit experimental native route")
    parser.add_argument("--compare-second-region", action="store_true",
                        help="after the baseline, separately observe coverage and experimental native at 545,495")
    parser.add_argument("--fresh-reply-experiment", action="store_true",
                        help="private source-bound native-reply completion, never default product source")
    parser.add_argument("--camera-free-direct", action="store_true",
                        help="additional bounded direct-request experiment without the initial camera shift")
    parser.add_argument("--sequence-experiment", action="store_true",
                        help="after baseline, three serial coverage regions with one final restoration")
    parser.add_argument("--zoom-experiment", action="store_true",
                        help="three paired coverage/ZoomMax observations at the same second region, source-correlated and individually restored")
    parser.add_argument("--wide-aspect-experiment", action="store_true",
                        help="three alternating coverage aspect-4/6 pairs on one populated region; each restores home and must pass source-reply checks")
    parser.add_argument("--city-subset-probe", action="store_true",
                        help="single current-world City-rich region at 995,975: exact current-point versus retained identity subset; never a full scan")
    parser.add_argument("--city-mixed-only-target", action="store_true",
                        help="single native region centered on the genuine Oct13 mixed-only City identity absent from earlier City-only scan")
    parser.add_argument("--city-rejection-probe", action="store_true",
                        help="observe the freshly failed baseline City region (45,175); record exact native identity rejection counters")
    parser.add_argument("--intermediate-zoom-experiment", action="store_true",
                        help="opt-in source-guarded middle zoom (not ZoomMax), three pairs only if fresh LOD0 and shared identities qualify")
    parser.add_argument("--event-source-probe", action="store_true",
                        help="bounded official-game read-only Monster/Doomsday/Treasure/Supplies source availability in one LOD0 region, not a full scan")
    parser.add_argument("--post-lens-sequence", action="store_true",
                        help="private LOD0 consecutive A/B/C source test: refresh subscription after each coverage lens change; compared to same-session restored baseline")
    parser.add_argument("--subscription-stage-trace", action="store_true",
                        help="private observation of camera anchors/subscriptions before and after updates; preserve first complete camera snapshot")
    parser.add_argument("--bounded-lens-sequence", action="store_true",
                        help="distinct private FOV40 candidate: keep ground-facing rays below the observed 45-degree horizon")
    parser.add_argument("--sequence-lens-fov", type=float, choices=(20.0, 40.0), default=40.0,
                        help="private bounded lens; FOV20 reduces the FOV40 split-request footprint")
    parser.add_argument("--full-world-sequence", action="store_true",
                        help="after three qualified regions, privately observe exact 10000-cell City/Resource coverage and final restoration")
    options = parser.parse_args()
    if options.city_rejection_probe and (not(options.baseline_region and options.fresh_reply_experiment) or options.sequence_experiment or options.compare_second_region or options.event_source_probe):
        parser.error("City rejection probe is one distinct source-attributed failure region")
    if options.subscription_stage_trace and not options.post_lens_sequence:
        parser.error("subscription stage trace requires the distinct owned post-lens sequence")
    if options.bounded_lens_sequence and not options.subscription_stage_trace:
        parser.error("bounded lens candidate requires owned subscription stage observations")
    if options.full_world_sequence and not (options.bounded_lens_sequence and options.sequence_lens_fov == 20):
        parser.error("full-world candidate requires the observed non-split FOV20 source path")
    if options.compare_second_region and (not options.baseline_region or options.unmodified_control):
        parser.error("second-region comparison requires the instrumented baseline")
    if options.fresh_reply_experiment and (not options.baseline_region or options.unmodified_control):
        parser.error("fresh-reply experiment requires the instrumented baseline")
    if options.camera_free_direct and not (options.fresh_reply_experiment and options.compare_second_region):
        parser.error("camera-free direct requires fresh-reply and second-region comparison")
    if options.sequence_experiment and not (options.fresh_reply_experiment and options.baseline_region):
        parser.error("sequence requires fresh-reply and a baseline")
    if options.sequence_experiment and (options.camera_free_direct or options.compare_second_region):
        parser.error("sequence is a distinct experiment, not mixed with direct comparisons")
    if options.zoom_experiment and not (options.fresh_reply_experiment and options.baseline_region and options.compare_second_region):
        parser.error("zoom experiment requires fresh-reply, baseline-region and compare-second-region")
    if options.zoom_experiment and (options.sequence_experiment or options.camera_free_direct):
        parser.error("zoom is a distinct paired camera experiment")
    if options.wide_aspect_experiment and not (options.fresh_reply_experiment and options.baseline_region and options.compare_second_region):
        parser.error("aspect experiment requires fresh-reply, baseline-region and compare-second-region")
    if options.wide_aspect_experiment and (options.zoom_experiment or options.sequence_experiment or options.camera_free_direct):
        parser.error("aspect is a distinct experiment")
    if options.city_subset_probe and not (options.baseline_region and options.fresh_reply_experiment):
        parser.error("City subset requires an instrumented fresh-reply baseline")
    if options.city_subset_probe and (options.compare_second_region or options.sequence_experiment or options.zoom_experiment or options.wide_aspect_experiment):
        parser.error("City subset is a single bounded production-source acquisition, not a scanner comparison")
    if options.city_mixed_only_target and not options.city_subset_probe:
        parser.error("mixed-only City comparison requires the bounded source-qualified City subset gate")
    if options.intermediate_zoom_experiment and not (options.fresh_reply_experiment and options.baseline_region and options.compare_second_region):
        parser.error("intermediate zoom requires source-attributed fresh region comparisons")
    if options.intermediate_zoom_experiment and (options.zoom_experiment or options.wide_aspect_experiment or options.sequence_experiment or options.camera_free_direct or options.city_subset_probe):
        parser.error("intermediate zoom is a distinct isolated experiment")
    if options.event_source_probe and not (options.baseline_region and options.fresh_reply_experiment):
        parser.error("event source availability requires a fresh native bounded region")
    if options.event_source_probe and (options.city_subset_probe or options.compare_second_region or options.sequence_experiment or options.zoom_experiment or options.wide_aspect_experiment or options.intermediate_zoom_experiment or options.camera_free_direct):
        parser.error("event source availability must not be conflated with scanner benchmark or other experiments")
    if options.post_lens_sequence and not (options.sequence_experiment and options.fresh_reply_experiment and options.baseline_region):
        parser.error("post-lens candidate requires three owned consecutive-region source comparisons")
    if options.post_lens_sequence and (options.event_source_probe or options.intermediate_zoom_experiment or options.city_subset_probe or options.city_mixed_only_target):
        parser.error("post-lens candidate is a distinct lens-order experiment")
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
    # The production app supplies this existing adapter. Even with no pipe
    # connection, its read-only snapshot delegate classifies Windows locks.
    # Bare Lua io.open returns nil on sharing contention and wrongly turns
    # a live diagnostic into overview_session_unavailable.
    snapshot_adapter = TOOLS / "LWBridge.GamePipeAdapter.dll"
    if not snapshot_adapter.is_file():
        snapshot_adapter = (REPO / "src/LWBridge.GamePipeAdapter/bin/Release/"
                            "netstandard2.0/LWBridge.GamePipeAdapter.dll")
    if not snapshot_adapter.is_file():
        raise RuntimeError("packaged runtime snapshot reader is absent")
    owned_adapter = attempt / "LWBridge.GamePipeAdapter.dll"
    shutil.copyfile(snapshot_adapter, owned_adapter)
    os.environ["LWBRIDGE_PIPE_ADAPTER_PATH"] = str(owned_adapter.resolve())
    sampler = (REPO / "tools/map_009_offline/managed_sampler/bin/Release/"
               "netstandard2.0/LWBridge.MapTraceSampler.dll")
    sampler_hash = None
    if sampler.is_file():
        owned_sampler = attempt / "LWBridge.MapTraceSampler.dll"
        shutil.copyfile(sampler, owned_sampler)
        os.environ["LWBRIDGE_MAP013_SAMPLER_PATH"] = str(owned_sampler.resolve())
        sampler_hash = hashlib.sha256(sampler.read_bytes()).hexdigest()
    else:
        os.environ.pop("LWBRIDGE_MAP013_SAMPLER_PATH", None)
    experiment_source_hash = None
    if options.fresh_reply_experiment:
        if sampler_hash is None:
            raise RuntimeError("fresh-reply experiment requires built owned sampler")
        from native_reply_instrument_013 import instrument_native_reply
        original_source = overview.resource_probe_source
        experimental_body = instrument_native_reply(original_source(), camera_free_direct=options.camera_free_direct)
        if options.sequence_experiment:
            from sequence_instrument_013 import instrument_sequence
            experimental_body = instrument_sequence(experimental_body)
        if options.post_lens_sequence:
            from sequence_post_lens_014 import instrument_post_lens_refresh
            experimental_body = instrument_post_lens_refresh(experimental_body)
        if options.subscription_stage_trace:
            from sequence_stage_trace_014 import instrument_subscription_stages
            experimental_body = instrument_subscription_stages(
                experimental_body, bounded_lens=options.bounded_lens_sequence,
                lens_fov=options.sequence_lens_fov)
        if options.intermediate_zoom_experiment:
            from intermediate_zoom_instrument_014 import instrument_intermediate_zoom
            experimental_body = instrument_intermediate_zoom(experimental_body)
        if options.event_source_probe:
            from event_source_instrument_014 import instrument_event_source
            experimental_body = instrument_event_source(experimental_body)
        (attempt / "experimental-resource-probe.lua").write_bytes(experimental_body)
        experiment_source_hash = hashlib.sha256(experimental_body).hexdigest()
        overview.resource_probe_source = lambda: experimental_body
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
        "snapshotAdapterSha256": hashlib.sha256(snapshot_adapter.read_bytes()).hexdigest(),
        "managedSamplerSha256": sampler_hash,
        "experimentalResourceSourceSha256": experiment_source_hash,
        "gameRoot": str(GAME_ROOT), "status": "CREATED",
    }, indent=2), encoding="utf-8")
    owned = None
    exception = None
    stop_result = None
    trace = None
    world = None
    region = None
    accuracy_comparison_failed = False
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
        if options.baseline_region and not options.unmodified_control:
            gate = observe_scene_gate(live_root, attempt, profile=profile,
                                      session=session, challenge=challenge,
                                      game_pid=pid)
            (attempt / "scene-gate-admission.json").write_text(
                json.dumps(gate, indent=2), encoding="utf-8")
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
            region_id = "map013citysubset" + secrets.token_hex(8) if options.city_subset_probe else "map013first" + secrets.token_hex(8)
            known_target = None
            if options.city_mixed_only_target:
                sys.path.insert(0,str(REPO/"tools/map_014_lead_native"))
                from audit_city_source_coverage import rows_for
                from audit_city_records import SOURCES
                old_city = rows_for(SOURCES[0])
                later_mixed = rows_for(SOURCES[1])
                only_new = set(later_mixed)-set(old_city)
                if len(only_new)!=1:
                    raise RuntimeError("Current genuine mixed-only City witness is ambiguous")
                known_uid = next(iter(only_new))
                known_target = {"uid":known_uid, "pointIndex":later_mixed[known_uid][2],
                                "x":later_mixed[known_uid][0],"y":later_mixed[known_uid][1]}
                region_x,region_y = known_target["x"],known_target["y"]
            elif options.event_source_probe:
                # Distinct from the earlier central event sample. This
                # checks loaded source/manager availability at the other
                # genuine populated upper/right border, without a full scan.
                region_x, region_y = (995, 975)
            elif options.city_rejection_probe:
                region_x, region_y = (45,175)
            else:
                region_x, region_y = (995,975) if options.city_subset_probe else (455,425)
            region_command = {
                "schema": "1", "probeVersion": "lwbridge-live-resource-probe-2",
                "requestId": region_id, "profileId": profile,
                "launchSessionId": session, "challenge": challenge, "gamePid": str(pid),
                "serverId": str(world["serverId"]), "scanRunId": region_id,
                "viewLevel": "-1", "requestMode": options.region_mode,
                "targetTileX": str(region_x), "targetTileY": str(region_y),
                "requestedCount": "8", "holdMilliseconds": "0",
                "homeTileX": str(world.get("playerTileX") or -1),
                "homeTileY": str(world.get("playerTileY") or -1),
                "includeCity": "true", "includeResource": "true",
                "includeMonster": "true" if options.event_source_probe else "false", "includeMonsterProtection": "false",
                "includeTrain": "false", "includeDispatch": "false",
                "includeGhost": "false", "includeTreasure": "true" if options.event_source_probe else "false",
                "includeResourceDetails": "false", "deferRestoreUntilResponse": "true",
            }
            region_published = publish_command(live_root / "bulk-aoi-diagnostic.txt", region_command)
            print("NATIVE_FIRST_REGION_REQUESTED", world["serverId"],f"{region_x},{region_y}", flush=True)
            try:
                region = wait_for(live_root / "bulk-aoi-diagnostic-result.json",
                    ExpectedReply("region", region_id, profile, session, pid, challenge,
                                  region_published, server_id=world["serverId"],
                                  world_id=world["worldId"], request_mode=options.region_mode,
                                  target_tile_x=int(region_command["targetTileX"]),
                                  target_tile_y=int(region_command["targetTileY"])), 28)
                (attempt / "native-first-region.json").write_text(
                    json.dumps(region, indent=2), encoding="utf-8")
                print("NATIVE_FIRST_REGION_REPLY", region.get("state"), region.get("error"),
                      "pointRows", len(region.get("point_records") or []),
                      "matched", region.get("matchedCount"),
                      "cameraStable", region.get("cameraTileStable"),
                      flush=True)
                if options.event_source_probe:
                    # A real seasonal source may be empty. That is a genuine
                    # bounded availability observation, NEVER a positive
                    # event claim. Keep private player identities out of
                    # this compact, versioned evidence artifact.
                    from collections import Counter
                    all_rows=(region.get("point_records") or [])+(region.get("monster_march_records") or [])
                    classes=Counter(str(row.get("runtimeClass") or "unspecified") for row in all_rows if isinstance(row,dict))
                    supplies=[row for row in all_rows if isinstance(row,dict) and row.get("pointType")==27 and row.get("suppliesType")==1]
                    doom=[row for row in all_rows if isinstance(row,dict) and row.get("runtimeClass")=="LWDoomsdayManager.BossVO" and row.get("marchUuid")]
                    treasure=[row for row in all_rows if isinstance(row,dict) and row.get("pointType") in (21,27)]
                    qualified=(region.get("state")=="proven" and region.get("map013NativeReplyState")=="matched"
                               and region.get("cameraGeometryConfirmed") is True and region.get("cameraTileStable") is True
                               and region.get("serverLod")==0 and region.get("blockSize")==10
                               and region.get("blockCount")==100 and len(region.get("requestedIndices") or [])>0)
                    event_status={
                        "state":"source_region_qualified" if qualified else "source_region_rejected",
                        "source":"real exact-owned production Lua world current bounded AOI; no fixture/no claims",
                        "serverId":world["serverId"],"worldId":world["worldId"],
                        "aoiCount":len(region.get("requestedIndices") or []),
                        "nativeReplyState":region.get("map013NativeReplyState"),
                        "cameraRestored":region.get("cameraGeometryConfirmed"),
                        "runtimeClasses":dict(classes),
                        "doomsdayBossAoiCount":region.get("doomsdayBossCount"),
                        "nativeEventSourceAvailability":region.get("map014EventSourceStats"),
                        "sourceNamedDoomWalkerCount":len(doom),
                        "scatteredSuppliesType1Count":len(supplies),
                        "ordinaryTreasureAndSuppliesCount":len(treasure),
                        "celebrationPartyIdentified":False,
                        "claimScope":"single currently requested native LOD0 region; not source-global spawn census",
                    }
                    (attempt/"map014-event-availability.json").write_text(json.dumps(event_status,indent=2),encoding="utf-8")
                    print("MAP014_EVENT_SOURCE_AVAILABILITY",event_status["state"],
                          "doom",len(doom),"scattered",len(supplies),"treasure",len(treasure),flush=True)
                    if not qualified:
                        raise RuntimeError("event source lacks exact fresh LOD0 camera-restored native acceptance")
                if options.city_subset_probe:
                    c=region.get("cityCompleteness") or {}
                    candidates=[r for r in region.get("point_records") or [] if r.get("kind")=="player_base"]
                    passed=(region.get("state")=="proven" and region.get("map013NativeReplyState")=="matched"
                            and region.get("cameraGeometryConfirmed") is True
                            and len(candidates)>0 and c.get("accepted")==len(candidates)
                            and c.get("nativeCurrentCityCount")==region.get("nativePointInfoCityCount")
                            and c.get("missingNativeCurrentPointIndices")==0
                            and c.get("duplicateRetainedPointIndices")==0
                            and len({r.get("pointId") for r in candidates})==len(candidates)
                            and all(c.get(k)==0 for k in ("missingPointIndex","invalidPosition","missingServer","missingOwnerUid","missingOwnerName")))
                    (attempt/"city-subset-verdict.json").write_text(json.dumps({
                        "state": "verified" if passed else "rejected",
                        "sourceState":region.get("state"), "replyState":region.get("map013NativeReplyState"),
                        "nativeCurrentCityCount":region.get("nativePointInfoCityCount"),
                        "deliveredCityCount":len(candidates), "accounting":c,
                        "cameraRestored":region.get("cameraGeometryConfirmed"),
                        "wholeWorldClaim":False,
                        "independentMixedOnlyReference":({
                            "source":"genuine same-server Oct13 mixed scan, absent earlier City-only scan",
                            "targetInRequestedRegion": any(
                                r.get("ownerUid")==known_target["uid"] and
                                r.get("pointId")==known_target["pointIndex"] for r in candidates),
                            "sourceCoordinatesMatched": any(
                                r.get("ownerUid")==known_target["uid"] and
                                r.get("pointId")==known_target["pointIndex"] and
                                r.get("x")==known_target["x"] and r.get("y")==known_target["y"]
                                for r in candidates),
                        } if known_target else None),
                    },indent=2),encoding="utf-8")
                    print("MAP014_CITY_SUBSET_VERDICT", "verified" if passed else "rejected",flush=True)
                    if not passed:
                        raise RuntimeError("native City subset did not qualify exact sources, fresh AOI and restoration")
            except Exception as region_error:
                print("NATIVE_FIRST_REGION_UNCONFIRMED", repr(region_error), flush=True)
                (attempt / "native-first-region-error.txt").write_text(
                    repr(region_error), encoding="utf-8")
        try:
            trace = wait_for(live_root / "map013-native-trace-result.json",
                ExpectedReply("trace", trace_id, profile, session, pid, challenge,
                              trace_published), 24)
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
        if options.sequence_experiment:
            if not isinstance(region, dict) or region.get("state") != "proven":
                raise RuntimeError("sequence requires a working populated baseline")
            if options.post_lens_sequence and (region.get("map013NativeReplyState")!="matched" or
                    region.get("cameraGeometryConfirmed") is not True):
                raise RuntimeError("post-lens baseline lacks a verified owner-scoped reply/restore")
            peer_regions=[region]
            retire_owned_reply(live_root / "map013-native-trace-result.json",trace_id,profile,session,pid,challenge)
            group_trace_id="map013grouptrace"+secrets.token_hex(8)
            group_trace_time=publish_command(live_root / "map013-native-trace.txt", {
                "schema":"1","probeVersion":"lwbridge-live-resource-probe-2",
                "requestId":group_trace_id,"profileId":profile,"launchSessionId":session,
                "challenge":challenge,"gamePid":str(pid)})
            time.sleep(1)
            previous_id=region_id
            for baseline_index,(tx,ty) in enumerate(((545,495),(655,565)),2):
                retire_owned_reply(live_root / "bulk-aoi-diagnostic-result.json",previous_id,profile,session,pid,challenge)
                baseline_id="map013baseline"+secrets.token_hex(8)
                sent=publish_command(live_root / "bulk-aoi-diagnostic.txt",
                    {**region_command,"requestId":baseline_id,"scanRunId":baseline_id,
                     "targetTileX":str(tx),"targetTileY":str(ty),"requestMode":"coverage"})
                baseline_result=wait_for(live_root / "bulk-aoi-diagnostic-result.json",
                    ExpectedReply("region",baseline_id,profile,session,pid,challenge,sent,
                                  server_id=world["serverId"],world_id=world["worldId"],
                                  request_mode="coverage",target_tile_x=tx,target_tile_y=ty),28)
                (attempt / f"sequence-baseline-{baseline_index}.json").write_text(json.dumps(baseline_result,indent=2),encoding="utf-8")
                print("SEQUENCE_BASELINE",baseline_index,baseline_result.get("state"),baseline_result.get("error"),
                      "rows",len(baseline_result.get("point_records") or []),flush=True)
                if baseline_result["state"]!="proven" or baseline_result.get("cameraTileStable") is not True:
                    raise RuntimeError("sequence comparison baseline failed")
                if options.post_lens_sequence and (baseline_result.get("map013NativeReplyState")!="matched" or
                        baseline_result.get("cameraGeometryConfirmed") is not True):
                    raise RuntimeError("post-lens baseline lacks fresh owner-scoped source/restore")
                peer_regions.append(baseline_result)
                previous_id=baseline_id
            for index,(tx,ty,step) in enumerate(((455,425,"keep"),(545,495,"keep"),(655,565,"final")),1):
                retire_owned_reply(live_root / "bulk-aoi-diagnostic-result.json",previous_id,profile,session,pid,challenge)
                seq_id="map013seq"+secrets.token_hex(8)
                command={**region_command,"requestId":seq_id,"scanRunId":seq_id,
                         "requestMode":"coverage","targetTileX":str(tx),"targetTileY":str(ty),
                         "map013Sequence":step}
                sent=publish_command(live_root / "bulk-aoi-diagnostic.txt",command)
                try:
                    result=wait_for(live_root / "bulk-aoi-diagnostic-result.json",
                        ExpectedReply("region",seq_id,profile,session,pid,challenge,sent,
                                      server_id=world["serverId"],world_id=world["worldId"],
                                      request_mode="coverage",target_tile_x=tx,target_tile_y=ty),28)
                except Exception:
                    source=live_root / "bulk-aoi-diagnostic-result.json"
                    if source.is_file():shutil.copyfile(source,attempt / f"sequence-region-{index}-unvalidated.json")
                    raise
                (attempt / f"sequence-region-{index}.json").write_text(json.dumps(result,indent=2),encoding="utf-8")
                print("SEQUENCE_REGION",index,step,result.get("state"),result.get("error"),
                      "rows",len(result.get("point_records") or []),"retained",result.get("map013PositionRetained"),
                      "restored",result.get("positionRestoredAfterResponse"),flush=True)
                if result["state"]!="proven" or result.get("map013NativeReplyState")!="matched":
                    raise RuntimeError("sequence region lacks verified fresh native reply")
                if options.post_lens_sequence:
                    reference=peer_regions[index-1]
                    def native_keys(doc):
                        output={}
                        indices=set(doc.get("requestedIndices") or [])
                        for row in doc.get("point_records") or []:
                            if not isinstance(row,dict) or row.get("serverId")!=world["serverId"] or row.get("worldId")!=world["worldId"]:
                                raise RuntimeError("post-lens missing native row server/world provenance")
                            x,y=row.get("x"),row.get("y")
                            if not isinstance(x,(int,float)) or not isinstance(y,(int,float)) or int(y)//10*100+int(x)//10 not in indices:
                                raise RuntimeError("post-lens record outside delivered native AOIs")
                            key=(row.get("kind"),str(row.get("pointId") or row.get("id") or row.get("uuid") or ""))
                            if key in output or key[1]=="":
                                raise RuntimeError("post-lens duplicate/empty source record key")
                            output[key]=(x,y,row.get("pointType"),row.get("ownerUid"),row.get("source"))
                        return output
                    if (result.get("serverLod"),result.get("blockSize"),result.get("blockCount")) != (0,10,100):
                        raise RuntimeError("post-lens native LOD0 geometry failed")
                    if sorted(result.get("requestedIndices") or [])!=sorted(reference.get("requestedIndices") or []):
                        raise RuntimeError("post-lens AOI coverage differs from same-session restored control")
                    if native_keys(result)!=native_keys(reference):
                        if not options.subscription_stage_trace:
                            raise RuntimeError("post-lens missing/extra/changed source identities/coordinates/fields")
                        accuracy_comparison_failed = True
                        # This flag observes where the subscription diverges;
                        # it never qualifies this candidate. Preserve a failed
                        # comparison rather than prevent the next stage from
                        # being observed after time-separated resource churn.
                        (attempt / f"sequence-comparison-{index}.json").write_text(
                            json.dumps({"accuracyQualified": False,
                                        "reason": "source identity/field sets changed",
                                        "baselineRows": len(native_keys(reference)),
                                        "candidateRows": len(native_keys(result)),
                                        "diagnosticContinuationOnly": True}, indent=2),
                            encoding="utf-8")
                        print("SEQUENCE_ACCURACY_NOT_QUALIFIED", index,
                              "continuing stage observation only", flush=True)
                    if index==3 and (result.get("cameraGeometryConfirmed") is not True or
                                     result.get("cameraTileStable") is not True):
                        raise RuntimeError("post-lens final exact original camera did not restore")
                if step=="keep" and result.get("map013PositionRetained") is not True:
                    raise RuntimeError("sequence unexpectedly restored between regions")
                if step=="final" and (result.get("cameraTileStable") is not True or result.get("positionRestoredAfterResponse") is not True):
                    raise RuntimeError("sequence final restoration not confirmed")
                previous_id=seq_id
            if options.full_world_sequence:
                import sys as _sys
                from full_sequence_observe_014 import run_full_sequence
                previous_id = run_full_sequence(_sys.modules[__name__], attempt=attempt,
                    live_root=live_root, command=region_command, world=world,
                    profile=profile, session=session, pid=pid, challenge=challenge,
                    previous_id=previous_id)
            group_trace=wait_for(live_root / "map013-native-trace-result.json",
                ExpectedReply("trace",group_trace_id,profile,session,pid,challenge,group_trace_time),24)
            (attempt / "sequence-trace.json").write_text(json.dumps(group_trace,indent=2),encoding="utf-8")
        if options.compare_second_region:
            if not isinstance(region, dict) or region.get("state") != "proven":
                raise RuntimeError("cannot compare a second region without a working baseline")
            # Each observation has its own trace and reply identity. Coverage
            # restores after its response; native remains an explicit experiment.
            # This does not enable a consecutive-region production shortcut.
            previous_trace_id, previous_region_id = trace_id, region_id
            if options.wide_aspect_experiment:
                methods = ("coverage", "coverage") * 3
            elif options.zoom_experiment or options.intermediate_zoom_experiment:
                methods = ("coverage", "zoom") * 3
            else:
                methods = ("coverage", "native", "direct") if options.camera_free_direct else ("coverage", "native")
            zoom_observations = []
            for comparison_index,mode in enumerate(methods,1):
                retire_owned_reply(live_root / "map013-native-trace-result.json",
                                   previous_trace_id, profile, session, pid, challenge)
                retire_owned_reply(live_root / "bulk-aoi-diagnostic-result.json",
                                   previous_region_id, profile, session, pid, challenge)
                next_trace_id = "map013trace" + secrets.token_hex(8)
                trace_time = publish_command(live_root / "map013-native-trace.txt", {
                    "schema": "1", "probeVersion": "lwbridge-live-resource-probe-2",
                    "requestId": next_trace_id, "profileId": profile,
                    "launchSessionId": session, "challenge": challenge, "gamePid": str(pid),
                })
                time.sleep(1)
                next_id = "map013second" + secrets.token_hex(8)
                next_command = {**region_command, "requestId": next_id, "scanRunId": next_id,
                                "requestMode": mode, "targetTileX": "545", "targetTileY": "495"}
                aspect = 4 if comparison_index % 2 else 6
                if options.wide_aspect_experiment:
                    next_command["coverageAspect"] = str(aspect)
                if mode == "direct":
                    next_command["requestedCount"] = "50"
                next_time = publish_command(live_root / "bulk-aoi-diagnostic.txt", next_command)
                try:
                    result = wait_for(live_root / "bulk-aoi-diagnostic-result.json",
                        ExpectedReply("region", next_id, profile, session, pid, challenge,
                                      next_time, server_id=world["serverId"], world_id=world["worldId"],
                                      request_mode=mode, target_tile_x=545, target_tile_y=495), 28)
                    output = ("midzoom-pair-%02d-" % comparison_index + mode if options.intermediate_zoom_experiment else
                              "aspect-pair-%02d-aspect%d" % (comparison_index, aspect)
                              if options.wide_aspect_experiment else
                              ("zoom-pair-%02d-" % comparison_index if options.zoom_experiment else "second-region-") + mode)
                    (attempt / (output + ".json")).write_text(
                        json.dumps(result, indent=2), encoding="utf-8")
                    print("SECOND_REGION", mode, result.get("state"), result.get("error"),
                          "pointRows", len(result.get("point_records") or []), flush=True)
                except Exception as error:
                    output = ("midzoom-pair-%02d-" % comparison_index + mode if options.intermediate_zoom_experiment else
                              "aspect-pair-%02d-aspect%d" % (comparison_index, aspect)
                              if options.wide_aspect_experiment else
                              ("zoom-pair-%02d-" % comparison_index if options.zoom_experiment else "second-region-") + mode)
                    (attempt / (output + "-error.txt")).write_text(repr(error), encoding="utf-8")
                    print("SECOND_REGION_UNCONFIRMED", mode, repr(error), flush=True)
                    raise
                if options.zoom_experiment or options.wide_aspect_experiment or options.intermediate_zoom_experiment:
                    observation={
                        "pair":(comparison_index+1)//2,"method":mode,
                        "aspect": aspect if options.wide_aspect_experiment else None,
                        "state":result.get("state"),"error":result.get("error"),
                        "nativeReply":result.get("map013NativeReplyState"),
                        "cameraRestored":result.get("cameraGeometryRestored"),
                        "cameraStable":result.get("cameraTileStable"),
                        "pointCount":len(result.get("point_records") or []),
                        "aoiCount":len(result.get("requestedIndices") or []),
                        "requestedIndices":result.get("requestedIndices") or [],
                        "elapsed":result.get("totalProbeElapsedSeconds"),
                        "rows":result.get("point_records") or [],
                        "serverLod":result.get("zoomFinalServerLod") if mode=="zoom" else result.get("serverLod"),
                        "blockSize":result.get("zoomFinalBlockSize") if mode=="zoom" else result.get("blockSize"),
                        "blockCount":result.get("zoomFinalBlockCount") if mode=="zoom" else result.get("blockCount"),
                    }
                    zoom_observations.append(observation)
                    (attempt / ("midzoom-paired-results.json" if options.intermediate_zoom_experiment else
                                "aspect-paired-results.json" if options.wide_aspect_experiment else
                                "zoom-paired-results.json")).write_text(json.dumps(
                        zoom_observations, indent=2),encoding="utf-8")
                    if(result.get("state")!="proven" or result.get("map013NativeReplyState")!="matched" or
                       result.get("cameraGeometryRestored") is not True or result.get("cameraTileStable") is not True):
                        raise RuntimeError("Alternative native reply/restore rejected: "+mode)
                    if options.wide_aspect_experiment:
                        if not result.get("point_records") or not result.get("requestedIndices"):
                            raise RuntimeError("Aspect candidate gave a fast empty or missing AOI result")
                        if comparison_index % 2 == 0:
                            narrow=zoom_observations[-2]
                            wide=zoom_observations[-1]
                            def keys_in_common_aoi(source, common):
                                return {
                                    (r.get("kind"),str(r.get("id") or r.get("uuid") or r.get("pointId")))
                                    for r in source.get("rows") or []
                                    if isinstance(r,dict) and r.get("x") is not None and r.get("y") is not None and
                                    ((int(r["y"])//10)*100+(int(r["x"])//10)) in common
                                }
                            common=set(wide["requestedIndices"]) & set(narrow["requestedIndices"])
                            current=keys_in_common_aoi(wide,common)
                            previous=keys_in_common_aoi(narrow,common)
                            if not current or not previous or current != previous:
                                raise RuntimeError("Aspect candidates have missing/extra identities on shared AOIs")
                    if options.intermediate_zoom_experiment and mode=="zoom":
                        if observation["serverLod"]!=0 or observation["blockSize"]!=10 or observation["blockCount"]!=100:
                            raise RuntimeError("Intermediate zoom lost required native LOD0/10x100 geometry")
                        baseline=zoom_observations[-2]
                        common=set(observation["requestedIndices"]) & set(baseline["requestedIndices"])
                        def source_keys(rowset):
                            return {(r.get("kind"),str(r.get("pointId") or r.get("uuid") or r.get("id")))
                                    for r in rowset if isinstance(r,dict) and
                                    isinstance(r.get("x"),(int,float)) and isinstance(r.get("y"),(int,float)) and
                                    int(r["y"])//10*100+int(r["x"])//10 in common}
                        if not common or source_keys(observation["rows"])!=source_keys(baseline["rows"]):
                            raise RuntimeError("Intermediate zoom changed identity set on shared AOIs")
                        if observation["aoiCount"] <= baseline["aoiCount"]:
                            raise RuntimeError("Intermediate zoom did not expand fresh native LOD0 coverage")
                next_trace = wait_for(live_root / "map013-native-trace-result.json",
                    ExpectedReply("trace", next_trace_id, profile, session, pid, challenge, trace_time), 24)
                (attempt / ("second-trace-" + mode + ".json")).write_text(
                    json.dumps(next_trace, indent=2), encoding="utf-8")
                if next_trace["state"] != "observed":
                    raise RuntimeError("second-region observer ended without a complete trace")
                previous_trace_id, previous_region_id = next_trace_id, next_id
        # Fail closed: even successful listener subscription is not a new
        # positive baseline until actual raw payloads can be correlated.
        return 0
    except Exception as error:
        exception = f"{type(error).__name__}: {error}"
        print("LIVE_OBSERVER_ERROR", exception, flush=True)
        return 2
    finally:
        if options.sequence_experiment and owned is not None and 'group_trace_id' in locals() and not (attempt / "sequence-trace.json").exists():
            try:
                partial_trace=wait_for(live_root / "map013-native-trace-result.json",
                    ExpectedReply("trace",group_trace_id,profile,session,pid,challenge,group_trace_time),24)
                (attempt / "sequence-trace.json").write_text(json.dumps(partial_trace,indent=2),encoding="utf-8")
            except Exception as trace_error:
                (attempt / "sequence-trace-error.txt").write_text(repr(trace_error),encoding="utf-8")
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
            "state": "PARTIAL" if exception or accuracy_comparison_failed or (trace is None and not options.unmodified_control) else "OBSERVATION_ONLY",
            "sequenceAccuracyComparisonFailed": accuracy_comparison_failed,
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
