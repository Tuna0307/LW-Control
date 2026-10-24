"""INERT synthetic MarchData enumeration benchmark with production source guards.

Run from the repository root:
    python tools/map_019_review/mobile_hotpath_bench.py --repeats 5

There is no game, Lua VM, network or native packet access. This models the exact
collection-enumeration decision in the production Lua lane. The synthetic input
is constructed before measurements; no live/runtime speed claim follows.
"""

from __future__ import annotations

import argparse
from collections import defaultdict
from dataclasses import dataclass
import gc
import hashlib
import json
from pathlib import Path
import statistics
import time
import tracemalloc


ROOT = Path(__file__).resolve().parents[2]
PRODUCTION = ROOT / "tools/current_live_resource_probe.lua"
FPS = 60
SEND_SPACING_FRAMES = 3  # 20/s at synthetic 60 frames/s
FENCE = 25
RECTANGLES = 625
LOST_INITIAL = frozenset({173, 449})  # two incomplete fences -> 50 repairs
BACKGROUND = 400
START_UNIX_MS = 1_800_000_000_000


def source_guard() -> str:
    """Stop if the actual Lua request/collector contract has changed."""
    source = PRODUCTION.read_text(encoding="utf-8")
    required = (
        'local function train_march_aoi_records(world, block_size, block_count, selected_lookup, already_seen, capture_type)',
        'call(march_manager, "GetAllMarchesByCS")',
        'call(collection, "GetEnumerator")',
        'already_seen[march_id]',
        'local new_push = stamp ~= s.lastPushStamp',
        'train_march_aoi_records(s.world, 10, 100, s.wholeWorld, s.seen, s.captureType)',
        'if new_push then',
        'responseAttribution = "bounded_sequential_fences"',
        'fenceSize = requested_fence',
        'for n = s.next, math.min(624, s.next + (s.fenceSize or 25) - 1) do',
        'if s.pass == 2 then M._map015FastTruck.finish("lost_march_push")',
        's.replyWaitSeconds',
    )
    absent = [anchor for anchor in required if anchor not in source]
    if absent:
        raise ValueError(f"Production mobile collector/receipt contract changed: {absent}")
    return hashlib.sha256(PRODUCTION.read_bytes()).hexdigest()


@dataclass(frozen=True)
class March:
    uuid: str
    train_type: int
    x: int
    y: int
    owner_name: str
    train_quality: int
    carriage_num: int
    power: int
    end_time: int
    arrive_ts: int
    goods: tuple[tuple[int, int, int], ...]


@dataclass(frozen=True)
class FrameEvent:
    action: str  # received, handled, add, remove
    index: int


@dataclass(frozen=True)
class FenceEvidence:
    pass_number: int
    sends: int
    pushes: int
    certified: tuple[int, ...]


@dataclass(frozen=True)
class Trace:
    frames: int
    events: dict[int, tuple[FrameEvent, ...]]
    fences: tuple[FenceEvidence, ...]
    expected_pushes: int
    initial_requests: int
    repair_requests: int
    rectangles_verified: int
    initial_fences_failed: int
    repair_passes: int


