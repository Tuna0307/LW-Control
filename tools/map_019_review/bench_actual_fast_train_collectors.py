"""Six-run matched *actual Lua collector* old-vs-current inert benchmark.

Compare the collector parsed from the pinned 3da98523 git blob against the
current checked-out production Lua, using the existing Lua 5.3 fake-game mock.
Neither version is loaded by production; no game, package, app, sockets or
services are accessed. Timings describe ONLY collector CPU, not scan wall time.

Run:
    python tools/map_019_review/bench_actual_fast_train_collectors.py
"""

from __future__ import annotations

from dataclasses import dataclass
import hashlib
import json
from pathlib import Path
import statistics
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(ROOT / "artifacts/map-013-lua-site"))
from lupa.lua53 import LuaRuntime
from tools.map_014_lead_native.test_fast_train_seen_gate import (  # noqa: E402
    FAKE_GAME,
    END,
    START,
    native_record,
)

SOURCE = ROOT / "tools/current_live_resource_probe.lua"
PIN = "3da98523"
UNARRIVED_START = "function M._map015FastTruck.unarrived"
UNARRIVED_END = "function M._map015FastTruck.finish"
FRAMES = 30
RUNS = 6

TYPED_REQUIRED = {
    "uuid", "marchUuid", "runtimeClass", "serverId", "worldId",
    "x", "y", "positionIndex", "ownerUid", "ownerName", "allianceUid",
    "allianceName", "allianceAbbr", "ownerServer", "targetServer",
    "srcServer", "power", "startTime", "endTime", "trainUuid",
    "trainCfgId", "trainType", "trainQuality", "carriageNum", "arriveTs",
    "robTimes", "protectTime", "truckMetadataKnown", "source",
}
OPTIONAL_TYPED = {
    "truckCurrentGoodsRaw", "truckMaxLootCount", "truckBaseGoodsCur",
    "truckExtraGoodsCur", "truckVipOn", "trainDataJson",
    "currentGoods", "maxLootCount",
}


def sha256(data: str) -> str:
    return hashlib.sha256(data.encode("utf-8")).hexdigest()


def pinned_old() -> str:
    result = subprocess.run(
        ["git", "show", f"{PIN}:tools/current_live_resource_probe.lua"],
        cwd=ROOT, check=True, capture_output=True, encoding="utf-8")
    return result.stdout


def extract(source: str, start: str, end: str) -> str:
    if source.count(start) != 1 or source.count(end) != 1:
        raise AssertionError(f"Source signature changed: {start} -> {end}")
    return start + source.split(start, 1)[1].split(end, 1)[0]


@dataclass(frozen=True)
class Case:
    name: str
    marches: int
    seen_mod: int
    # seen indices have i % seen_mod != 0, i.e. 90% seen with seen_mod=10
    capture: int
    no_seen: bool = False


CASES = (
    Case("truck-low-seen", 200, 10, 1, False),
    Case("combined-high-seen", 850, 8, 3, False),
    Case("truck-very-high-seen", 1800, 25, 1, False),
    Case("camera-no-seen", 160, 9, 3, True),
)


# Intentionally reuses the actual mock_game primitives/counters from
# test_fast_train_seen_gate, upgrading its TrainData payload with deterministic
# rich typed details to catch any loss in collector serialization.
RICH_FAKE_TRAIN_DATA = r"""
DataCenter.LWTrainDataManager.GetOneTrain = function(_, uuid)
    counters.trainLookups = counters.trainLookups + 1
    local n = tonumber(tostring(uuid):match("(%d+)$")) or 0
    return {
        type = n % 7 == 0 and 2 or 1,
        arriveTs = 1780000000000 + (n % 3 == 0 and -10000 or 10000),
        marchInfo = {robTimes = n % 3, protectTime = 1780000012345,
                     vipOn = n % 2 == 0},
        maxLootPerTrain = 3,
        baseGoods = {cur = {{type = 1, value = n * 10 + 5}}},
        extraGoods = {cur = {{type = 7, value = n + 4}}},
        GetCurRewardData = function()
            return {{type = 7, value = {id = 520011, num = n % 5 + 1}},
                    {type = 1, value = n + 25}}
        end,
        ToJson = function() return '{"type":2,"source":"mock"}' end,
    }
end
"""


