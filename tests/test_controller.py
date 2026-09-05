import json
import unittest
from dataclasses import replace
from pathlib import Path

from lwcontrol import Controller, FeatureConfig, MockBridge, Result, Snapshot, run_scenario


class ControllerTests(unittest.TestCase):
    def setUp(self):
        self.bridge = MockBridge(Snapshot(0, True, True, 3))

    def controller(self, feature="collect_resources", **kwargs):
        return Controller(self.bridge, [FeatureConfig(feature, True, 10)], **kwargs)

    def test_disabled_by_default(self):
        c = Controller(self.bridge, [FeatureConfig("claim_daily")])
        self.assertEqual(c.tick(0)["status"], "idle")
        self.assertEqual(self.bridge.history, [])

    def test_daily_claim_changes_state_and_does_not_repeat(self):
        c = self.controller("claim_daily")
        self.assertEqual(c.tick(0)["status"], "completed")
        self.bridge.state = replace(self.bridge.state, observed_at=10)
        self.assertEqual(c.tick(10)["status"], "idle")
        self.assertFalse(self.bridge.state.daily_available)
        self.assertEqual(len(self.bridge.history), 1)

    def test_cooldown_and_unique_correlation(self):
        c = self.controller()
        self.assertEqual(c.tick(0)["status"], "completed")
        self.bridge.state = replace(self.bridge.state, observed_at=9)
        self.assertEqual(c.tick(9)["status"], "idle")
        self.assertEqual(c.tick(10)["status"], "completed")
        self.assertEqual(self.bridge.state.resource_batches, 1)
        self.assertNotEqual(self.bridge.history[0].request_id, self.bridge.history[1].request_id)

    def test_unhealthy_snapshots_never_execute(self):
        cases = [Snapshot(0, False, True, 3), Snapshot(0, True, True, 3), Snapshot(20, True, True, 3)]
        for state in cases:
            with self.subTest(state=state):
                self.bridge.state = state
                self.assertEqual(self.controller().tick(10)["status"], "blocked")
        self.assertEqual(self.bridge.history, [])

    def test_stop_and_resume(self):
        c = self.controller()
        c.stop()
        self.assertEqual(c.tick(0)["status"], "paused")
        self.assertEqual(self.bridge.history, [])
        c.resume()
        self.assertEqual(c.tick(1)["status"], "completed")

    def test_only_one_action_per_tick(self):
        c = Controller(self.bridge, [FeatureConfig(n, True, 10) for n in ("claim_daily", "collect_resources")])
        self.assertEqual(c.tick(0)["feature"], "claim_daily")
        self.assertEqual(len(self.bridge.history), 1)
        self.assertEqual(c.tick(1)["feature"], "collect_resources")

    def test_success_without_effect_pauses(self):
        self.bridge.execute = lambda cmd, now: Result(cmd.request_id, True, "fake success")
        c = self.controller()
        self.assertEqual(c.tick(0)["detail"], "effect not verified")
        self.assertEqual(c.tick(1)["status"], "paused")

    def test_wrong_result_id_pauses(self):
        self.bridge.execute = lambda cmd, now: Result("unrelated", True, "")
        c = self.controller()
        self.assertEqual(c.tick(0)["detail"], "result correlation mismatch")
        self.assertTrue(c.paused)

    def test_transport_failure_pauses_and_keeps_cooldown(self):
        def fail(cmd, now):
            raise TimeoutError("unknown outcome")
        self.bridge.execute = fail
        c = self.controller()
        self.assertEqual(c.tick(0)["status"], "fault")
        c.resume()
        self.assertEqual(c.tick(1)["status"], "idle")
        self.assertEqual(c.next_due["collect_resources"], 10)

    def test_snapshot_failure_pauses(self):
        def fail(now):
            raise OSError("unavailable")
        self.bridge.snapshot = fail
        c = self.controller()
        self.assertEqual(c.tick(0)["detail"], "snapshot failed")
        self.assertTrue(c.paused)

    def test_reported_failure_pauses(self):
        self.bridge.execute = lambda cmd, now: Result(cmd.request_id, False, "failure")
        c = self.controller()
        self.assertEqual(c.tick(0)["detail"], "adapter reported failure")
        self.assertTrue(c.paused)

    def test_health_lost_after_action_is_not_success(self):
        execute = self.bridge.execute
        def disconnect(cmd, now):
            result = execute(cmd, now)
            self.bridge.state = replace(self.bridge.state, connected=False)
            return result
        self.bridge.execute = disconnect
        self.assertEqual(self.controller().tick(0)["detail"], "effect not verified")

    def test_configuration_and_clock_validation(self):
        for bad in (0, -1, float("nan"), float("inf"), True, "10"):
            with self.subTest(bad=bad), self.assertRaises(ValueError):
                FeatureConfig("claim_daily", True, bad)
        with self.assertRaises(ValueError):
            FeatureConfig("unknown")
        with self.assertRaises(ValueError):
            FeatureConfig("claim_daily", "false")
        with self.assertRaises(ValueError):
            Controller(self.bridge, [FeatureConfig("claim_daily")] * 2)
        c = self.controller()
        c.tick(1)
        with self.assertRaises(ValueError):
            c.tick(0)

    def test_demo_expected_behavior(self):
        path = Path(__file__).resolve().parents[1] / "examples" / "demo.json"
        data = json.loads(path.read_text())
        events = run_scenario(data)
        self.assertEqual([e["status"] for e in events], ["completed", "completed", "idle", "completed", "blocked", "paused", "idle"])
        self.assertTrue(all(e["mode"] == "offline" for e in events))

    def test_malformed_scenario_rejected(self):
        data = {"features": [], "initial_state": {"observed_at": 0, "connected": True, "daily_available": False, "resource_batches": 0}, "steps": [{"at": 2}, {"at": 1}]}
        with self.assertRaises(ValueError):
            run_scenario(data)
        data["steps"] = [{"at": 0, "state": {"resource_batches": -1}}]
        with self.assertRaises(ValueError):
            run_scenario(data)


if __name__ == "__main__":
    unittest.main()
