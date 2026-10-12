"""Read-only Last War RG/RGMD metadata and CIL decoder.

Lineage: only metadata parser and IL decoder from
research/offline-controller:tools/inspect_lastwar_rdl_metadata.py;
original parser lineage 0d23432 of Macro-Clicker-and-Icon-Watcher.
Current installed assembly hash gate is in inspect_managed.py.
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