def make_scene(case: Case) -> str:
    # March positions mutate deterministically on every frame; already-seen
    # UUIDs move as well and cannot be emitted a second time.
    # Distinct expired and future rows exercise the *actual* unarrived gate.
    return r"""
local marches, already_seen, selected = {}, {}, {}
for a = 0, 9999 do selected[a] = (a %% 71 ~= 0) end
for i = 1, %(n)d do
    local uuid = string.format("march-%%05d", i)
    local train_type
    if i %% 13 == 0 then train_type = "TrainType: 2"
    elseif i %% 7 == 0 then train_type = 2
    elseif i %% 29 == 0 then train_type = nil
    else train_type = 1 end
    local march = {
        kind = "march", uuid = uuid, ownerUid = "owner-"..i,
        ownerName = "Mock owner "..i, allianceUid = "alliance-"..i,
        allianceName = "Inert Alliance", allianceAbbr = "IA",
        ownerServer = 2212, targetServer = 2212, srcServer = 2212,
        serverId = 2212, worldId = 0, power = i*100,
        startTime = 1779998000000, endTime = 1780000000000 +
            (i %% 9 == 0 and -1000 or 200000),
        train = {kind = "train", type = train_type,
                 uuid = string.format("train-%%05d", i), cfgId = 100+i,
                 config = {quality = i %% 5 + 1, carriageNum = i %% 8 + 1}},
        GetMarchCurPosIndex = function(self) return self.position end
    }
    marches[#marches+1] = march
    if %(no_seen)s and i %% %(seen_mod)d ~= 0 then
        already_seen[uuid] = true
    end
end
local selectedWorld = mock_world(marches)
local seen = %(seen_value)s
local call_results = {}
function step_actual_collector(frame)
    for i = 1, #marches do
        local position = 1001 + ((i * 131 + frame * 59) %% 900000)
        marches[i].position = position
    end
    local rows, err = train_march_aoi_records(
        selectedWorld, 10, 100, selected, seen, %(capture)d)
    if rows == nil then return nil, nil, err end
    local admitted = {}
    for _, row in ipairs(rows) do
        if row.trainType ~= 1 or M._map015FastTruck.unarrived(row, 1780000000000) then
            admitted[#admitted + 1] = row
        end
    end
    return rows, admitted, nil
end
return step_actual_collector, counters
""" % {
        "n": case.marches,
        "seen_mod": case.seen_mod,
        "no_seen": "true" if not case.no_seen else "false",
        "seen_value": "nil" if case.no_seen else "already_seen",
        "capture": case.capture,
    }


class ActualCollector:
    def __init__(self, source: str, case: Case):
        self.lua = LuaRuntime(unpack_returned_tuples=True)
        # Execute exactly the production collector and expiry function; only
        # dependencies are synthetic, shared with worker1's Lua 5.3 tests.
        fragment = extract(source, START, END)
        unarrived = extract(source, UNARRIVED_START, UNARRIVED_END)
        # Worker1's small mock uses an and/or safe_get, which drops false Lua
        # booleans. For field-complete parity preserve false-valued vipOn.
        mock = FAKE_GAME.replace(
            'return object ~= nil and object[key] or nil',
            'if object == nil then return nil end; return object[key]')
        lua_code = (
            mock + "\n" + RICH_FAKE_TRAIN_DATA + "\n"
            + "local M = {_map015FastTruck={}}\n"
            + unarrived + "\n" + fragment + "\n" + make_scene(case)
        )
        self.step, self.counters = self.lua.execute(lua_code)
        self.lua.eval("collectgarbage")("collect")
        self.gc = self.lua.eval("collectgarbage")

    def run(self):
        # Bench only the collector call, never compile-time or row conversion.
        output = []
        before = {key: int(self.counters[key]) for key in
                  ("typeReads", "uuidReads", "trainLookups", "enumerated")}
        gc_before = float(self.gc("count"))
        wall_ns = cpu_ns = 0
        for frame in range(FRAMES):
            w0, c0 = time.perf_counter_ns(), time.process_time_ns()
            rows, admitted, err = self.step(frame)
            cpu_ns += time.process_time_ns() - c0
            wall_ns += time.perf_counter_ns() - w0
            if err is not None or rows is None or admitted is None:
                raise AssertionError(f"collector failure at frame={frame}: {err}")
            # Conversion is intentionally after measured native-Lua collector.
            output.append({
                "full": [native_record(rows[i]) for i in range(1, len(rows) + 1)],
                "admitted": [native_record(admitted[i])
                             for i in range(1, len(admitted) + 1)],
            })
        after = {key: int(self.counters[key]) for key in before}
        return {
            "wallMs": wall_ns / 1e6, "cpuMs": cpu_ns / 1e6,
            "gcDeltaKiB": float(self.gc("count")) - gc_before,
            "counters": {key: after[key] - before[key] for key in before},
            "rows": output,
        }


