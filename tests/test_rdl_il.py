import unittest

from tools.rdl_il import (
    HEADER_FORMAT_BIT,
    RdlMethodBodyError,
    instruction_rows,
    parse_method_body_bytes,
    repair_method_header_copy,
)


class RdlIlChecks(unittest.TestCase):
    def test_repairs_tiny_header_without_mutating_input(self):
        original = bytes([0x08, 0x16, 0x2A])
        repaired = repair_method_header_copy(original)
        self.assertEqual(original, bytes([0x08, 0x16, 0x2A]))
        self.assertEqual(repaired[0], original[0] | HEADER_FORMAT_BIT)

    def test_parses_transformed_tiny_boolean_method(self):
        parsed = parse_method_body_bytes(bytes([0x08, 0x16, 0x2A]), file_offset=0x2DD0)
        self.assertEqual(parsed.original_header_byte, 0x08)
        self.assertEqual(parsed.repaired_header_byte, 0x0A)
        self.assertEqual(parsed.code_size, 2)
        rows = instruction_rows(parsed)
        self.assertEqual([row["opcode"] for row in rows], ["ldc.i4.0", "ret"])

    def test_parses_transformed_fat_boolean_method(self):
        transformed = bytes.fromhex(
            "11 30 08 00 02 00 00 00 00 00 00 00 16 2A"
        )
        parsed = parse_method_body_bytes(transformed, file_offset=0x1000)
        self.assertEqual(parsed.original_header_byte, 0x11)
        self.assertEqual(parsed.repaired_header_byte, 0x13)
        self.assertEqual(parsed.header_size, 12)
        self.assertEqual(parsed.code_size, 2)
        self.assertEqual(
            [row["opcode"] for row in instruction_rows(parsed)],
            ["ldc.i4.0", "ret"],
        )

    def test_rejects_empty_body(self):
        with self.assertRaises(RdlMethodBodyError):
            parse_method_body_bytes(b"")


if __name__ == "__main__":
    unittest.main()
