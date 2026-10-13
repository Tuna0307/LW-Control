"""Guard the distinct private subscription-order experiment only."""
import sys
import unittest
from pathlib import Path

TOOLS=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(TOOLS))
from luaparser import ast
import run_overview_bridge as bridge
from map_009_offline.native_reply_instrument_013 import instrument_native_reply
from map_009_offline.sequence_instrument_013 import instrument_sequence
from map_009_offline.sequence_post_lens_014 import instrument_post_lens_refresh


class PostLensSource(unittest.TestCase):
    def test_exact_supported_source_and_original_unchanged(self):
        original=bridge.resource_probe_source()
        modified=instrument_post_lens_refresh(instrument_sequence(instrument_native_reply(original)))
        ast.parse(modified.decode("utf-8"))
        marker=modified.index(b'map014_post_lens_subscription_failed')
        self.assertLess(modified.index(b'SetFOV", 120.0'), marker)
        self.assertLess(marker, modified.index(b'local manager_reply_before = manager_response_flag(point_manager)'))
        self.assertEqual(original,bridge.resource_probe_source())
        packed=b"\n".join(l.rstrip() for l in modified.splitlines() if l.strip()
                         and not l.lstrip().startswith(b"--"))+b"\n"
        self.assertLess(len(bridge.lr.encode_lenc(packed)),65536)

    def test_drift_fails_closed(self):
        with self.assertRaisesRegex(ValueError,"anchor/ownership drift"):
            instrument_post_lens_refresh(bridge.resource_probe_source())


if __name__=="__main__":
    unittest.main()
