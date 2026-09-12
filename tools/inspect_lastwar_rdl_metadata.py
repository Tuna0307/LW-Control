#!/usr/bin/env python3
"""Read-only metadata/IL inspector for Last War's RG/RGMD .rdl container.

The installed Assembly-CSharp.rdl keeps ordinary managed metadata streams and
method bodies but uses an ``RG`` container header and ``RGMD`` metadata magic.
This helper parses those preserved standard structures without rewriting the
artifact or attempting to decode any protected Lua/Python payload.

Recovered for this repository from historical evidence commit ``0d23432`` in
Macro-Clicker-and-Icon-Watcher, where the same read-only parser established
current-game managed contracts.  LWBridge findings must still record the exact
RDL build/hash they inspect; this tool is only a decoder, not feature authority.
"""

from __future__ import annotations

import argparse
import struct
from dataclasses import dataclass
from pathlib import Path
from dncil.cil.body.reader import read_method_body_from_bytes
from dnfile import base, mdtable, stream


class _EventPtrRowStruct(base.RowStruct):
    Event_Index: int


class _EventPtrRow(base.MDTableRow):
    _struct_class = _EventPtrRowStruct
    _struct_indexes = {"Event_Index": ("Event", "Event")}

    def _compute_format(self):
        size = self._clr_coded_index_struct_size(0, ("Event",))
        return ("CLR_METADATA_TABLE_EVENTPTR", (size + ",Event_Index",))


class _PropertyPtrRowStruct(base.RowStruct):
    Property_Index: int


class _PropertyPtrRow(base.MDTableRow):
    _struct_class = _PropertyPtrRowStruct
    _struct_indexes = {"Property_Index": ("Property", "Property")}

    def _compute_format(self):
        size = self._clr_coded_index_struct_size(0, ("Property",))
        return ("CLR_METADATA_TABLE_PROPERTYPTR", (size + ",Property_Index",))


# dnfile intentionally leaves these uncommon ECMA pointer tables as TODOs.
# The RDL valid-table mask contains both, so provide their standard one-column
# definitions locally rather than modifying the installed dependency.
mdtable.EventPtr._row_class = _EventPtrRow
mdtable.PropertyPtr._row_class = _PropertyPtrRow


PRIMITIVES = {
    0x01: "void",
    0x02: "bool",
    0x03: "char",
    0x04: "int8",
    0x05: "uint8",
    0x06: "int16",
    0x07: "uint16",
    0x08: "int32",
    0x09: "uint32",
    0x0A: "int64",
    0x0B: "uint64",
    0x0C: "float32",
    0x0D: "float64",
    0x0E: "string",
    0x16: "typedref",
    0x18: "native int",
    0x19: "native uint",
    0x1C: "object",
}


def _read_compressed_uint(blob: bytes, pos: int) -> tuple[int, int]:
    first = blob[pos]
    if first & 0x80 == 0:
        return first, pos + 1
    if first & 0xC0 == 0x80:
        return ((first & 0x3F) << 8) | blob[pos + 1], pos + 2
    return (
        ((first & 0x1F) << 24)
        | (blob[pos + 1] << 16)
        | (blob[pos + 2] << 8)
        | blob[pos + 3],
        pos + 4,
    )


