#!/usr/bin/env python3
"""Read-only FieldRVA extractor for Last War RGMD assemblies."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

from inspect_baseutils_rdl import (
    _coded_index_width,
    _find_metadata_root,
    _find_text_section,
    _parse_metadata_streams,
    _parse_tables,
    _rva_to_offset,
    _string_reader,
    _table_index_width,
    _u,
)


def _parse_rid_length(value: str) -> tuple[int, int]:
    try:
        rid_text, length_text = value.split(":", 1)
        rid = int(rid_text, 0)
        length = int(length_text, 0)
    except Exception as exc:
        raise argparse.ArgumentTypeError("expected RID:LENGTH, e.g. 1467:32") from exc
    if rid <= 0 or length <= 0:
        raise argparse.ArgumentTypeError("RID and LENGTH must be positive")
    return rid, length


def _table_sizes_through_field_rva(rows: dict[int, int], heap_sizes: int) -> dict[int, int]:
    string = 4 if heap_sizes & 0x01 else 2
    guid = 4 if heap_sizes & 0x02 else 2
    blob = 4 if heap_sizes & 0x04 else 2
    return {
        0: 2 + string + guid * 3,
        1: _coded_index_width(rows, [0, 26, 35, 1], 2) + string * 2,
        2: 4 + string * 2 + _coded_index_width(rows, [2, 1, 27], 2)
        + _table_index_width(rows, 4) + _table_index_width(rows, 6),
        3: _table_index_width(rows, 4),
        4: 2 + string + blob,
        5: _table_index_width(rows, 6),
        6: 8 + string + blob + _table_index_width(rows, 8),
        7: _table_index_width(rows, 8),
        8: 4 + string,
        9: _table_index_width(rows, 2) + _coded_index_width(rows, [2, 1, 27], 2),
        10: _coded_index_width(rows, [2, 1, 26, 6, 27], 3) + string + blob,
        11: 2 + _coded_index_width(rows, [4, 8, 23], 2) + blob,
        12: _coded_index_width(
            rows,
            [6, 4, 1, 2, 8, 9, 10, 0, 14, 23, 20, 17, 26, 27, 32, 35, 38, 39, 40, 42, 44],
            5,
        ) + _coded_index_width(rows, [6, 10], 3) + blob,
        13: _coded_index_width(rows, [4, 8], 1) + blob,
        14: 2 + _coded_index_width(rows, [2, 6, 32], 2) + blob,
        15: 6 + _table_index_width(rows, 2),
        16: 4 + _table_index_width(rows, 4),
        17: blob,
        18: _table_index_width(rows, 2) + _table_index_width(rows, 20),
        19: _table_index_width(rows, 20),
        20: 2 + string + _coded_index_width(rows, [2, 1, 27], 2),
        21: _table_index_width(rows, 2) + _table_index_width(rows, 23),
        22: _table_index_width(rows, 23),
        23: 2 + string + blob,
        24: 2 + _table_index_width(rows, 6) + _coded_index_width(rows, [20, 23], 1),
        25: _table_index_width(rows, 2) + 2 * _coded_index_width(rows, [6, 10], 1),
        26: string,
        27: blob,
        28: 2 + _coded_index_width(rows, [4, 6], 1) + string + _table_index_width(rows, 26),
        29: 4 + _table_index_width(rows, 4),
    }


def inspect_field_rvas(path: Path, requests: list[tuple[int, int]]) -> dict[str, Any]:
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
    field_names: dict[int, str] = {}
    for rid in range(1, rows.get(4, 0) + 1):
        field_cursor = offsets[4] + (rid - 1) * sizes[4] + 2
        name_index = _u(data, field_cursor, string_width)
        field_names[rid] = string_at(name_index)

    field_index_width = _table_index_width(rows, 4)
    rvas: dict[int, int] = {}
    for row_index in range(rows.get(29, 0)):
        entry = offsets[29] + row_index * sizes[29]
        rva = _u(data, entry, 4)
        field_rid = _u(data, entry + 4, field_index_width)
        rvas[field_rid] = rva

    section = _find_text_section(data)
    results: list[dict[str, Any]] = []
    for rid, length in requests:
        rva = rvas.get(rid)
        if rva is None:
            results.append({"rid": rid, "length": length, "found": False})
            continue
        offset = _rva_to_offset(rva, section)
        value = data[offset : offset + length]
        results.append(
            {
                "rid": rid,
                "metadata_token": f"0x{0x04000000 | rid:08X}",
                "name": field_names.get(rid),
                "length": length,
                "found": True,
                "rva": rva,
                "file_offset": offset,
                "hex": value.hex(),
                "ascii": "".join(chr(byte) if 32 <= byte <= 126 else "." for byte in value),
            }
        )

    return {
        "path": str(path),
        "sha256": hashlib.sha256(data).hexdigest(),
        "metadata": metadata,
        "fieldrva_rows": rows.get(29, 0),
        "requests": results,
        "read_only": True,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path", type=Path)
    parser.add_argument("--field", action="append", type=_parse_rid_length, required=True)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    result = inspect_field_rvas(args.path, args.field)
    if args.json:
        print(json.dumps(result, indent=2))
    else:
        print(f"File: {result['path']}")
        for item in result["requests"]:
            if not item["found"]:
                print(f"- FieldDef RID {item['rid']}: no FieldRVA")
                continue
            print(
                f"- {item['metadata_token']} {item['name']} file+0x{item['file_offset']:X} "
                f"len={item['length']} hex={item['hex']}"
            )
        print("Read-only: source bytes were not modified")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
