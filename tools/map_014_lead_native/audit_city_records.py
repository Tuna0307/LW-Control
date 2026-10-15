"""Read-only current City identity and spatial-recall audit across genuine Map stores.

No historical count is treated as a quota. Only native scans from the local
evidence folders are considered; reported missing keys need an independently
observed *current* target before calling them a live acquisition failure.
Individual owner IDs/names and raw rows are never printed.
"""
from __future__ import annotations

import collections
import hashlib
import json
from pathlib import Path
import sqlite3

ROOT = Path(__file__).resolve().parents[2]
SOURCES = [
    "artifacts/map014-lead-live/native-city-20261013-180338/map-014.db",
    "artifacts/map014-lead-live/native-mixed-20261013-180909/map-014.db",
    "artifacts/map014-lead-ui/native-20261013-104453/profiles/map014-native-ui/map-data/map-data.db",
    "artifacts/map-006/trial-scan008r5_20261012/isolated/profiles/map-006-scan008r5_20261012/map-data/map-data.db",
    "artifacts/map-006/trial-scan008qual_20261011/isolated/profiles/map-006-scan008qual_20261011/map-data/map-data.db",
]


def read_rows(relative: str) -> dict:
    path = (ROOT / relative).resolve()
    if not path.exists():
        return {"source": relative, "missingSource": True}
    connection = sqlite3.connect(path.as_uri() + "?mode=ro", uri=True)
    try:
        rows = list(connection.execute(
            "SELECT record_key, data_json, updated_at "
            "FROM map_records WHERE server_id=2212 AND kind='city'"
        ))
    finally:
        connection.close()
    by_owner: dict[str, tuple] = {}
    duplicate_owner = 0
    invalid_owner = 0
    missing_coordinates = 0
    duplicate_locations = collections.Counter()
    tiles: dict[tuple[int, int], set[str]] = collections.defaultdict(set)
    for key, data_json, stamp in rows:
        data = json.loads(data_json)
        x, y = data.get("x"), data.get("y")
        owner = str(data.get("ownerUid") or "").strip()
        if not owner or owner == "0":
            invalid_owner += 1
            continue
        if x is None or y is None or not (0 <= x < 1000 and 0 <= y < 1000):
            missing_coordinates += 1
            continue
        location = (int(x), int(y))
        duplicate_locations[location] += 1
        tiles[(location[0] // 10, location[1] // 10)].add(owner)
        old = by_owner.get(owner)
        if old:
            duplicate_owner += 1
            if stamp <= old[2]:
                continue
        by_owner[owner] = (location[0], location[1], stamp, key)
    return {
        "source": relative, "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        "totalRows": len(rows), "distinctOwnerUids": len(by_owner),
        "invalidOwner": invalid_owner, "invalidPosition": missing_coordinates,
        "duplicateOwnerRows": duplicate_owner,
        "collocatedCoordinates": sum(n - 1 for n in duplicate_locations.values() if n > 1),
        "nonemptyAois": len(tiles), "byOwner": by_owner, "tiles": tiles,
        "homeTile498x63Present": any(position[:2] == (498, 63) for position in by_owner.values()),
    }


def main():
    sources = [read_rows(name) for name in SOURCES]
    display = [{k: v for k, v in source.items() if k not in ("byOwner", "tiles")}
               for source in sources]
    print(json.dumps({"sources": display}, indent=2))
    available = [source for source in sources if "byOwner" in source]
    for i, lhs in enumerate(available):
        for rhs in available[i + 1:]:
            if lhs["sha256"] == rhs["sha256"]:
                continue
            owners_a, owners_b = lhs["byOwner"], rhs["byOwner"]
            common = owners_a.keys() & owners_b.keys()
            drift = sum(owners_a[owner][:2] != owners_b[owner][:2] for owner in common)
            left_only = owners_a.keys() - owners_b.keys()
            right_only = owners_b.keys() - owners_a.keys()
            print(json.dumps({
                "comparison": [lhs["source"], rhs["source"]],
                "ownerUidOverlap": len(common), "coordinateChangedForSharedOwner": drift,
                "onlyInFirst": len(left_only), "onlyInSecond": len(right_only),
                "onlyInFirstNonemptyAois": len({(owners_a[u][0] // 10, owners_a[u][1] // 10)
                                                  for u in left_only}),
                "onlyInSecondNonemptyAois": len({(owners_b[u][0] // 10, owners_b[u][1] // 10)
                                                   for u in right_only}),
                "missingClassification": "historical_or_time-separated; not independent live absence proof",
            }))


if __name__ == "__main__":
    main()
