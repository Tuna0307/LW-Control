"""Task-local passive sampler of the *actual* Lua native response file.

It does not issue game commands, select records, arm jobs, or touch the scanner.
Writes bounded source-owned snapshots per scan run for independent field
comparison after publication; copies only genuine 'proven' responses with exact
request identity and the native response flags. All outputs must stay ignored.
"""
import argparse
import collections
import json
import pathlib
import time


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True, type=pathlib.Path)
    parser.add_argument("--out-dir", required=True, type=pathlib.Path)
    parser.add_argument("--stop-file", required=True, type=pathlib.Path)
    parser.add_argument("--run", help="Exact run ID from live SQLite status; required when native envelope lacks scanRunId")
    args = parser.parse_args()
    args.out_dir.mkdir(parents=True, exist_ok=True)
    seen_ids = set()
    counts = collections.Counter()
    saved = collections.Counter()
    covered_kinds = collections.defaultdict(set)
    while not args.stop_file.exists():
        try:
            doc = json.loads(args.source.read_text(encoding="utf-8"))
            rid = doc.get("requestId")
            run = doc.get("scanRunId") or args.run
            if not run or not rid or rid in seen_ids or doc.get("state") != "proven":
                time.sleep(.13)
                continue
            seen_ids.add(rid)
            counts[run] += 1
            kinds = set()
            for row in doc.get("point_records", []):
                k = {"resource_point":"resource", "player_base":"city",
                     "dispatch_task":"dispatch", "treasure":"treasure"}.get(row.get("kind"))
                if k: kinds.add(k)
            if doc.get("monster_march_records"): kinds.add("monster")
            if doc.get("train_march_records"): kinds.add("truck_or_railway")
            if not kinds:
                time.sleep(.13)
                continue
            new = kinds - covered_kinds[run]
            # Preserve first native positive of every source kind, plus a few
            # spaced samples of moving populations for timestamp comparisons.
            if saved[run] < 20 and (new or counts[run] in (1, 4, 8, 20, 35, 70, 110, 170, 225, 280, 335)):
                record = {k: doc.get(k) for k in (
                    "schemaVersion", "requestId", "scanRunId", "requestMode",
                    "state", "profileId", "launchSessionId", "gamePid",
                    "capturedAt", "cameraTileStable", "responseFlagsTransitioned",
                    "positionRestoredAfterResponse", "positionRestoredBeforeResponse",
                    "deferRestoreUntilResponse", "requestMethod",
                    "requestedIndices", "point_records", "monster_march_records",
                    "train_march_records", "coverageAspect", "targetTileX", "targetTileY")}
                record["samplerAssociatedRunId"] = run
                record["samplerUsedNativeScanRunId"] = bool(doc.get("scanRunId"))
                file = args.out_dir / ("native-samples-" + run + ".jsonl")
                with file.open("a", encoding="utf-8") as out:
                    out.write(json.dumps(record, separators=(",", ":"), ensure_ascii=False) + "\n")
                covered_kinds[run].update(kinds)
                saved[run] += 1
        except (OSError, ValueError, json.JSONDecodeError):
            pass  # producer replaces the file between ticks; never alter it
        time.sleep(.13)
    print(json.dumps({"processedNativeResponses": dict(counts), "savedNativeSamples": dict(saved),
                      "positiveSourceKinds": {k: sorted(v) for k,v in covered_kinds.items()}}))


if __name__ == "__main__":
    main()
