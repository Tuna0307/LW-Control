"""Fail-closed, READ-ONLY audit of an actual or proposed March source receipt.

This is an evidence checker, NOT a game probe. In particular, a point-response
flag or a dictionary of WorldMarch UUIDs does not imply a March rect request,
response, consumption, complete population, or known TrainData lifetime.

CLI emits aggregate counts / reasons only (no raw UUIDs).
"""

import argparse
from datetime import datetime, timezone
from contextlib import closing
import json
import math
from pathlib import Path
import sqlite3


RECT_KEYS = ("xMin", "yMin", "xMax", "yMax")
TICKS_AT_UNIX_EPOCH = 621355968000000000
MIN_TICKS = 630822816000000000  # 2000-01-01
MAX_TICKS = 662380416000000000  # 2100-01-01
SOURCE_METHODS = {
    "request": "WorldGetRectMarchInfosMessage.CSSetData",
    "response": "WorldGetRectMarchInfosMessage.CSHandleResponse",
    "handler": "WorldMarchDataManager.HandleWorldGetRectMarchInfos",
    "process": "WorldMarchDataManager.TryExcuteMarchMessage",
}
SOURCE_CLASSES_AND_TOKENS = {
    "request": ("WorldGetRectMarchInfosMessage", "0x060019a6"),
    "response": ("WorldGetRectMarchInfosMessage", "0x060019a7"),
    "handler": ("WorldMarchDataManager", "0x0600327b"),
    "process": ("WorldMarchDataManager", "0x06003276"),
}
# The stage DateTime fields and correlated source event stamps must share the
# same *observed* clock domain. Five minutes is deliberately broad and not a
# substitute for direct request/response identity or event ordering.
NATIVE_CLOCK_CAPTURE_TOLERANCE_MS = 5 * 60 * 1000


def _number(value):
    return type(value) in (int, float) and math.isfinite(value)


def _rect(value):
    if not isinstance(value, dict) or not all(_number(value.get(k)) for k in RECT_KEYS):
        return None
    result = tuple(float(value[k]) for k in RECT_KEYS)
    return result if result[0] < result[2] and result[1] < result[3] else None


def _ticks(value):
    return type(value) is int and MIN_TICKS <= value <= MAX_TICKS


def _captured_unix_ms(value):
    if not isinstance(value, str):
        return None
    try:
        iso = datetime.fromisoformat(value.replace("Z", "+00:00"))
        return round(iso.timestamp() * 1000) if iso.tzinfo else None
    except (ValueError, OverflowError):
        return None


def _source_timestamps(stage, name, capture_ms, failures):
    get = stage.get("lastGetBlockMessageTicks")
    handled = stage.get("lastHandleBlockMessageTicks")
    if not _ticks(get) or not _ticks(handled):
        failures.append(f"{name}: numeric native DateTime ticks unavailable (display strings are not receipts)")
        return None
    # Fail closed on stale or timezone-ambiguous native timestamps. The observed
    # game's Oct 14 DateTime text uses the same 02:29:13 as capturedAt 02:29Z.
    # A future local-time offset requires an independently documented source
    # mapping rather than silently admitting a many-hour mismatch.
    for key, ticks in (("get", get), ("handle", handled)):
        if abs((ticks - TICKS_AT_UNIX_EPOCH) / 10000 - capture_ms) > NATIVE_CLOCK_CAPTURE_TOLERANCE_MS:
            failures.append(f"{name}: native {key} timestamp incompatible with capture clock")
    return get, handled


def _uuid_set(stage, name, failures):
    uuids = stage.get("marchUuids")
    count = stage.get("marchDictionaryCount")
    if (not isinstance(uuids, list) or not all(isinstance(u, str) and u for u in uuids)
            or len(set(uuids)) != len(uuids)
            or stage.get("marchUuidSampleCapped") is not False):
        failures.append(f"{name}: incomplete, duplicate, or capped native March UUID sample")
        return None
    if type(count) is not int or count != len(uuids):
        failures.append(f"{name}: native March dictionary count/UUID mismatch")
    return set(uuids)


