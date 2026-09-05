#!/usr/bin/env python3
"""Read-only Lua 5.3 bytecode inspector for decoded Last War LWLF-v3 entries."""

from __future__ import annotations

import argparse
import json
import struct
from dataclasses import dataclass
from pathlib import Path
from typing import Any

try:
    from .extract_lenc_v3 import decode_lenc_bytes, derive_xlua_key_nonce
    from .install_loader_probe import discover_paths, read_lwlf
except ImportError:
    from extract_lenc_v3 import decode_lenc_bytes, derive_xlua_key_nonce
    from install_loader_probe import discover_paths, read_lwlf


LUA_SIGNATURE = b"\x1bLua"
LUA53_VERSION = 0x53
LUA_FORMAT_STANDARD = 0
LUA_FORMAT_LAST_WAR = 1
LUAC_DATA = b"\x19\x93\r\n\x1a\n"
LUAC_INT = 0x5678
LUAC_NUM = 370.5


class Lua53Error(ValueError):
    pass


@dataclass(frozen=True)
class LuaConstant:
    tag: int
    value: Any


@dataclass(frozen=True)
class LuaLocal:
    name: str | None
    start_pc: int
    end_pc: int


@dataclass
class LuaPrototype:
    index_path: str
    source: str | None
    line_defined: int
    last_line_defined: int
    num_params: int
    is_vararg: int
    max_stack_size: int
    instructions: list[int]
    constants: list[LuaConstant]
    upvalues: list[tuple[int, int]]
    children: list["LuaPrototype"]
    line_info: list[int]
    locals: list[LuaLocal]
    upvalue_names: list[str | None]


class _Reader:
    def __init__(self, data: bytes):
        self.data = data
        self.offset = 0
        self.endian = "<"
        self.int_size = 4
        self.size_t_size = 8
        self.instruction_size = 4
        self.lua_integer_size = 8
        self.lua_number_size = 8

    def read(self, size: int) -> bytes:
        if size < 0 or self.offset + size > len(self.data):
            raise Lua53Error(f"truncated Lua chunk at offset 0x{self.offset:X}")
        value = self.data[self.offset : self.offset + size]
        self.offset += size
        return value

    def byte(self) -> int:
        return self.read(1)[0]

    def uint(self, size: int) -> int:
        if size not in (1, 2, 4, 8):
            raise Lua53Error(f"unsupported integer width {size}")
        return int.from_bytes(self.read(size), "little" if self.endian == "<" else "big")

    def int(self) -> int:
        raw = self.read(self.int_size)
        return int.from_bytes(raw, "little" if self.endian == "<" else "big", signed=True)

    def lua_integer(self) -> int:
        raw = self.read(self.lua_integer_size)
        return int.from_bytes(raw, "little" if self.endian == "<" else "big", signed=True)

    def lua_number(self) -> float:
        raw = self.read(self.lua_number_size)
        if self.lua_number_size != 8:
            raise Lua53Error(f"unsupported lua_Number width {self.lua_number_size}")
        return struct.unpack(self.endian + "d", raw)[0]

    def string(self) -> str | None:
        size = self.byte()
        if size == 0:
            return None
        if size == 0xFF:
            size = self.uint(self.size_t_size)
        if size < 1:
            raise Lua53Error("invalid Lua string size")
        raw = self.read(size - 1)
        return raw.decode("utf-8", errors="replace")


