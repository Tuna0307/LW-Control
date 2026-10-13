"""Inert classification inverses and source-owned native-child monitoring receipt."""
from __future__ import annotations

import importlib.util
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("map013_monitor", Path(__file__).with_name("monitor_native_013.py"))
MONITOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MONITOR)


class MonitorClassificationTests(unittest.TestCase):
    def test_distinguishes_all_meaningful_completion_paths(self):
        with tempfile.TemporaryDirectory(prefix="map013-classify-") as dir_name:
            report = Path(dir_name) / "report.txt"
            for flag, expected in (
                ("DECOMPILE_COMPLETED=true\nDECOMPILE_VALID=true", "COMPLETED"),
                ("DECOMPILE_COMPLETED=false\nDECOMPILE_TIMED_OUT=true", "FUNCTION_TIMEOUT"),
                ("DECOMPILE_COMPLETED=false\nDECOMPILE_CANCELLED=true", "FUNCTION_CANCELLED"),
                ("DECOMPILE_COMPLETED=false\nDECOMPILE_FAILED_TO_START=true", "DECOMPILER_STARTUP_FAILURE"),
            ):
                report.write_text(flag, encoding="utf-8")
                self.assertEqual(MONITOR.status_of(report, 0, False), expected)
            self.assertEqual(MONITOR.status_of(report, 1, False), "DECOMPILER_STARTUP_FAILURE")
            self.assertEqual(MONITOR.status_of(report, 0, True), "OUTER_RUNNER_TIMEOUT")
            self.assertEqual(MONITOR.status_of(report, 0, False, True), "RESOURCE_PRESSURE_STOP")
            report.write_text("", encoding="utf-8")
            self.assertEqual(MONITOR.status_of(report, 0, False), "OBSERVABILITY_INSUFFICIENT")
            self.assertEqual(MONITOR.status_of(report, 1, False), "HEADLESS_FAILURE")
            self.assertEqual(MONITOR.status_of(report, None, False), "RUNNING")

    def test_control_actual_native_pid_and_cleanup(self):
        root = ROOT / "artifacts/rea-toolchain/map013-monitored/control-reuse"
        summary = root / "result.json"
        if not summary.exists():
            self.skipTest("private monitored control not installed on this machine")
        import json

        result = json.loads(summary.read_text(encoding="utf-8"))
        self.assertEqual(result["status"], "COMPLETED")
        self.assertTrue(result["nativeProcessObserved"])
        first = result["firstNativeChild"]
        self.assertGreater(first["createdEpoch"], 0)
        self.assertGreater(first["pid"], 0)
        native_logs = [
            json.loads(line)
            for line in (root / "native-child-fast-samples.jsonl").read_text(encoding="utf-8").splitlines()
        ]
        self.assertTrue(
            any(p["pid"] == first["pid"] and p["createdEpoch"] == first["createdEpoch"] and
                p["name"].lower() == "decompile.exe" for p in native_logs)
        )
        self.assertTrue(
            all(p["pid"] != first["pid"] or
                p["createdEpoch"] != first["createdEpoch"]
                for p in list_owned_native_processes())
        )


def list_owned_native_processes():
    try:
        return [MONITOR.sample_process(p) for p in MONITOR.psutil.process_iter(["name"]) if
                p.info["name"] and p.info["name"].lower() == "decompile.exe" and
                MONITOR.sample_process(p) is not None]
    except MONITOR.psutil.Error:
        return []


if __name__ == "__main__":
    unittest.main()
