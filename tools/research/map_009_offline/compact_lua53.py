"""Read-only compact Lua 5.3 bytecode parser for the installed LWLF package (written against v24).

Lineage: selected OPS, Reader, parse_proto, parse_chunk, opcode and operand
helpers from archive/research-offline-controller:tools/inspect_current_dispatch_plunder_sources.py
(v19 parser). Historical hash gates and semantic claims are NOT imported;
current artifact identity is validated by inspect_current.py.
"""
from __future__ import annotations
import struct

OPS = [
    "MOVE", "LOADK", "LOADKX", "LOADBOOL", "LOADNIL", "GETUPVAL",
    "GETTABUP", "GETTABLE", "SETTABUP", "SETUPVAL", "SETTABLE",
    "NEWTABLE", "SELF", "ADD", "SUB", "MUL", "MOD", "POW", "DIV",
    "IDIV", "BAND", "BOR", "BXOR", "SHL", "SHR", "UNM", "BNOT",
    "NOT", "LEN", "CONCAT", "JMP", "EQ", "LT", "LE", "TEST",
    "TESTSET", "CALL", "TAILCALL", "RETURN", "FORLOOP", "FORPREP",
    "TFORCALL", "TFORLOOP", "SETLIST", "CLOSURE", "VARARG", "EXTRAARG",
]


class InspectError(RuntimeError):
    pass


class Reader:
    def __init__(self, data: bytes):
        self.data = data
        self.offset = 0

    def read(self, size: int) -> bytes:
        end = self.offset + size
        if end > len(self.data):
            raise InspectError(
                f"unexpected Lua bytecode EOF at {self.offset} reading {size}"
            )
        value = self.data[self.offset:end]
        self.offset = end
        return value

    def u8(self) -> int:
        return self.read(1)[0]

    def i32(self) -> int:
        return struct.unpack("<i", self.read(4))[0]

    def u32(self) -> int:
        return struct.unpack("<I", self.read(4))[0]

    def i64(self) -> int:
        return struct.unpack("<q", self.read(8))[0]

    def f64(self) -> float:
        return struct.unpack("<d", self.read(8))[0]

    def string(self) -> str | None:
        first = self.u8()
        if first == 0:
            return None
        size = first if first < 0xFF else self.u32()
        if size < 1:
            raise InspectError("invalid Lua string size")
        return self.read(size - 1).decode("utf-8", "replace")


def parse_proto(reader: Reader, parent_source: str | None = None) -> dict:
    source = reader.string() or parent_source
    line = reader.i32()
    last_line = reader.i32()
    params = reader.u8()
    vararg = reader.u8()
    stack = reader.u8()
    code = [reader.u32() for _ in range(reader.i32())]

    constants: list[object] = []
    for _ in range(reader.i32()):
        tag = reader.u8()
        if tag == 0:
            value = None
        elif tag == 1:
            value = bool(reader.u8())
        elif tag == 3:
            value = reader.f64()
        elif tag == 0x13:
            value = reader.i64()
        elif tag in (4, 0x14):
            value = reader.string()
        else:
            raise InspectError(f"unsupported Lua constant tag 0x{tag:02X}")
        constants.append(value)

    upvalues = [(reader.u8(), reader.u8()) for _ in range(reader.i32())]
    children = [
        parse_proto(reader, source)
        for _ in range(reader.i32())
    ]
    lines = [reader.i32() for _ in range(reader.i32())]
    locals_ = [
        (reader.string(), reader.i32(), reader.i32())
        for _ in range(reader.i32())
    ]
    upvalue_names = [reader.string() for _ in range(reader.i32())]
    return {
        "source": source,
        "line": line,
        "lastLine": last_line,
        "params": params,
        "vararg": vararg,
        "stack": stack,
        "code": code,
        "constants": constants,
        "upvalues": upvalues,
        "children": children,
        "lines": lines,
        "locals": locals_,
        "upvalueNames": upvalue_names,
    }


def parse_chunk(data: bytes) -> dict:
    reader = Reader(data)
    if reader.read(4) != b"\x1bLua":
        raise InspectError("decoded module is not Lua bytecode")
    if reader.u8() != 0x53 or reader.u8() != 1:
        raise InspectError("unexpected Last War Lua bytecode version/format")
    if reader.read(6) != b"\x19\x93\r\n\x1a\n":
        raise InspectError("unexpected Lua bytecode signature")
    if reader.read(4) != bytes((4, 4, 8, 8)):
        raise InspectError("unexpected Last War Lua serialized-size header")
    reader.read(8)  # LUAC_INT
    reader.read(8)  # LUAC_NUM
    reader.u8()     # root upvalue count
    root = parse_proto(reader)
    if reader.offset != len(data):
        raise InspectError(
            f"unexpected trailing Lua bytecode: {len(data) - reader.offset} bytes"
        )
    return root


def opcode(instruction: int) -> str:
    value = instruction & 0x3F
    return OPS[value] if value < len(OPS) else f"OP{value}"
def instruction_a(instruction: int) -> int:
    return (instruction >> 6) & 0xFF


def instruction_b(instruction: int) -> int:
    return (instruction >> 23) & 0x1FF


def instruction_c(instruction: int) -> int:
    return (instruction >> 14) & 0x1FF


def instruction_bx(instruction: int) -> int:
    return (instruction >> 14) & 0x3FFFF
