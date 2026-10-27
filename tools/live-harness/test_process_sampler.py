#!/usr/bin/env python3
"""Self-test for process_sampler using only this process and temp files."""

from __future__ import annotations

import json
import os
from pathlib import Path
import sys
import tempfile
import threading
import time

from process_sampler import FileRootSpec, ProcessTargetSpec, run_sampler, write_report


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def main() -> int:
    if sys.platform != "win32":
        print("R29_PART_C_SAMPLER_SELFTEST_SKIP: Windows required")
        return 0

    with tempfile.TemporaryDirectory(prefix="r29-part-c-sampler-") as temporary:
        root = Path(temporary) / "runtime"
        root.mkdir()
        heartbeat = root / "heartbeat.json"
        mailbox = root / "feature018-request.txt"
        heartbeat.write_text('{"seq":0}\n', encoding="utf-8")
        mailbox.write_text("schema=1\nseq=0\n", encoding="utf-8")

        stop = threading.Event()

        def write_activity() -> None:
            time.sleep(0.12)
            for index in range(24):
                heartbeat.write_text(json.dumps({"seq": index, "payload": "x" * (index % 7)}) + "\n", encoding="utf-8")
                if index % 2 == 0:
                    mailbox.write_text(f"schema=1\nseq={index}\npayload={'y' * (index % 5)}\n", encoding="utf-8")
                time.sleep(0.035)
            stop.set()

        def cpu_activity() -> None:
            value = 0x12345678
            deadline = time.monotonic() + 1.25
            while time.monotonic() < deadline:
                value = ((value << 5) ^ (value >> 3) ^ 0x9E3779B9) & 0xFFFFFFFF
            if value == -1:
                print(value)

        writer = threading.Thread(target=write_activity, name="r29-selftest-writer")
        burner = threading.Thread(target=cpu_activity, name="r29-selftest-cpu")
        writer.start()
        burner.start()
        report = run_sampler(
            [ProcessTargetSpec("self", os.getpid())],
            [FileRootSpec("runtime", root)],
            duration_seconds=1.35,
            interval_seconds=0.10,
            file_patterns=["heartbeat.json", "*.txt"],
            notifications=True,
        )
        writer.join(timeout=2.0)
        burner.join(timeout=2.0)

        report_path = Path(temporary) / "sampler-selftest-report.json"
        write_report(report, report_path)
        loaded_report = json.loads(report_path.read_text(encoding="utf-8"))

        require(stop.is_set(), "writer activity did not complete")
        require(loaded_report["schema_version"] == report["schema_version"], "JSON report round-trip failed")
        require(len(report["samples"]) >= 10, "too few raw interval samples")
        summary = report["summary"]["processes"]["self"]
        require(summary["access"]["opened"] is True, f"self OpenProcess failed: {summary['access']}")
        require((summary["cpu_delta_seconds"] or {}).get("sum", 0) > 0, "CPU deltas were not observed")
        require((summary["working_set_bytes"] or {}).get("min", 0) > 0, "working set was not observed")
        require((summary["private_bytes"] or {}).get("min", 0) > 0, "private bytes were not observed")
        require((summary["io_write_ops_delta"] or {}).get("sum", 0) > 0, "write I/O operations were not observed")
        require((summary["handles"] or {}).get("min", 0) > 0, "handle count was not observed")
        require((summary["threads"] or {}).get("max", 0) >= 2, "thread count did not include self-test workers")

        file_summary = report["summary"]["file_roots"]["runtime"]
        require(file_summary["observed_metadata_change_events"] > 0, "file metadata changes were not observed")
        require(
            file_summary["modified_notifications"] > 0 or file_summary["watcher_available"] is False,
            "ReadDirectoryChangesW did not observe modifications",
        )
        require("heartbeat.json" in file_summary["tracked_files"], "heartbeat file missing from summary")
        require("feature018-request.txt" in file_summary["tracked_files"], "mailbox file missing from summary")

        compact = {
            "status": "PASS",
            "samples": len(report["samples"]),
            "actual_duration_seconds": report["actual_duration_seconds"],
            "cpu_delta_seconds": summary["cpu_delta_seconds"],
            "io_write_ops_delta": summary["io_write_ops_delta"],
            "threads": summary["threads"],
            "file_modified_notifications": file_summary["modified_notifications"],
            "metadata_change_events": file_summary["observed_metadata_change_events"],
            "watcher_available": file_summary["watcher_available"],
            "watcher_error": file_summary["watcher_error"],
        }
        print("R29_PART_C_SAMPLER_SELFTEST_PASS " + json.dumps(compact, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
