"""Current production Lua packaging stays semantically identical and loadable."""
import unittest
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'artifacts/map-013-lua-site'))
sys.path.insert(0, str(ROOT / 'src/LWBridge.GameScripts'))
import run_overview_bridge as bridge


class CurrentProbePackaging(unittest.TestCase):
    def test_current_probe_ast_literals_loader_limit_and_roundtrip(self):
        from luaparser import ast
        lines = bridge.resource_probe_source().decode('utf-8').splitlines()
        baseline = '\n'.join(line.strip() for line in lines
            if line.strip() and not line.lstrip().startswith('--')) + '\n'
        packaged = bridge.packaged_resource_probe_source()
        self.assertEqual(ast.to_lua_source(ast.parse(baseline)),
                         ast.to_lua_source(ast.parse(packaged.decode('utf-8'))))
        encoded = bridge.lr.encode_lenc(packaged)
        self.assertLessEqual(len(encoded), 0xFFFF)
        self.assertEqual(bridge.lr.decode_lenc(encoded), packaged)


if __name__ == '__main__':
    unittest.main()
