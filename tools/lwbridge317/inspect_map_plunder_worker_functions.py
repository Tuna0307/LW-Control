#!/usr/bin/env python3
"""Annotate exact LWBridge 0.3.17 scheduled-plunder worker/store functions.

This is a read-only companion to ``inspect_map_plunder_worker_surface.py``.
It hash-locks the exact 0.3.17 executable, consumes the surface manifest, and
expands only runtime functions referenced by the recovered plunder-worker
markers.  The output is evidence for branch tracing; it never executes the PE.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import pefile

from inspect_map_native_functions import REFERENCE_SHA256, annotate_function, parse_range


class InspectError(ValueError):
    pass


EXTRA_RVAS = {
    # Store/result helpers reached by the exact 0.3.17 worker/result handler but
    # not directly discoverable from the marker-string xrefs alone.
    "dispatch_result_store": 0x3DC4BA,
    "dispatch_history_prune": 0x3DC3E6,
    "dispatch_connection_predicate": 0x2D47A6,
    "dispatch_worker_store_helper": 0x3D6625,
    "truck_worker_store_helper": 0x3D0CA1,
    "truck_status_store_helper": 0x3D9496,
    "dispatch_history_prune_primary": 0x3DCE85,
    "dispatch_history_prune_secondary": 0x3DD5F8,
}


def runtime_functions(pe: pefile.PE) -> list[tuple[int, int]]:
    pe.parse_data_directories(
        directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]]
    )
    rows: list[tuple[int, int]] = []
    for entry in getattr(pe, "DIRECTORY_ENTRY_EXCEPTION", []):
        begin = int(entry.struct.BeginAddress)
        end = int(entry.struct.EndAddress)
        if begin < end:
            rows.append((begin, end))
    rows.sort()
    return rows


def containing_function(functions: list[tuple[int, int]], rva: int) -> tuple[int, int]:
    for begin, end in functions:
        if begin <= rva < end:
            return begin, end
        if begin > rva:
            break
    raise InspectError(f"RVA 0x{rva:X} is not inside a PE runtime-function range")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("surface_manifest", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    data = args.binary.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if digest != REFERENCE_SHA256:
        raise InspectError(f"unexpected LWBridge 0.3.17 SHA-256: {digest}")

    surface_bytes = args.surface_manifest.read_bytes()
    surface = json.loads(surface_bytes.decode("utf-8"))
    if surface.get("native", {}).get("sha256") != digest:
        raise InspectError("plunder-worker surface manifest does not match inspected binary")

    pe = pefile.PE(data=data, fast_load=False)
    image_base = int(pe.OPTIONAL_HEADER.ImageBase)
    function_table = runtime_functions(pe)
    ranges: dict[str, list[str]] = {}
    for marker, entry in surface.get("native", {}).get("markers", {}).items():
        for xref in entry.get("xrefs", []):
            value = xref.get("functionRva")
            if value:
                ranges.setdefault(value, []).append(marker)

    for label, rva in EXTRA_RVAS.items():
        begin, end = containing_function(function_table, rva)
        value = f"0x{begin:X}-0x{end:X}"
        ranges.setdefault(value, []).append(f"extra:{label}@0x{rva:X}")

    functions = []
    for value, markers in sorted(ranges.items(), key=lambda item: int(item[0].split("-")[0], 0)):
        begin, end = parse_range(value)
        row = annotate_function(pe, data, image_base, begin, end)
        row["markers"] = sorted(set(markers))
        functions.append(row)

    result = {
        "schema": 1,
        "findingId": "LWB317-RE-MAP-005-PLUNDER-WORKER-FUNCTIONS",
        "evidenceState": "EXACT_BYTES_DISCOVERY",
        "source": {
            "path": str(args.binary),
            "sha256": digest,
            "surfaceManifest": str(args.surface_manifest),
            "surfaceManifestSha256": hashlib.sha256(surface_bytes).hexdigest(),
            "imageBase": f"0x{image_base:X}",
        },
        "functions": functions,
        "limits": [
            "Function/string/call annotations are exact bytes, but branch meaning still requires targeted instruction tracing.",
            "Absence of a searched literal is not proof that a semantic path is absent when strings are merged or constructed.",
        ],
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
