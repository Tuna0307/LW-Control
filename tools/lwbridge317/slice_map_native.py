#!/usr/bin/env python3
"""Print bounded instruction windows from a generated Map native manifest."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


def parse_rva(value: str) -> int:
    return int(value, 0)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", type=Path)
    parser.add_argument("--start", required=True, type=parse_rva)
    parser.add_argument("--end", required=True, type=parse_rva)
    args = parser.parse_args()
    if args.start >= args.end:
        parser.error("--start must be below --end")

    data = json.loads(args.manifest.read_text(encoding="utf-8"))
    seen: set[tuple[str, str, str]] = set()
    for marker, entry in data.get("markers", {}).items():
        for handler in entry.get("handlerCandidates", []):
            for ins in handler.get("instructions", []):
                rva = int(str(ins["rva"]), 0)
                if not (args.start <= rva < args.end):
                    continue
                key = (str(ins["rva"]), str(ins["mnemonic"]), str(ins["opStr"]))
                if key in seen:
                    continue
                seen.add(key)
                print(f"{key[0]:>10}  {key[1]:<8} {key[2]}")
    if not seen:
        parser.error("no instructions found in requested window")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