def _queue_proven(stage, name, failures):
    count = stage.get("pendingQueueCount")
    merged = stage.get("mergedQueueCount")
    if (stage.get("pendingQueuePresent") is not True or
            stage.get("mergedQueuePresent") is not True or
            type(count) is not int or count < 0 or
            type(merged) is not int or merged < 0):
        failures.append(f"{name}: March queue count missing, ambiguous, or invalid")
        return
    proof = stage.get("pendingQueueCountProvenance")
    if not isinstance(proof, dict) or not (
        proof.get("fieldToken") == "0x04002561"
        and proof.get("runtimeType") == "GameKit.Base.StepProcessQueue"
        and proof.get("memberName") == "Count"
        and proof.get("getterParameterCount") == 0
        and proof.get("readOnlyGetterVerified") is True
        and proof.get("exactInstalledTypeVerified") is True
    ):
        failures.append(f"{name}: StepProcessQueue.Count getter provenance unverified")
    if count != 0 or merged != 0:
        failures.append(f"{name}: March queue or merged backlog not drained")


def _stage(receipt, key, capture_ms, failures):
    stage = receipt.get(key)
    if not isinstance(stage, dict) or stage.get("available") is not True:
        failures.append(f"{key}: missing before/point/processed native march source")
        return None
    if not _number(stage.get("observedAtRuntimeSeconds")):
        failures.append(f"{key}: native observation runtime timestamp missing")
    if _rect(stage.get("viewRect")) is None or _rect(stage.get("cameraViewRect")) is None:
        failures.append(f"{key}: native March view/camera rectangle unavailable")
    _source_timestamps(stage, key, capture_ms, failures)
    _uuid_set(stage, key, failures)
    _queue_proven(stage, key, failures)
    return stage


