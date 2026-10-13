#!/usr/bin/env python3
"""Run selected CURRENT v24 compact Lua 5.3 bodies with inert test callbacks.

No game process or network connection is created. Inverse cases alter source
conditions/arguments, so a substring-only source checker could not pass them.
"""
from __future__ import annotations

import unittest
from pathlib import Path

import compact_lua53 as lua
import execute_selected as runner
from inspect_current import load_entries, probe, root_methods


class CurrentBodyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.entries = load_entries(probe.paths()["data"])
        cls.cache = {}

    @classmethod
    def body(cls, module, method):
        if module not in cls.cache:
            cls.cache[module] = lua.parse_chunk(probe.decode_lenc(cls.entries[module]))
        root = cls.cache[module]
        ref = root_methods(root).get(method, method)
        path = ref.split("/")[1:]
        body = root
        for index in path:
            body = body["children"][int(index)]
        return body

    def test_world_position_roundtrips_real_bytecode(self):
        encoder = self.body("Util/SceneUtils.luac", "EncodeWorldPos")
        decoder = self.body("Util/SceneUtils.luac", "DecodeWorldPos")
        for x, y in ((0, 0), (1, 1), (550, 573), (999, 999), (1000, 0), (2147483647, 4294967295)):
            with self.subTest(x=x, y=y):
                encoded, = runner.execute(encoder, x, y)
                self.assertEqual(encoded & runner.MASK64, x << 32 | y)
                self.assertEqual(runner.execute(decoder, encoded), (x, y))
        self.assertEqual(runner.execute(encoder, None, 1), (0,))
        self.assertEqual(runner.execute(decoder, None), (0, 0))
        # Inverse: y is NOT the high half, x is NOT the low half.
        self.assertNotEqual(runner.execute(encoder, 550, 573), runner.execute(encoder, 573, 550))

    def test_world_vs_city_index_and_boundary_real_bytecode(self):
        to_index = self.body("Util/SceneUtils.luac", "TileXYToIndex")
        from_index = self.body("Util/SceneUtils.luac", "IndexToTilePos")
        globals_ = {"WorldTileCount": 1000, "CityTileCount": 50, "ForceChangeScene": {"World": "world"},
                    "math": {"floor": lambda n: int(n // 1)}, "Logger": {"LogInfo": lambda *args: None},
                    "string": {"format": lambda *args: str(args)}}
        upvalues = {1: lambda: False, 2: lambda: {}}
        for x, y in ((0, 0), (550, 573), (999, 999)):
            with self.subTest(x=x, y=y):
                idx, = runner.execute(to_index, x, y, "world", globals_=globals_, upvalues=upvalues)
                self.assertEqual(idx, y * 1000 + x + 1)
                pos, = runner.execute(from_index, idx, "world", globals_=globals_, upvalues=upvalues)
                self.assertEqual(pos, {"x": x, "y": y})
        for x, y in ((-1, 10), (0, -1), (1000, 0), (0, 1000)):
            self.assertEqual(runner.execute(to_index, x, y, "world", globals_=globals_, upvalues=upvalues), (0,))
        for x, y in ((0, 0), (25, 49), (49, 49)):
            idx, = runner.execute(to_index, x, y, "city", globals_=globals_, upvalues=upvalues)
            self.assertEqual(idx, y * 50 + x + 1)
            self.assertEqual(runner.execute(from_index, idx, "city", globals_=globals_, upvalues=upvalues), ({"x": x, "y": y},))
        self.assertEqual(runner.execute(to_index, 550, 573, "city", globals_=globals_, upvalues=upvalues), (0,))
        self.assertEqual(runner.execute(from_index, 0, "world", globals_=globals_, upvalues=upvalues), ({"x": 0, "y": 0},))

    def test_known_railway_march_request_fields_real_bytecode(self):
        method = self.body("Net/Msgs/Railway/GetMarchPosMessage.luac", "OnCreate")
        recorded = []
        sfs = {"PutInt": lambda self, key, value: recorded.append(("int", key, value)),
               "PutLong": lambda self, key, value: recorded.append(("long", key, value))}
        instance = {"sfsObj": sfs}
        self.assertEqual(runner.execute(method, instance, 2172, 4, 987654321, 6,
                                        globals_={"OnCreate": lambda self: None}), ())
        self.assertEqual(recorded, [("int", "serverId", 2172), ("int", "worldId", 4),
                                    ("long", "marchUuid", 987654321), ("int", "marchType", 6)])
        recorded.clear()
        runner.execute(method, instance, 2231, None, 10001, None, globals_={"OnCreate": lambda self: None})
        self.assertEqual(recorded, [("int", "serverId", 2231), ("int", "worldId", 0),
                                    ("long", "marchUuid", 10001), ("int", "marchType", 2)])
        # Inverse: selecting a different server modifies exactly the serialized selector.
        recorded.clear()
        runner.execute(method, instance, 2172, None, 10001, None, globals_={"OnCreate": lambda self: None})
        self.assertEqual(recorded[0], ("int", "serverId", 2172))

    def test_known_point_detail_request_fields_real_bytecode(self):
        method = self.body("Net/Msgs/WorldGetDetailMessage.luac", "OnCreate")
        recorded = []
        sfs = {"PutInt": lambda self, key, value: recorded.append(("int", key, value)),
               "PutUtfString": lambda self, key, value: recorded.append(("utf", key, value))}
        instance = {"sfsObj": sfs}
        # Request includes point index and target server/world selector; one known point only.
        runner.execute(method, instance, 573551, 2172, 0, None, None,
                       globals_={"OnCreate": lambda self: None, "tostring": str})
        self.assertIn(("int", "point", 573551), recorded)
        self.assertIn(("int", "serverId", 2172), recorded)
        self.assertIn(("int", "worldId", 0), recorded)
        recorded.clear()
        runner.execute(method, instance, 573551, 2173, 0, None, None,
                       globals_={"OnCreate": lambda self: None, "tostring": str})
        self.assertIn(("int", "serverId", 2173), recorded)


if __name__ == "__main__":
    unittest.main()