def synth_march(index: int) -> March:
    """Deterministic typed record; some Trucks expire before final publication."""
    kind = 2 if index % 73 == 0 else 1
    captured_unix_ms = START_UNIX_MS + (index * SEND_SPACING_FRAMES // FPS) * 1000
    # Vary lifetime; endTime and arriveTs are independent admission deadlines.
    life_ms = 5_000 if index % 17 == 0 else 80_000
    return March(
        uuid=f"synthetic-march-{index:04d}", train_type=kind,
        x=(index * 37) % 1000, y=(index * 131) % 1000,
        owner_name=f"test-owner-{index % 41}", train_quality=index % 5 + 1,
        carriage_num=index % 6, power=index * 4711 + 9000,
        end_time=captured_unix_ms + life_ms,
        arrive_ts=captured_unix_ms + life_ms + 5_000,
        goods=((7, 500000 + index % 19, index % 9 + 1), (1, 1, index + 100)),
    )


def make_trace(*, late_frames: int = 1, unsolicited: bool = False) -> Trace:
    """Deterministic 625-request / fence25 transport with two lost first-pass pushes.

    A dropped reply makes its entire 25-send fence unproven. Each member of
    that fence is then repaired with exactly one outstanding request. Pushes
    carry NO rectangle echo; the generator knows identities only as the test
    scheduler. Neither algorithm receives request-ID-to-push mapping.
    """
    if late_frames < 0:
        raise ValueError("late_frames must be nonnegative")
    events: dict[int, list[FrameEvent]] = defaultdict(list)
    clock = 0
    pushes = 0
    repair_count = 0
    verified: set[int] = set()
    repairs: list[int] = []
    failed_fences = 0
    fence_evidence: list[FenceEvidence] = []

    def request(index: int, *, missing: bool = False) -> None:
        nonlocal clock, pushes
        clock += SEND_SPACING_FRAMES
        if missing:
            return
        received_frame = clock + 2
        handled_frame = received_frame + 1
        pushes += 1
        events[received_frame].append(FrameEvent("received", index))
        events[handled_frame].append(FrameEvent("handled", index))
        # Simulates row creation after native handling (possibly next frame).
        if index % 5 == 0 or index % 73 == 0:
            appeared = handled_frame + late_frames
            events[appeared].append(FrameEvent("add", index))
            events[appeared + 7].append(FrameEvent("remove", index))

    for start in range(0, RECTANGLES, FENCE):
        indices = list(range(start, start + FENCE))
        misses = [index for index in indices if index in LOST_INITIAL]
        for index in indices:
            request(index, missing=index in LOST_INITIAL)
        if misses:
            # Same 3-second bounded fence wait as native pump: ambiguous batch.
            clock += 3 * FPS
            repairs.extend(indices)
            failed_fences += 1
            fence_evidence.append(FenceEvidence(0, len(indices), len(indices) - len(misses), ()))
        else:
            verified.update(indices)
            fence_evidence.append(FenceEvidence(0, len(indices), len(indices), tuple(indices)))
        clock += 5  # fence acknowledgment settles before next group

    for index in repairs:
        request(index)
        repair_count += 1
        verified.add(index)  # one outstanding reply is an attributable repair
        fence_evidence.append(FenceEvidence(1, 1, 1, (index,)))
        clock += 5

    if unsolicited:
        # A cache-only row without new receipt/handled marker. The baseline
        # discovers it, a stamp-based optimization cannot prove it absent.
        index = 650
        events[clock + 10].append(FrameEvent("add", index))
        events[clock + 14].append(FrameEvent("remove", index))

    frames = max(clock + 15, max(events, default=0) + 2)
    return Trace(frames, {frame: tuple(batch) for frame, batch in events.items()}, tuple(fence_evidence),
                 pushes, RECTANGLES, repair_count, len(verified), failed_fences, 1)


def deadline_ms(value: int) -> int | None:
    return value * 1000 if 0 < value < 100_000_000_000 else value if value > 0 else None


def truck_unarrived(row: March, at_ms: int) -> bool:
    """Identical positive endTime/arriveTs ordering as native+managed Truck."""
    return all(deadline_ms(value) is None or deadline_ms(value) > at_ms
               for value in (row.end_time, row.arrive_ts))


def normalize(row: March) -> dict:
    """Fixed subset of production typed/owner/goods fields, held for equality."""
    return {
        "uuid": row.uuid, "trainType": row.train_type, "x": row.x, "y": row.y,
        "ownerName": row.owner_name, "trainQuality": row.train_quality,
        "carriageNum": row.carriage_num, "power": row.power,
        "endTime": row.end_time, "arriveTs": row.arrive_ts,
        "goods": row.goods,
    }


@dataclass(frozen=True)
class Result:
    rows: dict[str, dict]
    snapshots: int
    examined: int
    pushes: int
    handled: int
    arrival_dropped: int
    publish_dropped: int


def collect(trace: Trace, policy: str, *, background: int = BACKGROUND) -> Result:
    """One synthetic Update frame per step; input transport independent of policy.

    baseline: Lua FastTruck.pump currently enumerates every frame, including
              unchanged reply stamps, via GetAllMarchesByCS.
    handled_plus_one: enumerate after handled marker changes and the next frame.
              This is a hypothesis only; no source guarantee covers unrelated
              cache updates or an arbitrarily delayed parser mutation.
    """
    if policy not in ("baseline", "handled_plus_one"):
        raise ValueError(policy)
    cache: dict[str, March] = {
        f"background-{i}": March(f"background-{i}", 0, i % 1000, i % 1000,
                                   "", 0, 0, 0, 0, 0, ())
        for i in range(background)
    }
    seen: set[str] = set()
    accepted: dict[str, dict] = {}
    snapshots = examined = received = handled = arrival_dropped = 0
    follow_up_at = -1

    for frame in range(trace.frames):
        events = trace.events.get(frame, ())
        changed_handled = False
        for event in events:
            if event.action == "received":
                received += 1
            elif event.action == "handled":
                handled += 1
                changed_handled = True
            elif event.action == "add":
                row = synth_march(event.index)
                cache[row.uuid] = row
            elif event.action == "remove":
                cache.pop(f"synthetic-march-{event.index:04d}", None)
            else:
                raise AssertionError(event.action)

        if changed_handled:
            follow_up_at = frame + 1
        should_capture = (policy == "baseline" or frame == 0 or
                          changed_handled or frame == follow_up_at)
        if not should_capture:
            continue

        snapshots += 1
        captured_at = START_UNIX_MS + frame * 1000 // FPS
        # Source-shaped enumerator: every native march, then typed train check,
        # then seen-UUID check, then field/reward extraction for new rows only.
        for row in cache.values():
            examined += 1
            if row.train_type not in (1, 2) or row.uuid in seen:
                continue
            if row.train_type == 1 and not truck_unarrived(row, captured_at):
                arrival_dropped += 1
                continue
            seen.add(row.uuid)
            accepted[row.uuid] = normalize(row)

    # Native captures are rechecked at managed import before publication.
    publish_at = START_UNIX_MS + trace.frames * 1000 // FPS
    final = {}
    for uid, row in accepted.items():
        if row["trainType"] == 1 and not all(
            deadline_ms(row[name]) is None or deadline_ms(row[name]) > publish_at
            for name in ("endTime", "arriveTs")
        ):
            continue
        final[uid] = row
    return Result(final, snapshots, examined, received, handled,
                  arrival_dropped, len(accepted) - len(final))


def fingerprint(rows: dict[str, dict]) -> str:
    canonical = json.dumps(rows, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(canonical.encode("utf-8")).hexdigest()


def validate_equivalence(trace: Trace, old: Result, new: Result) -> None:
    assert (trace.initial_requests, trace.repair_requests, trace.rectangles_verified,
            trace.initial_fences_failed, trace.repair_passes) == (625, 50, 625, 2, 1)
    assert trace.expected_pushes == trace.initial_requests + trace.repair_requests - 2
    certified = []
    for fence in trace.fences:
        # Native March push has no rectangle ID. Either a full 25-ACK fence
        # certifies all 25, or no rectangle in it is certified until an
        # independently observed single-outstanding repair arrives.
        assert fence.sends in (1, FENCE)
        assert 0 <= fence.pushes <= fence.sends
        assert len(fence.certified) == (fence.sends if fence.pushes == fence.sends else 0)
        if fence.pass_number == 1:
            assert fence.sends == 1
        certified.extend(fence.certified)
    assert len(certified) == len(set(certified)) == RECTANGLES
    assert set(certified) == set(range(RECTANGLES))
    assert sum(fence.sends for fence in trace.fences) == 675
    assert sum(fence.pushes for fence in trace.fences) == trace.expected_pushes
    assert old.pushes == new.pushes == trace.expected_pushes
    assert old.handled == new.handled == trace.expected_pushes
    assert old.rows == new.rows, (
        f"typed row mismatch old={sorted(old.rows)} new={sorted(new.rows)}")
    assert {row["trainType"] for row in old.rows.values()} == {1, 2}
    assert len(old.rows) > 40


def measure(trace: Trace, policy: str, background: int) -> dict:
    gc.collect()
    tracemalloc.start()
    before_wall, before_cpu = time.perf_counter_ns(), time.process_time_ns()
    result = collect(trace, policy, background=background)
    cpu = (time.process_time_ns() - before_cpu) / 1e6
    wall = (time.perf_counter_ns() - before_wall) / 1e6
    _, peak = tracemalloc.get_traced_memory()
    tracemalloc.stop()
    return {"policy": policy, "wallMs": round(wall, 3), "cpuMs": round(cpu, 3),
            "peakTracedKiB": round(peak / 1024, 1), "snapshots": result.snapshots,
            "examinedMarches": result.examined, "rows": len(result.rows),
            "truckRows": sum(r["trainType"] == 1 for r in result.rows.values()),
            "railwayRows": sum(r["trainType"] == 2 for r in result.rows.values()),
            "arrivalDroppedObservations": result.arrival_dropped,
            "publishDroppedRows": result.publish_dropped,
            "fingerprint": fingerprint(result.rows)}


def benchmark(repeats: int = 5, background: int = BACKGROUND) -> dict:
    source_hash = source_guard()
    if repeats < 1 or background < 0:
        raise ValueError("repeats must be positive and background nonnegative")
    trace = make_trace(late_frames=1)
    reference = collect(trace, "baseline", background=background)
    candidate = collect(trace, "handled_plus_one", background=background)
    validate_equivalence(trace, reference, candidate)

    # Alternate order to lessen process thermal/cache/GC bias. Source events
    # are immutable and built before timing; each run gets a fresh native cache.
    trials: list[dict] = []
    for round_index in range(repeats):
        order = ("baseline", "handled_plus_one") if round_index % 2 == 0 else (
            "handled_plus_one", "baseline")
        for policy in order:
            trial = measure(trace, policy, background)
            assert trial["fingerprint"] == fingerprint(reference.rows)
            trials.append(trial)

    def summary(policy: str) -> dict:
        group = [trial for trial in trials if trial["policy"] == policy]
        return {"wallMedianMs": round(statistics.median(x["wallMs"] for x in group), 3),
                "cpuMedianMs": round(statistics.median(x["cpuMs"] for x in group), 3),
                "peakTracedMedianKiB": round(statistics.median(x["peakTracedKiB"] for x in group), 1),
                "snapshotCalls": group[0]["snapshots"],
                "examinedMarches": group[0]["examinedMarches"],
                "truckRows": group[0]["truckRows"],
                "railwayRows": group[0]["railwayRows"]}

    base, opt = summary("baseline"), summary("handled_plus_one")
    return {
        "scope": "inert synthetic frame/collection enumeration microbenchmark; no real native Lua execution",
        "productionLuaSha256": source_hash, "transport": {
            "frames": trace.frames, "syntheticFps": FPS, "sendRatePerSecond": 20,
            "fenceSize": FENCE, "initialRequests": trace.initial_requests,
            "repairRequests": trace.repair_requests,
            "repairPasses": trace.repair_passes,
            "failedFences": trace.initial_fences_failed,
            "receivedPushes": trace.expected_pushes,
            "verifiedRectangles": trace.rectangles_verified,
            "unprovenRectangles": RECTANGLES - trace.rectangles_verified,
            "backgroundMarches": background, "lateCacheFrames": 1},
        "equivalence": {"typedFieldsIdentical": True,
                        "finalRowHashSha256": fingerprint(reference.rows),
                        "truckRows": base["truckRows"], "railwayRows": base["railwayRows"]},
        "baseline": base, "handledPlusOne": opt,
        "candidateOverBaselineCpuRatio": round(opt["cpuMedianMs"] / base["cpuMedianMs"], 4),
        "candidateOverBaselineWallRatio": round(opt["wallMedianMs"] / base["wallMedianMs"], 4),
        "trials": trials,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repeats", type=int, default=5)
    parser.add_argument("--background", type=int, default=BACKGROUND)
    parser.add_argument("--output", type=Path, help="Optional ignored JSON output path")
    args = parser.parse_args()
    report = benchmark(args.repeats, args.background)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
