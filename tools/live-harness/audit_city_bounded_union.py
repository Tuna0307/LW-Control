"""Read-only real source inventory accounting, never a worldwide census.

Only the immutable, previously accepted bounded LOD0 native receipts are
compared with genuine persisted Map stores. Print aggregate geometry/counts,
not player UIDs, names, profile tokens or private game rows.
"""
from __future__ import annotations

from collections import Counter
import json
from pathlib import Path

from audit_city_source_coverage import ROOT, rows_for
from audit_city_records import SOURCES

RECEIPTS = (
    "artifacts/map-013-live/native-causal-20261013-223148/native-first-region.json",
    "artifacts/map-013-live/native-causal-20261013-230739/native-first-region.json",
    "artifacts/map-013-live/native-causal-20261013-234053/native-first-region.json",
    "artifacts/map-013-live/native-causal-20261013-234544/native-first-region.json",
)


def examine(doc: dict, name: str, persisted: dict):
    footprint = doc.get("requestedIndices")
    if not isinstance(footprint, list) or not footprint or len(footprint) != len(set(footprint)):
        raise ValueError("missing or duplicate native footprint for " + name)
    if any(not isinstance(v, int) or v < 0 or v >= 10000 for v in footprint):
        raise ValueError("out-of-grid native index " + name)
    if (doc.get("state"), doc.get("map013NativeReplyState"), doc.get("serverLod"),
            doc.get("blockSize"), doc.get("blockCount")) != ("proven", "matched", 0, 10, 100):
        raise ValueError("not a fresh exact-L0 native source region " + name)
    if doc.get("cameraGeometryConfirmed") is not True or doc.get("cameraTileStable") is not True:
        raise ValueError("camera not exact-restored " + name)
    by_owner = {}
    for row in doc.get("point_records") or []:
        if row.get("kind") != "player_base":
            continue
        uid = str(row.get("ownerUid") or "")
        x, y = row.get("x"), row.get("y")
        idx = row.get("pointId")
        if (not uid or uid == "0" or not isinstance(x, (int, float)) or
                not isinstance(y, (int, float)) or not (0 <= x < 1000 and 0 <= y < 1000) or
                not isinstance(idx, int) or idx <= 0 or row.get("serverId") != 2212 or
                row.get("worldId") != 0 or int(y)//10*100+int(x)//10 not in footprint):
            raise ValueError("identity, geometry, or world invalid " + name)
        if uid in by_owner:
            raise ValueError("duplicate native owner " + name)
        by_owner[uid] = (int(x), int(y), idx)
    diag = doc.get("cityCompleteness") or {}
    counters = ("missingPointIndex", "invalidPosition", "missingServer", "missingOwnerUid",
                "missingOwnerName", "duplicateRetainedPointIndices", "missingNativeCurrentPointIndices")
    # A fresh independently measured region may legitimately have zero City
    # rows while still containing other real kinds. Zero is not an omitted
    # source row when both current and retained counters explicitly agree.
    if (any(diag.get(k) != 0 for k in counters) or
            diag.get("accepted") != len(by_owner) or
            diag.get("nativeCurrentCityCount") != doc.get("nativePointInfoCityCount")):
        raise ValueError("City source-completeness assertion failed " + name)
    overlap = by_owner.keys() & persisted.keys()
    differing = [k for k in overlap if by_owner[k] != persisted[k]]
    return set(footprint), by_owner, {
        "source": name, "nativeAOIs": len(footprint),
        "nativeCurrentSourceCityCount": diag.get("nativeCurrentCityCount"),
        "acceptedNativeCityOwners": len(by_owner),
        "matchedPreviouslySavedOwners": len(overlap),
        "sameOwnerIndexOrCoordinateChanged": len(differing),
        "sourceOwnerAbsentFromEarlierSaved": len(by_owner.keys()-persisted.keys()),
        "inWorldGrid": True,"rejectionCounters":{k:diag[k] for k in counters},
        "limitation": "time-separated regional source vs persisted census; absence/presence not proof of current worldwide recall",
    }


def main():
    saved = rows_for(SOURCES[2])
    union_aoi, union_uid = set(), {}
    results = []
    for path in RECEIPTS:
        geometry, owners, result = examine(json.loads((ROOT/path).read_text(encoding="utf-8")), path, saved)
        union_aoi.update(geometry)
        for uid, v in owners.items():
            if uid in union_uid and union_uid[uid] != v:
                raise ValueError("same owner from overlapping bounded observations has conflicting identity/position")
            union_uid[uid] = v
        results.append(result)
    print(json.dumps({
        "qualifiedRegionCount":len(results),
        "qualifiedAOIUnion":len(union_aoi),
        "worldLOD0AOIs":10000,
        "directlyReobservedWorldAreaPercent":round(100*len(union_aoi)/10000,3),
        "uniqueAcceptedNativeOwners":len(union_uid),
        "uniquePreviouslySavedOwnersInQualifiedRegions":len(union_uid.keys() & saved.keys()),
        "unprovenCurrentWorldAreaPercent":round(100*(10000-len(union_aoi))/10000,3),
        "reports":results,
        "notAWorldRecallPercentage":True,
        "noHistoricalPopulationQuota":True,
    },indent=2))


if __name__=="__main__":
    main()