def _parse_header(reader: _Reader) -> dict[str, Any]:
    if reader.read(4) != LUA_SIGNATURE:
        raise Lua53Error("decoded entry is not a Lua binary chunk")
    version = reader.byte()
    fmt = reader.byte()
    if version != LUA53_VERSION or fmt not in (LUA_FORMAT_STANDARD, LUA_FORMAT_LAST_WAR):
        raise Lua53Error(f"unsupported Lua chunk version/format 0x{version:02X}/{fmt}")
    if reader.read(6) != LUAC_DATA:
        raise Lua53Error("Lua chunk LUAC_DATA marker changed")
    reader.int_size = reader.byte()
    reader.size_t_size = reader.byte()
    if fmt == LUA_FORMAT_STANDARD:
        reader.instruction_size = reader.byte()
        reader.lua_integer_size = reader.byte()
        reader.lua_number_size = reader.byte()
    else:
        # Current Last War chunks use custom format byte 1 and serialize four
        # size fields after LUAC_DATA: int, size_t, lua_Integer, lua_Number.
        # Instructions remain 4 bytes, matching the native VM and every current
        # decoded chunk inspected so far.
        reader.instruction_size = 4
        reader.lua_integer_size = reader.byte()
        reader.lua_number_size = reader.byte()
    if reader.instruction_size != 4:
        raise Lua53Error(f"unsupported Lua instruction width {reader.instruction_size}")

    int_raw = reader.read(reader.lua_integer_size)
    little = int.from_bytes(int_raw, "little", signed=True)
    big = int.from_bytes(int_raw, "big", signed=True)
    if little == LUAC_INT:
        reader.endian = "<"
    elif big == LUAC_INT:
        reader.endian = ">"
    else:
        raise Lua53Error("Lua chunk endianness/integer sentinel is unsupported")
    num_raw = reader.read(reader.lua_number_size)
    if reader.lua_number_size != 8:
        raise Lua53Error(f"unsupported lua_Number width {reader.lua_number_size}")
    num = struct.unpack(reader.endian + "d", num_raw)[0]
    if num != LUAC_NUM:
        raise Lua53Error(f"Lua number sentinel changed: {num!r}")
    main_upvalues = reader.byte()
    return {
        "version": version,
        "format": fmt,
        "int_size": reader.int_size,
        "size_t_size": reader.size_t_size,
        "instruction_size": reader.instruction_size,
        "lua_integer_size": reader.lua_integer_size,
        "lua_number_size": reader.lua_number_size,
        "endianness": "little" if reader.endian == "<" else "big",
        "main_upvalues": main_upvalues,
    }


def _parse_constant(reader: _Reader) -> LuaConstant:
    tag = reader.byte()
    if tag == 0:  # nil
        return LuaConstant(tag, None)
    if tag == 1:  # boolean
        return LuaConstant(tag, bool(reader.byte()))
    if tag == 3:  # float
        return LuaConstant(tag, reader.lua_number())
    if tag == 0x13:  # integer
        return LuaConstant(tag, reader.lua_integer())
    if tag in (4, 0x14):  # short/long string
        return LuaConstant(tag, reader.string())
    raise Lua53Error(f"unsupported Lua constant tag 0x{tag:02X} at offset 0x{reader.offset - 1:X}")


def _parse_prototype(reader: _Reader, path: str, inherited_source: str | None) -> LuaPrototype:
    source = reader.string() or inherited_source
    line_defined = reader.int()
    last_line_defined = reader.int()
    num_params = reader.byte()
    is_vararg = reader.byte()
    max_stack_size = reader.byte()

    code_count = reader.int()
    if code_count < 0:
        raise Lua53Error("negative Lua instruction count")
    instructions = [reader.uint(reader.instruction_size) for _ in range(code_count)]

    constant_count = reader.int()
    if constant_count < 0:
        raise Lua53Error("negative Lua constant count")
    constants = [_parse_constant(reader) for _ in range(constant_count)]

    upvalue_count = reader.int()
    if upvalue_count < 0:
        raise Lua53Error("negative Lua upvalue count")
    upvalues = [(reader.byte(), reader.byte()) for _ in range(upvalue_count)]

    child_count = reader.int()
    if child_count < 0:
        raise Lua53Error("negative Lua child-prototype count")
    children = [
        _parse_prototype(reader, f"{path}.{index}", source)
        for index in range(child_count)
    ]

    line_count = reader.int()
    if line_count < 0:
        raise Lua53Error("negative Lua line-info count")
    line_info = [reader.int() for _ in range(line_count)]

    local_count = reader.int()
    if local_count < 0:
        raise Lua53Error("negative Lua local-variable count")
    locals_ = [LuaLocal(reader.string(), reader.int(), reader.int()) for _ in range(local_count)]

    name_count = reader.int()
    if name_count < 0:
        raise Lua53Error("negative Lua upvalue-name count")
    upvalue_names = [reader.string() for _ in range(name_count)]

    return LuaPrototype(
        index_path=path,
        source=source,
        line_defined=line_defined,
        last_line_defined=last_line_defined,
        num_params=num_params,
        is_vararg=is_vararg,
        max_stack_size=max_stack_size,
        instructions=instructions,
        constants=constants,
        upvalues=upvalues,
        children=children,
        line_info=line_info,
        locals=locals_,
        upvalue_names=upvalue_names,
    )


def parse_lua53_chunk(data: bytes) -> tuple[dict[str, Any], LuaPrototype]:
    reader = _Reader(data)
    header = _parse_header(reader)
    main = _parse_prototype(reader, "0", None)
    if reader.offset != len(data):
        raise Lua53Error(
            f"Lua chunk has {len(data) - reader.offset} trailing bytes after prototype tree"
        )
    return header, main


