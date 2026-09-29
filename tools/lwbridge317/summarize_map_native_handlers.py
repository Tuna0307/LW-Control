#!/usr/bin/env python3
"""Build a compact review manifest from the full 0.3.17 Map handler trace.

The full native-handler-discovery.json intentionally retains every decoded
instruction.  This helper preserves provenance while projecting only handler
ranges, string references, and direct call targets so semantic review can be
performed without repeatedly loading a multi-megabyte report.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("native_manifest", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    source = json.loads(args.native_manifest.read_text(encoding="utf-8"))
    markers: dict[str, Any] = {}
    for marker, entry in source.get("markers", {}).items():
        handlers = []
        for handler in entry.get("handlerCandidates", []):
            handlers.append(
                {
                    "functionRva": handler["functionRva"],
                    "size": handler["size"],
                    "strings": handler.get("strings", []),
                    "directCalls": handler.get("directCalls", []),
                }
            )
        markers[marker] = {"handlerCandidates": handlers}

    result = {
        "schema": 1,
        "findingId": "LWB317-RE-MAP-001-NATIVE-HANDLERS-COMPACT",
        "evidenceState": "EXACT_BYTES_DISCOVERY",
        "source": {
            "path": str(args.native_manifest),
            "sha256": sha256(args.native_manifest),
            "reference": source.get("source", {}),
        },
        "markers": markers,
        "limits": [
            "This is a lossless projection of handler ranges/strings/direct calls from the full disassembly manifest; branch semantics still require targeted instruction tracing.",
            "String references are discovery evidence and do not alone prove control-flow meaning.",
        ],
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
