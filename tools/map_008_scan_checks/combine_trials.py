"""Combine separately audited native scanner benchmark trials.

Each input is a compact result of summarize_benchmarks.py. No raw game data,
private records or metric estimates are imported into the combined report.
"""
import argparse
import collections
import json
import pathlib
from summarize_benchmarks import summary


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--benchmark", action="append", type=pathlib.Path, required=True)
    p.add_argument("--out", type=pathlib.Path, required=True)
    a = p.parse_args()
    seen = set()
    samples = []
    excluded = []
    for benchmark in a.benchmark:
        source = json.loads(benchmark.read_text(encoding="utf-8"))
        for raw in source["samples"]:
            if raw["runId"] in seen:
                raise ValueError("Duplicate native run " + raw["runId"])
            if raw["attributableLuaProbeAttempts"] != 340 or raw["nativeCameraPeakInflight"] != 1:
                raise ValueError("Unexpected real world footprint/concurrency: " + raw["runId"])
            if not raw["publishedKeyUniqueness"] or raw["nativeFieldDiscrepancies"]:
                raise ValueError("Unqualified publication or native witness discrepancy: " + raw["runId"])
            seen.add(raw["runId"])
            if not raw.get("sourceContractQualified",False):
                excluded.append({"runId":raw["runId"], "workload":"+".join(raw["selected"]),
                                 "reason":"fresh deferred native contract or independent positive-kind field witness missing",
                                 "missingPositiveKinds":raw.get("unwitnessedPositiveKinds",[])})
                continue
            samples.append({**raw, "benchmarkSource": str(benchmark.as_posix())})
    workloads = collections.defaultdict(lambda:collections.defaultdict(list))
    for row in samples:
        workload = "+".join(row["selected"])
        workloads[workload][row["mode"]].append(row)
    comparisons = {}
    for workload, modes in workloads.items():
        comparisons[workload] = {}
        for mode, rows in modes.items():
            comparisons[workload][mode] = {
                "runs":len(rows),
                "wholeSeconds":summary([r["wholeMs"]/1000 for r in rows]),
                "acquisitionSeconds":summary([r["acquisitionMs"]/1000 for r in rows]),
                "hydrationSeconds":summary([sum(r[k] for k in (
                    "railwayRefreshMs","truckHydrationMs","resourceHydrationMs","monsterHydrationMs"))/1000 for r in rows]),
                "afterNativePreparationSeconds":summary([r["afterNativePreparationMs"]/1000 for r in rows]),
                "oldestAgeAtPreparationSeconds":summary([r["oldestCapturedAgeMs"]/1000 for r in rows]),
                "cpuMemorySamples":sum(r.get("cpuMemoryObserved",{}).get("sampleCount",0) for r in rows),
                "nativePositiveKinds":sorted(set(k for row in rows for k in row["positiveKinds"])),
                "fieldWitnesses":sum(r["nativeFieldSampleCount"] for r in rows),
                "stagePublicationSeconds":summary([r["engineMeasured"]["publicationMs"]/1000 for r in rows if "engineMeasured" in r]),
                "stageCheckpointSeconds":summary([r["engineMeasured"]["checkpointMs"]/1000 for r in rows if "engineMeasured" in r]),
            }
    payload = {"kind":"MAP008_v24_source_identified_native_comparisons",
               "evidence":"actual packaged production native provider, audited complete maps and independent source fields",
               "sourceCount":len(a.benchmark),"runCount":len(samples),"unqualifiedExcluded":excluded,
               "comparisons":comparisons,"samples":samples,
               "limitations":["A source-confirmed finite AOI footprint does not establish perfect whole-world object recall",
                              "Only positive native kinds count as live positive witnesses; missing seasonal Railway/Ghost/Treasure remain pending",
                              "Lua AOI commands are not raw attributed network packets",
                              "Mode comparisons use actual run elapsed, never configured 8/20 as measured camera concurrency",
                              "Dynamic Truck, Dispatch and Monster records change between scans"]}
    a.out.parent.mkdir(parents=True,exist_ok=True)
    a.out.write_text(json.dumps(payload,indent=2)+"\n",encoding="utf-8")
    print(json.dumps({"runs":len(samples),"workloads":len(comparisons),
                      "perWorkload":{k:{m:v["wholeSeconds"] for m,v in modes.items()} for k,modes in comparisons.items()}},indent=2))


if __name__ == "__main__":
    main()
