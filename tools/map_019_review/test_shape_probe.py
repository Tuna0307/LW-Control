"""Private opt-in transform seam/syntax tests, not native proof."""
from pathlib import Path
import sys
import unittest
from shape_probe import instrument

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'artifacts/map-013-lua-site'))
from lupa.lua53 import LuaRuntime


class ShapeProbeChecks(unittest.TestCase):
    def test_current_source_is_preserved_and_private_copy_compiles(self):
        source=(ROOT/'tools/current_live_resource_probe.lua').read_bytes()
        private=instrument(source)
        self.assertNotEqual(private,source)
        self.assertEqual(source,(ROOT/'tools/current_live_resource_probe.lua').read_bytes())
        result=LuaRuntime().eval('load')(private.decode('utf-8'),'private-map019','t')
        self.assertFalse(isinstance(result,tuple),result)
        self.assertIsNotNone(result)

    def test_missing_or_duplicate_source_seam_refuses_instrumentation(self):
        source=(ROOT/'tools/current_live_resource_probe.lua').read_bytes()
        for changed in (source.replace(b's.nextAoi + 159',b's.nextAoi + 158'),
                        source+b'\n-- math.min(9999, s.nextAoi + 159)'):
            with self.assertRaises(ValueError): instrument(changed)


if __name__=='__main__': unittest.main()
