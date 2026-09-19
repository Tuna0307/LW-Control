#!/usr/bin/env python3
"""Package hash-locked R6-061 world-readiness evidence after native assertions pass."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import lwbridge_map_scan_world_readiness_assertions as assertions


def build_result(binary: Path) -> dict[str, object]:
    assertions.inspect(binary)
    return {
        "findingId": "LWB-R6-061",
        "date": "2026-09-14",
        "scope": "map scan Start world-map readiness and live-server gate",
        "evidenceStatus": "RECOVERED",
        "sourceIdentity": {
            "path": str(binary),
            "sha256": assertions.EXPECTED_SHA256,
            "imageBase": hex(assertions.IMAGE_BASE),
        },
        "recoveredResult": {
            "enterWorldMapRequestTimeoutMs": 5000,
            "worldMapReadyDeadlineMs": 10000,
            "worldMapReadyPollIntervalMs": 500,
            "worldMapFailure": "WORLD_MAP_FAILED / failed to enter world map",
            "liveServerGate": "after world readiness, serverId must be positive and serverIdSource must equal live",
            "serverFailure": "SERVER_UNAVAILABLE / current server id unavailable",
        },
        "locators": {
            "enterWorldMapRequest": "0x1400F95D9-0x1400F961D",
            "readinessDeadline": "0x1400F96D0-0x1400F96DB",
            "readinessPoll": "0x1400F9730-0x1400F974A",
            "deadlineFailure": "0x1400F99FA-0x1400F9A38",
            "liveServerGate": "0x1400F9B53-0x1400F9B9E",
            "serverFailure": "0x1400F9D39-0x1400F9D61",
        },
        "limits": [
            "this finding recovers Start readiness and live-server admission, not block traversal or request scheduling",
            "the rebuilt helper mirrors only recovered constants and fail-closed predicates; production Start wiring remains separate",
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = build_result(args.binary)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(f"finding={result['findingId']} status={result['evidenceStatus']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
