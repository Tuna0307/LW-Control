#!/usr/bin/env python3
"""Read-only MemberRef/call-site inspector for Last War RGMD assemblies.

This complements inspect_baseutils_rdl.py. It resolves an exact MemberRef name
and optional declaring type, then finds direct IL call/callvirt operands to the
matching MemberRef token. It never writes to the target assembly.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import struct
from typing import Any

from inspect_baseutils_rdl import (
    RdlFormatError,
    _coded_index_width,
    _early_table_layout,
    _find_metadata_root,
    _find_text_section,
    _parse_metadata_streams,
    _parse_tables,
    _rva_to_offset,
    _string_reader,
    _table_index_width,
    _u,
)


def _layout_through_memberref(rows: dict[int, int], heap_sizes: int) -> tuple[dict[int, int], dict[str, int]]:
    sizes, widths = _early_table_layout(rows, heap_sizes)
    string_width = widths["string"]
    blob_width = widths["blob"]
    sizes.update(
        {
            7: _table_index_width(rows, 8),
            8: 2 + 2 + string_width,
            9: _table_index_width(rows, 2) + _coded_index_width(rows, [2, 1, 27], 2),
            10: _coded_index_width(rows, [2, 1, 26, 6, 27], 3) + string_width + blob_width,
        }
    )
    return sizes, widths


def _offsets(row_data: int, rows: dict[int, int], sizes: dict[int, int], through: int) -> dict[int, int]:
    cursor = row_data
    result: dict[int, int] = {}
    for table in range(through + 1):
        result[table] = cursor
        cursor += rows.get(table, 0) * sizes[table]
    return result


def inspect_memberref_callers(path: Path, member_name: str, type_name: str | None = None) -> dict[str, Any]:
    data = path.read_bytes()
    root = _find_metadata_root(data)
    streams, metadata = _parse_metadata_streams(data, root)
    rows, heap_sizes, row_data = _parse_tables(data, streams["#~"])
    sizes, widths = _layout_through_memberref(rows, heap_sizes)
    offsets = _offsets(row_data, rows, sizes, 10)
    string_at = _string_reader(data, streams["#Strings"])
    string_width = widths["string"]
    blob_width = widths["blob"]

    # Resolve TypeRef names first so MemberRefParent tag 1 can be named.
    resolution_scope_width = _coded_index_width(rows, [0, 26, 35, 1], 2)
    type_refs: dict[int, dict[str, Any]] = {}
    for rid in range(1, rows.get(1, 0) + 1):
        cursor = offsets[1] + (rid - 1) * sizes[1]
        scope = _u(data, cursor, resolution_scope_width)
        cursor += resolution_scope_width
        name_index = _u(data, cursor, string_width)
        cursor += string_width
        namespace_index = _u(data, cursor, string_width)
        type_refs[rid] = {
            "rid": rid,
            "name": string_at(name_index),
            "namespace": string_at(namespace_index),
            "resolution_scope": scope,
        }

    type_defs: dict[int, dict[str, Any]] = {}
    extends_width = _coded_index_width(rows, [2, 1, 27], 2)
    field_width = _table_index_width(rows, 4)
    method_list_width = _table_index_width(rows, 6)
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

    parent_width = _coded_index_width(rows, [2, 1, 26, 6, 27], 3)
    matches: list[dict[str, Any]] = []
    for rid in range(1, rows.get(10, 0) + 1):
        cursor = offsets[10] + (rid - 1) * sizes[10]
        parent = _u(data, cursor, parent_width)
        cursor += parent_width
        name_index = _u(data, cursor, string_width)
        cursor += string_width
        signature_index = _u(data, cursor, blob_width)
        name = string_at(name_index)
        if name != member_name:
            continue
        tag = parent & 0x7
        parent_rid = parent >> 3
        declaring: dict[str, Any] | None = None
        parent_kind = {0: "TypeDef", 1: "TypeRef", 2: "ModuleRef", 3: "MethodDef", 4: "TypeSpec"}.get(tag, f"tag_{tag}")
        if tag == 0:
            declaring = type_defs.get(parent_rid)
        elif tag == 1:
            declaring = type_refs.get(parent_rid)
        if type_name is not None and (declaring is None or declaring.get("name") != type_name):
            continue
        token_value = 0x0A000000 | rid
        matches.append(
            {
                "rid": rid,
                "metadata_token": f"0x{token_value:08X}",
                "token_value": token_value,
                "name": name,
                "parent_kind": parent_kind,
                "parent_rid": parent_rid,
                "declaring_type": declaring,
                "signature_index": signature_index,
            }
        )

    if not matches:
        raise RdlFormatError(
            f"no MemberRef named {member_name!r}"
            + (f" on type {type_name!r}" if type_name else "")
        )

    # MethodDef catalogue and MethodPtr-aware TypeDef ownership.
    param_width = _table_index_width(rows, 8)
    methods: dict[int, dict[str, Any]] = {}
    for rid in range(1, rows.get(6, 0) + 1):
        cursor = offsets[6] + (rid - 1) * sizes[6]
        rva, impl_flags, flags = struct.unpack_from("<IHH", data, cursor)
        cursor += 8
        name_index = _u(data, cursor, string_width)
        cursor += string_width
        signature_index = _u(data, cursor, blob_width)
        cursor += blob_width
        param_list = _u(data, cursor, param_width)
        methods[rid] = {
            "rid": rid,
            "rva": rva,
            "impl_flags": impl_flags,
            "flags": flags,
            "name": string_at(name_index),
            "signature_index": signature_index,
            "param_list": param_list,
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
            method_rid = pointer_values[pointer_rid - 1] if pointer_values else pointer_rid
            if method_rid in methods:
                declaring_types[method_rid] = declaring

    section = _find_text_section(data)
    ranged_methods: list[tuple[int, dict[str, Any]]] = []
    for method in methods.values():
        if not method["rva"]:
            continue
        try:
            start = _rva_to_offset(int(method["rva"]), section)
        except RdlFormatError:
            continue
        ranged_methods.append((start, method))
    ranged_methods.sort(key=lambda item: (item[0], item[1]["rid"]))
    unique_starts = sorted({start for start, _ in ranged_methods})
    section_end = int(section["raw_pointer"]) + int(section["raw_size"])
    next_start = {
        start: unique_starts[index + 1] if index + 1 < len(unique_starts) else section_end
        for index, start in enumerate(unique_starts)
    }

    callers: list[dict[str, Any]] = []
    text_start = int(section["raw_pointer"])
    text_end = text_start + int(section["raw_size"])
    for match in matches:
        token = struct.pack("<I", int(match["token_value"]))
        search_at = text_start
        while True:
            position = data.find(token, search_at, text_end)
            if position < 0:
                break
            search_at = position + 1
            opcode_offset = position - 1
            opcode = data[opcode_offset] if opcode_offset >= text_start else None
            if opcode not in (0x28, 0x6F):
                continue
            owner = None
            for start, method in reversed(ranged_methods):
                if start <= opcode_offset < next_start[start]:
                    owner = (start, method)
                    break
            if owner is None:
                continue
            start, method = owner
            callers.append(
                {
                    "memberref_rid": match["rid"],
                    "memberref_token": match["metadata_token"],
                    "method_rid": method["rid"],
                    "method_name": method["name"],
                    "declaring_type": declaring_types.get(int(method["rid"])),
                    "method_rva": method["rva"],
                    "method_file_offset": start,
                    "call_file_offset": opcode_offset,
                    "call_relative_offset": opcode_offset - start,
                    "opcode": "call" if opcode == 0x28 else "callvirt",
                    "context": data[max(start, opcode_offset - 24) : min(next_start[start], position + 28)].hex(" "),
                }
            )

    for match in matches:
        match.pop("token_value", None)
    return {
        "path": str(path),
        "sha256": hashlib.sha256(data).hexdigest(),
        "metadata": metadata,
        "member_name": member_name,
        "type_name": type_name,
        "matches": matches,
        "caller_count": len(callers),
        "callers": callers,
        "read_only": True,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path", type=Path)
    parser.add_argument("--member", required=True, help="exact MemberRef name")
    parser.add_argument("--type", dest="type_name", help="optional exact declaring type name")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    result = inspect_memberref_callers(args.path, args.member, args.type_name)
    if args.json:
        print(json.dumps(result, indent=2))
    else:
        print(f"File: {result['path']}")
        print(f"SHA-256: {result['sha256']}")
        print(f"MemberRefs: {len(result['matches'])}; direct callers: {result['caller_count']}")
        for caller in result["callers"]:
            declaring = caller.get("declaring_type") or {}
            namespace = declaring.get("namespace") or ""
            type_label = declaring.get("name") or "?"
            qualified = f"{namespace + '.' if namespace else ''}{type_label}.{caller['method_name']}"
            print(
                f"- {qualified} MethodDef {caller['method_rid']} "
                f"RVA 0x{caller['method_rva']:X}, {caller['opcode']} "
                f"{caller['memberref_token']} at file+0x{caller['call_file_offset']:X}"
            )
        print("Read-only: no file changes performed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
