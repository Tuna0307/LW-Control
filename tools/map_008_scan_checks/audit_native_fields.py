"""Independent source-field witness for a completed passive Map scan.

Inputs are copied game-owned native diagnostic JSON and read-only published DB.
Only bounded aggregate evidence is saved; this never treats a sampled region as
a full-world census or explains away a changing moving/expiring target.
"""
import argparse
import collections
import datetime as dt
import json
import pathlib
import sqlite3


def get(path):
    return json.loads(pathlib.Path(path).read_text(encoding="utf-8"))


def witness_kind(row):
    return {"player_base": "city", "resource_point": "resource",
            "dispatch_task": "dispatch"}.get(row.get("kind"))


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--db", required=True, type=pathlib.Path)
    p.add_argument("--run", required=True)
    p.add_argument("--server", required=True, type=int)
    p.add_argument("--native", action="append", required=True, type=pathlib.Path)
    p.add_argument("--resource-details", type=pathlib.Path)
    p.add_argument("--require-deferred", action="store_true", help="Require fresh post-response camera restoration in every source snapshot")
    p.add_argument("--out", required=True, type=pathlib.Path)
    a = p.parse_args()
    c = sqlite3.connect(f"file:{a.db.resolve().as_posix()}?mode=ro", uri=True)
    c.row_factory = sqlite3.Row
    run = c.execute("select * from scan_runs where id=?", (a.run,)).fetchone()
    if not run or run["server_id"] != a.server or run["status"] != "completed":
        raise ValueError("No completed owned scan on target server")
    selected = set(json.loads(run["selected_types"]))
    stored = {}
    for record in c.execute("select * from map_records where server_id=?", (a.server,)):
        stored[(record["kind"], record["record_key"])] = (record, json.loads(record["data_json"]))

    counts = collections.Counter()
    discrepancies = collections.Counter()
    examples = []
    seen = set()
    dates = []
    owned_session = None
    def compare(kind, key, field, native, published):
        counts[kind + ":" + field] += 1
        if native != published:
            discrepancies[kind + ":" + field] += 1
            if len(examples) < 30:
                examples.append({"kind": kind, "field": field, "native": native,
                                 "published": published, "keyRedacted": True})

    native_documents = []
    for native_path in a.native:
        if native_path.suffix == ".jsonl":
            native_documents.extend(json.loads(line) for line in native_path.read_text(encoding="utf-8").splitlines() if line.strip())
        else:
            native_documents.append(get(native_path))
    for document in native_documents:
        if document.get("state") != "proven" or not document.get("cameraTileStable") or not document.get("responseFlagsTransitioned"):
            raise ValueError("Native response source/flags unproven")
        if document.get("requestMode") != "coverage" or not document.get("requestId") or not document.get("launchSessionId"):
            raise ValueError("Native response correlation absent")
        if a.require_deferred and (
                document.get("positionRestoredBeforeResponse") is not False or
                document.get("positionRestoredAfterResponse") is not True or
                document.get("deferRestoreUntilResponse", True) is not True or
                document.get("requestMethod", "WorldPointManager.UpdateViewRequest(true)+deferred-restore-after-response") !=
                "WorldPointManager.UpdateViewRequest(true)+deferred-restore-after-response"):
            raise ValueError("Native source did not preserve the fresh deferred spatial response contract")
        if document.get("samplerAssociatedRunId") not in (None, a.run):
            raise ValueError("Sampler associated this native result with another scan run")
        owner = (document.get("profileId"), document.get("launchSessionId"), document.get("gamePid"))
        if not owner[0] or not owner[1] or not isinstance(owner[2], int) or owner[2] < 1:
            raise ValueError("Native process, profile or session identity absent")
        if owned_session is None:
            owned_session = owner
        elif owner != owned_session:
            raise ValueError("Native witness responses mixed game ownership")
        if document.get("scanRunId") is not None and document["scanRunId"] != a.run:
            raise ValueError("Native snapshot belonged to another scan run")
        captured = int(dt.datetime.fromisoformat(document["capturedAt"].replace("Z", "+00:00")).timestamp()*1000)
        if not run["created_at"] - 120000 <= captured <= run["updated_at"] + 120000:
            raise ValueError("Native snapshot outside scan time bounds")
        dates.append(captured)
        rows = list(document.get("point_records", [])) + list(document.get("monster_march_records", [])) + list(document.get("train_march_records", []))
        for source in rows:
            kind = witness_kind(source)
            if kind is None and source.get("kind") == "monster":
                kind = "monster" if not source.get("monsterProtectionEligible") else "zombie_boss"
            if kind is None and (source.get("trainType") in (1, 2)):
                kind = "truck" if source["trainType"] == 1 else "railway"
            if kind not in selected or source.get("serverId") != a.server:
                continue
            key = str(source.get("uuid") if kind in ("monster", "zombie_boss", "truck", "railway") else source.get("pointId"))
            if (kind, key) in seen:
                continue
            seen.add((kind, key))
            found = stored.get((kind, key))
            counts[kind + ":sampled"] += 1
            if not found:
                discrepancies[kind + ":missing"] += 1
                continue
            record, data = found
            compare(kind, key, "server", a.server, record["server_id"])
            # Required native identities/contract fields may not silently vanish
            # from publication. The generic optional comparisons below continue
            # to tolerate legitimately absent game fields.
            mandatory = {
                "city": {"x", "y", "pointId", "serverId", "worldId", "ownerUid", "level"},
                "resource": {"x", "y", "pointId", "serverId", "worldId", "resourceConfigId", "resourceMaxAmount", "gatherOccupied", "isBlackTile"},
                "monster": {"x", "y", "uuid", "serverId", "worldId", "monsterId", "configId", "monsterLevel", "monsterProtectionKnown", "monsterProtectionActive"},
                "truck": {"x", "y", "uuid", "arriveTs", "robTimes", "trainType", "ownerUid"},
                "railway": {"x", "y", "uuid", "arriveTs", "robTimes", "trainType"},
                "dispatch": {"x", "y", "pointId", "serverId", "worldId", "completionTime", "expiredTime", "stealListCount", "stealMaxTimes"},
            }.get(kind, set())
            generic_fields = ("x", "y", "worldId", "serverId", "uuid", "cfgId", "quality",
                              "completionTime", "expiredTime", "protectTimeMinutes", "stealListCount",
                              "stealMaxTimes", "rewarded", "monsterId", "configId", "monsterType",
                              "monsterLevel", "monsterProtectionEligible", "monsterProtectionKnown",
                              "monsterProtectionActive", "monsterProtectionEndTime", "trainType",
                              "trainQuality", "robTimes", "arriveTs", "protectTime", "ownerUid",
                              "resourceConfigId", "resourceMaxAmount", "gatherOccupied", "isBlackTile")
            for field in generic_fields:
                if field in source and (field in data or field in mandatory):
                    compare(kind, key, field, source[field], data.get(field))
            for field in mandatory - set(generic_fields):
                if field in source:
                    compare(kind, key, field, source[field], data.get(field))
            if kind in ("truck", "railway") and source.get("trainQuality") is not None:
                compare(kind, key, "normalizedQuality", source["trainQuality"], record["quality"])
            level = source.get("monsterLevel") if kind == "monster" else source.get("level")
            if level is not None:
                compare(kind, key, "normalizedLevel", level, record["level"])
            if kind == "dispatch" and source.get("completionTime") and source.get("protectTimeMinutes") is not None:
                compare(kind, key, "derivedPlunderAt", source["completionTime"] + source["protectTimeMinutes"] * 60000, data.get("plunderAt"))
                compare(kind, key, "derivedStolenCount", source.get("stealListCount"), data.get("stolenCount"))
                compare(kind, key, "derivedMaxStealCount", source.get("stealMaxTimes"), data.get("maxStealCount"))
            if kind == "monster" and source.get("monsterProtectionKnown") and source.get("monsterProtectionActive"):
                # This value can be modified by later detail refresh: disclose
                # mismatch rather than forcing equality for an expired object.
                compare(kind, key, "shieldEndTime", source.get("monsterProtectionEndTime"), record["shield_end_time"])
            if kind == "truck":
                if source.get("truckMetadataKnown") and source.get("truckMaxLootCount") is not None:
                    compare(kind, key, "nativeMaxLootCount", source["truckMaxLootCount"], data.get("maxLootCount"))
                    effective_cap = 1 if source.get("trainQuality") == 10 else source["truckMaxLootCount"]
                    compare(kind, key, "derivedRemainingLootCount", max(0, effective_cap - (source.get("robTimes") or 0)), data.get("remainingLootCount"))
                if source.get("truckCurrentGoodsRaw"):
                    def aggregate(values, type_key, id_key, count_key):
                        total = collections.Counter()
                        for item in values:
                            if item.get(type_key) is None or item.get(id_key) is None or item.get(count_key) is None:
                                continue
                            total[(int(item[type_key]), int(item[id_key]))] += int(item[count_key])
                        return dict(sorted((f"{k[0]}:{k[1]}", v) for k, v in total.items()))
                    native_goods = aggregate(source["truckCurrentGoodsRaw"], "type", "itemId", "count")
                    published_goods = aggregate(data.get("currentGoods") or [], "rewardType", "itemId", "count")
                    compare(kind, key, "aggregatedNativeGoods", native_goods, published_goods)
                if source.get("arriveTs") is not None:
                    compare(kind, key, "nativeArrivalExpiry", source["arriveTs"], data.get("arriveTs"))

    if a.resource_details:
        detail = get(a.resource_details)
        if detail.get("state") != "completed" or detail.get("scanRunId") != a.run or detail.get("serverId") != a.server:
            raise ValueError("Resource native detail result belongs to another run")
        if detail.get("readyCount") != len(detail.get("details", [])):
            raise ValueError("Native resource readiness counter disagrees with response")
        for source in detail["details"]:
            key = source["recordKey"]
            found = stored.get(("resource", key))
            counts["resource:enrichmentSampled"] += 1
            if not found:
                discrepancies["resource:enrichmentMissing"] += 1
                continue
            _, data = found
            d = source.get("detail") or {}
            if "remainRes" in d:
                compare("resource", key, "remainingAmount", d["remainRes"], data.get("resourceRemainingAmount"))
            if "initRes" in d:
                compare("resource", key, "fullAmount", d["initRes"], data.get("resourceFullAmount"))

    proof = {"kind": "independent_current_client_native_field_values",
             "runId": a.run, "serverId": a.server, "selected": sorted(selected),
             "sourceNativeFiles": len(a.native), "sourceNativeResponses": len(native_documents),
             "capturedRangeMs": [min(dates), max(dates)],
             "sourceNativeScanRunIdPresent": sum(1 for document in native_documents if document.get("scanRunId")),
             "deferredRestorationRequired":a.require_deferred,
             "source": "fresh game-owned native region/list/record snapshots; read-only SQLite publication",
             "checks": dict(sorted(counts.items())), "discrepancies": dict(sorted(discrepancies.items())),
             "examples": examples, "wholeWorldGroundTruth": False,
             "limitations": ["Snapshots sample finite native AOI regions; no independent whole-server census",
                             "Moving or expiring objects may legitimately change between capture and final publication",
                             "Only source fields present in the game response are compared; no fabricated unknown values",
                             "Resource detail list is matched against completed run ID, not inferred from row count"]}
    a.out.parent.mkdir(parents=True, exist_ok=True)
    a.out.write_text(json.dumps(proof, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"checks": len(counts), "fieldComparisons": sum(counts.values()),
                      "sampledKinds": {k: v for k,v in counts.items() if k.endswith(":sampled")},
                      "discrepancyCount": sum(discrepancies.values())}))
    if not any(k.endswith(":sampled") for k in counts):
        raise ValueError("No independently sampled native records were compared")
    if discrepancies:
        raise ValueError("Native field-value mismatch; see source-identified proof")


if __name__ == "__main__":
    main()
