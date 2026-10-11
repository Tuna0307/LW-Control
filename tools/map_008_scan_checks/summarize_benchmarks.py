"""Summarize genuine individual/mixed scanner timings without invented speedups.

Consumes source-identified task-private metrics/scan receipts; emits only compact
aggregate statistics. Native AOI Lua commands are not network packet counts.
"""
import argparse
import collections
import csv
import datetime as dt
import json
import pathlib
import statistics


def read(file):
    return json.loads(file.read_text(encoding="utf-8"))


def summary(values):
    return {"n":len(values), "median":round(statistics.median(values),3),
            "min":round(min(values),3), "max":round(max(values),3)} if values else None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--trial", required=True, type=pathlib.Path)
    parser.add_argument("--out", required=True, type=pathlib.Path)
    a = parser.parse_args()
    samples = []
    pressure = []
    path = a.trial / "pressure.csv"
    if path.exists():
        with path.open(encoding="utf-8-sig", newline="") as stream:
            for row in csv.DictReader(stream):
                try:
                    row["epochMs"] = dt.datetime.fromisoformat(row["utcTime"].replace("Z", "+00:00")).timestamp()*1000
                    pressure.append(row)
                except (ValueError, KeyError):
                    continue
    for path in sorted(a.trial.glob("metrics-*.json")):
        m = read(path)
        run_id = m["identity"]["RunId"]
        scan_files = [p for p in a.trial.glob("scan-*.json") if read(p).get("runId") == run_id]
        if len(scan_files) != 1:
            raise ValueError(f"Expected one completed scan audit for {run_id}")
        s = read(scan_files[0])
        if s["state"] != "completed" or s["totalBlocks"] != s["completedBlocks"] or s["failedBlocks"] or s["stagedRows"]:
            raise ValueError(f"Incomplete native run {run_id}")
        if m.get("phase") != "prepared" or m["uniqueCoveredAois"] != 10000 or m["attributableRetries"]:
            raise ValueError(f"AOI coverage/metrics incomplete in {path}")
        field_files = [p for p in a.trial.glob("audit-*.json") if read(p).get("runId") == run_id]
        field_sample = [read(p) for p in field_files]
        if any(item.get("discrepancies") for item in field_sample):
            raise ValueError("Unresolved native field mismatch " + run_id)
        witnessed = {key.removesuffix(":sampled")
                     for item in field_sample for key, count in item.get("checks", {}).items()
                     if key.endswith(":sampled") and count > 0}
        unwitnessed_positive = set(s["positiveSelectedKinds"]) - witnessed
        sample = {"runId":run_id,"label":path.stem.removeprefix("metrics-"),
                  "server":s["serverId"],"mode":m["identity"]["ScanMode"],
                  "selected":sorted(s["selectedTypes"]),
                  "wholeMs":s["updatedAt"]-s["createdAt"],
                  "acquisitionMs":m["acquisitionMilliseconds"],
                  "nativePreparationMs":m["elapsedMilliseconds"],
                  "railwayRefreshMs":m["railwayRefreshMilliseconds"],
                  "truckHydrationMs":m["truckHydrationMilliseconds"],
                  "resourceHydrationMs":m["resourceHydrationMilliseconds"],
                  "monsterHydrationMs":m["monsterHydrationMilliseconds"],
                  "projectionMs":m["projectionMilliseconds"],
                  "afterNativePreparationMs":max(0,s["updatedAt"]-s["createdAt"]-m["elapsedMilliseconds"]),
                  "oldestCapturedAgeMs":m["oldestCapturedAgeMilliseconds"],
                  "attributableLuaProbeAttempts":m["attributableLuaProbeAttempts"],
                  "nativeCameraPeakInflight":m["actualPeakConcurrentCameraProbes"],
                  "resourceDetailRequests":m["resourceDetailNativeRequests"],
                  "resourceDetailReady":m["resourceDetailNativeReady"],
                  "monsterDetailRequests":m["monsterDetailNativeRequests"],
                  "monsterDetailReady":m["monsterDetailNativeReady"],
                  "positiveKinds":s["positiveSelectedKinds"],
                  "nativeWitnessedPositiveKinds":sorted(witnessed),
                  "unwitnessedPositiveKinds":sorted(unwitnessed_positive),
                  "nativeFieldSampleCount":sum(sum(v for k,v in item.get("checks",{}).items() if k.endswith(":sampled")) for item in field_sample),
                  "nativeFieldDiscrepancies":0,
                  "publishedKeyUniqueness":s["publishedKeyUniqueness"],
                  # The old same-tick non-Resource policy can produce plausible
                  # count/coverage with omitted known spatial populations.
                  # Resource selection always used source-deferred restore;
                  # non-Resource requires explicit deferred-source witness.
                  "sourceContractQualified":not unwitnessed_positive and bool(field_sample) and (
                      "resource" in s["selectedTypes"] or
                      any(item.get("deferredRestorationRequired") is True for item in field_sample))}
        stage_files = [p for p in a.trial.glob("stage-*.json") if read(p).get("runId") == run_id]
        if len(stage_files) > 1:
            raise ValueError("Ambiguous engine timing receipt for " + run_id)
        if stage_files:
            stage = read(stage_files[0])
            if stage.get("serverId") != s["serverId"] or stage.get("scanMode") != sample["mode"]:
                raise ValueError("Engine timing belongs to the wrong server or scan mode")
            sample["engineMeasured"] = {
                "sourceCaptureMs":stage["sourceCaptureMilliseconds"],
                "checkpointMs":stage["checkpointMilliseconds"],
                "publicationMs":stage["publicationMilliseconds"],
                "wholeEngineMs":stage["wholeEngineMilliseconds"],
            }
        load = [row for row in pressure if s["createdAt"] <= row["epochMs"] <= s["updatedAt"]]
        if load:
            sample["cpuMemoryObserved"] = {
                "sampleCount":len(load),
                "peakHostWorkingSetMiB":round(max(int(row["hostWorkingSetBytes"]) for row in load)/1048576,2),
                "peakGameWorkingSetMiB":round(max(int(row["gameWorkingSetBytes"]) for row in load)/1048576,2),
                "hostCpuDeltaSeconds":round(max(0,float(load[-1]["hostCpuSeconds"])-float(load[0]["hostCpuSeconds"])),2),
                "gameCpuDeltaSeconds":round(max(0,float(load[-1]["gameCpuSeconds"])-float(load[0]["gameCpuSeconds"])),2),
            }
        samples.append(sample)
    groups = collections.defaultdict(lambda: collections.defaultdict(list))
    for s in samples:
        label = "+".join(s["selected"])
        groups[label][s["mode"]].append(s)
    comparisons = {}
    for group, modes in groups.items():
        comparisons[group] = {}
        for mode, runs in modes.items():
            metrics = {
                name:summary([r[name] for r in runs]) for name in (
                    "wholeMs","acquisitionMs","nativePreparationMs","afterNativePreparationMs",
                    "resourceHydrationMs","truckHydrationMs","railwayRefreshMs","monsterHydrationMs",
                    "projectionMs","oldestCapturedAgeMs","attributableLuaProbeAttempts")}
            for stage_key in ("checkpointMs", "publicationMs", "sourceCaptureMs"):
                stage_values = [r["engineMeasured"][stage_key] for r in runs if r.get("engineMeasured")]
                if stage_values:
                    metrics["engine" + stage_key[0].upper() + stage_key[1:]] = summary(stage_values)
            comparisons[group][mode] = metrics
    proof = {"kind":"genuine_current_client_v24_alternating_scan_comparison",
             "source":"packaged production Map Engine/CurrentClientMapBlockSource, task-private SQLite and native metrics",
             "totalRuns":len(samples), "rawPacketCount":None,"samples":samples,
             "comparisons":comparisons,
             "warnings":["n<3 per method/workload cannot establish strong repeatability or universal optimality",
                         "A game world changes between successive scans; populations can move, expire and spawn",
                         "oldestCapturedAge is age at native preparation, not at the end of a multi-server Auto cycle",
                         "afterNativePreparation includes staging and publication and any engine/SQLite overhead"]}
    a.out.parent.mkdir(parents=True, exist_ok=True)
    a.out.write_text(json.dumps(proof,indent=2) + "\n",encoding="utf-8")
    print(json.dumps({"runs":len(samples),"workloads":list(comparisons)},indent=2))


if __name__ == "__main__":
    main()
