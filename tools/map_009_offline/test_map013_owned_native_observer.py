"""Inert MAP-013 diagnostic admission/handler regression; never launches a game."""
from __future__ import annotations

import hashlib
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

HERE = Path(__file__).resolve().parent
TOOLS = HERE.parent
sys.path.insert(0, str(TOOLS))
import run_overview_bridge_current as bridge


class Map013ObserverSourceTests(unittest.TestCase):
    def test_no_opt_in_is_bit_identical_to_current_bridge(self):
        with patch.dict(os.environ, {}, clear=False):
            os.environ.pop("LWBRIDGE_MAP013_PRIVATE_TRACE", None)
            os.environ.pop("LWBRIDGE_MAP013_WRAPPER_TRACE", None)
            original_probe = bridge.base.packaged_resource_probe_source()
            original_wrapper = bridge.base.wrapper_source()
        self.assertEqual(hashlib.sha256(original_probe).hexdigest(),
                         "8c6e150c7c90ccabc196a5d9059b8c81c32bb15f3ae9096d650a90e287e84668")
        self.assertEqual(hashlib.sha256(original_wrapper).hexdigest(),
                         "d7441383fc34a096357a43eb7b6dde396df421302a1ebf105cdb3e56e3793708")

    def test_exact_private_wrapper_only_patches_diagnostic_candidate(self):
        with tempfile.TemporaryDirectory(prefix="map013-wrapper-test-") as temporary:
            root = Path(temporary).resolve()
            source = root / "map013-wrapper-trace.lua"
            source.write_bytes((HERE / "map013_wrapper_trace.lua").read_bytes())
            with patch.dict(os.environ, {
                "LWBRIDGE_REBUILD_DATA_ROOT": str(root),
                "LWBRIDGE_MAP013_WRAPPER_TRACE": str(source),
            }):
                os.environ.pop("LWBRIDGE_MAP013_PRIVATE_TRACE", None)
                candidate_wrapper = bridge.base.wrapper_source()
                candidate_probe = bridge.base.packaged_resource_probe_source()
            self.assertIn(b"_trace_original_bridge_pump", candidate_wrapper)
            self.assertIn(b"GameEntry", candidate_wrapper)
            self.assertEqual(hashlib.sha256(candidate_probe).hexdigest(),
                             "8c6e150c7c90ccabc196a5d9059b8c81c32bb15f3ae9096d650a90e287e84668")
            self.assertLess(len(bridge.base.lr.encode_lenc(candidate_wrapper)), 65536)

    def test_foreign_path_and_missing_isolation_fail_closed(self):
        with tempfile.TemporaryDirectory(prefix="map013-wrapper-test-") as temporary:
            root = Path(temporary).resolve()
            source = root / "not-allowed.lua"
            source.write_text("return {}", encoding="utf-8")
            with patch.dict(os.environ, {
                "LWBRIDGE_REBUILD_DATA_ROOT": str(root),
                "LWBRIDGE_MAP013_WRAPPER_TRACE": str(source),
            }):
                with self.assertRaisesRegex(bridge.base.OverviewBridgeError, "exact private file"):
                    bridge.base.wrapper_source()
            with patch.dict(os.environ, {
                "LWBRIDGE_MAP013_WRAPPER_TRACE": str(source),
                "LWBRIDGE_REBUILD_DATA_ROOT": "",
            }):
                with self.assertRaisesRegex(bridge.base.OverviewBridgeError, "private root"):
                    bridge.base.wrapper_source()

    def test_diagnostic_lua_parses_without_mutating_game(self):
        from luaparser import ast
        ast.parse((HERE / "map013_wrapper_trace.lua").read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