OP_NAMES = [
    "MOVE", "LOADK", "LOADKX", "LOADBOOL", "LOADNIL", "GETUPVAL", "GETTABUP",
    "GETTABLE", "SETTABUP", "SETUPVAL", "SETTABLE", "NEWTABLE", "SELF", "ADD",
    "SUB", "MUL", "MOD", "POW", "DIV", "IDIV", "BAND", "BOR", "BXOR", "SHL",
    "SHR", "UNM", "BNOT", "NOT", "LEN", "CONCAT", "JMP", "EQ", "LT", "LE",
    "TEST", "TESTSET", "CALL", "TAILCALL", "RETURN", "FORLOOP", "FORPREP",
    "TFORCALL", "TFORLOOP", "SETLIST", "CLOSURE", "VARARG", "EXTRAARG",
]

OP_MODES = {
    "MOVE": "ABC", "LOADK": "ABx", "LOADKX": "ABx", "LOADBOOL": "ABC",
    "LOADNIL": "ABC", "GETUPVAL": "ABC", "GETTABUP": "ABC", "GETTABLE": "ABC",
    "SETTABUP": "ABC", "SETUPVAL": "ABC", "SETTABLE": "ABC", "NEWTABLE": "ABC",
    "SELF": "ABC", "ADD": "ABC", "SUB": "ABC", "MUL": "ABC", "MOD": "ABC",
    "POW": "ABC", "DIV": "ABC", "IDIV": "ABC", "BAND": "ABC", "BOR": "ABC",
    "BXOR": "ABC", "SHL": "ABC", "SHR": "ABC", "UNM": "ABC", "BNOT": "ABC",
    "NOT": "ABC", "LEN": "ABC", "CONCAT": "ABC", "JMP": "AsBx", "EQ": "ABC",
    "LT": "ABC", "LE": "ABC", "TEST": "ABC", "TESTSET": "ABC", "CALL": "ABC",
    "TAILCALL": "ABC", "RETURN": "ABC", "FORLOOP": "AsBx", "FORPREP": "AsBx",
    "TFORCALL": "ABC", "TFORLOOP": "AsBx", "SETLIST": "ABC", "CLOSURE": "ABx",
    "VARARG": "ABC", "EXTRAARG": "Ax",
}

RK_OPS = {
    "GETTABUP": ("C",), "GETTABLE": ("C",), "SETTABUP": ("B", "C"),
    "SETTABLE": ("B", "C"), "SELF": ("C",), "ADD": ("B", "C"),
    "SUB": ("B", "C"), "MUL": ("B", "C"), "MOD": ("B", "C"),
    "POW": ("B", "C"), "DIV": ("B", "C"), "IDIV": ("B", "C"),
    "BAND": ("B", "C"), "BOR": ("B", "C"), "BXOR": ("B", "C"),
    "SHL": ("B", "C"), "SHR": ("B", "C"), "EQ": ("B", "C"),
    "LT": ("B", "C"), "LE": ("B", "C"),
}


def decode_instruction(word: int) -> dict[str, Any]:
    opcode = word & 0x3F
    if opcode >= len(OP_NAMES):
        return {"word": f"0x{word:08X}", "opcode": opcode, "name": f"OP_{opcode}"}
    name = OP_NAMES[opcode]
    a = (word >> 6) & 0xFF
    c = (word >> 14) & 0x1FF
    b = (word >> 23) & 0x1FF
    bx = (word >> 14) & 0x3FFFF
    ax = (word >> 6) & 0x3FFFFFF
    sbx = bx - 131071
    row: dict[str, Any] = {"word": f"0x{word:08X}", "opcode": opcode, "name": name, "A": a}
    mode = OP_MODES[name]
    if mode == "ABC":
        row.update(B=b, C=c)
    elif mode == "ABx":
        row["Bx"] = bx
    elif mode == "AsBx":
        row["sBx"] = sbx
    elif mode == "Ax":
        row = {"word": f"0x{word:08X}", "opcode": opcode, "name": name, "Ax": ax}
    return row


def _constant_text(constant: LuaConstant) -> str:
    if isinstance(constant.value, str):
        return repr(constant.value)
    return repr(constant.value)


