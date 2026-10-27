"""Read-only independent cross-source City geometry, candidate and AOI audit.

Print only aggregate information. Never disclose owner identities from genuine
game captures, and never use the old population as a current quota.
"""
from __future__ import annotations

from collections import Counter, defaultdict
import json
from pathlib import Path
import sqlite3

from audit_city_records import ROOT, SOURCES


def rows_for(relative: str):
    file = ROOT / relative
    with sqlite3.connect(file.as_uri() + "?mode=ro", uri=True) as db:
        data = list(db.execute(
            "SELECT point_index, data_json FROM map_records "
            "WHERE server_id=2212 AND kind='city'"))
    by_owner = {}
    for point_index, text in data:
        d = json.loads(text)
        uid = str(d.get("ownerUid") or "")
        if uid:
            by_owner[uid] = (int(d["x"]), int(d["y"]), point_index)
    return by_owner


def correlate(a, b):
    mx = sum(a) / len(a)
    my = sum(b) / len(b)
    num = sum((x - mx) * (y - my) for x, y in zip(a, b))
    da = sum((x - mx) ** 2 for x in a)
    db = sum((y - my) ** 2 for y in b)
    return round(num / (da * db) ** .5, 4) if da > 0 and db > 0 else None


def print_pair(label, a, b):
    overlap = list(a.keys() & b.keys())
    coords = [(a[k][:2], b[k][:2]) for k in overlap]
    dx = [new[0] - old[0] for old, new in coords]
    dy = [new[1] - old[1] for old, new in coords]
    print(json.dumps({
        "pair": label,
        "sharedUidCount": len(coords),
        "sameCoordinate": sum(old == new for old, new in coords),
        "samePointIndex": sum(a[k][2] is not None and a[k][2] == b[k][2] for k in overlap),
        "changeDXMostFrequent": Counter(dx).most_common(3),
        "changeDYMostFrequent": Counter(dy).most_common(3),
        "oldXToNewXCorrelation": correlate([v[0][0] for v in coords], [v[1][0] for v in coords]),
        "oldYToNewYCorrelation": correlate([v[0][1] for v in coords], [v[1][1] for v in coords]),
        "oldXToNewYCorrelation": correlate([v[0][0] for v in coords], [v[1][1] for v in coords]),
        "oldYToNewXCorrelation": correlate([v[0][1] for v in coords], [v[1][0] for v in coords]),
        "oldApproxTiles": len({old for old, _ in coords}),
        "newApproxTiles": len({new for _, new in coords}),
        "uniqueOldAois": len({(old[0]//10, old[1]//10) for old, _ in coords}),
        "uniqueNewAois": len({(new[0]//10, new[1]//10) for _, new in coords}),
    }))


def main():
    native = ROOT / "artifacts/map014-lead-live/native-city-20261013-180338/live-resource/bulk-aoi-diagnostic-result.json"
    if native.exists():
        d = json.loads(native.read_text(encoding="utf-8"))
        rows = [v for v in d.get("point_records", []) if v.get("kind") == "player_base"]
        print(json.dumps({
            "receipt": "last City native source-correlated probe",
            "state": d.get("state"), "aoiCount": len(d.get("requestedIndices") or []),
            "sourceCount": (d.get("cityCompleteness") or {}).get("sourceCount"),
            "inRequestedAoi": (d.get("cityCompleteness") or {}).get("inRequestedAoi"),
            "matchedCityCount": d.get("matchedCityCount"),
            "deliveredCityRows": len(rows),
            "nativeCurrentPointInfoCities": d.get("nativePointInfoCityCount"),
            "acceptedUniqueOwners": len({str(r.get("ownerUid")) for r in rows}),
            "invalidGeometry": sum(not isinstance(r.get("x"), (int, float)) or
                                   not isinstance(r.get("y"), (int, float)) or
                                   not (0<=r["x"]<1000 and 0<=r["y"]<1000) for r in rows),
            "rejections": {k:v for k,v in (d.get("cityCompleteness") or {}).items()
                           if k in ("invalidPosition", "missingPointIndex", "missingServer",
                                    "missingOwnerUid", "missingOwnerName")},
            "noTotalUniverseClaim": True,
        }))
    names = [SOURCES[0], SOURCES[1], SOURCES[2], SOURCES[3]]
    a, b, c, old = (rows_for(name) for name in names)
    print_pair("Oct13 City-only to mixed", a, b)
    print_pair("Oct13 City-only to packaged", a, c)
    print_pair("Oct12 old to Oct13 City-only", old, a)
    # Independently account for the small *current-run* union, without treating
    # a later source as ground truth for an earlier time.
    union = set(a) | set(b) | set(c)
    print(json.dumps({"sameDayUnionCount":len(union),
                      "cityOnlyNotInUnion": len(union-set(a)),
                      "mixedNotInUnion": len(union-set(b)),
                      "packagedNotInUnion": len(union-set(c)),
                      "interpretation": "time-separated current native records; not a census"}))
    newest = ROOT / "artifacts/map-013-live/native-causal-20261013-223148/native-first-region.json"
    if newest.exists():
        observed = json.loads(newest.read_text(encoding="utf-8"))
        matched = [r for r in observed.get("point_records", [])
                   if r.get("kind") == "player_base" and str(r.get("ownerUid") or "").strip()]
        unique = {str(r["ownerUid"]): (int(r["x"]),int(r["y"]),int(r["pointId"])) for r in matched}
        requested = observed.get("requestedIndices") or []
        columns = sorted({v % 100 for v in requested if isinstance(v,int)})
        rows = sorted({v // 100 for v in requested if isinstance(v,int)})
        prior = c
        common = unique.keys() & prior.keys()
        position_changed = {uid for uid in common if unique[uid][:2] != prior[uid][:2]}
        print(json.dumps({
            "comparison": "current owned native City subset versus older saved same-server scan",
            "sourceCityServers": sorted({r.get("serverId") for r in matched}),
            "nativeReplyState": observed.get("map013NativeReplyState"),
            "requestedAois": len(requested),
            "inWorldAoiBounds": len(requested)>0 and all(isinstance(v,int) and 0<=v<10000 for v in requested),
            "rectangularAoiFootprint": len(requested)==len(rows)*len(columns),
            "aoiRowRange": [rows[0],rows[-1]] if rows else None,
            "aoiColumnRange": [columns[0],columns[-1]] if columns else None,
            "includesRightmostAoi": 99 in columns,
            "includesTopmostAoi": 99 in rows,
            "nativeSourceCityCount": (observed.get("cityCompleteness") or {}).get("accepted"),
            "nativeUniqueIdentities": len(unique), "overlapWithEarlierSaved": len(common),
            "pointIndexChangedAmongOverlap": sum(unique[uid][2] != prior[uid][2] for uid in common),
            "positionChangedAmongOverlap": len(position_changed),
            "newNativeIdentityAbsentFromEarlierSaved": len(unique.keys()-prior.keys()),
            "oldSavedIdentityNotRepresentedByLimitedNativeAoi": len(prior.keys()-unique.keys()),
            "caution": "old saved records and newer native source were acquired at different times; no present-day full-world missing quota",
        }))


if __name__ == "__main__":
    main()
