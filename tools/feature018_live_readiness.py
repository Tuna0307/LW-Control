"""One-shot read-only feature readiness from a task-owned staged packaged game.

Each operation receives its own native receipt. A timeout never retries the
request, and every operation is independently admitted by the packaged app.
"""

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path

from feature018_live_queue import submit


READS = [
    ("attackTargetInventory", "feature018_attack_prepare", {"mode": "inventory"}),
    ("equipmentSnapshot", "feature018_equipment_snapshot", {}),
    ("shieldInventory", "feature018_shield_inventory_get", {}),
    *((f"shieldShop{hours}", "feature018_shield_shop",
       {"mode": "inspect", "slot": hours}) for hours in (8, 12, 24)),
    *((f"rapidInventory{slot}", "feature018_rapid_action",
       {"mode": "inventory", "slot": slot}) for slot in (1, 2)),
    *((f"recall{slot}", "feature018_hotkey_action",
       {"action": "recall", "slot": slot, "queryOnly": True}) for slot in (1, 2)),
    ("randomRelocation", "feature018_hotkey_action",
     {"action": "randomRelocate", "slot": 0, "queryOnly": True}),
    ("allianceRelocation", "feature018_hotkey_action",
     {"action": "allianceRelocate", "slot": 0, "queryOnly": True}),
    ("landCurrent", "mini_land_status", {}),
    ("foodHouseAvailability", "mini_sheep_status", {}),
]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    args = parser.parse_args()
    directory = args.directory.resolve(strict=True)
    output = directory / "feature018-live-readiness.json"
    if output.exists():
        raise FileExistsError("This session's read-only readiness run already exists")
    evidence = {"schemaVersion": 1, "startedAtUtc": datetime.now(timezone.utc).isoformat(),
                "operations": {}, "state": "running"}

    def persist():
        output.write_text(json.dumps(evidence, indent=2, ensure_ascii=False), encoding="utf-8")

    persist()
    for label, command, payload in READS:
        try:
            receipt = submit(directory, {"kind": "command", "command": command,
                                         "payload": payload}, timeout=24)
            evidence["operations"][label] = receipt
            print(label, receipt.get("state"), receipt.get("code", ""), flush=True)
        except Exception as error:
            evidence["operations"][label] = {"state": "transport_error_no_retry",
                                              "error": str(error)}
            print(label, "transport_error_no_retry", str(error), flush=True)
            evidence["state"] = "partial"
            persist()
            break
        persist()
    if evidence["state"] == "running":
        evidence["state"] = "completed_read_only"
    evidence["endedAtUtc"] = datetime.now(timezone.utc).isoformat()
    persist()
    print(output, flush=True)


if __name__ == "__main__":
    main()
