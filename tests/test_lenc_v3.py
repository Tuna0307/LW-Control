import unittest

from tools.extract_lenc_v3 import (
    EXPECTED_KEY,
    EXPECTED_NONCE,
    LencV3Error,
    chacha8_core_no_feedforward_block,
    decode_lenc_bytes,
    derive_secret_from_tables,
    transform_payload,
)


TABLE_A = bytes.fromhex(
    "bfc69b79c50ce3a34a0724392e62928f2837cd899bfce35b11cbcbaecb6beafe"
    "2ccf031ad27d7ff09f8fb111"
)
TABLE_B = bytes.fromhex(
    "56d02627c4091c751b4b82e93f1571120edd07ffb66058d28aa1d7716f9fd8ab"
    "af912230d1384fc927b1534b"
)
OFFICIAL_PAYLOAD_PREFIX = bytes.fromhex(
    "39a8e6d028e94750c40eeb92b24f6696453d12a4194ead8473560dcec610d7f3"
    "a9f59bc6f12b7719c3b62b9b1d18e22855de2aed522a48f9466e89f2806782e4"
)
EXPECTED_TRANSFORMED_PREFIX = bytes.fromhex(
    "78da8556dd4e1b47149ef18eb15903a1ada2368902558aa2364a4da5b68a9256"
    "ea6e08b85486fe9090aab2b45adb03322cbb66775db0daa6639c402e72d737a8"
)


class LencV3Checks(unittest.TestCase):
    def test_derives_current_native_key_and_nonce(self):
        key, nonce = derive_secret_from_tables(TABLE_A, TABLE_B)
        self.assertEqual(key, EXPECTED_KEY)
        self.assertEqual(nonce, EXPECTED_NONCE)

    def test_reproduces_official_first_native_transform_block(self):
        transformed = transform_payload(OFFICIAL_PAYLOAD_PREFIX, EXPECTED_KEY, EXPECTED_NONCE)
        self.assertEqual(transformed, EXPECTED_TRANSFORMED_PREFIX)
        self.assertEqual(transformed[:2], b"\x78\xda")

    def test_native_block_is_not_standard_chacha_feed_forward(self):
        block = chacha8_core_no_feedforward_block(EXPECTED_KEY, EXPECTED_NONCE, 0)
        self.assertEqual(len(block), 64)
        standard_first_word = (
            int.from_bytes(block[:4], "little") + int.from_bytes(b"expa", "little")
        ) & 0xFFFFFFFF
        self.assertNotEqual(int.from_bytes(block[:4], "little"), standard_first_word)

    def test_decodes_synthetic_zlib_entry(self):
        import zlib

        plain = b"\x1bLuaS\x01 synthetic loader fixture"
        compressed = zlib.compress(plain, 9)
        # The native loader inflates only the 78 DA zlib form.
        self.assertEqual(compressed[:2], b"\x78\xda")
        entry = b"LENC" + transform_payload(compressed, EXPECTED_KEY, EXPECTED_NONCE)
        result = decode_lenc_bytes(entry, EXPECTED_KEY, EXPECTED_NONCE)
        self.assertTrue(result["zlib_inflated"])
        self.assertEqual(result["decoded"], plain)

    def test_rejects_non_lenc_entry(self):
        with self.assertRaises(LencV3Error):
            decode_lenc_bytes(b"NOPE", EXPECTED_KEY, EXPECTED_NONCE)


if __name__ == "__main__":
    unittest.main()
