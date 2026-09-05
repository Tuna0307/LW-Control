import unittest

from tools.rdl_il import (
    HEADER_FORMAT_BIT,
    RdlMethodBodyError,
    decode_metadata_token,
    encode_metadata_token,
    encoded_field_token_rid_mod8,
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

    def test_decodes_current_field_token_rid_mod8(self):
        known = {
            0x679F0E42: 1459,
            0x679F0EE2: 1460,
            0x679F0E82: 1461,
            0x679F0E22: 1462,
            0x679F0EC2: 1463,
            0x679F09E2: 1468,
            0x679F0922: 1470,
            0x679F3802: 1473,
            0x679F3BA2: 1474,
            0x679F38C2: 1479,
            0x679F3B62: 1480,
        }
        for token, rid in known.items():
            self.assertEqual(encoded_field_token_rid_mod8(token), rid % 8)

    def test_rejects_token_that_decodes_to_non_field_table(self):
        with self.assertRaises(RdlMethodBodyError):
            encoded_field_token_rid_mod8(0x679F3863)

    def test_full_metadata_token_decoder_matches_recovered_cross_table_pairs(self):
        pairs = {
            0x679F3862: 0x040005C0,
            0x679F9902: 0x04000039,
            0xA79F9142: 0x0600007B,
            0xA79F9182: 0x0600007D,
            0x279FBD03: 0x0A000119,
            0x879F9A82: 0x010000D5,
            0xE79C4850: 0x70000243,
        }
        for encoded, decoded in pairs.items():
            with self.subTest(encoded=f"0x{encoded:08X}"):
                self.assertEqual(decode_metadata_token(encoded), decoded)
                self.assertEqual(encode_metadata_token(decoded), encoded)

    def test_rejects_empty_body(self):
        with self.assertRaises(RdlMethodBodyError):
            parse_method_body_bytes(b"")


if __name__ == "__main__":
    unittest.main()