def _annotate_instruction(row: dict[str, Any], constants: list[LuaConstant]) -> dict[str, Any]:
    row = dict(row)
    refs: list[dict[str, Any]] = []
    name = row["name"]
    if name == "LOADK":
        index = int(row["Bx"])
        if 0 <= index < len(constants):
            refs.append({"operand": "Bx", "index": index, "value": constants[index].value})
    elif name == "LOADKX":
        pass
    for operand in RK_OPS.get(name, ()):
        value = int(row[operand])
        if value & 0x100:
            index = value & 0xFF
            if 0 <= index < len(constants):
                refs.append({"operand": operand, "index": index, "value": constants[index].value})
    if refs:
        row["constant_refs"] = refs
    return row


def flatten_prototypes(root: LuaPrototype) -> list[LuaPrototype]:
    result = [root]
    for child in root.children:
        result.extend(flatten_prototypes(child))
    return result


def inspect_chunk(data: bytes, contains: str | None = None, context: int = 5) -> dict[str, Any]:
    header, main = parse_lua53_chunk(data)
    prototype_rows = []
    contains_lower = contains.lower() if contains is not None else None
    for proto in flatten_prototypes(main):
        constants = [constant.value for constant in proto.constants]
        matching_indices = [
            index
            for index, value in enumerate(constants)
            if isinstance(value, str) and contains_lower is not None and contains_lower in value.lower()
        ]
        instructions = [
            _annotate_instruction(decode_instruction(word), proto.constants)
            for word in proto.instructions
        ]
        hits = []
        if contains_lower is not None and matching_indices:
            target_indices = set(matching_indices)
            for pc, row in enumerate(instructions):
                refs = row.get("constant_refs", [])
                if not any(int(ref["index"]) in target_indices for ref in refs):
                    continue
                start = max(0, pc - context)
                end = min(len(instructions), pc + context + 1)
                hits.append(
                    {
                        "pc": pc,
                        "line": proto.line_info[pc] if pc < len(proto.line_info) else None,
                        "instructions": [
                            {
                                "pc": item_pc,
                                "line": proto.line_info[item_pc] if item_pc < len(proto.line_info) else None,
                                **instructions[item_pc],
                            }
                            for item_pc in range(start, end)
                        ],
                    }
                )
        if contains_lower is None or matching_indices:
            prototype_rows.append(
                {
                    "path": proto.index_path,
                    "source": proto.source,
                    "line_defined": proto.line_defined,
                    "last_line_defined": proto.last_line_defined,
                    "num_params": proto.num_params,
                    "is_vararg": proto.is_vararg,
                    "max_stack_size": proto.max_stack_size,
                    "instruction_count": len(proto.instructions),
                    "constant_count": len(proto.constants),
                    "matching_constant_indices": matching_indices,
                    "matching_constants": [
                        {"index": index, "value": constants[index]} for index in matching_indices
                    ],
                    "locals": [
                        {"name": local.name, "start_pc": local.start_pc, "end_pc": local.end_pc}
                        for local in proto.locals
                    ],
                    "upvalue_names": proto.upvalue_names,
                    "hits": hits,
                }
            )
    return {"header": header, "prototype_count": len(flatten_prototypes(main)), "prototypes": prototype_rows}


def inspect_installed_entry(entry_name: str, contains: str | None, context: int) -> dict[str, Any]:
    paths = discover_paths()
    xlua = paths["game_exe"].parent / "LastWar_Data" / "Plugins" / "x86_64" / "xlua.dll"
    native = derive_xlua_key_nonce(xlua)
    file_version, content_version, entries = read_lwlf(paths["data"])
    mapped = dict(entries)
    if entry_name not in mapped:
        raise Lua53Error(f"entry {entry_name!r} is missing from LWScripts.data")
    decoded = decode_lenc_bytes(mapped[entry_name], native["key"], native["nonce"])["decoded"]
    result = inspect_chunk(decoded, contains=contains, context=context)
    return {
        "entry": entry_name,
        "file_version": file_version,
        "content_version": content_version,
        "decoded_size": len(decoded),
        "read_only": True,
        **result,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--entry", required=True)
    parser.add_argument("--contains", help="show prototypes/instructions referencing matching string constants")
    parser.add_argument("--context", type=int, default=5)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        result = inspect_installed_entry(args.entry, args.contains, args.context)
    except (Lua53Error, OSError, EOFError, ValueError) as exc:
        output = {"ok": False, "error": str(exc), "error_type": type(exc).__name__}
        print(json.dumps(output, indent=2) if args.json else f"REFUSED: {exc}")
        return 2
    output = {"ok": True, **result}
    print(json.dumps(output, indent=2) if args.json else output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
