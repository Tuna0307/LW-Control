"""Export a compact, reproducible qualification receipt from private native runs.

Never copies native personal account records or raw client packages to docs.
Every excluded run stays visible with a concrete failed qualification gate.
"""
import argparse
import json
import pathlib


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--combined", required=True, type=pathlib.Path)
    p.add_argument("--out", required=True, type=pathlib.Path)
    p.add_argument("--auto-cycle", action="append", type=pathlib.Path, default=[])
    p.add_argument("--auto-watch", action="append", type=pathlib.Path, default=[])
    args = p.parse_args()
    if len(args.auto_cycle) != len(args.auto_watch):
        raise ValueError("Each separate Auto cycle requires its own passive target watcher")
    data = json.loads(args.combined.read_text(encoding="utf-8"))
    auto_cycles=[]
    for cycle_path, watch in zip(args.auto_cycle,args.auto_watch):
        receipt=json.loads(cycle_path.read_text(encoding="utf-8"))
        cycle=receipt["cycle"]
        if not (receipt["completeMultiServerPublication"] and cycle["outcome"]=="completed" and
                cycle["originalServerId"]==receipt["returnResult"]["currentServerId"] and
                receipt["returnResult"]["state"]=="proven" and
                not receipt["auto"]["enabled"] and not receipt["auto"]["running"] and
                not any(receipt["actionJobs"].values()) and receipt["stagedRows"]==0):
            raise ValueError("Not a complete owned returned Auto cycle: " + str(cycle_path))
        scans=[]
        for run in receipt["runs"]:
            identifier=run["runId"]
            metrics=json.loads((watch/("metrics-"+identifier+".json")).read_text(encoding="utf-8"))
            stage=json.loads((watch/("stage-"+identifier+".json")).read_text(encoding="utf-8"))
            audit=json.loads((watch/("audit-"+str(run["serverId"])+".json")).read_text(encoding="utf-8"))
            if (metrics["identity"]["RunId"]!=identifier or stage["runId"]!=identifier or
                    metrics["identity"]["ServerId"]!=run["serverId"] or stage["serverId"]!=run["serverId"] or
                    audit["runId"]!=identifier or audit.get("discrepancies") or
                    run["completedBlocks"]!=2500 or run["failedBlocks"]!=0 or
                    metrics["uniqueCoveredAois"]!=10000 or metrics["attributableLuaProbeAttempts"]!=340):
                raise ValueError("Incomplete Auto target coverage/timing/field proof: " + identifier)
            scans.append({
                "runId":identifier,"server":run["serverId"],"selected":run["selectedTypes"],
                "mode":metrics["identity"]["ScanMode"],
                "wholeSeconds":round(run["elapsedMilliseconds"]/1000,3),
                "sourceAcquisitionSeconds":round(metrics["acquisitionMilliseconds"]/1000,3),
                "checkpointSeconds":round(stage["checkpointMilliseconds"]/1000,3),
                "publicationSeconds":round(stage["publicationMilliseconds"]/1000,3),
                "oldestSourceAgeAtPreparationSeconds":round(metrics["oldestCapturedAgeMilliseconds"]/1000,3),
                "nativeFieldComparisons":sum(n for k,n in audit["checks"].items() if not k.endswith(":sampled")),
                "positiveNativeKinds":sorted(k.removesuffix(":sampled") for k,n in audit["checks"].items()
                                              if k.endswith(":sampled") and n>0),
                "nativeFieldDiscrepancies":0,
                "requiredAois":10000,"probeAttempts":340,"nativeRetries":0,
                "peakCameraConcurrency":metrics["actualPeakConcurrentCameraProbes"],
            })
        total=cycle["completedAt"]-cycle["startedAt"]
        overhead=total-sum(run["elapsedMilliseconds"] for run in receipt["runs"])
        if overhead < 0:
            raise ValueError("Auto wall time shorter than target scan durations")
        auto_cycles.append({"label":receipt["label"],"serverOrder":cycle["targetServerIds"],
                            "originalServer":cycle["originalServerId"],
                            "returnedServer":receipt["returnResult"]["currentServerId"],
                            "nativeReturnMethod":receipt["returnResult"]["method"],
                            "wholeCycleSeconds":round(total/1000,3),
                            "combinedSwitchAndReturnAndOrchestrationSeconds":round(overhead/1000,3),
                            "separateSwitchAndReturnMilliseconds":None,
                            "targetRuns":scans,
                            "autoAfterCycle":False,"armedActionJobsAfterCycle":0})
    workloads = {}
    for selection, modes in data["comparisons"].items():
        workloads[selection] = {
            mode: {
                "qualifiedRuns": metrics["runs"],
                "wholeSeconds":metrics["wholeSeconds"],
                "acquisitionSeconds":metrics["acquisitionSeconds"],
                "hydrationSeconds":metrics["hydrationSeconds"],
                "checkpointSeconds":metrics["stageCheckpointSeconds"],
                "publicationSeconds":metrics["stagePublicationSeconds"],
                "oldestRecordAgeAtPreparationSeconds":metrics["oldestAgeAtPreparationSeconds"],
                "positiveNativeKinds":metrics["nativePositiveKinds"],
                "nativeFieldWitnesses":metrics["fieldWitnesses"],
                "cpuMemorySamples":metrics["cpuMemorySamples"],
            }
            for mode, metrics in modes.items()
        }
    sample_index = [
        {
            "runId":sample["runId"],
            "selection":sample["selected"],
            "server":sample["server"],
            "mode":sample["mode"],
            "wholeSeconds":round(sample["wholeMs"]/1000, 3),
            "aois":10000,
            "logicalBlocks":2500,
            "luaProbeRequests":sample["attributableLuaProbeAttempts"],
            "peakCameraProbes":sample["nativeCameraPeakInflight"],
            "fieldWitnessKinds":sample["nativeWitnessedPositiveKinds"],
            "oldestAtPreparationSeconds":round(sample["oldestCapturedAgeMs"]/1000, 3),
            "stage":sample.get("engineMeasured"),
            "pressure":sample.get("cpuMemoryObserved"),
        }
        for sample in data["samples"]
    ]
    proof = {
        "schemaVersion":1,
        "task":"LWB317-MAP-SCAN-METHODS-008",
        "client":"official current Last War v24, owned isolated source-built Desktop/native providers",
        "sourceCheckpoint":"5e44531bf0283314da90cf3efa5757bbbac08be1",
        "candidateMethod":"fresh shared LOD0 AOI union with owned deferred native camera response/restore, selected type-specific enrichment",
        "methodDecision":"fastest accuracy-qualified supported acquisition among tested credible variants is the shared deferred native AOI path; Normal and Fast do not show repeatable material speed separation",
        "rawNetworkPacketCount":None,
        "requestedNormalAdmissionLimit":8,
        "requestedFastAdmissionLimit":20,
        "measuredCameraInflight":1,
        "nativeSource":"WorldPointManager.UpdateViewRequest(true)+deferred-restore-after-response",
        "fullWorldRequiredAois":10000,
        "fullWorldLogicalBlocks":2500,
        "qualifiedRunCount":data["runCount"],
        "excludedFromStrictAccuracySpeedQualification":data["unqualifiedExcluded"],
        "workloadComparisons":workloads,
        "qualifiedRunIndex":sample_index,
        "correctedMultiServerAutoCycles":auto_cycles,
        "historicalNegativesPreserved":[
            "docs/proof/MAP-SCAN-MODES-LEAD-2026-10-11.json",
            "docs/proof/MAP-008-DEFERRED-SPATIAL-NATIVE-2026-10-11.json",
            "docs/proof/MAP-007-LEAD-LIFETIME-2026-10-11.json"
        ],
        "proofLimits":[
            "Selected kind positive field witnesses are sampled independently and do not constitute a complete-world census",
            "Only a finite number of v24 runs were measured; n=1 modes have no repeatability range",
            "Natural movement, spawning and expiry can vary populations between runs",
            "Ghost and full seasonal Treasure positives are owner-deferred",
            "Rare Railway positive rows without independent native record capture are excluded from strict speed qualification",
            "One isolated 4-column native footprint with no spatial records cannot justify a complete faster 4-column method",
            "Camera sweeps remain serial and actual network packet counts were not measured"
        ],
        "privateReproductionSources":[
            "artifacts/map-006/trial-scan008qual_20261011",
            "artifacts/map-006/trial-scan008fix_20261011",
            "artifacts/map-006/trial-scan008stage_20261011"
        ]
    }
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(proof, indent=2, ensure_ascii=False)+"\n", encoding="utf-8")
    print(json.dumps({"qualified":len(sample_index),"workloads":len(workloads),
                      "excluded":len(data["unqualifiedExcluded"]),"autoCycles":len(auto_cycles)}))


if __name__=="__main__":
    main()
