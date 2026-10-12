"""LEADMAP013-04 — execute the real MAP-013 reply reader with inert files.

Each test is an exact reverse of a validation predicate. No game is run.
Retains the lead's historical negative as a regression input.
"""
from __future__ import annotations

from datetime import datetime, timedelta, timezone
import json
import os
from pathlib import Path
import tempfile
import time
import unittest

from run_map013_owned_observer import (
    ExpectedReply, ReplyValidationError, validate_reply, wait_for,
)


def timestamp(offset: int = 0) -> str:
    return (datetime.now(timezone.utc) + timedelta(seconds=offset)).strftime("%Y-%m-%dT%H:%M:%SZ")


class ReplyAdmissionTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="map013-reply-inverse-")
        self.addCleanup(self.temporary.cleanup)
        self.path = Path(self.temporary.name) / "reply.json"
        self.published = time.time_ns() - 2_000_000
        self.expect = ExpectedReply("world", "same_request", "correct_owner",
                                    "correct_session", 43210, "correct_challenge",
                                    self.published, server_id=2212, world_id=0)

    def payload(self, kind="world", state=None):
        if kind == "world":
            return {"requestId": "same_request", "schemaVersion": 1,
                    "bridgeVersion": "lwbridge-overview-bridge-1",
                    "profileId": "correct_owner", "sessionId": "correct_session",
                    "gamePid": 43210, "challenge": "correct_challenge",
                    "capturedAt": timestamp(), "state": state or "proven",
                    "serverId": 2212, "worldId": 0, "tileWidth": 1000,
                    "tileHeight": 1000, "playerTileX": 498, "playerTileY": 63}
        return {"requestId": "same_request", "schemaVersion": 1,
                "probeVersion": "lwbridge-live-resource-probe-2",
                "profileId": "correct_owner", "launchSessionId": "correct_session",
                "gamePid": 43210, "challenge": "correct_challenge",
                "capturedAt": timestamp(), "state": state or "observed",
                "events": [], "registrations": [], "errors": [],
                "requestMode": "native", "targetTileX": 455, "targetTileY": 425,
                "targetAoiIndex": 4245}

    def publish(self, payload):
        self.path.write_text(json.dumps(payload), encoding="utf-8")

    def read(self, kind="world", **expected_overrides):
        kwargs = dict(kind=kind, request_id="same_request", profile_id="correct_owner",
                      session_id="correct_session", game_pid=43210,
                      challenge="correct_challenge", published_at_ns=self.published,
                      server_id=2212 if kind == "world" else None,
                      world_id=0 if kind == "world" else None,
                      request_mode="native" if kind == "region" else None)
        kwargs.update(expected_overrides)
        return wait_for(self.path, ExpectedReply(**kwargs), 0.4)

    def test_exact_world_proven_and_validated_failure(self):
        good = self.payload()
        self.publish(good)
        self.assertEqual(self.read()["serverId"], 2212)
        good["state"] = "failed"
        good["error"] = "change_to_world_failed"
        for key in ("serverId", "worldId", "tileWidth", "tileHeight"):
            good.pop(key)
        self.publish(good)
        self.assertEqual(self.read()["state"], "failed")

    def test_world_player_position_is_optional_in_actual_emitter(self):
        j = self.payload()
        j.pop("playerTileX")
        j.pop("playerTileY")
        self.publish(j)
        self.assertEqual(self.read()["state"], "proven")

    def test_failed_outcome_requires_nonempty_native_error(self):
        for kind in ("world", "region"):
            for missing in (None, ""):
                with self.subTest(kind=kind, missing=missing):
                    j = self.payload(kind, "failed")
                    j["error"] = missing
                    self.publish(j)
                    with self.assertRaises(ReplyValidationError):
                        self.read(kind)

    def test_actual_lead_foreign_reply_rejected(self):
        self.publish({"requestId": "same_request", "profileId": "foreign",
                      "sessionId": "foreign", "gamePid": 123, "state": "proven"})
        with self.assertRaises(ReplyValidationError):
            self.read()

    def test_foreign_or_missing_owner_fields_always_reject(self):
        for field, wrong in (("profileId", "foreign"), ("sessionId", "foreign"),
                             ("gamePid", 123), ("challenge", "foreign"),
                             ("requestId", "foreign")):
            for missing in (False, True):
                with self.subTest(field=field, missing=missing):
                    j = self.payload()
                    if missing:
                        j.pop(field)
                    else:
                        j[field] = wrong
                    self.publish(j)
                    with self.assertRaises(ReplyValidationError):
                        self.read()

    def test_wrong_types_versions_and_world_context_reject(self):
        for field, wrong in (("schemaVersion", 2), ("bridgeVersion", "other"),
                             ("gamePid", "43210"), ("serverId", 2213),
                             ("worldId", 1), ("tileHeight", 0), ("tileWidth", None),
                             ("playerTileY", 1000), ("error", "hidden failure")):
            with self.subTest(field=field):
                j = self.payload()
                j[field] = wrong
                self.publish(j)
                with self.assertRaises(ReplyValidationError):
                    self.read()

    def test_stale_file_stale_capture_future_and_invalid_capture_reject(self):
        good = self.payload()
        self.publish(good)
        old = self.published - 5_000_000_000
        os.utime(self.path, ns=(old, old))
        with self.assertRaisesRegex(ReplyValidationError, "predates"):
            self.read()
        for captured in (timestamp(-20), timestamp(60), "2026/10/13", None):
            with self.subTest(captured=captured):
                j = self.payload()
                j["capturedAt"] = captured
                self.publish(j)
                with self.assertRaises(ReplyValidationError):
                    self.read()

    def test_pending_is_never_a_terminal_success_and_invalid_state_rejects(self):
        pending = self.payload(state="pending")
        self.publish(pending)
        with self.assertRaises(TimeoutError):
            self.read()
        pending["state"] = "unsupported"
        self.publish(pending)
        with self.assertRaises(ReplyValidationError):
            self.read()

    def test_region_actual_session_field_challenge_context_and_states(self):
        j = self.payload("region", "proven")
        self.publish(j)
        self.assertEqual(self.read("region")["state"], "proven")
        for wrong in ({"launchSessionId": "foreign"}, {"sessionId": "correct_session",
                                                         "launchSessionId": None},
                      {"requestMode": "anchor"}, {"targetAoiIndex": None},
                      {"challenge": None}, {"probeVersion": "wrong"}):
            with self.subTest(wrong=wrong):
                invalid = dict(j, **wrong)
                self.publish(invalid)
                with self.assertRaises(ReplyValidationError):
                    self.read("region")
        j["state"] = "failed"
        j["error"] = "native_view_missing"
        self.publish(j)
        self.assertEqual(self.read("region")["error"], "native_view_missing")

    def test_trace_requires_actual_session_challenge_version_and_arrays(self):
        j = self.payload("trace")
        self.publish(j)
        self.assertEqual(self.read("trace")["state"], "observed")
        for field in ("challenge", "probeVersion", "launchSessionId", "registrations",
                      "events", "capturedAt"):
            with self.subTest(field=field):
                bad = self.payload("trace")
                bad.pop(field)
                self.publish(bad)
                with self.assertRaises(ReplyValidationError):
                    self.read("trace")
        j = self.payload("trace", "observer_error")
        self.publish(j)
        self.assertEqual(self.read("trace")["state"], "observer_error")
        j["state"] = "pending"
        self.publish(j)
        with self.assertRaises(TimeoutError):
            self.read("trace")


if __name__ == "__main__":
    unittest.main()
