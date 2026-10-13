"""Passive per-region current-client timing sampler, bounded and owner-checked.

The producer overwrites its native result after each region. Samples are a
subset of actual completed, fresh, restored requests, never a complete census.
Only durations, geometry and identity digests enter the output. No game action.
"""
import argparse
import collections
import datetime as dt
import json
import pathlib
import sqlite3
import statistics
import time


def millis(timestamp):
    return dt.datetime.fromisoformat(timestamp.replace("Z", "+00:00")).timestamp() * 1000


def aggregate(rows):
    def series(field):
        values = sorted(row[field] for row in rows)
        return {"median": round(statistics.median(values), 3),
                "min": round(values[0], 3), "max": round(values[-1], 3),
                "p90": round(values[min(len(values) - 1, int(.9 * (len(values) - 1)))], 3),
                "mean": round(statistics.mean(values), 3)}
    return {"sampledRequests": len(rows), "targetMs": series("targetMs"),
            "homeMs": series("homeMs"), "wholeMs": series("wholeMs"),
            "homeFractionOfSampledTotal": round(sum(row["homeMs"] for row in rows) /
                sum(row["wholeMs"] for row in rows), 5),
            "homeTotalMsSampled": round(sum(row["homeMs"] for row in rows), 3),
            "nativeAoisPerResponse": sorted(set(row["aois"] for row in rows)),
            "positiveSamples": {kind: sum(row[kind] > 0 for row in rows)
                for kind in ("points", "monsters", "trains", "railways")}}


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--source", required=True, type=pathlib.Path)
    p.add_argument("--owner-receipt", required=True, type=pathlib.Path)
    p.add_argument("--profile", required=True)
    p.add_argument("--db", required=True, type=pathlib.Path,
                   help="Exact isolated Map SQLite owner for source versions omitting scanRunId")
    p.add_argument("--world-state", required=True, type=pathlib.Path)
    p.add_argument("--stop-file", required=True, type=pathlib.Path)
    p.add_argument("--out", required=True, type=pathlib.Path)
    p.add_argument("--max-per-run", type=int, default=400)
    a = p.parse_args()
    if a.out.exists() or a.stop_file.exists() or not 1 <= a.max_per_run <= 500:
        raise ValueError("Preserve existing sample or stop marker; require bounded sample size")
    owner = json.loads(a.owner_receipt.read_text(encoding="utf-8"))
    expected = (owner["profileId"], owner["sessionId"], owner["gamePid"])
    if a.profile != expected[0] or not owner["hostProcess"].get("sha256") or not owner["desktopAssembly"].get("sha256"):
        raise ValueError("Expected independent owned native host-start receipt")
    entries = collections.defaultdict(list)
    seen = set()
    failures = collections.Counter()
    while not a.stop_file.exists():
        try:
            data = json.loads(a.source.read_text(encoding="utf-8"))
            rid = data.get("requestId")
            if not rid or rid in seen or data.get("state") != "proven" or data.get("requestMode") != "coverage":
                time.sleep(.07)
                continue
            seen.add(rid)
            with sqlite3.connect(f"file:{a.db.resolve().as_posix()}?mode=ro", uri=True) as db:
                current = db.execute("SELECT id,server_id,created_at,updated_at,status FROM scan_runs ORDER BY created_at DESC LIMIT 1").fetchone()
            world = json.loads(a.world_state.read_text(encoding="utf-8"))
            if not current or current[4] != "running":
                time.sleep(.07)
                continue
            run = data.get("scanRunId") or current[0]
            if (data.get("profileId"), data.get("launchSessionId"), data.get("gamePid")) != expected:
                raise RuntimeError("Native game ownership changed during timing capture")
            if (not run or run != current[0] or
                world.get("state") != "proven" or world.get("serverId") != current[1] or
                (world.get("profileId"),world.get("sessionId"),world.get("gamePid")) != expected or
                not data.get("capturedAt") or millis(data["capturedAt"]) < millis(owner["recordedAtUtc"]) or
                not current[2] - 3000 <= millis(data["capturedAt"]) <= current[3] + 3000):
                raise RuntimeError("Native source run/capture identity missing or precedes host start")
            if len(entries[run]) >= a.max_per_run:
                time.sleep(.07)
                continue
            if (data.get("positionRestoredBeforeResponse") is not False or
                data.get("positionRestoredAfterResponse") is not True or
                data.get("deferRestoreUntilResponse") is not True or
                data.get("responseFlagsTransitioned") is not True or
                data.get("cameraTileStable") is not True or
                data.get("requestMethod") != "WorldPointManager.UpdateViewRequest(true)+deferred-restore-after-response"):
                failures[run] += 1
                continue
            def elapsed(field):
                value = data.get(field)
                if type(value) not in (int, float) or not 0 <= value <= 15:
                    raise ValueError("Missing/implausible measured native phase: " + field)
                return value * 1000
            target = elapsed("targetResponseElapsedSeconds")
            home = elapsed("homeRestorationElapsedSeconds")
            whole = elapsed("totalProbeElapsedSeconds")
            # Lua measures from invocation and from native target completion;
            # a few milliseconds of processing may lie between phases.
            if whole + 1 < target + home or whole < target or whole < home:
                raise ValueError("Impossible native phase timing relation")
            trains = data.get("train_march_records") or []
            entries[run].append({"targetMs": target, "homeMs": home, "wholeMs": whole,
                                 "aois": len(data.get("requestedIndices") or []),
                                 "points": len(data.get("point_records") or []),
                                 "monsters": len(data.get("monster_march_records") or []),
                                 "trains": len(trains),
                                 "railways": sum(row.get("trainType") == 2 for row in trains)})
        except (OSError, json.JSONDecodeError, sqlite3.OperationalError):
            pass  # Producer's atomic rewrite can momentarily be unavailable
        time.sleep(.06)
    if not entries:
        raise RuntimeError("No owned and freshly restored instrumented native region replies were observed")
    payload = {"proofType": "bounded_passive_genuine_native_phase_sample",
               "source": "current live source-built bulk-aoi-diagnostic-result.json",
               "ownerReceipt": str(a.owner_receipt), "sourceBuildHash": owner["desktopAssembly"]["sha256"],
               "sampling": "overwritten single-result producer observed by polling; unobserved regions not estimated",
               "measurements": {run: aggregate(rows) for run, rows in entries.items()},
               "invalidRestorationReplies": dict(failures),
               "rawNativeRecordsPersisted": False, "externalActions": 0}
    a.out.parent.mkdir(parents=True, exist_ok=True)
    a.out.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"observed": {run: len(rows) for run, rows in entries.items()},
                      "invalidRestorationReplies": dict(failures)}))


if __name__ == "__main__":
    main()
