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
sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "src" / "LWBridge.GameScripts"))
import run_overview_bridge_current as bridge


class Map013ObserverSourceTests(unittest.TestCase):
    # MAP-013's original fingerprint was
    # 8c6e150c7c90ccabc196a5d9059b8c81c32bb15f3ae9096d650a90e287e84668.
    # MAP-014 captures its changed production source exactly; the installed game
    # identity gate in inspect_current.py remains unchanged.
    # Fingerprint of packaged_resource_probe_source() after MAP014's
    # standard-projection camera restoration fix. This intentionally remains
    # a pinned digest, not one generated from the source under test.
    # Previous projection-only source: a3b86fae706636ba02180a5533450ea2c7abe1af653a921c84ff464043cea633.
    # Reviewed MAP014 lens admission and native canonical-owner accounting:
    # 5af48655ef47f7ad0deb34b40c160266a8621ac9ba139cbdc36450612a388357.
    # New source adds a strictly opt-in read-only March manager snapshot at
    # point acquisition; the installed game script identity gate is unchanged.
    # Initial passive march counts-only prototype:
    # 2e15f58ec51225e98cbb17a16dbdaca395924a008221d6a87d3b7448ad2f814b.
    # This revision additionally captures exact native March UUID identities
    # to distinguish source-manager membership from point-AOI row filtering.
    # First live telemetry negative exposed a Lua details-table replacement
    # dropping the pre-camera March state. Preserve its historical digest:
    # f392736541feb92d6155132626af8a925dfb5090220c9ca603e1acee47767650.
    # Added optional native DateTime.Ticks alongside diagnostic manager strings:
    # previous approved snapshot 7e3e7499eb3e6e70af8635af14414ba7dd91383d09992a38227315c4cf54d109.
    # The first point-manager ready frame is now sampled *before* native
    # Train/Resource enrichment; previous hash was 4f268b9fb4adfee84a3f66b3cc75a52f9f09dd66ec4ed321711c77a6f0995e11.
    # Previous pin (before the R30 probe updates): df8325d3594c0268389db527983ab2250440bae99e675378fca1192ab86fe645.
    CURRENT_PROBE_SHA256 = "cf1054d46d626930227bb1a869b085c62d0aeb4e5a8638ecfcb8a0d32e909abf"
    # Historical original wrapper SHA: d7441383fc34a096357a43eb7b6dde396df421302a1ebf105cdb3e56e3793708.
    # Follow receipts now carry actual WorldMarch position source and current
    # native camera tile. This is the separately source-pinned updated wrapper.
    # Previous pin: 5460b83a66eccf9924a8d043453b3414dbb6c2175a4116ee8f8c39ad8db61304.
    # Pin before the v1.2 wrapper (released 2026-10-27, unchanged by the cleanup):
    # ae9f233c34643cb69f84747e687d4706ddd822ddb7386f01c577f0fca92b0b4d.
    CURRENT_WRAPPER_SHA256 = "1c82b20098406bc535ba6838e9c5e032eff6dc45e5131172d1d7d6bdd1a25ce5"
    def test_no_opt_in_is_bit_identical_to_current_bridge(self):
        with patch.dict(os.environ, {}, clear=False):
            os.environ.pop("LWBRIDGE_MAP013_PRIVATE_TRACE", None)
            os.environ.pop("LWBRIDGE_MAP013_WRAPPER_TRACE", None)
            original_probe = bridge.base.packaged_resource_probe_source()
            original_wrapper = bridge.base.wrapper_source()
        self.assertEqual(hashlib.sha256(original_probe).hexdigest(),
                         self.CURRENT_PROBE_SHA256)
        self.assertEqual(hashlib.sha256(original_wrapper).hexdigest(),
                         self.CURRENT_WRAPPER_SHA256)

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
                             self.CURRENT_PROBE_SHA256)
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

    def test_bootstrap_module_below_last_native_ready_envelope_with_identical_lua_ast(self):
        from luaparser import ast
        raw_lines = bridge.base.resource_probe_source().decode("utf-8").splitlines()
        pre_fix_source = "\n".join(line.rstrip() for line in raw_lines
                                  if line.strip() and not line.lstrip().startswith("--")) + "\n"
        packaged_source = bridge.base.packaged_resource_probe_source()
        # Compact solely to save script bytes. The actual failed native
        # bootstrap was caused by exceeding the Lua top-level LOCAL limit,
        # not by a proven 58-KiB resource loader threshold.
        pre_fix_bytes = len(bridge.base.lr.encode_lenc(pre_fix_source.encode()))
        compact_bytes = len(bridge.base.lr.encode_lenc(packaged_source))
        self.assertGreater(pre_fix_bytes, compact_bytes)
        # The game rejects packaged Lua modules of 65,536 bytes or more.
        self.assertLess(compact_bytes, 65536)
        self.assertEqual(ast.to_lua_source(ast.parse(pre_fix_source)),
                         ast.to_lua_source(ast.parse(packaged_source.decode("utf-8"))))

    def test_native_probe_never_exceeds_lua53_active_chunk_locals(self):
        from luaparser import ast
        tree = ast.parse(bridge.base.resource_probe_source().decode("utf-8"))
        top_level_locals = sum(len(node.targets) if type(node).__name__ == "LocalAssign" else 1
                               for node in tree.body.body
                               if type(node).__name__ in ("LocalAssign", "LocalFunction"))
        # Lua 5.3 allows at most 200 active locals per function; the first MAP-014
        # live failures had 206 and the module did not compile. The probe is at 200.
        self.assertLessEqual(top_level_locals, 200)

    def test_independent_camera_checkpoint_is_readonly_and_owner_gated(self):
        source = bridge.base.resource_probe_source().decode("utf8")
        self.assertIn('request.mode ~= "camera_checkpoint"', source)
        self.assertIn('request.mode == "camera_checkpoint"', source)
        section = source.split('if request.mode == "camera_checkpoint" then', 1)[1].split(
            'if request.mode == "resource_inventory" then', 1)[0]
        self.assertIn("runtime_world()", section)
        self.assertIn("M._map014CameraGeometryCheckpoint(manager, unity_camera)", section)
        self.assertIn('"camera_geometry_readonly_unavailable"', section)
        self.assertIn('source = "WorldScene.Camera_UnityCamera_readonly"', section)
        self.assertNotIn("SetCameraPos", section)
        self.assertNotIn("UpdateViewRequest", section)
        self.assertNotIn("SendAoiRequest", section)
        self.assertLess(source.index('local identity, identity_error = active_overview_identity(now)',
                                     source.index('local function read_runtime_diagnostic(now)')),
                        source.index('if request.mode == "camera_checkpoint" then'))
        self.assertIn(b'registrationMethod = registration_method',
                      bridge.base.resource_probe_source())

    def test_late_update_registration_retries_both_native_producers(self):
        wrapper = bridge.base.wrapper_source()
        original_register = b'if type(bridge.Register) == "function" then pcall(bridge.Register) end'
        probe_register = b'if type(probe.Register) == "function" then pcall(probe.Register) end'
        original_pump = b'if type(bridge.TryPump) == "function" then pcall(bridge.TryPump) end'
        probe_pump = b'if type(probe.Pump) == "function" then'
        self.assertEqual(wrapper.count(probe_register), 1)
        self.assertTrue(0 <= wrapper.index(original_register) <
                        wrapper.index(probe_register) <
                        wrapper.index(original_pump) <
                        wrapper.index(probe_pump))
        self.assertIn(b'if registration_method ~= nil then return true end',
                      bridge.base.resource_probe_source())


if __name__ == "__main__":
    unittest.main()
