#!/usr/bin/env python3
"""Locate exact 0.3.17 Map storage transaction/lifecycle flow markers."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from inspect_map_surface import native_surface


MARKERS = [
    "begin clear map data",
    "clear map records",
    "clear map scan runs",
    "commit clear map data",
    "DELETE FROM map_records WHERE server_id=?1",
    "DELETE FROM scan_runs WHERE server_id=?1",
    "begin cancel map scan",
    "cancel map scan",
    "clear cancelled scan staging",
    "commit cancel map scan",
    "UPDATE scan_runs SET status='cancelled'",
    "DELETE FROM scan_records WHERE run_id=?1",
    "begin direct scan completion",
    "replace direct map scan records",
    "publish direct map scan records",
    "complete direct map scan",
    "clear direct scan staging",
    "commit direct scan completion",
    "INCOMPLETE_SCAN",
    "direct map scan contains failed batches",
    "direct map scan is incomplete",
    "UPDATE scan_runs SET status='completed'",
    "DELETE FROM scan_runs WHERE server_id=?1 AND id<>?2",
    "begin preserve failed map scan",
    "preserve failed map scan records",
    "clear preserved map scan staging",
    "commit preserved map scan",
    "UPDATE scan_runs SET completed_blocks=?1,failed_blocks=?2",
    "update scan progress",
    "map scan is not running",
]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = {
        "schema": 1,
        "findingId": "LWB317-RE-MAP-002-STORAGE-FLOW-SURFACE",
        "evidenceState": "EXACT_BYTES_DISCOVERY",
        "native": native_surface(args.binary, MARKERS),
        "limits": [
            "SQL/log strings identify current storage flow functions; transaction ordering is promoted only after bounded control-flow inspection.",
        ],
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
