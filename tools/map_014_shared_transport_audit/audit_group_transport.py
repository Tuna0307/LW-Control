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


class BoundaryError(ValueError):
    """Missing authentic producer evidence or inconsistent controlled receipt."""


def section(source: str, beginning: str, end: str) -> str:
    start = source.find(beginning)
    stop = source.find(end, start + len(beginning))
    if start < 0 or stop < 0:
        raise BoundaryError(f"source contract changed near {beginning!r}")
    return source[start:stop]


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
        )
        if (not all(required) or not version or not lua_version or
                not commands or not results or lua_version.group(1) != version.group(1)):
            raise BoundaryError("source-backed single-file protocol anchors changed")
        if modes != {"native", "expanded", "direct", "coverage", "anchor", "edge", "zoom"}:
            raise BoundaryError(f"Lua request-mode whitelist changed: {sorted(modes)}")
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
    for proof in aois:
        require(isinstance(proof, dict) and
                all(proof.get(k) == owner[k] for k in owner),
                "mixed native AOI owner/profile/session")
        idx = proof.get("nativeAoiIndex")
        require(type(idx) is int and 0 <= idx < 10000 and idx not in found,
                "duplicate, missing or invalid native AOI")
        found.add(idx)
        col, row = idx % 100, idx // 100
        block = row // 2 * 50 + col // 2
        reply = (proof.get("nativeReplyRequestId"), proof.get("nativeReplyAtUnixMs"))
        require(bool(reply[0]) and type(reply[1]) is int and
                stages[0]["requestStartedAtUnixMs"] <= reply[1] < ack.get("homeNativeReplyAtUnixMs", 0),
                "stale/missing native AOI response")
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
    home_id = ack.get("homeRestoreRequestId")
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
    outcomes = [archive_rejection(path, contract)
                for path in (args.archive or ARCHIVES) if path.exists()]
    print(json.dumps({
        "producerVersion": contract.producer_version,
        "command": contract.command_path, "result": contract.result_path,
        "singleProbeModes": sorted(contract.modes),
        "sourceResultKeys": len(contract.output_keys),
        "unemittableGroupKeys": sorted(GROUP_ONLY_KEYS.difference(contract.output_keys)),
        "authenticatedGroupTransport": False, "archived": outcomes,
        "qualification": "CURRENT PRODUCER SINGLE-PROBE ONLY; NATIVE GROUP NOT PROVEN",
    }, indent=2))


if __name__ == "__main__":
    main()
