#!/usr/bin/env python3
"""Read-only FieldDef catalogue for Last War RGMD assemblies."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import struct
from typing import Any

from inspect_baseutils_rdl import (
    _blob_value,
    _coded_index_width,
    _early_table_layout,
    _find_metadata_root,
    _parse_metadata_streams,
    _parse_tables,
    _string_reader,
    _table_index_width,
    _table_offsets,
    _u,
)


def _constant_table_layout(
    rows: dict[int, int], row_data: int, sizes: dict[int, int], widths: dict[str, int]
) -> tuple[int, int, int]:
    """Return Constant-table offset, row size, and HasConstant index width."""
    string_width = widths["string"]
    blob_width = widths["blob"]
    extended_sizes = dict(sizes)
    extended_sizes[7] = _table_index_width(rows, 8)  # ParamPtr
    extended_sizes[8] = 4 + string_width  # Param
    extended_sizes[9] = _table_index_width(rows, 2) + _coded_index_width(rows, [2, 1, 27], 2)
    extended_sizes[10] = (
        _coded_index_width(rows, [2, 1, 26, 6, 27], 3) + string_width + blob_width
    )
    has_constant_width = _coded_index_width(rows, [4, 8, 23], 2)
    extended_sizes[11] = 2 + has_constant_width + blob_width
    cursor = row_data
    for table in range(11):
        cursor += rows.get(table, 0) * extended_sizes[table]
    return cursor, extended_sizes[11], has_constant_width


def _decode_constant(element_type: int, payload: bytes) -> int | float | bool | str | None:
    if element_type == 0x02 and payload:
        return payload[0] != 0
    formats = {
        0x04: "<b", 0x05: "<B", 0x06: "<h", 0x07: "<H",
        0x08: "<i", 0x09: "<I", 0x0A: "<q", 0x0B: "<Q",
        0x0C: "<f", 0x0D: "<d",
    }
    fmt = formats.get(element_type)
    if fmt is not None and len(payload) >= struct.calcsize(fmt):
        return struct.unpack_from(fmt, payload)[0]
    if element_type == 0x0E:
        return payload.decode("utf-16-le", errors="replace")
    if element_type == 0x12 and not payload:
        return None
    return payload.hex(" ")


def inspect_fields(
    path: Path,
    *,
    field_rids: set[int] | None = None,
    name: str | None = None,
    type_name: str | None = None,
) -> dict[str, Any]:
    if not field_rids and name is None and type_name is None:
        raise ValueError("at least one field selector is required")

    data = path.read_bytes()
    root = _find_metadata_root(data)
    streams, metadata = _parse_metadata_streams(data, root)
    rows, heap_sizes, row_data = _parse_tables(data, streams["#~"])
    sizes, widths = _early_table_layout(rows, heap_sizes)
    offsets = _table_offsets(row_data, rows, sizes)
    string_at = _string_reader(data, streams["#Strings"])
    string_width = widths["string"]
    blob_width = widths["blob"]

    fields: dict[int, dict[str, Any]] = {}
    for rid in range(1, rows.get(4, 0) + 1):
        cursor = offsets[4] + (rid - 1) * sizes[4]
        flags = struct.unpack_from("<H", data, cursor)[0]
        cursor += 2
        name_index = _u(data, cursor, string_width)
        cursor += string_width
        signature_index = _u(data, cursor, blob_width)
        fields[rid] = {
            "rid": rid,
            "metadata_token": f"0x{0x04000000 | rid:08X}",
            "flags": flags,
            "name": string_at(name_index),
            "signature_index": signature_index,
            "signature": _blob_value(data, streams["#Blob"], signature_index).hex(" "),
        }

    constant_offset, constant_row_size, has_constant_width = _constant_table_layout(
        rows, row_data, sizes, widths
    )
    for constant_rid in range(1, rows.get(11, 0) + 1):
        cursor = constant_offset + (constant_rid - 1) * constant_row_size
        element_type = data[cursor]
        cursor += 2  # element type + padding
        parent = _u(data, cursor, has_constant_width)
        cursor += has_constant_width
        value_index = _u(data, cursor, blob_width)
        parent_tag = parent & 0x03
        parent_rid = parent >> 2
        if parent_tag != 0 or parent_rid not in fields:
            continue
        payload = _blob_value(data, streams["#Blob"], value_index)
        fields[parent_rid]["constant_element_type"] = element_type
        fields[parent_rid]["constant"] = _decode_constant(element_type, payload)

    extends_width = _coded_index_width(rows, [2, 1, 27], 2)
    field_list_width = _table_index_width(rows, 4)
    method_list_width = _table_index_width(rows, 6)
    type_defs: dict[int, dict[str, Any]] = {}
    for rid in range(1, rows.get(2, 0) + 1):
        cursor = offsets[2] + (rid - 1) * sizes[2]
        flags = struct.unpack_from("<I", data, cursor)[0]
        cursor += 4
        name_index = _u(data, cursor, string_width)
        cursor += string_width
        namespace_index = _u(data, cursor, string_width)
        cursor += string_width + extends_width
        field_list = _u(data, cursor, field_list_width)
        cursor += field_list_width
        method_list = _u(data, cursor, method_list_width)
        type_defs[rid] = {
            "rid": rid,
            "name": string_at(name_index),
            "namespace": string_at(namespace_index),
            "flags": flags,
            "field_list": field_list,
            "method_list": method_list,
        }

    pointer_values: list[int] = []
    if rows.get(3, 0):
        field_index_width = _table_index_width(rows, 4)
        pointer_values = [
            _u(data, offsets[3] + index * sizes[3], field_index_width)
            for index in range(rows[3])
        ]

    declaring_types: dict[int, dict[str, Any]] = {}
    for rid, declaring in type_defs.items():
        start = int(declaring["field_list"])
        if rid < rows.get(2, 0):
            stop = int(type_defs[rid + 1]["field_list"])
        else:
            stop = (rows.get(3, 0) if rows.get(3, 0) else rows.get(4, 0)) + 1
        for pointer_rid in range(start, stop):
            current_field_rid = pointer_values[pointer_rid - 1] if pointer_values else pointer_rid
            if current_field_rid in fields:
                declaring_types[current_field_rid] = declaring

    matches: list[dict[str, Any]] = []
    for rid, field in fields.items():
        declaring = declaring_types.get(rid)
        if field_rids and rid not in field_rids:
            continue
        if name is not None and field["name"] != name:
            continue
        if type_name is not None and (declaring is None or declaring["name"] != type_name):
            continue
        row = dict(field)
        row["declaring_type"] = declaring
        matches.append(row)

    return {
        "path": str(path),
        "sha256": hashlib.sha256(data).hexdigest(),
        "metadata": metadata,
        "selectors": {
            "field_rids": sorted(field_rids) if field_rids else None,
            "name": name,
            "type": type_name,
        },
        "match_count": len(matches),
        "matches": matches,
        "read_only": True,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path", type=Path)
    parser.add_argument("--rid", action="append", type=lambda value: int(value, 0))
    parser.add_argument("--name")
    parser.add_argument("--type", dest="type_name")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    result = inspect_fields(
        args.path,
        field_rids=set(args.rid or []),
        name=args.name,
        type_name=args.type_name,
    )
    if args.json:
        print(json.dumps(result, indent=2))
    else:
        print(f"File: {result['path']}")
        print(f"Matches: {result['match_count']}")
        for field in result["matches"]:
            declaring = field.get("declaring_type") or {}
            namespace = declaring.get("namespace") or ""
            type_label = declaring.get("name") or "?"
            qualified = f"{namespace + '.' if namespace else ''}{type_label}.{field['name']}"
            constant = f" = {field['constant']}" if "constant" in field else ""
            print(f"- {qualified} {field['metadata_token']}{constant} signature {field['signature']}")
        print("Read-only: no file changes performed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