def audit_receipt(receipt):
    """Return a NONQUALIFIED verdict unless the *entire* causal chain is proved."""
    failures = []
    capture_ms = _captured_unix_ms(receipt.get("capturedAt"))
    if capture_ms is None:
        failures.append("capture timestamp is not an aware ISO-8601 instant")
        capture_ms = 0
    server = receipt.get("serverId")
    world = receipt.get("worldId")
    if type(server) is not int or server <= 0 or type(world) is not int:
        failures.append("native server/world attribution absent")
    if (receipt.get("isRecvViewPoints") is not True
            or receipt.get("hasReceiveViewPointsReply") is not True
            or receipt.get("responseFlagsTransitioned") is not True):
        failures.append("independent point readiness missing")
    before = _stage(receipt, "marchSourceBefore", capture_ms, failures)
    point = _stage(receipt, "marchSourceAtPointReady", capture_ms, failures)
    after = _stage(receipt, "marchSourceAfterProcess", capture_ms, failures)
    stages = (before, point, after)
    if all(stages):
        moments = [st["observedAtRuntimeSeconds"] for st in stages]
        if not all(_number(m) for m in moments) or not moments[0] < moments[1] < moments[2]:
            failures.append("before/point/processed observation times are not strictly ordered")
        for field in ("lastGetBlockMessageTicks", "lastHandleBlockMessageTicks"):
            values = [st.get(field) for st in stages]
            if all(_ticks(v) for v in values) and not values[0] <= values[1] <= values[2]:
                failures.append(f"native {field} moved backwards between stages")
        handled = [s.get("lastHandleBlockMessageTicks") for s in stages]
        if all(_ticks(v) for v in handled) and handled[2] <= handled[1]:
            failures.append("no native March process timestamp advanced after point readiness")
        if (_rect(point.get("viewRect")) != _rect(after.get("viewRect")) or
                _rect(point.get("cameraViewRect")) != _rect(after.get("cameraViewRect"))):
            failures.append("March manager rectangle changed before processing completed")

    events = receipt.get("marchRectEvents")
    if not isinstance(events, list):
        events = []
    by_phase = {name: [event for event in events if isinstance(event, dict)
                       and event.get("phase") == name] for name in SOURCE_METHODS}
    if any(len(found) != 1 for found in by_phase.values()):
        failures.append("missing or duplicate March rect request/response/handler/process source events")
    else:
        phases = ("request", "response", "handler", "process")
        source = [by_phase[phase][0] for phase in phases]
        keys = [event.get("sourceCorrelationId") for event in source]
        if not all(isinstance(k, str) and k for k in keys) or len(set(keys)) != 1:
            failures.append("March request/response/handler/process are not correlated by source ID")
        rects = [_rect(event.get("rect")) for event in source]
        active_rect = _rect(point.get("viewRect")) if point else None
        if (active_rect is None or any(rect != active_rect for rect in rects)
                or len(set(rects)) != 1):
            failures.append("March request/response/handler/process rectangle does not match active manager view rectangle")
        for phase, event in zip(phases, source):
            expected_class, expected_token = SOURCE_CLASSES_AND_TOKENS[phase]
            if (event.get("sourceObserved") is not True or
                    event.get("method") != SOURCE_METHODS[phase] or
                    event.get("sourceClass") != expected_class or
                    event.get("methodToken") != expected_token or
                    event.get("serverId") != server or event.get("worldId") != world):
                failures.append(f"March {phase} method/source/server/world attribution unverified")
        moments = [event.get("observedAtRuntimeSeconds") for event in source]
        if not all(_number(t) for t in moments) or not all(
                a <= b for a, b in zip(moments, moments[1:])):
            failures.append("March request/response/handler/process runtime order invalid")
        elif all(stages) and not (
            before["observedAtRuntimeSeconds"] <= moments[0] <= moments[-1]
            <= after["observedAtRuntimeSeconds"] and moments[-1] >= point["observedAtRuntimeSeconds"]
        ):
            failures.append("March processing not bound to the retained target-camera stage")
        stamps = [event.get("nativeTicks") for event in source]
        if not all(_ticks(t) for t in stamps) or not all(
                a <= b for a, b in zip(stamps, stamps[1:])):
            failures.append("March request/response/handler/process native timestamps invalid")
        elif all(stages) and all(_ticks(st.get("lastHandleBlockMessageTicks")) for st in stages):
            if not (point["lastHandleBlockMessageTicks"] < stamps[-1]
                    <= after["lastHandleBlockMessageTicks"]):
                failures.append("native handle timestamp does not corroborate observed processing")
        for stamp in stamps:
            if (_ticks(stamp) and
                    abs((stamp - TICKS_AT_UNIX_EPOCH) / 10000 - capture_ms)
                    > NATIVE_CLOCK_CAPTURE_TOLERANCE_MS):
                failures.append("March rect event native timestamp outside capture window")

    oracle = receipt.get("independentActiveMarches")
    if not isinstance(oracle, dict) or oracle.get("sourceObserved") is not True:
        failures.append("independent contemporaneous native active-March UUID oracle absent")
    elif after:
        oracle_ms = oracle.get("observedAtUnixMs")
        server_now = receipt.get("nativeServerTimeUnixMs")
        if (type(server_now) is not int or
                type(oracle_ms) is not int or abs(oracle_ms - capture_ms) > 1000 or
                abs(server_now - oracle_ms) > 1000):
            failures.append("independent March oracle stale or server-clock mismatch")
        if oracle.get("serverId") != server or oracle.get("worldId") != world or (
                _rect(oracle.get("rect")) not in
                (_rect(after.get("viewRect")), _rect(after.get("cameraViewRect")))):
            failures.append("independent March oracle belongs to another server/world/rectangle")
        identities = oracle.get("activeMarches")
        if not isinstance(identities, list) or not identities:
            failures.append("independent native active-March records absent")
        else:
            seen = set()
            available = _uuid_set(after, "marchSourceAfterProcess", failures)
            for item in identities:
                if not isinstance(item, dict):
                    failures.append("malformed native active-March identity")
                    continue
                uid = item.get("uuid")
                if not isinstance(uid, str) or not uid or uid in seen or (
                        available is not None and uid not in available):
                    failures.append("duplicate, missing or stale native March UUID")
                else:
                    seen.add(uid)
                arrived = item.get("arriveTs")
                arrival_ms = (arrived * 1000 if type(arrived) is int and 946684800 <= arrived <= 4102444800
                              else arrived if type(arrived) is int and 946684800000 <= arrived <= 4102444800000
                              else None)
                if (arrival_ms is None or type(server_now) is not int or
                        arrival_ms <= server_now or item.get("serverId") != server or
                        item.get("worldId") != world or
                        type(item.get("observedAtUnixMs")) is not int or
                        abs(item["observedAtUnixMs"] - oracle_ms) > 1000):
                    failures.append("stale, expired or uncorrelated native March UUID")

    return {
        "receiptHasActualPointReadyMarchManagerSnapshot": bool(
            point and point.get("available") is True and isinstance(point.get("marchUuids"), list)),
        "observedMarchDictionaryCount": point.get("marchDictionaryCount") if point else None,
        "mobileFreshnessQualified": not failures,
        "globallyQualified": False,  # never infer full-world from one region
        "failureReasons": sorted(set(failures)),
    }