def validate_typed_rows(rows: list[dict], case: Case) -> None:
    positions_by_uuid = {}
    expired_trucks = admitted_trucks = seen_checks = 0
    for frame_index, frame in enumerate(rows):
        for row in frame["full"]:
            march_number = int(row["uuid"].split("-")[-1])
            if not case.no_seen and march_number % case.seen_mod != 0:
                raise AssertionError("Already-seen moving march was recaptured")
            seen_checks += 1
            positions_by_uuid.setdefault(row["uuid"], set()).add(row["positionIndex"])
            missing = TYPED_REQUIRED - set(row)
            if missing:
                raise AssertionError(
                    f"{case.name}: frame {frame_index} missing typed fields: {missing}")
            extra = set(row) - TYPED_REQUIRED - OPTIONAL_TYPED
            if extra:
                raise AssertionError(f"Unexpected typed keys: {sorted(extra)}")
            if row["trainType"] == 1:
                if not row["truckMetadataKnown"]:
                    raise AssertionError("Truck TrainData typed metadata missing")
                for key in ("truckCurrentGoodsRaw", "truckBaseGoodsCur",
                            "truckExtraGoodsCur", "truckMaxLootCount"):
                    if key not in row:
                        raise AssertionError(f"Truck lost typed {key}")
                # Production uses and/or selection that collapses false vipOn
                # to nil; demand all true-valued VIP fixture fields instead.
                train_number = int(row["trainUuid"].split("-")[-1])
                if train_number % 2 == 0 and row.get("truckVipOn") is not True:
                    raise AssertionError("Positive VIP metadata lost")
                if len(row["truckCurrentGoodsRaw"]) != 2:
                    raise AssertionError("Truck reward entries dropped")
            elif row["trainType"] == 2:
                for key in ("trainDataJson", "currentGoods", "maxLootCount"):
                    if key not in row:
                        raise AssertionError(f"Railway lost typed {key}")
            else:
                raise AssertionError(f"Unexpected train type {row['trainType']}")
        # Both expiry paths are evaluated from the genuine helper; no expired
        # future rows can leak into admitted truck results.
        for row in frame["admitted"]:
            if row["trainType"] == 1 and (
                row["endTime"] <= 1780000000000 or
                row["arriveTs"] <= 1780000000000
            ):
                raise AssertionError("Expired Truck escaped native admission")
            if row["trainType"] == 1:
                admitted_trucks += 1
        captured = {row["uuid"] for row in frame["admitted"]}
        for row in frame["full"]:
            if row["trainType"] != 1:
                if row["uuid"] not in captured:
                    raise AssertionError("Railway wrongly filtered by Truck expiry gate")
            elif (row["endTime"] > 1780000000000 and
                  row["arriveTs"] > 1780000000000) != (row["uuid"] in captured):
                raise AssertionError("Expired or unexpired Truck admission mismatch")
            if row["trainType"] == 1 and row["uuid"] not in captured:
                expired_trucks += 1
    if seen_checks == 0 or not any(len(positions) > 1
                                    for positions in positions_by_uuid.values()):
        raise AssertionError("Fixture failed to exercise new moving marches")
    if admitted_trucks == 0 or expired_trucks == 0:
        raise AssertionError(
            f"{case.name}: fixture needs admitted and expired Trucks: "
            f"admitted={admitted_trucks} expired={expired_trucks}")


