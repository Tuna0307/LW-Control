#!/usr/bin/env python3
"""MAP-013 passive native-trace reply helpers, kept for tools/live-harness/run_owned.py.

observe_scene_gate() waits for the game's own City/World scene through the private
wrapper trace (map013_wrapper_trace.lua) before a harness run sends world requests.
The original MAP-013/014 experiment CLI and its resource-probe instruments no longer
fit the current probe package; they are in the archive/pre-cleanup-2026-10-27 tag.
"""
from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import secrets
import sys
import time
import uuid

REPO = Path(__file__).resolve().parents[3]
TOOLS = REPO / "src" / "LWBridge.GameScripts"
sys.path.insert(0, str(TOOLS))
import run_overview_bridge_current as admitted
overview = admitted.base  # shim applies current-client v24 hash/compatibility gate


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
