#!/usr/bin/env python3
"""Resolve MethodDef P/Invoke mappings from a Last War RGMD assembly."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from inspect_baseutils_rdl import (
    _find_metadata_root,
    _parse_metadata_streams,
    _parse_tables,
    _string_reader,
    _table_index_width,
    _u,
)
from inspect_rdl_field_rva import _table_sizes_through_field_rva


def inspect_pinvoke(path: Path, method_rid: int) -> dict:
    data = path.read_bytes()
    root = _find_metadata_root(data)
    streams, metadata = _parse_metadata_streams(data, root)
    rows, heap_sizes, row_data = _parse_tables(data, streams["#~"])
    sizes = _table_sizes_through_field_rva(rows, heap_sizes)

    offsets: dict[int, int] = {}
    cursor = row_data
    for table in range(30):
        offsets[table] = cursor
        cursor += rows.get(table, 0) * sizes[table]

    string_width = 4 if heap_sizes & 0x01 else 2
    string_at = _string_reader(data, streams["#Strings"])
    module_ref_width = _table_index_width(rows, 26)
    member_forwarded_width = 2 if max(rows.get(4, 0), rows.get(6, 0)) < (1 << 15) else 4

    modules: dict[int, str] = {}
    for rid in range(1, rows.get(26, 0) + 1):
        entry = offsets[26] + (rid - 1) * sizes[26]
        modules[rid] = string_at(_u(data, entry, string_width))

    matches = []
    for index in range(rows.get(28, 0)):
        entry = offsets[28] + index * sizes[28]
        mapping_flags = _u(data, entry, 2)
        entry += 2
        forwarded = _u(data, entry, member_forwarded_width)
        entry += member_forwarded_width
        tag = forwarded & 1
        rid = forwarded >> 1
        import_name_index = _u(data, entry, string_width)
        entry += string_width
        import_scope = _u(data, entry, module_ref_width)
        if tag == 1 and rid == method_rid:
            matches.append(
                {
                    "implmap_rid": index + 1,
                    "mapping_flags": mapping_flags,
                    "method_rid": rid,
                    "method_token": f"0x{0x06000000 | rid:08X}",
                    "import_name": string_at(import_name_index),
                    "module_ref_rid": import_scope,
                    "module_name": modules.get(import_scope),
                }
            )

    return {
        "path": str(path),
        "sha256": hashlib.sha256(data).hexdigest(),
        "metadata": metadata,
        "method_rid": method_rid,
        "match_count": len(matches),
        "matches": matches,
        "read_only": True,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path", type=Path)
    parser.add_argument("--method-rid", required=True, type=lambda value: int(value, 0))
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    result = inspect_pinvoke(args.path, args.method_rid)
    if args.json:
        print(json.dumps(result, indent=2))
    else:
        for item in result["matches"]:
            print(
                f"{item['method_token']} -> {item['module_name']}!{item['import_name']} "
                f"flags=0x{item['mapping_flags']:04X}"
            )
        if not result["matches"]:
            print("No matching P/Invoke mapping")
        print("Read-only: no file changes performed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