def summarize(samples):
    def fmt(key):
        values = [sample[key] for sample in samples]
        return {"median": round(statistics.median(values), 4),
                "min": round(min(values), 4),
                "max": round(max(values), 4),
                "samples": [round(value, 4) for value in values]}
    return {
        "wallMs": fmt("wallMs"),
        "cpuMs": fmt("cpuMs"),
        "gcDeltaKiB": fmt("gcDeltaKiB"),
        "counters": samples[0]["counters"],
    }


def benchmark() -> dict:
    old_source = pinned_old()
    current_source = SOURCE.read_text(encoding="utf-8")
    initial_hash = sha256(current_source)
    old_collector_hash = sha256(extract(old_source, START, END))
    new_collector_hash = sha256(extract(current_source, START, END))
    if old_collector_hash == new_collector_hash:
        raise AssertionError("Pinned collector and current collector are identical")
    output = {
        "kind": "inert-actual-lua5.3-collector-benchmark",
        "pinned": PIN,
        "oldCollectorSha256": old_collector_hash,
        "newCollectorSha256": new_collector_hash,
        "framesPerSample": FRAMES, "runsPerVersion": RUNS,
        "cases": [],
    }
    for case in CASES:
        owners = {
            "old": ActualCollector(old_source, case),
            "new": ActualCollector(current_source, case),
        }
        runs = {"old": [], "new": []}
        matched_sha = None
        count = None
        for trial in range(RUNS):
            # Alternate order per repetition; each version runs an identical
            # isolated deterministic native dictionary starting at frame zero.
            # Each version's frame sequence uses its own world instance, so
            # order cannot introduce cross-runtime cache mutation.
            order = ("old", "new") if trial % 2 == 0 else ("new", "old")
            frames = {}
            for variant in order:
                sample = owners[variant].run()
                validate_typed_rows(sample["rows"], case)
                typed_digest = sha256(json.dumps(
                    sample["rows"], ensure_ascii=False, sort_keys=True))
                frames[variant] = typed_digest
                if count is None:
                    count = {
                        "firstFrameFull": len(sample["rows"][0]["full"]),
                        "firstFrameAdmitted": len(sample["rows"][0]["admitted"]),
                        "lastFrameFull": len(sample["rows"][-1]["full"]),
                        "lastFrameAdmitted": len(sample["rows"][-1]["admitted"]),
                    }
                runs[variant].append({key: val for key, val in sample.items()
                                      if key != "rows"})
            if frames["old"] != frames["new"]:
                raise AssertionError(
                    f"{case.name} run {trial}: complete typed-row digest differs "
                    f"old={frames['old']} new={frames['new']}")
            if matched_sha is not None and frames["old"] != matched_sha:
                raise AssertionError(f"{case.name}: same-version output drift in run {trial}")
            matched_sha = frames["old"]
        old, new = summarize(runs["old"]), summarize(runs["new"])
        old_wall, new_wall = old["wallMs"]["median"], new["wallMs"]["median"]
        output["cases"].append({
            "scenario": case.name,
            "dictionarySize": case.marches, "captureType": case.capture,
            "hasAlreadySeen": not case.no_seen,
            "typedRowsSha256": matched_sha, "rowCounts": count,
            "old": old, "new": new,
            "medianWallSpeedup": round(old_wall / new_wall, 4) if new_wall else None,
            "medianWallSavedPercent": round(100 * (1 - new_wall / old_wall), 3)
            if old_wall else None,
        })
    if sha256(SOURCE.read_text(encoding="utf-8")) != initial_hash:
        raise AssertionError("Current production source changed DURING benchmark; rerun snapshot")
    output["sourceUnchanged"] = True
    return output


if __name__ == "__main__":
    print(json.dumps(benchmark(), ensure_ascii=False, indent=2))
