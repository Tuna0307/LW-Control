#!/usr/bin/env python3
"""Read-only inspector for Last War BaseUtils.rdl metadata.

The current game format keeps ECMA-335 metadata streams but replaces the normal
BSJB metadata signature with RGMD. This tool resolves a MethodDef by exact name
and maps its RVA through the embedded PE-style .text section header.

It never writes to the target file.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import struct
from typing import Any


METHODDEF_TABLE = 6


class RdlFormatError(ValueError):
    pass


def _u(data: bytes, offset: int, width: int) -> int:
    if width == 2:
        return struct.unpack_from("<H", data, offset)[0]
    if width == 4:
        return struct.unpack_from("<I", data, offset)[0]
    raise AssertionError(f"unsupported integer width {width}")


def _coded_index_width(rows: dict[int, int], tables: list[int], tag_bits: int) -> int:
    largest = max((rows.get(table, 0) for table in tables), default=0)
    return 2 if largest < (1 << (16 - tag_bits)) else 4


def _table_index_width(rows: dict[int, int], table: int) -> int:
    return 2 if rows.get(table, 0) < 65536 else 4


def _read_compressed_uint(data: bytes, offset: int) -> tuple[int, int]:
    first = data[offset]
    if first < 0x80:
        return first, 1
    if first < 0xC0:
        return ((first & 0x3F) << 8) | data[offset + 1], 2
    if first < 0xE0:
        return (
            ((first & 0x1F) << 24)
            | (data[offset + 1] << 16)
            | (data[offset + 2] << 8)
            | data[offset + 3],
            4,
        )
    raise RdlFormatError("invalid compressed unsigned integer")


def _find_metadata_root(data: bytes) -> int:
    candidates = [pos for sig in (b"RGMD", b"BSJB") if (pos := data.find(sig)) >= 0]
    if not candidates:
        raise RdlFormatError("no RGMD/BSJB metadata root found")
    return min(candidates)


def _parse_metadata_streams(
    data: bytes, root: int
) -> tuple[dict[str, tuple[int, int]], dict[str, Any]]:
    signature = data[root : root + 4]
    if signature not in (b"RGMD", b"BSJB"):
        raise RdlFormatError("unexpected metadata signature")

    major, minor = struct.unpack_from("<HH", data, root + 4)
    version_len = struct.unpack_from("<I", data, root + 12)[0]
    version_start = root + 16
    version_end = version_start + version_len
    if version_end > len(data):
        raise RdlFormatError("metadata version string exceeds file size")
    version = data[version_start:version_end].rstrip(b"\0").decode("ascii", "replace")

    cursor = (version_end + 3) & ~3
    flags, stream_count = struct.unpack_from("<HH", data, cursor)
    cursor += 4
    streams: dict[str, tuple[int, int]] = {}
    for _ in range(stream_count):
        rel_offset, size = struct.unpack_from("<II", data, cursor)
        cursor += 8
        end = data.find(b"\0", cursor)
        if end < 0:
            raise RdlFormatError("unterminated metadata stream name")
        name = data[cursor:end].decode("ascii", "replace")
        cursor = (end + 4) & ~3
        absolute = root + rel_offset
        if absolute + size > len(data):
            raise RdlFormatError(f"metadata stream {name} exceeds file size")
        streams[name] = (absolute, size)

    return streams, {
        "signature": signature.decode("ascii", "replace"),
        "offset": root,
        "major": major,
        "minor": minor,
        "version": version,
        "flags": flags,
        "stream_count": stream_count,
    }


def _parse_tables(
    data: bytes, stream: tuple[int, int]
) -> tuple[dict[int, int], int, int]:
    start, size = stream
    if size < 24:
        raise RdlFormatError("#~ stream too small")
    major = data[start + 4]
    minor = data[start + 5]
    if (major, minor) != (2, 0):
        raise RdlFormatError(f"unsupported #~ version {major}.{minor}")
    heap_sizes = data[start + 6]
    valid = struct.unpack_from("<Q", data, start + 8)[0]
    cursor = start + 24
    rows: dict[int, int] = {}
    for table in range(64):
        if (valid >> table) & 1:
            rows[table] = struct.unpack_from("<I", data, cursor)[0]
            cursor += 4
    return rows, heap_sizes, cursor


def _early_table_layout(
    rows: dict[int, int], heap_sizes: int
) -> tuple[dict[int, int], dict[str, int]]:
    string_width = 4 if heap_sizes & 0x01 else 2
    guid_width = 4 if heap_sizes & 0x02 else 2
    blob_width = 4 if heap_sizes & 0x04 else 2

    sizes = {
        0: 2 + string_width + guid_width * 3,
        1: _coded_index_width(rows, [0, 26, 35, 1], 2) + string_width * 2,
        2: (
            4
            + string_width * 2
            + _coded_index_width(rows, [2, 1, 27], 2)
            + _table_index_width(rows, 4)
            + _table_index_width(rows, 6)
        ),
        3: _table_index_width(rows, 4),
        4: 2 + string_width + blob_width,
        5: _table_index_width(rows, 6),
        6: 4
        + 2
        + 2
        + string_width
        + blob_width
        + _table_index_width(rows, 8),
    }
    return sizes, {"string": string_width, "guid": guid_width, "blob": blob_width}


def _table_offsets(
    row_data: int, rows: dict[int, int], sizes: dict[int, int]
) -> dict[int, int]:
    cursor = row_data
    offsets: dict[int, int] = {}
    for table in range(METHODDEF_TABLE + 1):
        offsets[table] = cursor
        cursor += rows.get(table, 0) * sizes[table]
    return offsets


def _string_reader(data: bytes, stream: tuple[int, int]):
    start, size = stream

    def read(index: int) -> str:
        if not (0 <= index < size):
            raise RdlFormatError(f"string heap index {index} out of bounds")
        end = data.find(b"\0", start + index, start + size)
        if end < 0:
            raise RdlFormatError("unterminated string heap value")
        return data[start + index : end].decode("utf-8", "replace")

    return read


def _blob_value(data: bytes, stream: tuple[int, int], index: int) -> bytes:
    start, size = stream
    if not (0 <= index < size):
        raise RdlFormatError(f"blob heap index {index} out of bounds")
    length, prefix = _read_compressed_uint(data, start + index)
    value_start = start + index + prefix
    value_end = value_start + length
    if value_end > start + size:
        raise RdlFormatError("blob value exceeds heap")
    return data[value_start:value_end]


def _find_text_section(data: bytes) -> dict[str, int | str]:
    position = data.find(b".text\0\0\0", 0, min(len(data), 0x1000))
    if position < 0 or position + 40 > len(data):
        raise RdlFormatError("PE-style .text section header not found")
    virtual_size, virtual_address, raw_size, raw_pointer = struct.unpack_from(
        "<IIII", data, position + 8
    )
    return {
        "name": ".text",
        "header_offset": position,
        "virtual_size": virtual_size,
        "virtual_address": virtual_address,
        "raw_size": raw_size,
        "raw_pointer": raw_pointer,
    }


def _rva_to_offset(rva: int, section: dict[str, int | str]) -> int:
    va = int(section["virtual_address"])
    extent = max(int(section["virtual_size"]), int(section["raw_size"]))
    if not (va <= rva < va + extent):
        raise RdlFormatError(f"RVA 0x{rva:X} is outside .text")
    return int(section["raw_pointer"]) + (rva - va)


def inspect(path: Path, method_name: str = "IsDebug") -> dict[str, Any]:
    data = path.read_bytes()
    root = _find_metadata_root(data)
    streams, metadata = _parse_metadata_streams(data, root)
    for required in ("#~", "#Strings", "#Blob"):
        if required not in streams:
            raise RdlFormatError(f"missing required metadata stream {required}")

    rows, heap_sizes, row_data = _parse_tables(data, streams["#~"])
    if rows.get(METHODDEF_TABLE, 0) == 0:
        raise RdlFormatError("metadata has no MethodDef rows")
    sizes, widths = _early_table_layout(rows, heap_sizes)
    offsets = _table_offsets(row_data, rows, sizes)
    string_at = _string_reader(data, streams["#Strings"])

    string_width = widths["string"]
    blob_width = widths["blob"]
    param_width = _table_index_width(rows, 8)
    method_row_size = sizes[METHODDEF_TABLE]
    matches: list[dict[str, Any]] = []

    for rid in range(1, rows[METHODDEF_TABLE] + 1):
        cursor = offsets[METHODDEF_TABLE] + (rid - 1) * method_row_size
        rva, impl_flags, flags = struct.unpack_from("<IHH", data, cursor)
        cursor += 8
        name_index = _u(data, cursor, string_width)
        cursor += string_width
        signature_index = _u(data, cursor, blob_width)
        cursor += blob_width
        param_list = _u(data, cursor, param_width)
        name = string_at(name_index)
        if name == method_name:
            matches.append(
                {
                    "rid": rid,
                    "rva": rva,
                    "impl_flags": impl_flags,
                    "flags": flags,
                    "name": name,
                    "name_index": name_index,
                    "signature_index": signature_index,
                    "signature": _blob_value(
                        data, streams["#Blob"], signature_index
                    ).hex(" "),
                    "param_list": param_list,
                }
            )

    if len(matches) != 1:
        raise RdlFormatError(
            f"expected exactly one {method_name} MethodDef, found {len(matches)}"
        )
    method = matches[0]

    method_pointer = method["rid"]
    if rows.get(5, 0):
        method_index_width = _table_index_width(rows, 6)
        pointer_hits = []
        for pointer_rid in range(1, rows[5] + 1):
            value = _u(
                data,
                offsets[5] + (pointer_rid - 1) * sizes[5],
                method_index_width,
            )
            if value == method["rid"]:
                pointer_hits.append(pointer_rid)
        if len(pointer_hits) != 1:
            raise RdlFormatError(
                f"expected exactly one MethodPtr for MethodDef {method['rid']}, "
                f"found {len(pointer_hits)}"
            )
        method_pointer = pointer_hits[0]

    type_row_size = sizes[2]
    extends_width = _coded_index_width(rows, [2, 1, 27], 2)
    field_width = _table_index_width(rows, 4)
    method_list_width = _table_index_width(rows, 6)
    declaring_type = None
    for type_rid in range(1, rows.get(2, 0) + 1):
        cursor = offsets[2] + (type_rid - 1) * type_row_size
        type_flags = struct.unpack_from("<I", data, cursor)[0]
        cursor += 4
        type_name_index = _u(data, cursor, string_width)
        cursor += string_width
        namespace_index = _u(data, cursor, string_width)
        cursor += string_width + extends_width + field_width
        method_list = _u(data, cursor, method_list_width)

        if type_rid < rows[2]:
            next_cursor = (
                offsets[2]
                + type_rid * type_row_size
                + 4
                + string_width * 2
                + extends_width
                + field_width
            )
            next_method_list = _u(data, next_cursor, method_list_width)
        else:
            pointer_rows = rows.get(5, 0) if rows.get(5, 0) else rows[6]
            next_method_list = pointer_rows + 1

        if method_list <= method_pointer < next_method_list:
            declaring_type = {
                "rid": type_rid,
                "name": string_at(type_name_index),
                "namespace": string_at(namespace_index),
                "flags": type_flags,
            }
            break
    if declaring_type is None:
        raise RdlFormatError("could not resolve declaring TypeDef")

    section = _find_text_section(data)
    file_offset = _rva_to_offset(int(method["rva"]), section)
    body = data[file_offset : file_offset + 8]
    if len(body) < 3:
        raise RdlFormatError("method body is truncated")
    legacy_signature = (
        body[0] in (0x08, 0x0A)
        and body[1] in (0x16, 0x17)
        and body[2] == 0x2A
    )
    return_value = body[1] == 0x17 if legacy_signature else None

    method.update(
        {
            "declaring_type": declaring_type,
            "file_offset": file_offset,
            "body_prefix": body.hex(" "),
            "legacy_installer_signature_match": legacy_signature,
            "constant_boolean_return": return_value,
        }
    )

    return {
        "path": str(path),
        "size": len(data),
        "sha256": hashlib.sha256(data).hexdigest(),
        "file_magic": data[:4].hex(" "),
        "metadata": metadata,
        "streams": {
            name: {"offset": offset, "size": size}
            for name, (offset, size) in streams.items()
        },
        "tables": {
            "heap_sizes": heap_sizes,
            "row_data_offset": row_data,
            "methoddef_rows": rows[6],
            "typedef_rows": rows.get(2, 0),
            "methodptr_rows": rows.get(5, 0),
        },
        "text_section": section,
        "method": method,
        "read_only": True,
    }


def inspect_callers(path: Path, method_name: str = "IsDebug") -> dict[str, Any]:
    """Find direct IL call/callvirt operands that reference an exact MethodDef.

    This is intentionally a narrow read-only cross-reference pass. It does not
    attempt to be a general IL disassembler; a hit must have a call/callvirt
    opcode immediately before the exact MethodDef token and must fall inside the
    RVA range of a metadata MethodDef.
    """
    target = inspect(path, method_name)
    data = path.read_bytes()
    root = _find_metadata_root(data)
    streams, _ = _parse_metadata_streams(data, root)
    rows, heap_sizes, row_data = _parse_tables(data, streams["#~"])
    sizes, widths = _early_table_layout(rows, heap_sizes)
    offsets = _table_offsets(row_data, rows, sizes)
    string_at = _string_reader(data, streams["#Strings"])
    section = _find_text_section(data)

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
            "rva": rva,
            "impl_flags": impl_flags,
            "flags": flags,
            "name": string_at(name_index),
            "signature_index": signature_index,
            "param_list": param_list,
        }

    pointer_values: list[int] = []
    if rows.get(5, 0):
        width = _table_index_width(rows, 6)
        pointer_values = [
            _u(data, offsets[5] + index * sizes[5], width)
            for index in range(rows[5])
        ]

    type_row_size = sizes[2]
    extends_width = _coded_index_width(rows, [2, 1, 27], 2)
    field_width = _table_index_width(rows, 4)
    method_list_width = _table_index_width(rows, 6)
    declaring_types: dict[int, dict[str, Any]] = {}
    for type_rid in range(1, rows.get(2, 0) + 1):
        cursor = offsets[2] + (type_rid - 1) * type_row_size
        type_flags = struct.unpack_from("<I", data, cursor)[0]
        cursor += 4
        type_name_index = _u(data, cursor, string_width)
        cursor += string_width
        namespace_index = _u(data, cursor, string_width)
        cursor += string_width + extends_width + field_width
        method_list = _u(data, cursor, method_list_width)
        if type_rid < rows[2]:
            next_cursor = (
                offsets[2]
                + type_rid * type_row_size
                + 4
                + string_width * 2
                + extends_width
                + field_width
            )
            next_method_list = _u(data, next_cursor, method_list_width)
        else:
            next_method_list = (rows.get(5, 0) if rows.get(5, 0) else rows[6]) + 1
        declaring = {
            "rid": type_rid,
            "name": string_at(type_name_index),
            "namespace": string_at(namespace_index),
            "flags": type_flags,
        }
        for pointer_rid in range(method_list, next_method_list):
            method_rid = pointer_values[pointer_rid - 1] if pointer_values else pointer_rid
            if method_rid in methods:
                declaring_types[method_rid] = declaring

    ranged_methods = []
    for method in methods.values():
        if not method["rva"]:
            continue
        try:
            file_offset = _rva_to_offset(int(method["rva"]), section)
        except RdlFormatError:
            continue
        ranged_methods.append((file_offset, method))
    ranged_methods.sort(key=lambda item: (item[0], item[1]["rid"]))

    # Use the next strictly greater method start as the conservative body range.
    next_greater: dict[int, int] = {}
    unique_starts = sorted({offset for offset, _ in ranged_methods})
    section_end = int(section["raw_pointer"]) + int(section["raw_size"])
    for index, start in enumerate(unique_starts):
        next_greater[start] = unique_starts[index + 1] if index + 1 < len(unique_starts) else section_end

    target_rid = int(target["method"]["rid"])
    token_value = 0x06000000 | target_rid
    token = struct.pack("<I", token_value)
    text_start = int(section["raw_pointer"])
    text_end = text_start + int(section["raw_size"])
    callers = []
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
            if start <= opcode_offset < next_greater[start]:
                owner = (start, method)
                break
        if owner is None:
            continue
        start, method = owner
        declaring = declaring_types.get(int(method["rid"]), {})
        callers.append(
            {
                "method_rid": method["rid"],
                "method_name": method["name"],
                "declaring_type": declaring,
                "method_rva": method["rva"],
                "method_file_offset": start,
                "call_file_offset": opcode_offset,
                "call_relative_offset": opcode_offset - start,
                "opcode": "call" if opcode == 0x28 else "callvirt",
                "context": data[max(start, opcode_offset - 16) : min(next_greater[start], position + 20)].hex(" "),
            }
        )

    return {
        "path": str(path),
        "sha256": hashlib.sha256(data).hexdigest(),
        "target": {
            "method": target["method"],
            "metadata_token": f"0x{token_value:08X}",
            "token_bytes_le": token.hex(" "),
        },
        "caller_count": len(callers),
        "callers": callers,
        "read_only": True,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path", type=Path, help="path to BaseUtils.rdl")
    parser.add_argument(
        "--method", default="IsDebug", help="exact MethodDef name (default: IsDebug)"
    )
    parser.add_argument(
        "--callers", action="store_true", help="find direct IL callers of the exact MethodDef"
    )
    parser.add_argument("--json", action="store_true", help="emit machine-readable JSON")
    args = parser.parse_args()

    result = inspect_callers(args.path, args.method) if args.callers else inspect(args.path, args.method)
    if args.json:
        print(json.dumps(result, indent=2))
    elif args.callers:
        print(f"File: {result['path']}")
        print(f"SHA-256: {result['sha256']}")
        print(
            f"Target token: {result['target']['metadata_token']} "
            f"({result['target']['token_bytes_le']})"
        )
        print(f"Direct callers: {result['caller_count']}")
        for caller in result["callers"]:
            declaring = caller.get("declaring_type") or {}
            namespace = declaring.get("namespace") or ""
            type_name = declaring.get("name") or "?"
            qualified = f"{namespace + '.' if namespace else ''}{type_name}.{caller['method_name']}"
            print(
                f"- {qualified} MethodDef {caller['method_rid']} "
                f"RVA 0x{caller['method_rva']:X}, {caller['opcode']} at "
                f"file+0x{caller['call_file_offset']:X}"
            )
        print("Read-only: no file changes performed")
    else:
        method = result["method"]
        declaring = method["declaring_type"]
        prefix = f"{declaring['namespace']}." if declaring["namespace"] else ""
        qualified = f"{prefix}{declaring['name']}.{method['name']}"
        print(f"File: {result['path']}")
        print(f"SHA-256: {result['sha256']}")
        print(
            f"Metadata: {result['metadata']['signature']} at "
            f"0x{result['metadata']['offset']:X}, {result['metadata']['version']}"
        )
        print(f"Method: {qualified} (MethodDef RID {method['rid']})")
        print(f"RVA: 0x{method['rva']:X}")
        print(f"File offset: 0x{method['file_offset']:X}")
        print(f"Signature blob: {method['signature']}")
        print(f"Body prefix: {method['body_prefix']}")
        print(
            "Legacy installer signature match: "
            f"{method['legacy_installer_signature_match']}"
        )
        if method["constant_boolean_return"] is not None:
            value = str(method["constant_boolean_return"]).lower()
            print(f"Current constant return: {value}")
        print("Read-only: no file changes performed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
