"""Inert checks of *actual Lua* WorldReady boundaries, not a live packet test."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import sys
import tempfile
import time
import unittest
from unittest.mock import patch

from luaparser import ast

TOOLS = Path(__file__).resolve().parents[1]
ROOT = TOOLS.parents[1]
sys.path.insert(0, str(TOOLS))
sys.path.insert(0, str(ROOT / 'src' / 'LWBridge.GameScripts'))
import run_overview_bridge_current as c
from map_009_offline.worldready_instrument_013 import (
    instrument_worldready, WorldReadyInstrumentationError,
)
from map_009_offline.run_map013_owned_observer import (
    ExpectedReply, validate_reply, snapshot_worldready_stages,
)


class WorldReadyLifecycleTests(unittest.TestCase):
    # Historical MAP-013 bridge SHA: 559c2a41c4b10925ed85f6cf8d672399f1694d24bc97db16d327ca04ea893ed0.
    # Historical probe SHA: 8c6e150c7c90ccabc196a5d9059b8c81c32bb15f3ae9096d650a90e287e84668.
    # Pre-live-MAP014 candidate probe SHA: c074605929a248cc2a1a30bd54d0585b54e758f7c034767af9806ff73c29faf2.
    # Previous native Ready wrapper SHA: 6d935536e73756cc685b0fa361e4cb2dbc45f5333a04107e3ba745d9bc341101.
    # Older bridge/wrapper/probe control pins (ea701420..., 7a4b7c02..., 716d2cb9...)
    # predate the accepted MAP-014 lifecycle and native probe changes. Retain
    # their history; the current source pins below are byte-for-byte controls.
    @classmethod
    def setUpClass(cls):
        cls.original = (ROOT / "src/LWBridge.GameScripts/current_overview_bridge.lua").read_bytes()

    def test_unmodified_wrapper_control_is_byte_identical(self):
        with patch.dict(os.environ, {
                "LWBRIDGE_MAP013_WORLD_TRACE": "",
                "LWBRIDGE_MAP013_WRAPPER_TRACE": "",
                "LWBRIDGE_MAP013_PRIVATE_TRACE": ""}):
            self.assertEqual(hashlib.sha256(c.base.bridge_source()).hexdigest(),
                             "c6b2c6f046a4f6a15f94375742ca2159247e96eee099c5b47ac440d23cef534f")
            self.assertEqual(hashlib.sha256(c.base.wrapper_source()).hexdigest(),
                             "5460b83a66eccf9924a8d043453b3414dbb6c2175a4116ee8f8c39ad8db61304")
            self.assertEqual(hashlib.sha256(c.base.packaged_resource_probe_source()).hexdigest(),
                             "5af48655ef47f7ad0deb34b40c160266a8621ac9ba139cbdc36450612a388357")

    def test_real_source_syntax_order_and_all_observation_boundaries(self):
        original = self.original.decode("utf-8").replace("\r\n", "\n")
        instrumented = instrument_worldready(self.original).decode("utf-8")
        ast.parse(instrumented)
        stages = [
            "consumption_read", "schema_or_token_rejected",
            "owner_rejected", "owner_admitted", "begin_transition",
            "already_world_scene", "transition_unavailable", "transition_invoked",
            "transition_callback_invoked", "transition_invocation_failed", "pending_transition",
            "scene_world_confirmed", "scene_transition_timeout",
            "result_written", "result_write_failed", "control_or_lease_rejected",
            "owner_lease_admitted", "shared_read_deferred", "pending_abandoned",
        ]
        for stage in stages:
            with self.subTest(stage=stage):
                if stage in ("result_written", "result_write_failed"):
                    self.assertIn('"' + stage + '"', instrumented)
                    self.assertNotIn('"' + stage + '"', original)
                else:
                    self.assertIn('_map013_world_emit("' + stage + '"', instrumented)
                    self.assertNotIn('_map013_world_emit("' + stage + '"', original)
        self.assertLess(instrumented.index('"_map013_world_log"') if '"_map013_world_log"' in instrumented
                        else 0, instrumented.index('local function read_world_ready'))
        # The real source consumes the file before owner/version checks.
        self.assertLess(instrumented.index('"command_consumed" or "command_remove_failed"'),
                        instrumented.index('_map013_world_emit("schema_or_token_rejected"'))
        self.assertIn('local _map013_removed, _map013_remove_result = pcall(os.remove, world_ready_path)', instrumented)
        self.assertIn('_map013_removed and _map013_remove_result and\n        "command_consumed" or "command_remove_failed"', instrumented)
        # For multiple same-second commands, logging is keyed by request as well as stage.
        self.assertIn('local key = stage .. ":" .. id', instrumented)
        self.assertIn('_map013_world_last[key] == now', instrumented)
        self.assertIn('string.match(id, "^[%w_-]+$") == nil', instrumented)
        self.assertIn('local written = f:write(', instrumented)
        self.assertIn('if written and closed then', instrumented)
        self.assertIn('pcall(function()\n        local now = os.time()', instrumented)
        # The original empty callback returns as before; the opt-in callback
        # records its actual invocation separately from a throwing ChangeToWorld.
        self.assertIn('pcall(change_to_world, function()\n        _map013_world_emit("transition_callback_invoked"', instrumented)
        self.assertIn('local ok_change = pcall(change_to_world, function() end)', original)
        self.assertIn('WORLD_READY_TIMEOUT_SECONDS = 10', original)
        self.assertIn('if name ~= "WorldScene" then return nil', original)
        self.assertIn('if control == nil or not fresh then', original)
        self.assertIn('if request.error ~= nil then', instrumented)
        self.assertIn('local _map013_written = write_json(world_ready_result_path', instrumented)
        # In the real M.Pump, 'unavailable' reads used to clear a live pending
        # WorldReady while 'busy' reads retained it. Extend ONLY the exact-owner
        # and <=5s verified-lease path in the opt-in diagnostic, retaining
        # the original fail-closed behavior for invalid or stale reads.
        self.assertIn('control_error == "busy" or control_error == "unavailable"', instrumented)
        self.assertIn('lease_error == "busy" or lease_error == "unavailable"', instrumented)
        self.assertIn('can_defer_shared_read(control, now) then', instrumented)
        self.assertIn('if pending_world_ready ~= nil then\n            _map013_world_emit("pending_abandoned"', instrumented)
        self.assertIn('verified_lease = nil\n        active = control\n        close_pipe_runtime()', original)
        self.assertIn('can_defer_shared_read(control, now) then return false end', original)

    def test_source_drift_rejects_instead_of_silently_omitting_stage(self):
        replaced = self.original.replace(b'local function read_world_ready(control)',
                                         b'local function something_else(control)', 1)
        with self.assertRaises(WorldReadyInstrumentationError):
            instrument_worldready(replaced)
        duplicate = self.original + b'\nlocal pending_world_ready = nil\n'
        with self.assertRaises(WorldReadyInstrumentationError):
            instrument_worldready(duplicate)

    def test_opt_in_requires_exact_scoped_wrapper_and_lua_parses(self):
        with tempfile.TemporaryDirectory(prefix="map013-world-stage-") as folder:
            root = Path(folder).resolve()
            file = root / "map013-wrapper-trace.lua"
            file.write_bytes((TOOLS / "map_009_offline/map013_wrapper_trace.lua").read_bytes())
            with patch.dict(os.environ, {
                    "LWBRIDGE_REBUILD_DATA_ROOT": str(root),
                    "LWBRIDGE_MAP013_WRAPPER_TRACE": str(file),
                    "LWBRIDGE_MAP013_WORLD_TRACE": "1"}):
                body = c.base.wrapper_source()
                ast.parse(body.decode("utf-8"))
                self.assertIn(b'"command_consumed" or "command_remove_failed"', body)
                self.assertLess(len(c.base.lr.encode_lenc(body)), 65536)
            with patch.dict(os.environ, {
                    "LWBRIDGE_REBUILD_DATA_ROOT": str(root),
                    "LWBRIDGE_MAP013_WRAPPER_TRACE": "",
                    "LWBRIDGE_MAP013_WORLD_TRACE": "1"}):
                with self.assertRaisesRegex(c.base.OverviewBridgeError, "private wrapper"):
                    c.base.bridge_source()

    def test_saved_genuine_world_result_validated_when_bound_to_its_actual_owner(self):
        # Archived production-generated WorldReady result is a bounded source
        # control, NOT a fabricated fresh AOI positive or another live run.
        path = ROOT / ("artifacts/map-013-live/native-causal-20261012-233908/"
                       "world-status.json")
        if not path.is_file():
            self.skipTest("private ignored original world receipt unavailable")
        j = json.loads(path.read_text(encoding="utf-8"))
        captured = time.mktime(time.strptime(j["capturedAt"], "%Y-%m-%dT%H:%M:%SZ"))
        expected = ExpectedReply("world", j["requestId"], j["profileId"],
                                 j["sessionId"], j["gamePid"], j["challenge"],
                                 int((captured - 1) * 1e9),
                                 server_id=2212, world_id=0)
        self.assertEqual(validate_reply(j, expected,
                                        file_mtime_ns=path.stat().st_mtime_ns), "proven")

    def test_actual_helper_source_file_and_lease_admission_snapshots(self):
        with tempfile.TemporaryDirectory(prefix="map013-stage-files-") as folder:
            root = Path(folder)
            identity = dict(profile="p", session="s", challenge="c",
                            game_pid=125, request_id="world1")
            state = snapshot_worldready_stages(root, **identity)
            self.assertFalse(state["commandFilePresent"])
            self.assertFalse(state["controlIdentityMatches"])
            self.assertFalse(state["leaseMatchesAndIsFresh"])
            values = {
                "schema": "1", "bridgeVersion": "lwbridge-overview-bridge-1",
                "profileId": "p", "sessionId": "s", "challenge": "c",
                "gamePid": "125", "requestId": "world1",
            }
            for name, data in (("world-ready.txt", values),
                               ("control.txt", values),
                               ("lease.txt", {**values,
                                   "updatedAt": str(int(time.time()))})):
                (root / name).write_text(
                    "".join(f"{k}={v}\n" for k,v in data.items()), encoding="utf-8")
            (root / "map013-worldready-lifecycle.txt").write_text(
                f"{int(time.time())}|owner_lease_admitted|-|verified\n"
                f"{int(time.time())}|consumption_read|world1|file_seen\n",
                encoding="utf-8")
            state = snapshot_worldready_stages(root, **identity)
            self.assertTrue(state["commandIdentityMatches"])
            self.assertTrue(state["controlIdentityMatches"])
            self.assertTrue(state["leaseMatchesAndIsFresh"])
            self.assertEqual([s["event"] for s in state["sourceTraceStages"]],
                             ["owner_lease_admitted", "consumption_read"])
            # Exact session, not a matching opaque request ID, is required.
            (root / "control.txt").write_text(
                "".join(f"{k}={v}\n" for k,v in {**values,
                        "sessionId": "foreign"}.items()), encoding="utf-8")
            self.assertFalse(snapshot_worldready_stages(root, **identity)["controlIdentityMatches"])
            # Raw credentials are never copied into the stage summary.
            self.assertNotIn('"challenge"', json.dumps(state))
            self.assertNotIn('"c"', json.dumps(state))

    def test_stage_snapshot_does_not_misattribute_other_request_to_target(self):
        """Inert diagnostic file: same-second stages cannot certify another request."""
        with tempfile.TemporaryDirectory(prefix="map013-world-stages-inert-") as folder:
            root = Path(folder)
            stamp = int(time.time())
            (root / "map013-worldready-lifecycle.txt").write_text(
                f"{stamp}|command_consumed|first|removed\n"
                f"{stamp}|command_remove_failed|second|not_removed\n"
                f"{stamp}|transition_callback_invoked|first|callback_returned\n"
                f"{stamp}|shared_read_deferred|-|busy\n",
                encoding="utf-8")
            args = dict(profile="p", session="s", challenge="secret",
                        game_pid=123, request_id="second")
            snapshot = snapshot_worldready_stages(root, **args)
            self.assertEqual([(r["event"], r["forRequest"])
                              for r in snapshot["sourceTraceStages"]], [
                                  ("command_remove_failed", True),
                                  ("shared_read_deferred", False),
                              ])
            # A global lease/busy stage does not prove command consumption.
            self.assertFalse(snapshot["commandFilePresent"])
            self.assertFalse(snapshot["resultFilePresent"])


if __name__ == "__main__":
    unittest.main()
