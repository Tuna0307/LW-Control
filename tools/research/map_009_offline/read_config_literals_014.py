"""Read generated v24 config literals without executing any game code.

Only literal table-building instructions are accepted. No calls, globals,
branches, closures, files or native runtime can be used by this decoder.
"""
from __future__ import annotations
import compact_lua53 as lua


def read_literals(proto: dict) -> dict:
    if proto["children"]:
        raise ValueError("config contains executable closures")
    registers = [None] * 256
    constants = proto["constants"]
    for pc, word in enumerate(proto["code"]):
        op = lua.opcode(word)
        a, b, c = lua.instruction_a(word), lua.instruction_b(word), lua.instruction_c(word)
        def rk(v):
            return constants[v & 255] if v & 256 else registers[v]
        if op == "NEWTABLE":
            registers[a] = {}
        elif op == "LOADK":
            registers[a] = constants[lua.instruction_bx(word)]
        elif op == "LOADBOOL" and c == 0:
            registers[a] = b != 0
        elif op == "LOADNIL":
            for i in range(a, a + b + 1):
                registers[i] = None
        elif op == "SETTABLE":
            if not isinstance(registers[a], dict):
                raise ValueError("config assignment is not a literal table")
            registers[a][rk(b)] = rk(c)
        elif op == "SETLIST" and b > 0 and c > 0:
            if not isinstance(registers[a], dict):
                raise ValueError("config list is not a literal table")
            for i in range(1, b + 1):
                registers[a][(c - 1) * 50 + i] = registers[a + i]
        elif op == "RETURN" and b == 2:
            tail = proto["code"][pc + 1:]
            if tail and not (len(tail) == 1 and lua.opcode(tail[0]) == "RETURN"
                             and lua.instruction_b(tail[0]) == 1):
                raise ValueError("unexpected executable config return tail")
            result = registers[a]
            if not isinstance(result, dict):
                raise ValueError("config result is not a literal table")
            return result
        else:
            raise ValueError(f"non-literal or unsupported config instruction: {op}")
    raise ValueError("config has no final literal return")
