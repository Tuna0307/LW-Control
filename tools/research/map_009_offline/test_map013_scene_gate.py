"""Inert scene-gate and reply-retirement checks for the MAP-013 observer helpers; no game."""
from pathlib import Path
import json
import tempfile
import unittest
from unittest.mock import patch

import run_map013_owned_observer as observer


class SceneGateTests(unittest.TestCase):
    def test_gate_reobserves_unknown_and_preserves_each_terminal_reply(self):
        with tempfile.TemporaryDirectory(prefix="map013-scene-gate-") as directory:
            attempt = Path(directory)
            live = attempt / "live-resource"
            views = iter([{"worldPresent": True, "sceneUtilsIsInCity": False,
                           "sceneUtilsIsInWorld": False},
                          {"sceneUtilsIsInCity": True, "sceneUtilsIsInWorld": False}])
            def reply(path, expected, seconds):
                self.assertEqual(expected.profile_id, "p")
                self.assertEqual(expected.kind, "trace")
                (live / "map013-native-trace.txt").unlink()
                value = {"state": "observed", "samples": [{"view": next(views)}]}
                path.write_text(json.dumps(value), encoding="utf-8")
                return value
            with patch.object(observer, "wait_for", side_effect=reply), \
                 patch.object(observer.time, "monotonic", side_effect=[0, 1, 1, 2, 2]):
                result = observer.observe_scene_gate(live, attempt, profile="p",
                    session="s", challenge="c", game_pid=123, seconds=4)
            self.assertTrue(result["city"])
            self.assertEqual(result["observation"], 2)
            self.assertTrue((attempt / "scene-gate-001.json").exists())
            self.assertTrue((attempt / "scene-gate-002.json").exists())
            self.assertFalse((live / "map013-native-trace-result.json").exists())
            self.assertFalse((attempt / "overview-bridge/world-ready.txt").exists())

    def test_observer_error_cannot_admit_world_request(self):
        with tempfile.TemporaryDirectory(prefix="map013-scene-error-") as directory:
            attempt = Path(directory)
            live = attempt / "live-resource"
            def reply(path, *_):
                value = {"state": "observer_error", "samples": [
                    {"view": {"sceneUtilsIsInCity": True}}]}
                path.write_text(json.dumps(value), encoding="utf-8")
                return value
            with patch.object(observer, "wait_for", side_effect=reply):
                with self.assertRaisesRegex(RuntimeError, "observer failed"):
                    observer.observe_scene_gate(live, attempt, profile="p",
                        session="s", challenge="c", game_pid=123)

    def test_retirement_preserves_foreign_reply_and_removes_only_archived_owner(self):
        with tempfile.TemporaryDirectory(prefix="map013-reply-retire-") as directory:
            path = Path(directory) / "reply.json"
            reply = {"requestId": "r", "profileId": "p", "launchSessionId": "s",
                     "gamePid": 123, "challenge": "c", "state": "proven"}
            path.write_text(json.dumps(reply), encoding="utf-8")
            with self.assertRaisesRegex(RuntimeError, "foreign"):
                observer.retire_owned_reply(path, "wrong", "p", "s", 123, "c")
            self.assertTrue(path.exists())
            path.write_text(json.dumps({**reply, "state": "pending"}), encoding="utf-8")
            with self.assertRaisesRegex(RuntimeError, "unexpected"):
                observer.retire_owned_reply(path, "r", "p", "s", 123, "c")
            path.write_text(json.dumps(reply), encoding="utf-8")
            observer.retire_owned_reply(path, "r", "p", "s", 123, "c")
            self.assertFalse(path.exists())


if __name__ == "__main__":
    unittest.main()