@dataclass
class MetadataImage:
    path: Path
    data: bytes
    root_offset: int
    tables: stream.MetaDataTables
    streams: list[base.ClrStream]

    def constant_values(self) -> dict[int, object]:
        cached = getattr(self, "_constant_values", None)
        if cached is not None:
            return cached
        formats = {
            0x02: "<?",
            0x03: "<H",
            0x04: "<b",
            0x05: "<B",
            0x06: "<h",
            0x07: "<H",
            0x08: "<i",
            0x09: "<I",
            0x0A: "<q",
            0x0B: "<Q",
            0x0C: "<f",
            0x0D: "<d",
        }
        cached = {}
        for row in self.tables.Constant.rows:
            parent = row.Parent
            if parent.table is not self.tables.Field:
                continue
            raw = bytes(row.Value.value)
            if row.Type == 0x0E:
                value: object = raw.decode("utf-16-le").rstrip("\0")
            elif row.Type == 0x12 and not raw:
                value = None
            else:
                fmt = formats.get(row.Type)
                value = struct.unpack(fmt, raw)[0] if fmt else raw.hex()
            cached[parent.row_index] = value
        self._constant_values = cached
        return cached

    @classmethod
    def load(cls, path: Path) -> "MetadataImage":
        data = path.read_bytes()
        root = data.find(b"RGMD")
        if root < 0:
            raise ValueError("RGMD metadata root not found")

        pos = root + 4
        major, minor = struct.unpack_from("<HH", data, pos)
        pos += 4
        if (major, minor) != (1, 1):
            raise ValueError(f"unexpected RGMD version {major}.{minor}")
        pos += 4  # reserved
        version_len = struct.unpack_from("<I", data, pos)[0]
        pos += 4 + version_len
        _flags, stream_count = struct.unpack_from("<HH", data, pos)
        pos += 4

        type_map = {
            b"#~": stream.MetaDataTables,
            b"#Strings": stream.StringsHeap,
            b"#US": stream.UserStringHeap,
            b"#GUID": stream.GuidHeap,
            b"#Blob": stream.BlobHeap,
        }
        parsed_streams: list[base.ClrStream] = []
        for _ in range(stream_count):
            offset, size = struct.unpack_from("<II", data, pos)
            name_end = data.index(b"\0", pos + 8)
            name = data[pos + 8 : name_end]
            name_field_len = ((len(name) + 1 + 3) // 4) * 4
            fmt = (
                "IMAGE_CLR_STREAM",
                ["I,Offset", "I,Size", f"{name_field_len}s,Name"],
            )
            struct_row = base.StreamStruct(fmt, file_offset=pos)
            struct_row.__unpack__(data[pos : pos + 8 + name_field_len])
            struct_row.Name = struct_row.Name.rstrip(b"\0")
            payload = data[root + offset : root + offset + size]
            stream_type = type_map.get(name, stream.GenericStream)
            parsed = stream_type(0, struct_row, payload)
            parsed.file_offset = root + offset
            parsed_streams.append(parsed)
            pos += 8 + name_field_len

        tables = next(
            item for item in parsed_streams if isinstance(item, stream.MetaDataTables)
        )
        tables.parse(parsed_streams)
        return cls(path, data, root, tables, parsed_streams)

    def type_name(self, table_name: str, row_index: int) -> str:
        table = getattr(self.tables, table_name, None)
        if table is None or row_index <= 0 or row_index > len(table.rows):
            return f"{table_name}[{row_index}]"
        row = table.rows[row_index - 1]
        if table_name in {"TypeDef", "TypeRef"}:
            if table_name == "TypeDef":
                nested_owners = getattr(self, "_nested_owners", None)
                if nested_owners is None:
                    nested_owners = {
                        item.NestedClass.row_index: item.EnclosingClass.row_index
                        for item in self.tables.NestedClass.rows
                    }
                    self._nested_owners = nested_owners
                enclosing = nested_owners.get(row_index)
                if enclosing is not None:
                    return f"{self.type_name('TypeDef', enclosing)}+{row.TypeName}"
            ns = str(row.TypeNamespace)
            name = str(row.TypeName)
            return f"{ns}.{name}" if ns else name
        if table_name == "TypeSpec":
            return self.decode_type(bytes(row.Signature.value), 0)[0]
        return f"{table_name}[{row_index}]"

    def decode_typedef_or_ref(self, encoded: int) -> str:
        tag = encoded & 0x3
        row = encoded >> 2
        table = {0: "TypeDef", 1: "TypeRef", 2: "TypeSpec"}.get(tag)
        if table is None:
            return f"TypeDefOrRef({encoded})"
        return self.type_name(table, row)

    def decode_type(self, blob: bytes, pos: int) -> tuple[str, int]:
        while blob[pos] in (0x1F, 0x20):  # cmod_reqd / cmod_opt
            _coded, pos = _read_compressed_uint(blob, pos + 1)
        et = blob[pos]
        pos += 1
        if et in PRIMITIVES:
            return PRIMITIVES[et], pos
        if et == 0x0F:
            inner, pos = self.decode_type(blob, pos)
            return inner + "*", pos
        if et == 0x10:
            inner, pos = self.decode_type(blob, pos)
            return "ref " + inner, pos
        if et in (0x11, 0x12):
            coded, pos = _read_compressed_uint(blob, pos)
            return self.decode_typedef_or_ref(coded), pos
        if et == 0x13:
            index, pos = _read_compressed_uint(blob, pos)
            return f"!{index}", pos
        if et == 0x14:
            inner, pos = self.decode_type(blob, pos)
            rank, pos = _read_compressed_uint(blob, pos)
            sizes_count, pos = _read_compressed_uint(blob, pos)
            for _ in range(sizes_count):
                _, pos = _read_compressed_uint(blob, pos)
            bounds_count, pos = _read_compressed_uint(blob, pos)
            for _ in range(bounds_count):
                _, pos = _read_compressed_uint(blob, pos)
            return inner + "[" + "," * max(0, rank - 1) + "]", pos
        if et == 0x15:
            kind = blob[pos]
            if kind not in (0x11, 0x12):
                return f"genericinst<{kind:#x}>", pos + 1
            coded, pos = _read_compressed_uint(blob, pos + 1)
            owner = self.decode_typedef_or_ref(coded)
            count, pos = _read_compressed_uint(blob, pos)
            args = []
            for _ in range(count):
                arg, pos = self.decode_type(blob, pos)
                args.append(arg)
            return f"{owner}<{', '.join(args)}>", pos
        if et == 0x1B:
            return "fnptr", pos
        if et == 0x1D:
            inner, pos = self.decode_type(blob, pos)
            return inner + "[]", pos
        if et == 0x1E:
            index, pos = _read_compressed_uint(blob, pos)
            return f"!!{index}", pos
        if et == 0x41:
            return "sentinel", pos
        if et == 0x45:
            inner, pos = self.decode_type(blob, pos)
            return inner + " pinned", pos
        return f"etype_{et:#x}", pos

    def decode_method_signature(self, blob: bytes) -> tuple[str, list[str]]:
        pos = 0
        callconv = blob[pos]
        pos += 1
        if callconv & 0x10:  # generic
            _, pos = _read_compressed_uint(blob, pos)
        count, pos = _read_compressed_uint(blob, pos)
        ret, pos = self.decode_type(blob, pos)
        args = []
        for _ in range(count):
            arg, pos = self.decode_type(blob, pos)
            args.append(arg)
        return ret, args

    def decode_field_signature(self, blob: bytes) -> str:
        if not blob or blob[0] != 0x06:
            return blob.hex()
        return self.decode_type(blob, 1)[0]

    def owner_maps(self):
        cached = getattr(self, "_owner_maps", None)
        if cached is not None:
            return cached
        method_owner = {}
        field_owner = {}
        for t_index, typedef in enumerate(self.tables.TypeDef.rows, 1):
            owner = self.type_name("TypeDef", t_index)
            for ref in typedef.MethodList:
                method_owner[ref.row_index] = owner
            for ref in typedef.FieldList:
                field_owner[ref.row_index] = owner
        cached = method_owner, field_owner
        self._owner_maps = cached
        return cached

    def method_label(self, row_index: int) -> str:
        owners, _ = self.owner_maps()
        row = self.tables.MethodDef.rows[row_index - 1]
        return f"{owners.get(row_index, '?')}::{row.Name}"

    def field_label(self, row_index: int) -> str:
        _, owners = self.owner_maps()
        row = self.tables.Field.rows[row_index - 1]
        return f"{owners.get(row_index, '?')}::{row.Name}"

    def resolve_token(self, token: int) -> str:
        table_id = (token >> 24) & 0xFF
        index = token & 0xFFFFFF
        if index == 0:
            return f"0x{token:08x}"
        if table_id == 0x02:
            return self.type_name("TypeDef", index)
        if table_id == 0x01:
            return self.type_name("TypeRef", index)
        if table_id == 0x04:
            return self.field_label(index)
        if table_id == 0x06:
            return self.method_label(index)
        if table_id == 0x0A:
            row = self.tables.MemberRef.rows[index - 1]
            return f"MemberRef::{row.Name}"
        if table_id == 0x1B:
            return self.type_name("TypeSpec", index)
        if table_id == 0x2B:
            row = self.tables.MethodSpec.rows[index - 1]
            return f"MethodSpec[{index}]"
        if table_id == 0x70:
            us = next(
                (s for s in self.streams if isinstance(s, stream.UserStringHeap)), None
            )
            if us is not None:
                value = us.get(index)
                if value is not None:
                    return repr(value.value)
        return f"0x{token:08x}"

    def rva_to_offset(self, rva: int) -> int:
        # The preserved .text section header is standard: RVA 0x2000, raw 0x200.
        section = self.data[0x90 : 0x90 + 40]
        if section[:5] != b".text":
            raise ValueError("unexpected RDL .text section header")
        virtual_size, virtual_address, raw_size, raw_offset = struct.unpack_from(
            "<IIII", section, 8
        )
        if not (virtual_address <= rva < virtual_address + max(virtual_size, raw_size)):
            raise ValueError(f"RVA 0x{rva:x} is outside preserved .text")
        return raw_offset + (rva - virtual_address)

    def disassemble(self, method_row) -> list[str]:
        if method_row.Rva == 0:
            return ["<no method body>"]
        offset = self.rva_to_offset(method_row.Rva)
        method_bytes = bytearray(self.data[offset:])
        # The RG container preserves CIL bodies but clears bit 1 of the first
        # CorILMethod header byte.  Reassert only that format bit in memory:
        # observed fat headers 0x11/0x19 become standard 0x13/0x1b, while tiny
        # headers with low bits 00 become the standard low bits 10.
        if method_bytes and (method_bytes[0] & 0x3) in (0, 1):
            method_bytes[0] |= 0x2
        body = read_method_body_from_bytes(bytes(method_bytes))
        lines = []
        for insn in body.instructions:
            operand = insn.operand
            rendered = ""
            if operand is not None:
                if isinstance(operand, int) and insn.opcode.name in {
                    "call",
                    "callvirt",
                    "newobj",
                    "ldfld",
                    "ldflda",
                    "stfld",
                    "ldsfld",
                    "ldsflda",
                    "stsfld",
                    "ldtoken",
                    "castclass",
                    "isinst",
                    "box",
                    "unbox",
                    "unbox.any",
                    "newarr",
                    "initobj",
                    "constrained.",
                    "sizeof",
                    "ldstr",
                }:
                    rendered = self.resolve_token(operand)
                else:
                    rendered = str(operand)
            lines.append(
                f"IL_{insn.offset:04x}: {insn.opcode.name}"
                + (f" {rendered}" if rendered else "")
            )
        return lines


def _matching_types(image: MetadataImage, query: str):
    for index, row in enumerate(image.tables.TypeDef.rows, 1):
        full = image.type_name("TypeDef", index)
        if query.lower() in full.lower():
            yield index, row, full


def _print_type(image: MetadataImage, query: str) -> None:
    constants = image.constant_values()
    for index, row, full in _matching_types(image, query):
        extends = row.Extends
        base_name = (
            image.type_name(extends.table.name, extends.row_index)
            if extends is not None and extends.table is not None
            else None
        )
        print(f"TYPE {index}: {full}" + (f" extends {base_name}" if base_name else ""))
        for field_ref in row.FieldList:
            field = field_ref.row
            ftype = image.decode_field_signature(bytes(field.Signature.value))
            constant = constants.get(field_ref.row_index)
            suffix = f" = {constant!r}" if field_ref.row_index in constants else ""
            print(f"  FIELD {field_ref.row_index}: {ftype} {field.Name}{suffix}")
        for method_ref in row.MethodList:
            method = method_ref.row
            ret, args = image.decode_method_signature(bytes(method.Signature.value))
            names = {p.row.Sequence: str(p.row.Name) for p in method.ParamList}
            rendered_args = [
                f"{arg} {names.get(i + 1, f'arg{i + 1}')}" for i, arg in enumerate(args)
            ]
            print(
                f"  METHOD {method_ref.row_index} RVA 0x{method.Rva:x}: "
                f"{ret} {method.Name}({', '.join(rendered_args)})"
            )


def _print_method(image: MetadataImage, spec: str) -> None:
    if "::" not in spec:
        raise SystemExit("--method requires TypeName::MethodName")
    type_query, method_query = spec.split("::", 1)
    found = False
    for _index, typedef, full in _matching_types(image, type_query):
        for method_ref in typedef.MethodList:
            method = method_ref.row
            if method_query.lower() != str(method.Name).lower():
                continue
            found = True
            ret, args = image.decode_method_signature(bytes(method.Signature.value))
            names = {p.row.Sequence: str(p.row.Name) for p in method.ParamList}
            rendered_args = [
                f"{arg} {names.get(i + 1, f'arg{i + 1}')}" for i, arg in enumerate(args)
            ]
            print(
                f"METHOD {method_ref.row_index} {full}::{method.Name} "
                f"RVA 0x{method.Rva:x}: {ret}({', '.join(rendered_args)})"
            )
            for line in image.disassemble(method):
                print("  " + line)
    if not found:
        raise SystemExit(f"method not found: {spec}")


def _print_member_matches(image: MetadataImage, query: str) -> None:
    method_owners, field_owners = image.owner_maps()
    lowered = query.lower()
    for index, row in enumerate(image.tables.Field.rows, 1):
        name = str(row.Name)
        if lowered not in name.lower():
            continue
        field_type = image.decode_field_signature(bytes(row.Signature.value))
        print(f"FIELD {index}: {field_type} {field_owners.get(index, '?')}::{name}")
    for index, row in enumerate(image.tables.MethodDef.rows, 1):
        name = str(row.Name)
        if lowered not in name.lower():
            continue
        ret, args = image.decode_method_signature(bytes(row.Signature.value))
        names = {p.row.Sequence: str(p.row.Name) for p in row.ParamList}
        rendered_args = [
            f"{arg} {names.get(i + 1, f'arg{i + 1}')}" for i, arg in enumerate(args)
        ]
        print(
            f"METHOD {index} RVA 0x{row.Rva:x}: {ret} "
            f"{method_owners.get(index, '?')}::{name}({', '.join(rendered_args)})"
        )


def _print_type_references(image: MetadataImage, query: str) -> None:
    method_owners, field_owners = image.owner_maps()
    lowered = query.lower()
    for index, row in enumerate(image.tables.Field.rows, 1):
        field_type = image.decode_field_signature(bytes(row.Signature.value))
        if lowered not in field_type.lower():
            continue
        print(
            f"FIELD {index}: {field_type} "
            f"{field_owners.get(index, '?')}::{row.Name}"
        )
    for index, row in enumerate(image.tables.MethodDef.rows, 1):
        ret, args = image.decode_method_signature(bytes(row.Signature.value))
        if lowered not in ret.lower() and not any(lowered in arg.lower() for arg in args):
            continue
        names = {p.row.Sequence: str(p.row.Name) for p in row.ParamList}
        rendered_args = [
            f"{arg} {names.get(i + 1, f'arg{i + 1}')}" for i, arg in enumerate(args)
        ]
        print(
            f"METHOD {index} RVA 0x{row.Rva:x}: {ret} "
            f"{method_owners.get(index, '?')}::{row.Name}({', '.join(rendered_args)})"
        )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("rdl", type=Path)
    parser.add_argument("--type", dest="type_query")
    parser.add_argument("--method", dest="method_spec")
    parser.add_argument("--member", dest="member_query")
    parser.add_argument(
        "--references-to-type",
        dest="type_reference_query",
        help="print fields and method signatures whose decoded type references this name",
    )
    args = parser.parse_args()

    image = MetadataImage.load(args.rdl)
    print(
        f"RGMD=0x{image.root_offset:x} "
        f"TypeDef={len(image.tables.TypeDef.rows)} "
        f"MethodDef={len(image.tables.MethodDef.rows)}"
    )
    if args.type_query:
        _print_type(image, args.type_query)
    if args.method_spec:
        _print_method(image, args.method_spec)
    if args.member_query:
        _print_member_matches(image, args.member_query)
    if args.type_reference_query:
        _print_type_references(image, args.type_reference_query)
    if not any(
        (args.type_query, args.method_spec, args.member_query, args.type_reference_query)
    ):
        parser.error("provide --type, --method, --member, or --references-to-type")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