def compare_archived(receipt, benchmark_root):
    """Only time-separated aggregate UUID intersections, never a recall claim."""
    if not benchmark_root.is_dir():
        raise ValueError("archived benchmark directory missing")
    source = receipt.get("marchSourceAtPointReady")
    uuids = source.get("marchUuids") if isinstance(source, dict) else None
    if (not isinstance(uuids, list) or source.get("marchUuidSampleCapped") is not False
            or len(set(uuids)) != len(uuids)):
        raise ValueError("uncapped unique actual March snapshot required for historical comparison")
    observed = set(uuids)
    benchmark = json.loads((benchmark_root / "benchmark.json").read_text(encoding="utf-8"))
    if len(benchmark) != 6:
        raise ValueError("six saved benchmark rounds required")
    source_ms = _captured_unix_ms(receipt.get("capturedAt"))
    rounds = []
    for index, run in enumerate(benchmark):
        if run.get("round") != index or run.get("terminal", {}).get("phase") != "completed":
            raise ValueError("incomplete archived scan metadata")
        server = run["terminal"].get("serverId")
        db = (benchmark_root / f"benchmark-{index}.db").resolve()
        if not db.is_file():
            raise ValueError("archived database missing")
        kinds = {}
        with closing(sqlite3.connect(db.as_uri() + "?mode=ro", uri=True)) as connection:
            for kind in ("truck", "railway"):
                keys = set()
                for record_key, stored_server, payload in connection.execute(
                    "SELECT record_key,server_id,data_json FROM map_records WHERE kind=?", (kind,)
                ):
                    data = json.loads(payload)
                    if (stored_server != server or str(data.get("uuid", record_key)) != str(record_key)
                            or record_key in keys):
                        raise ValueError("archived March UUID/server identity mismatch")
                    keys.add(str(record_key))
                kinds[kind] = {"archivedCount": len(keys), "overlapWithLaterUnclassifiedManagerUUIDs": len(keys & observed)}
        finish_ms = round((run["terminal"]["startedAt"] + run["seconds"]) * 1000)
        rounds.append({"round": index, "elapsedBetweenArchiveFinishAndLaterObservationSeconds":
                       round((source_ms - finish_ms) / 1000, 3) if source_ms else None,
                       "kinds": kinds})
    return {
        "sameTime": False,
        "comparison": "time-separated, unclassified March-manager UUIDs versus archived Truck/Railway identities",
        "laterObservedMarchUUIDCount": len(observed),
        "rounds": rounds,
        "accuracyOrExpiryInferred": False,
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("receipt", type=Path)
    parser.add_argument("--archived-mixed", type=Path)
    args = parser.parse_args()
    receipt = json.loads(args.receipt.read_text(encoding="utf-8"))
    output = {"audit": audit_receipt(receipt)}
    if args.archived_mixed:
        output["historicalOverlap"] = compare_archived(receipt, args.archived_mixed)
    print(json.dumps(output, indent=2))
