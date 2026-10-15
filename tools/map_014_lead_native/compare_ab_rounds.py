"""Compare record identity sets between benchmark rounds (A/B scanner accuracy).

usage: python compare_ab_rounds.py <benchmark-run-dir>
Rounds alternate configs (see LWBRIDGE_MAP014_BENCH_CONFIGS). For each kind it reports how many records
of one round are missing from another. Same-config pairs show natural world drift (moving Trucks, spawns,
gathering); a faster config is only accurate if its loss vs. baseline is no worse than that drift.
"""
import json
import sqlite3
import sys
from pathlib import Path

run = Path(sys.argv[1])
bench = json.loads((run / "benchmark.json").read_text(encoding="utf-8"))
rounds = {}
for entry in bench:
    db = sqlite3.connect(f"file:{(run / f'benchmark-{entry['round']}.db').as_posix()}?mode=ro", uri=True)
    keys = {}
    for kind, key in db.execute("select kind, record_key from map_records"):
        keys.setdefault(kind, set()).add(key)
    rounds[entry["round"]] = dict(config=f"{entry.get('lensZoom')}:{entry.get('lensFov')}",
                                  seconds=round(entry["seconds"], 1), keys=keys)

print("round config seconds counts")
for number, data in rounds.items():
    print(number, data["config"], data["seconds"], {k: len(v) for k, v in sorted(data["keys"].items())})

def missing(a, b):
    out = {}
    for kind in sorted(set(rounds[a]["keys"]) | set(rounds[b]["keys"])):
        left, right = rounds[a]["keys"].get(kind, set()), rounds[b]["keys"].get(kind, set())
        out[kind] = f"{len(left - right)}/{len(left)} of r{a} not in r{b}"
    return out

numbers = sorted(rounds)
for i, a in enumerate(numbers):
    for b in numbers[i + 1:]:
        same = rounds[a]["config"] == rounds[b]["config"]
        print(f"\nr{a} vs r{b} ({'same config: natural drift' if same else 'different config'})")
        for kind, text in missing(a, b).items():
            print("  ", kind, text, "|", missing(b, a)[kind])
