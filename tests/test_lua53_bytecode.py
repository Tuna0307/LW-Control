import struct
import unittest

from tools.inspect_lua53_bytecode import (
    Lua53Error,
    decode_instruction,
    inspect_chunk,
    parse_lua53_chunk,
)


def minimal_last_war_chunk() -> bytes:
    data = bytearray()
    data += b"\x1bLua"
    data += bytes((0x53, 1))
    data += b"\x19\x93\r\n\x1a\n"
    data += bytes((4, 4, 8, 8))
    data += (0x5678).to_bytes(8, "little", signed=True)
    data += struct.pack("<d", 370.5)
    data += b"\x00"  # main upvalue count
    data += b"\x00"  # inherited source
    data += struct.pack("<ii", 0, 0)
    data += bytes((0, 0, 2))  # params, vararg, max stack
    for _ in range(7):
        data += struct.pack("<i", 0)  # code/constants/upvalues/children/lines/locals/upvalue names
    return bytes(data)


class Lua53BytecodeChecks(unittest.TestCase):
    def test_parses_last_war_format_one_header(self):
        header, prototype = parse_lua53_chunk(minimal_last_war_chunk())
        self.assertEqual(header["version"], 0x53)
        self.assertEqual(header["format"], 1)
        self.assertEqual(header["instruction_size"], 4)
        self.assertEqual(header["endianness"], "little")
        self.assertEqual(prototype.instructions, [])
        self.assertEqual(prototype.constants, [])

    def test_decodes_known_loadk_word(self):
        # LOADK A=2, Bx=10 from the recovered DailyQuestReward call site.
        row = decode_instruction(0x00028081)
        self.assertEqual(row["name"], "LOADK")
        self.assertEqual(row["A"], 2)
        self.assertEqual(row["Bx"], 10)

    def test_rejects_non_lua_chunk(self):
        with self.assertRaises(Lua53Error):
            parse_lua53_chunk(b"not lua")

    def test_exact_prototype_selector_returns_full_body_and_rejects_missing_path(self):
        result = inspect_chunk(minimal_last_war_chunk(), prototype_path="0")
        self.assertEqual(result["prototype_count"], 1)
        self.assertEqual(result["prototypes"][0]["path"], "0")
        self.assertEqual(result["prototypes"][0]["instructions"], [])
        self.assertEqual(result["prototypes"][0]["constants"], [])
        with self.assertRaises(Lua53Error):
            inspect_chunk(minimal_last_war_chunk(), prototype_path="0.10")


if __name__ == "__main__":
    unittest.main()
