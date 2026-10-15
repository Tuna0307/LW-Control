"""Classify existing genuine Map scans for seasonal/Doom witnesses, read-only.

The absence of a point in an old scan is evidence only about that capture; it
does not establish today's event calendar or a game's total hidden population.
"""
from __future__ import annotations
from collections import Counter
import json
from pathlib import Path
import sqlite3
import time

root = Path(__file__).resolve().parents[2]
sources = [
    "artifacts/map014-lead-live/native-mixed-20261013-180909/map-014.db",
    "artifacts/map014-lead-ui/native-20261013-104453/profiles/map014-native-ui/map-data/map-data.db",
    "artifacts/map-006/trial-scan008r5_20261012/isolated/profiles/map-006-scan008r5_20261012/map-data/map-data.db",
]
for relative in sources:
    path = (root / relative).resolve()
    if not path.is_file():
        print(json.dumps({"source": relative, "availability": "missing file"}))
        continue
    db = sqlite3.connect(path.as_uri() + "?mode=ro", uri=True)
    try:
        truck_arrivals = Counter()
        epoch_ms = time.time() * 1000
        nearest_future_seconds = None
        for (truck_json,) in db.execute(
            "SELECT data_json FROM map_records WHERE server_id=2212 AND kind='truck'"
        ):
            truck = json.loads(truck_json)
            arrival = truck.get("arriveTs")
            if not arrival:
                truck_arrivals["unknown"] += 1
            else:
                try:
                    arrival = float(arrival)
                    truck_arrivals["future"] += int(arrival > epoch_ms)
                    truck_arrivals["past"] += int(arrival <= epoch_ms)
                    if arrival > epoch_ms:
                        distance = (arrival - epoch_ms) / 1000
                        nearest_future_seconds = distance if nearest_future_seconds is None else min(
                            nearest_future_seconds, distance
                        )
                except (ValueError, TypeError):
                    truck_arrivals["invalid"] += 1
        rows = db.execute("SELECT kind,data_json FROM map_records WHERE server_id=2212 AND kind IN ('monster','treasure')")
        types, treasure_types, supply_types, events, moving_boss, known_boss = (Counter() for _ in range(6))
        count = Counter()
        for kind, raw in rows:
            obj = json.loads(raw)
            count[kind] += 1
            if kind == "treasure":
                treasure_types[str(obj.get("pointType"))] += 1
                supply_types[str(obj.get("suppliesType"))] += 1
                events[str(obj.get("eventId"))] += 1
            else:
                for key in ("runtimeClass", "monsterType", "bossType", "monsterCategory"):
                    if obj.get(key) is not None:
                        types[f"{key}:{obj[key]}"] += 1
                if obj.get("marchUuid") and (obj.get("isDoomWalker") or "boss" in str(obj.get("runtimeClass", "")).lower()):
                    moving_boss["march_uuid_boss_like"] += 1
                if obj.get("marchUuid") and obj.get("monsterKind") == "doomWalker":
                    known_boss["source_named_doom_walker"] += 1
        print(json.dumps({
            "source": relative, "counts": count, "truckArrivalAvailabilityAtAuditTime": truck_arrivals,
            "nearestFutureTruckArrivalSeconds": None if nearest_future_seconds is None else round(nearest_future_seconds, 2),
            "treasurePointTypes": treasure_types, "suppliesTypes": supply_types,
            "treasureEventIds": events,
            "monsterMetadataTypes": types.most_common(16),
            "movingBossLikeUuidRows": sum(moving_boss.values()),
            "sourceNamedDoomRows": sum(known_boss.values()),
            "dependency": "positive current source object and field identity still required",
        }, default=dict))
    finally:
        db.close()
