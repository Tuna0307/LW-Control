"""MAP-012: real decoded v24 detail cache body, inert remote-source adversities.

This proves how the *client cache* keys native detail replies. It deliberately
does not prove source-server identity, remote acceptance or network delivery.
"""
import unittest

import compact_lua53 as lua
import execute_selected as runner
from inspect_current import load_entries, probe, root_methods, disassemble


MODULE = "DataCenter/WorldPointDetail/WorldPointDetailManager.luac"
DATA = "DataCenter/WorldPointDetail/WorldPointDetailData.luac"


class RemoteDetailSource012Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        entries = load_entries(probe.paths()["data"])  # SHA-locked genuine v24
        cls.manager = lua.parse_chunk(probe.decode_lenc(entries[MODULE]))
        cls.data = lua.parse_chunk(probe.decode_lenc(entries[DATA]))

    @staticmethod
    def method(root, name):
        path = root_methods(root)[name].split("/")[1:]
        for index in path:
            root = root["children"][int(index)]
        return root

    def test_real_cache_update_and_lookup_use_point_id_not_server_id(self):
        updater = self.method(self.manager, "UpdateDetail")
        lookup = self.method(self.manager, "GetDetailByPointId")
        cache = {"worldPointDetailList": {}}

        def create():
            return {"ParseData": lambda self, msg: self.update(
                pointId=msg["pointId"], serverId=msg["scrServer"],
                level=msg["level"])}

        native = {"WorldPointDetailData": {"New": create}}
        first = {"pointId": 573551, "scrServer": 2212, "level": 10}
        second = {"pointId": 573551, "scrServer": 2213, "level": 11}
        first_value, = runner.execute(updater, cache, first, globals_=native)
        self.assertIs(runner.execute(lookup, cache, 573551)[0], first_value)
        # A different server's response with the same pointID overwrites it.
        # A match on this key alone therefore CANNOT attribute R12's response.
        second_value, = runner.execute(updater, cache, second, globals_=native)
        self.assertIsNot(first_value, second_value)
        self.assertEqual(cache["worldPointDetailList"][573551]["serverId"], 2213)
        self.assertIs(runner.execute(lookup, cache, 573551)[0], second_value)
        self.assertEqual(len(cache["worldPointDetailList"]), 1)

    def test_real_detail_parser_maps_native_scr_server_only_when_present(self):
        parser = self.method(self.data, "ParseData")
        source = disassemble(parser)
        pos = next(i for i, line in enumerate(source) if "'scrServer'" in line)
        nearby = "\n".join(source[pos:pos+5])
        self.assertIn("GETTABLE", source[pos])
        self.assertIn("'serverId'", nearby)
        self.assertIn("SETTABLE", nearby)
        # No source evidence that the outgoing requested serverId is copied
        # into a reply. R12's native zero is not a valid remote provenance.


if __name__ == "__main__":
    unittest.main()
