"""Conservative *offline* same-session mobile-march comparison contract.

This checker does not observe or operate the game. A 'contractPass' means that
the supplied evidence satisfies structural/causal inverse predicates; it does
not establish that the producer's self-reported source provenance is genuine,
or qualify a full-world/mobile scanner. Only real hash-pinned source witnesses,
independently reviewed by the native owner, can provide that provenance.
"""

import math

from audit_passive_march_receipt import audit_receipt, _rect


ASSEMBLY_SHA256 = "bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e"
ALLOWED_TYPES = {1: "truck", 2: "railway"}
MAX_OBSERVER_SKEW_SECONDS = 1.0
# Cohorts with collection windows longer than the stated observer skew cannot
# certify a simultaneous complete population even when the intervals overlap.
MAX_COHORT_CAPTURE_SECONDS = MAX_OBSERVER_SKEW_SECONDS
# A native clock sampled inside a (<=1s) snapshot interval cannot attest to
# freshness at its end if the last arrival is only milliseconds away.
ARRIVAL_AFTER_CLOCK_MS = 1000


def _int(value):
    return type(value) is int


def _runtime(value):
    return type(value) in (int, float) and math.isfinite(value) and value >= 0


def _arrival_ms(value):
    if not _int(value):
        return None
    if 946684800 <= value <= 4102444800:
        return value * 1000
    if 946684800000 <= value <= 4102444800000:
        return value
    return None


def _interval(value):
    if not isinstance(value, dict):
        return None
    start, end = value.get("observedStartRuntimeSeconds"), value.get("observedEndRuntimeSeconds")
    return ((start, end) if _runtime(start) and _runtime(end) and start <= end else None)


def _identity_map(cohort, label, server, world, errors):
    if not isinstance(cohort, dict) or cohort.get("sourceObserved") is not True:
        errors.append(f"{label}: missing source-observed typed cohort")
        return {}, None
    interval = _interval(cohort)
    if interval is None:
        errors.append(f"{label}: missing typed monotonic observation interval")
    elif interval[1] - interval[0] > MAX_COHORT_CAPTURE_SECONDS:
        errors.append(f"{label}: capture duration exceeds observer bound")
    if cohort.get("serverId") != server or cohort.get("worldId") != world:
        errors.append(f"{label}: server/world does not match native session")
    rows = cohort.get("records")
    if not isinstance(rows, list):
        errors.append(f"{label}: typed March records absent")
        return {}, interval
    identities = {}
    train_ids = {}
    for index, item in enumerate(rows):
        if not isinstance(item, dict):
            errors.append(f"{label}: malformed typed native March row")
            continue
        uuid = item.get("uuid")
        march_uuid = item.get("marchUuid")
        train_uuid = item.get("trainUuid")
        train_type = item.get("trainType")
        x, y = item.get("x"), item.get("y")
        arrival = _arrival_ms(item.get("arriveTs"))
        # GetMarchCurPosIndex():int32 is an *index getter*, NOT a source
        # method that directly returns an x/y pair or a lifecycle event.
        # Lua converts the observed index with world.IndexToTilePos, with
        # a size-based fallback; require the source-backed conversion.
        raw_index = item.get("pointIndex")
        if (not isinstance(uuid, str) or not uuid or march_uuid != uuid or
                not isinstance(train_uuid, str) or not train_uuid or
                not _int(train_type) or train_type not in ALLOWED_TYPES or
                not _int(item.get("ownerServer")) or item["ownerServer"] <= 0 or
                not _int(x) or not 0 <= x <= 999 or
                not _int(y) or not 0 <= y <= 999 or arrival is None or
                not _int(raw_index) or raw_index != y * 1000 + x + 1 or
                item.get("positionSource") != "WorldMarch.GetMarchCurPosIndex" or
                item.get("positionMethodToken") != "0x06003208" or
                item.get("positionConversion") != "WorldScene.IndexToTilePos" or
                item.get("positionConversionToken") != "0x060039c3" or
                item.get("serverId") != server or item.get("worldId") != world or
                item.get("sourceClass") != "WorldMarch.train" or
                item.get("sourceObserved") is not True):
            errors.append(f"{label}: unknown/stale/untagged Truck/Train type, clock or position")
            continue
        if uuid in identities:
            errors.append(f"{label}: current manager UUID cache collision")
            continue
        if train_uuid in train_ids and train_ids[train_uuid] != uuid:
            errors.append(f"{label}: train UUID maps to competing March UUIDs")
        train_ids[train_uuid] = uuid
        identities[uuid] = {
            "trainUuid": train_uuid,
            "trainType": train_type,
            "x": x,
            "y": y,
            "pointIndex": raw_index,
            "arrivalMs": arrival,
            "ownerServer": item.get("ownerServer"),
        }
    if not _int(cohort.get("count")) or cohort["count"] != len(rows):
        errors.append(f"{label}: unverified or inconsistent typed count")
    if cohort.get("capped") is not False:
        errors.append(f"{label}: capped or unproven complete typed cohort")
    return identities, interval


