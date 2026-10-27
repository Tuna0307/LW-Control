"""Tiny bounded interpreter for SELECTED real game Lua 5.3 function bodies.

Runs only the opcodes needed for the source-backed position/index and request
serialization tests. Network, UI, Unity and filesystem callbacks are supplied
by inert Python fixtures. Unknown opcodes fail closed; this is not a general Lua
runtime and gives no evidence of remote server acceptance.
"""
from __future__ import annotations

from collections.abc import Mapping
from typing import Any

import compact_lua53 as lua


MASK64 = (1 << 64) - 1


def integer(value: int) -> int:
    """Model Lua 5.3 signed 64-bit integer storage for bitwise ops."""
    value &= MASK64
    return value if value < 1 << 63 else value - (1 << 64)


def truthy(value: object) -> bool:
    return value is not False and value is not None


def getfield(value: Any, field: Any) -> Any:
    return value[field] if isinstance(value, Mapping) else getattr(value, field)


class ExecutionError(RuntimeError):
    pass


def execute(proto: dict, *args: Any, globals_: Mapping[str, object] | None = None,
            upvalues: Mapping[int, object] | None = None,
            instruction_limit: int = 1000) -> tuple[object, ...]:
    """Execute real decoded instructions with only explicit, isolated inputs."""
    registers: list[Any] = [None] * max(128, proto["stack"] + 32)
    registers[:len(args)] = args
    top = len(args)
    constant = proto["constants"]
    up = {0: dict(globals_ or {}), **dict(upvalues or {})}
    pc = 0
    steps = 0

    def rk(value: int) -> Any:
        return constant[value & 255] if value & 256 else registers[value]

    while pc < len(proto["code"]):
        if steps >= instruction_limit:
            raise ExecutionError("instruction limit exceeded")
        steps += 1
        word = proto["code"][pc]
        op = lua.opcode(word)
        a, b, c = (lua.instruction_a(word), lua.instruction_b(word),
                   lua.instruction_c(word))
        bx = lua.instruction_bx(word)
        pc += 1
        if op == "MOVE":
            registers[a] = registers[b]
        elif op == "LOADK":
            registers[a] = constant[bx]
        elif op == "LOADBOOL":
            registers[a] = b != 0
            if c:
                pc += 1
        elif op == "LOADNIL":
            for i in range(a, a + b + 1):
                registers[i] = None
        elif op == "GETUPVAL":
            registers[a] = up[b]
        elif op == "GETTABUP":
            registers[a] = getfield(up[b], rk(c))
        elif op == "GETTABLE":
            registers[a] = getfield(registers[b], rk(c))
        elif op == "NEWTABLE":
            registers[a] = {}
        elif op == "SETTABLE":
            registers[a][rk(b)] = rk(c)
        elif op == "SELF":
            registers[a + 1] = registers[b]
            registers[a] = getfield(registers[b], rk(c))
        elif op in ("ADD", "SUB", "MUL", "MOD", "IDIV", "DIV", "BAND", "BOR", "BXOR", "SHL", "SHR"):
            left, right = rk(b), rk(c)
            if op == "ADD":
                value = left + right
            elif op == "SUB":
                value = left - right
            elif op == "MUL":
                value = left * right
            elif op == "MOD":
                value = left % right
            elif op == "IDIV":
                value = left // right
            elif op == "DIV":
                value = left / right
            elif op == "BAND":
                value = integer(left & right)
            elif op == "BOR":
                value = integer(left | right)
            elif op == "BXOR":
                value = integer(left ^ right)
            elif op == "SHL":
                value = integer(left << right) if right >= 0 else integer((left & MASK64) >> -right)
            else:
                value = integer((left & MASK64) >> right) if right >= 0 else integer(left << -right)
            registers[a] = value
        elif op == "JMP":
            pc += bx - 131071
        elif op in ("EQ", "LT", "LE"):
            left, right = rk(b), rk(c)
            comparison = left == right if op == "EQ" else (left < right if op == "LT" else left <= right)
            if comparison != (a != 0):
                pc += 1
        elif op == "TEST":
            if truthy(registers[a]) != (c != 0):
                pc += 1
        elif op == "TESTSET":
            if truthy(registers[b]) == (c != 0):
                registers[a] = registers[b]
            else:
                pc += 1
        elif op in ("CALL", "TAILCALL"):
            callback = registers[a]
            values = callback(*registers[a + 1:top] if b == 0 else registers[a + 1:a + b])
            if op == "TAILCALL":
                return values if isinstance(values, tuple) else (values,)
            returned = values if isinstance(values, tuple) else (values,)
            if c == 0:
                for index, result in enumerate(returned):
                    registers[a + index] = result
                top = a + len(returned)
                continue
            for index in range(c - 1):
                registers[a + index] = returned[index] if index < len(returned) else None
        elif op == "RETURN":
            if b == 0:
                return tuple(registers[a:top])
            return tuple(registers[a:a + b - 1])
        else:
            raise ExecutionError(f"unsupported current-bytecode opcode {op} at pc={pc - 1}")
    raise ExecutionError("selected function ended without a return")
