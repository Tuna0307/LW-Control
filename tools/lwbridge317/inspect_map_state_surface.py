#!/usr/bin/env python3
"""Discover exact 0.3.17 Map scan-state/lifecycle markers and code xrefs."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from inspect_map_surface import native_surface


MARKERS = [
    "resume",
    "resumeAvailable",
    "concurrency",
    "retryCount",
    "scanRunId",
    "phase",
    "scanning",
    "completed",
    "failed",
    "idle",
    "totalBlocks",
    "completedBlocks",
    "readBlocks",
    "failedBlocks",
    "unreadBlocks",
    "inflightBlocks",
    "scanRate",
    "progressPercent",
    "nativeCaptureReady",
    "nativePendingRecords",
    "nativeDroppedRecords",
    "current server changed during map scan",
    "map scan failed",
    "map scan block count mismatch",
    "native world capture session stopped",
    "direct map scan completed",
    "bridge://map-scan-status",
]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = {
        "schema": 1,
        "findingId": "LWB317-RE-MAP-002-STATE-SURFACE",
        "evidenceState": "EXACT_BYTES_DISCOVERY",
        "native": native_surface(args.binary, MARKERS),
        "limits": [
            "Markers/xrefs identify exact current state-machine discovery sites but do not alone prove branch semantics.",
            "Semantic promotion is based on bounded instruction tracing around these sites and recorded separately.",
        ],
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
