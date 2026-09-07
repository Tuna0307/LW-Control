import unittest

from tools.inspect_lenc_contract import (
    LencContractError,
    select_rid_mod8_candidate,
    static_field_access_sites,
)


class LencContractChecks(unittest.TestCase):
    def test_selects_unique_current_key_residue(self):
        candidates = {1467: b"a", 1469: b"b", 1470: b"c", 1472: b"key", 1478: b"d"}
        rid, value = select_rid_mod8_candidate(0x679F3862, candidates)
        self.assertEqual(rid, 1472)
        self.assertEqual(value, b"key")

    def test_rejects_ambiguous_residue(self):
        with self.assertRaises(LencContractError):
            select_rid_mod8_candidate(0x679F3862, {1472: b"a", 1480: b"b"})

    def test_static_field_access_sites_are_opcode_specific(self):
        token = 0x679F9902
        encoded = token.to_bytes(4, "little")
        data = b"xx" + b"\x7e" + encoded + b"yy" + b"\x80" + encoded + b"zz"
        sites = static_field_access_sites(data, 2, len(data) - 4, token)
        self.assertEqual(sites["ldsfld"], [2])
        self.assertEqual(sites["ldsflda"], [])
        self.assertEqual(sites["stsfld"], [9])


if __name__ == "__main__":
    unittest.main()
