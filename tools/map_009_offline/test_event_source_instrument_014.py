"""Source lock and Lua5.3 syntax/packaging; no simulated native positives."""
import unittest
from pathlib import Path
import sys

BASE = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(BASE))
import run_overview_bridge as bridge
from luaparser import ast
from map_009_offline.native_reply_instrument_013 import instrument_native_reply
from map_009_offline.event_source_instrument_014 import instrument_event_source


class EventSourcePrivateObserver(unittest.TestCase):
    def test_admitted_read_only_source_extension_and_original_unchanged(self):
        original = bridge.resource_probe_source()
        modified = instrument_event_source(instrument_native_reply(original))
        ast.parse(modified.decode("utf-8"))
        self.assertIn(b"theaterCollectionType = type(theater)", modified)
        self.assertIn(b"nativeSuppliesType27Loaded = observed_supplies", modified)
        self.assertEqual(original, bridge.resource_probe_source())
        packed = b"\n".join(line.rstrip() for line in modified.splitlines()
                             if line.strip() and not line.lstrip().startswith(b"--")) + b"\n"
        self.assertLess(len(bridge.lr.encode_lenc(packed)), 65536)

    def test_changed_native_source_anchor_rejected(self):
        body = instrument_native_reply(bridge.resource_probe_source())
        with self.assertRaisesRegex(ValueError, "anchor drift"):
            instrument_event_source(body.replace(
                b"details.doomsdayBossCount = 0", b"details.doomsdayBossCount = 99"))


if __name__=="__main__":
    unittest.main()
