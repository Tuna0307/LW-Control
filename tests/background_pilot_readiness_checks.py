"""Inert inverse cases for the archived Home/Map background readiness classifier."""
import json
import sys
import tempfile
import unittest
from pathlib import Path
from shutil import copyfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools" / "lwbridge317"))
from check_background_pilot_readiness import DEFAULT_EVIDENCE, evaluate


class BackgroundPilotReadinessChecks(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix="lwb317-background-readiness-")
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        for name in ("control.txt", "ready.json", "heartbeat.json",
                     "pipe-transport.json", "map-pre-scan.json",
                     "processes.json", "recovery-active.json"):
            copyfile(DEFAULT_EVIDENCE / name, self.root / name)

    def mutate(self, filename, change):
        path = self.root / filename
        value = json.loads(path.read_text(encoding="utf-8-sig"))
        change(value)
        path.write_text(json.dumps(value), encoding="utf-8")

    def test_original_prelaunch_resource_cancellation_not_attempt5_failure(self):
        result = evaluate(self.root)
        self.assertTrue(result["resourceRunCancelledBeforeAttempt5"])
        self.assertTrue(result["resourceRunZeroCompletedBlocks"])
        self.assertEqual(result["resourceRowsBeforeScan"], 0)
        self.assertFalse(result["pipeSnapshotReportsConnected"])
        self.assertTrue(result["gameHeartbeatReady"])
        self.assertFalse(result["pipeHelloAckProvenForSession"])

    def test_inverse_resource_run_after_launch_removes_prelaunch_diagnosis(self):
        def shift(value):
            run = next(x for x in value["latestScanRuns"]
                       if json.loads(x["selected_types"]) == ["resource"])
            run["created_at"] = 1791388640000
            run["updated_at"] = 1791388650000
        self.mutate("map-pre-scan.json", shift)
        result = evaluate(self.root)
        self.assertFalse(result["resourceRunCancelledBeforeAttempt5"])
        resource = next(x for x in result["runs"] if x["selectedTypes"] == ["resource"])
        self.assertTrue(resource["temporallyEligibleForAttempt5"])
        # No session identifier exists on the persisted scan-run rows.
        self.assertFalse(result["pipeHelloAckProvenForSession"])

    def test_connected_pipe_diagnostic_still_not_host_ack_proof(self):
        self.mutate("pipe-transport.json",
                    lambda x: x.update(state="connected", clientConnected=True))
        result = evaluate(self.root)
        self.assertTrue(result["pipeSnapshotReportsConnected"])
        self.assertFalse(result["pipeHelloAckProvenForSession"])

    def test_inconsistent_game_identity_is_rejected(self):
        self.mutate("heartbeat.json", lambda x: x.update(gamePid=1234))
        with self.assertRaisesRegex(ValueError, "heartbeat does not match"):
            evaluate(self.root)

    def test_inverted_run_timestamps_are_rejected(self):
        def invert(value):
            value["latestScanRuns"][0]["updated_at"] = value["latestScanRuns"][0]["created_at"] - 1
        self.mutate("map-pre-scan.json", invert)
        with self.assertRaisesRegex(ValueError, "inverted"):
            evaluate(self.root)


if __name__ == "__main__":
    unittest.main()