def _overlap(a, b):
    return a is not None and b is not None and max(a[0], b[0]) <= min(a[1], b[1])


def audit_compare_ready(receipt):
    """Distinguish native *contract* success from actual proven source parity.

    Inputs beyond the point/rect/queue base receipt are explicitly future-only
    and MUST originate from a same-session independent native owner.
    No archive presently has these complete simultaneous typed cohorts.
    """
    base = audit_receipt(receipt)
    errors = []
    if not base["mobileFreshnessQualified"]:
        errors.append("underlying March request/response/queue source receipt not qualified")
    compare = receipt.get("compareReadyEvidence")
    if not isinstance(compare, dict):
        errors.append("no same-session compare-ready evidence")
        return _result(base, errors, None)
    if compare.get("installedAssemblySha256") != ASSEMBLY_SHA256:
        errors.append("native source assembly hash not proven")
    if (compare.get("sessionId") != receipt.get("launchSessionId")
            or not isinstance(compare.get("sessionId"), str) or not compare["sessionId"]):
        errors.append("native compare cohort belongs to different or missing session")
    server, world = receipt.get("serverId"), receipt.get("worldId")
    clock = compare.get("serverClock")
    now_ms, clock_time = None, None
    if not isinstance(clock, dict) or not (
        clock.get("sourceObserved") is True and
        clock.get("sourceDomain") == "native-server-clock" and
        isinstance(clock.get("sourceRef"), str) and bool(clock["sourceRef"].strip()) and
        _int(clock.get("unixMilliseconds")) and
        _runtime(clock.get("observedAtRuntimeSeconds")) and
        clock.get("serverId") == server and clock.get("worldId") == world
    ):
        errors.append("typed independent native server clock/source unavailable")
    else:
        now_ms, clock_time = clock["unixMilliseconds"], clock["observedAtRuntimeSeconds"]
        if receipt.get("nativeServerTimeUnixMs") != now_ms:
            errors.append("native server clock disagrees with base receipt")
    before_rect = _rect((receipt.get("marchSourceBefore") or {}).get("viewRect"))
    ready_rect = _rect((receipt.get("marchSourceAtPointReady") or {}).get("viewRect"))
    after_rect = _rect((receipt.get("marchSourceAfterProcess") or {}).get("viewRect"))
    target_rect = _rect(compare.get("targetMarchRect"))
    if (target_rect is None or ready_rect != target_rect or after_rect != target_rect
            or before_rect is None or before_rect == target_rect):
        errors.append("old manager rectangle or mismatched target view persisted")
    if _rect(compare.get("targetCameraViewRect")) != _rect(
            (receipt.get("marchSourceAfterProcess") or {}).get("cameraViewRect")):
        errors.append("target camera footprint missing or inconsistent")
    selected = compare.get("selectedTypes")
    if (not isinstance(selected, list) or not selected or
            any(type(t) is not str or t not in ("truck", "railway") for t in selected) or
            len(set(selected)) != len(selected)):
        errors.append("typed Truck/Railway selection missing or ambiguous")
        selected = []
    point, point_interval = _identity_map(
        compare.get("pointReadyCohort"), "pointReadyCohort", server, world, errors
    )
    after, after_interval = _identity_map(
        compare.get("afterProcessCohort"), "afterProcessCohort", server, world, errors
    )
    oracle, oracle_interval = _identity_map(
        compare.get("independentNativeCohort"), "independentNativeCohort", server, world, errors
    )
    point_at = (receipt.get("marchSourceAtPointReady") or {}).get("observedAtRuntimeSeconds")
    after_at = (receipt.get("marchSourceAfterProcess") or {}).get("observedAtRuntimeSeconds")
    if point_interval is None or not _runtime(point_at) or not (
            point_interval[0] <= point_at <= point_interval[1]):
        errors.append("point-ready typed cohort does not cover manager read")
    if after_interval is None or not _runtime(after_at) or not (
            after_interval[0] <= after_at <= after_interval[1]):
        errors.append("after-process typed cohort does not cover manager read")
    if (point_interval is not None and after_interval is not None and
            point_interval[1] >= after_interval[0]):
        errors.append("point-ready and after-process cohort capture windows overlap or reverse")
    if (not _overlap(after_interval, oracle_interval) or
            clock_time is None or not all(
                i is not None and i[0] - MAX_OBSERVER_SKEW_SECONDS <= clock_time <=
                i[1] + MAX_OBSERVER_SKEW_SECONDS
                for i in (after_interval, oracle_interval)
            )):
        errors.append("native March UUID cohorts and server clock are not temporally overlapping")
    stage = receipt.get("marchSourceAfterProcess") or {}
    point_stage = receipt.get("marchSourceAtPointReady") or {}
    point_source_uuids = point_stage.get("marchUuids")
    if not isinstance(point_source_uuids, list) or set(point) - set(point_source_uuids):
        errors.append("point-ready typed March absent from manager source snapshot")
    complete_uuids = stage.get("marchUuids")
    if not isinstance(complete_uuids, list):
        complete_uuids = []
    if not oracle:
        errors.append("no independently observed active typed Truck/Train UUIDs")
    if set(oracle) != set(after):
        errors.append("typed current manager population differs from same-time independent cohort")
    if set(after) - set(complete_uuids):
        errors.append("typed March exists outside sampled native manager UUID population")
    # Manager UUIDs cannot silently vanish between point-ready and
    # post-response stages, particularly when the final TrainData.arriveTs
    # remains in the future. Source-verified terminal lifecycle evidence
    # (not present in this archive/schema) would need separate admission.
    for missing_uuid in set(point) - set(after):
        earlier = point[missing_uuid]
        if now_ms is not None and earlier["arrivalMs"] > now_ms:
            errors.append("previously observed unexpired March vanished without source terminal proof")
        else:
            errors.append("previously observed March vanished without source terminal proof")
    # UUID collision may occur *between* stage dictionaries, not only
    # within one. An old March UUID and a new one sharing the same train
    # UUID must not become an apparently clean snapshot simply because both
    # are absent from the same isolated cohort.
    point_by_train = {item["trainUuid"]: uuid for uuid, item in point.items()}
    after_by_train = {item["trainUuid"]: uuid for uuid, item in after.items()}
    for train_uuid in point_by_train.keys() & after_by_train.keys():
        if point_by_train[train_uuid] != after_by_train[train_uuid]:
            errors.append("train UUID rebound to different March between native stages")
    for uuid, cur in oracle.items():
        if uuid not in after or uuid not in complete_uuids:
            errors.append("independent native UUID absent from current manager cache")
            continue
        if cur != after[uuid]:
            errors.append("current manager cache identity/type/position/lifetime collision")
        if ALLOWED_TYPES[cur["trainType"]] not in selected:
            errors.append("native Train type outside selected kind")
        if now_ms is None or cur["arrivalMs"] <= now_ms + ARRIVAL_AFTER_CLOCK_MS:
            errors.append("native Truck/Train final arrival expired or untyped server clock")
        earlier = point.get(uuid)
        if earlier is not None:
            if (earlier["trainUuid"] != cur["trainUuid"] or
                    earlier["trainType"] != cur["trainType"] or
                    earlier["ownerServer"] != cur["ownerServer"] or
                    earlier["arrivalMs"] != cur["arrivalMs"]):
                errors.append("same UUID repurposed between current manager stages")
            if (earlier["x"], earlier["y"]) != (cur["x"], cur["y"]):
                # A moved March is allowed *only* with an exact source-observed
                # intermediate position transition, not stale cache substitution.
                movements = compare.get("nativeMovementTransitions")
                candidates = [move for move in movements if isinstance(move, dict)
                              and move.get("uuid") == uuid] if isinstance(movements, list) else []
                if len(candidates) != 1:
                    errors.append("moved native UUID has no unique source-backed transition")
                else:
                    move = candidates[0]
                    if (move.get("sourceObserved") is not True or
                            move.get("sourceMethod") != "WorldMarch.GetMarchCurPosIndex" or
                            move.get("sourceMethodToken") != "0x06003208" or
                            move.get("serverId") != server or move.get("worldId") != world or
                            move.get("from") != [earlier["x"], earlier["y"]] or
                            move.get("to") != [cur["x"], cur["y"]] or
                            move.get("fromPointIndex") != earlier["pointIndex"] or
                            move.get("toPointIndex") != cur["pointIndex"] or
                            not _runtime(move.get("observedAtRuntimeSeconds")) or
                            point_interval is None or after_interval is None or
                            not point_interval[0] <= move["observedAtRuntimeSeconds"] <= after_interval[1]):
                        errors.append("native movement transition stale/uncorrelated")
    return _result(base, errors, {"point": len(point), "after": len(after), "oracle": len(oracle)})


def _result(base, errors, counts):
    return {
        "observedMarchDictionaryCount": base["observedMarchDictionaryCount"],
        "sameSessionCompareContractPass": not errors,
        "sourceAuthenticityIndependentlyVerified": False,
        "globalMobileAccuracyQualified": False,
        "typedCounts": counts,
        "reasons": sorted(set(errors)),
    }
