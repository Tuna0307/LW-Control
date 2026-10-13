"""Finite source-identified, accuracy-gated real Normal/Fast Map comparisons.

Reads actual owned scanner run/phase/native-field receipts. Records limitations
per run, and never turns unverified selected positives into speed wins.
"""
import argparse
import collections
import json
import pathlib
import statistics


def get(path):
    return json.loads(path.read_text(encoding="utf-8"))


def stats(values):
    if not values:
        return None
    return {"n": len(values), "median": round(statistics.median(values), 3),
            "min": round(min(values), 3), "max": round(max(values), 3)}


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--trial", required=True, type=pathlib.Path)
    p.add_argument("--owner-receipt", required=True, type=pathlib.Path)
    p.add_argument("--out", required=True, type=pathlib.Path)
    a = p.parse_args()
    if a.out.exists():
        raise FileExistsError("Preserve previous native qualification receipt")
    owner = get(a.owner_receipt)
    if (owner.get("schemaVersion") != 1 or not owner.get("gamePid") or
        not owner.get("hostProcess", {}).get("sha256") or
        not owner.get("desktopAssembly", {}).get("sha256")):
        raise ValueError("Missing independent source-owner receipt")
    runs = []
    for file in sorted(a.trial.glob("scan-*.json")):
        scan = get(file)
        label = scan["label"]
        metrics = get(a.trial / ("metrics-" + label + ".json"))
        stage = get(a.trial / ("stage-" + label + ".json"))
        audit_path = a.trial / ("audit-" + label + "-expanded.json")
        if not audit_path.exists():
            audit_path = a.trial / ("audit-" + label + ".json")
        audit = get(audit_path) if audit_path.exists() else None
        selected = scan["selectedTypes"]
        identity = metrics["identity"]
        run_id = scan["runId"]
        if (scan["state"] != "completed" or scan["completedBlocks"] != 2500 or
            scan["totalBlocks"] != 2500 or scan["failedBlocks"] or scan["stagedRows"] or
            any(scan["actionJobs"].values()) or
            not scan["publishedKeyUniqueness"] or
            identity["RunId"] != run_id or identity["ServerId"] != scan["serverId"] or
            stage["runId"] != run_id or stage["serverId"] != scan["serverId"] or
            metrics["phase"] != "prepared" or metrics["uniqueCoveredAois"] != 10000 or
            metrics["attributableRetries"] != 0 or metrics["actualPeakConcurrentCameraProbes"] != 1 or
            metrics["validatedProbeResponses"] != metrics["attributableLuaProbeAttempts"] or
            metrics["nativePhaseSampleCount"] != metrics["attributableLuaProbeAttempts"]):
            raise ValueError("Incomplete owned world/phase/store evidence: " + label)
        if metrics["attributableLuaProbeAttempts"] != 340:
            raise ValueError("Measured current v24 AOI geometry changed: " + label)
        if (audit and (audit.get("runId") != run_id or audit.get("discrepancies") or
                       not audit.get("ownerReceiptVerified") or
                       audit.get("sourceAssemblySha256", "").lower() != owner["desktopAssembly"]["sha256"].lower())):
            raise ValueError("Native independent field/source audit failed: " + label)
        positive = sorted(scan["positiveSelectedKinds"])
        witnessed = sorted(key.removesuffix(":sampled") for key, count in (audit or {}).get("checks", {}).items()
                         if key.endswith(":sampled") and count > 0)
        missing = sorted(set(positive) - set(witnessed))
        native_total_ms = metrics["summedNativeTotalProbeMilliseconds"]
        home_ms = metrics["summedNativeHomeRestorationMilliseconds"]
        target_ms = metrics["summedNativeTargetResponseMilliseconds"]
        if not (0 < home_ms <= native_total_ms and 0 < target_ms <= native_total_ms and
                abs(home_ms + target_ms - native_total_ms) < 2000):
            raise ValueError("Invalid exact native phase sums: " + label)
        runs.append({"label": label, "runId": run_id, "mode": identity["ScanMode"],
                     "server": scan["serverId"], "selected": selected,
                     "wholeSeconds": round((scan["updatedAt"] - scan["createdAt"]) / 1000, 3),
                     "acquisitionSeconds": round(metrics["acquisitionMilliseconds"] / 1000, 3),
                     "targetNativeSeconds": round(target_ms / 1000, 3),
                     "homeNativeSeconds": round(home_ms / 1000, 3),
                     "fullNativeSeconds": round(native_total_ms / 1000, 3),
                     "homeFractionOfNative": round(home_ms / native_total_ms, 5),
                     "homeFractionOfWhole": round(home_ms / (scan["updatedAt"] - scan["createdAt"]), 5),
                     "probeWaitSeconds": round(metrics["summedProbeWaitMilliseconds"] / 1000, 3),
                     "sourceRecordAgeAtPreparationSeconds": round(metrics["oldestCapturedAgeMilliseconds"] / 1000, 3),
                     "sourceProducedAt": scan["createdAt"],
                     "nativeProbeRequests": 340, "nativeAois": 10000,
                     "publishedKinds": positive,
                     "nativePositiveWitnessKinds": witnessed,
                     "missingPositiveWitnessKinds": missing,
                     "nativeFieldComparisons": sum(count for key,count in (audit or {}).get("checks", {}).items()
                                                  if not key.endswith(":sampled")),
                     "strictSelectedPositiveAccuracyQualified": audit is not None and not missing,
                     "sourceHash": owner["desktopAssembly"]["sha256"]})
    groups = collections.defaultdict(lambda: collections.defaultdict(list))
    for run in runs:
        groups["+".join(sorted(run["selected"]))][run["mode"]].append(run)
    workloads = {}
    for workload, modes in sorted(groups.items()):
        workloads[workload] = {}
        for mode, rows in sorted(modes.items()):
            qualified = [r for r in rows if r["strictSelectedPositiveAccuracyQualified"]]
            workloads[workload][mode] = {
                "observed": stats([r["wholeSeconds"] for r in rows]),
                "strictPositiveAccuracyQualified": stats([r["wholeSeconds"] for r in qualified]),
                "targetNativeSeconds": stats([r["targetNativeSeconds"] for r in rows]),
                "homeNativeSeconds": stats([r["homeNativeSeconds"] for r in rows]),
                "homeNativeFraction": stats([r["homeFractionOfNative"] for r in rows]),
                "excludedForMissingNativePositiveKinds": {r["runId"]: r["missingPositiveWitnessKinds"] for r in rows if r not in qualified},
            }
    for workload, modes in workloads.items():
        if "normal" in modes and "fast" in modes:
            a_fast = modes["fast"]["strictPositiveAccuracyQualified"]
            a_normal = modes["normal"]["strictPositiveAccuracyQualified"]
            ordered = sorted((r for r in runs if "+".join(sorted(r["selected"])) == workload),
                             key=lambda row: row["sourceProducedAt"])
            comparisons = []
            for i in range(0, len(ordered) - 1, 2):
                pair = ordered[i:i + 2]
                if sorted(x["mode"] for x in pair) == ["fast", "normal"]:
                    normal = next(row for row in pair if row["mode"] == "normal")
                    fast = next(row for row in pair if row["mode"] == "fast")
                    comparisons.append({"normal": normal["runId"], "fast": fast["runId"],
                                        "fastMinusNormalSeconds": round(fast["wholeSeconds"] - normal["wholeSeconds"], 3),
                                        "bothStrictQualified": (fast["strictSelectedPositiveAccuracyQualified"] and
                                                                normal["strictSelectedPositiveAccuracyQualified"])})
            modes["alternatingOrder"] = [row["mode"] for row in ordered]
            modes["pairedComparisons"] = comparisons
            modes["observedFastMinusNormalMedianSeconds"] = round(
                modes["fast"]["observed"]["median"] - modes["normal"]["observed"]["median"], 3)
            modes["strictSpeedWinner"] = (
                "unresolved; insufficient fully witnessed repeated runs" if not a_fast or not a_normal or
                a_fast["n"] < 2 or a_normal["n"] < 2 else
                "undetermined; compare distribution ranges and current mutable populations before ranking")
    report = {"kind": "MAP008_current_v24_genuine_phase_qualified_comparisons",
              "source": "actual published Map engine/native LOD0 phase counters and owner-gated native/SQLite audits",
              "sourceAssemblySha256": owner["desktopAssembly"]["sha256"],
              "captureOwner": {"profileId": owner["profileId"], "gamePid": owner["gamePid"]},
              "runCount": len(runs), "runs": runs, "workloads": workloads,
              "limits": ["Source game world populations vary across successive scans and expiry windows",
                         "Native region witness samples check matched source values, not whole-world recall",
                         "Missing positive kind witnesses prohibit strict method winner claims",
                         "Native full-world request/restore work is serial; host requested 8/20 does not establish camera concurrency",
                         "Home-view time is observed required work, not proven avoidable savings",
                         "No actual raw network packet count was measured"]}
    a.out.parent.mkdir(parents=True, exist_ok=True)
    a.out.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"runs": len(runs), "workloads": list(workloads),
                      "strictQualified": sum(run["strictSelectedPositiveAccuracyQualified"] for run in runs)}))


if __name__ == "__main__":
    main()
