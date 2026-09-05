#!/usr/bin/env python3
"""Read-only MethodDef catalogue for Last War RGMD assemblies."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import struct
from typing import Any

from inspect_baseutils_rdl import (
    RdlFormatError,
    _blob_value,
    _coded_index_width,
    _early_table_layout,
    _find_metadata_root,
    _find_text_section,
    _parse_metadata_streams,
    _parse_tables,
    _rva_to_offset,
    _string_reader,
    _table_index_width,
    _table_offsets,
    _u,
)
from rdl_il import instruction_rows, parse_decoded_method_body_file


def inspect_methods(
    path: Path,
    *,
    method_rid: int | None = None,
    contains_offset: int | None = None,
    name: str | None = None,
    type_name: str | None = None,
    signature_hex: str | None = None,
    include_il: bool = False,
) -> dict[str, Any]:
    if (
        method_rid is None
        and contains_offset is None
        and name is None
        and type_name is None
        and signature_hex is None
    ):
        raise ValueError("at least one method selector is required")

    data = path.read_bytes()
    root = _find_metadata_root(data)
    streams, metadata = _parse_metadata_streams(data, root)
    rows, heap_sizes, row_data = _parse_tables(data, streams["#~"])
    sizes, widths = _early_table_layout(rows, heap_sizes)
    offsets = _table_offsets(row_data, rows, sizes)
    string_at = _string_reader(data, streams["#Strings"])

    string_width = widths["string"]
    blob_width = widths["blob"]
    param_width = _table_index_width(rows, 8)
    method_row_size = sizes[6]
    methods: dict[int, dict[str, Any]] = {}
    for rid in range(1, rows.get(6, 0) + 1):
        cursor = offsets[6] + (rid - 1) * method_row_size
        rva, impl_flags, flags = struct.unpack_from("<IHH", data, cursor)
        cursor += 8
        name_index = _u(data, cursor, string_width)
        cursor += string_width
        signature_index = _u(data, cursor, blob_width)
        cursor += blob_width
        param_list = _u(data, cursor, param_width)
        methods[rid] = {
            "rid": rid,
            "metadata_token": f"0x{0x06000000 | rid:08X}",
            "rva": rva,
            "impl_flags": impl_flags,
            "flags": flags,
            "name": string_at(name_index),
            "signature_index": signature_index,
            "signature": _blob_value(data, streams["#Blob"], signature_index).hex(" "),
            "param_list": param_list,
        }

    extends_width = _coded_index_width(rows, [2, 1, 27], 2)
    field_width = _table_index_width(rows, 4)
    method_list_width = _table_index_width(rows, 6)
    type_defs: dict[int, dict[str, Any]] = {}
    for rid in range(1, rows.get(2, 0) + 1):
        cursor = offsets[2] + (rid - 1) * sizes[2]
        flags = struct.unpack_from("<I", data, cursor)[0]
        cursor += 4
        name_index = _u(data, cursor, string_width)
        cursor += string_width
        namespace_index = _u(data, cursor, string_width)
        cursor += string_width + extends_width + field_width
        method_list = _u(data, cursor, method_list_width)
        type_defs[rid] = {
            "rid": rid,
            "name": string_at(name_index),
            "namespace": string_at(namespace_index),
            "flags": flags,
            "method_list": method_list,
        }

    pointer_values: list[int] = []
    if rows.get(5, 0):
        method_index_width = _table_index_width(rows, 6)
        pointer_values = [
            _u(data, offsets[5] + index * sizes[5], method_index_width)
            for index in range(rows[5])
        ]

    declaring_types: dict[int, dict[str, Any]] = {}
    for rid, declaring in type_defs.items():
        start = int(declaring["method_list"])
        if rid < rows.get(2, 0):
            stop = int(type_defs[rid + 1]["method_list"])
        else:
            stop = (rows.get(5, 0) if rows.get(5, 0) else rows.get(6, 0)) + 1
        for pointer_rid in range(start, stop):
            current_method_rid = pointer_values[pointer_rid - 1] if pointer_values else pointer_rid
            if current_method_rid in methods:
                declaring_types[current_method_rid] = declaring

    section = _find_text_section(data)
    matches: list[dict[str, Any]] = []
    wanted_signature = signature_hex.lower() if signature_hex is not None else None
    method_starts: list[tuple[int, int]] = []
    for rid, method in methods.items():
        if not method["rva"]:
            continue
        try:
            method_starts.append((_rva_to_offset(int(method["rva"]), section), rid))
        except RdlFormatError:
            pass
    method_starts.sort()
    next_start_by_rid: dict[int, int] = {}
    for index, (start, rid) in enumerate(method_starts):
        next_start_by_rid[rid] = (
            method_starts[index + 1][0]
            if index + 1 < len(method_starts)
            else int(section["raw_pointer"]) + int(section["raw_size"])
        )

    for rid, method in methods.items():
        declaring = declaring_types.get(rid)
        if method_rid is not None and method["rid"] != method_rid:
            continue
        if name is not None and method["name"] != name:
            continue
        if type_name is not None and (declaring is None or declaring["name"] != type_name):
            continue
        if wanted_signature is not None and method["signature"] != wanted_signature:
            continue
        row = dict(method)
        row["declaring_type"] = declaring
        if method["rva"]:
            try:
                row["file_offset"] = _rva_to_offset(int(method["rva"]), section)
            except RdlFormatError:
                row["file_offset"] = None
        else:
            row["file_offset"] = None
        if contains_offset is not None:
            if row["file_offset"] is None:
                continue
            if not (int(row["file_offset"]) <= contains_offset < next_start_by_rid[rid]):
                continue
            row["range_end"] = next_start_by_rid[rid]
        if include_il and row["file_offset"] is not None:
            parsed = parse_decoded_method_body_file(path, int(row["file_offset"]))
            row["method_body"] = {
                "original_header_byte": parsed.original_header_byte,
                "repaired_header_byte": parsed.repaired_header_byte,
                "header_size": parsed.header_size,
                "code_size": parsed.code_size,
                "total_size": parsed.total_size,
                "instructions": instruction_rows(parsed),
            }
        matches.append(row)

    return {
        "path": str(path),
        "sha256": hashlib.sha256(data).hexdigest(),
        "metadata": metadata,
        "selectors": {
            "rid": method_rid,
            "contains_offset": contains_offset,
            "name": name,
            "type": type_name,
            "signature": signature_hex,
            "include_il": include_il,
        },
        "match_count": len(matches),
        "matches": matches,
        "read_only": True,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path", type=Path)
    parser.add_argument("--rid", type=lambda value: int(value, 0), help="exact MethodDef RID")
    parser.add_argument(
        "--contains-offset",
        type=lambda value: int(value, 0),
        help="select the MethodDef whose .text range contains this file offset",
    )
    parser.add_argument("--name", help="exact MethodDef name")
    parser.add_argument("--type", dest="type_name", help="exact declaring TypeDef name")
    parser.add_argument("--signature", help="exact signature blob as lowercase hex bytes")
    parser.add_argument("--il", action="store_true", help="decode matching method bodies in memory")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    result = inspect_methods(
        args.path,
        method_rid=args.rid,
        contains_offset=args.contains_offset,
        name=args.name,
        type_name=args.type_name,
        signature_hex=args.signature,
        include_il=args.il,
    )
    if args.json:
        print(json.dumps(result, indent=2))
    else:
        print(f"File: {result['path']}")
        print(f"Matches: {result['match_count']}")
        for method in result["matches"]:
            declaring = method.get("declaring_type") or {}
            namespace = declaring.get("namespace") or ""
            type_label = declaring.get("name") or "?"
            qualified = f"{namespace + '.' if namespace else ''}{type_label}.{method['name']}"
            print(
                f"- {qualified} {method['metadata_token']} RVA 0x{method['rva']:X} "
                f"signature {method['signature']}"
            )
        print("Read-only: no file changes performed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
