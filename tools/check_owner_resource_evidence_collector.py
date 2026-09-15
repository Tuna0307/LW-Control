from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import tempfile

MODULE_PATH = Path(__file__).with_name("collect_owner_resource_evidence.py")
spec = importlib.util.spec_from_file_location("owner_evidence_collector", MODULE_PATH)
assert spec and spec.loader
collector = importlib.util.module_from_spec(spec)
spec.loader.exec_module(collector)

ROW = {
    "serverId": 2212,
    "recordKey": "32482",
    "pointIndex": 32482,
    "x": 481,
    "y": 32,
    "level": 3,
    "updatedAt": 1789017285000,
}
SEARCH_RESULT = {"rows": [ROW], "total": 1}
OTHER_RESULT = {"rows": [{**ROW, "recordKey": "other", "updatedAt": 1789017299000}], "total": 1}


def write_session(directory: Path, pid: int, body: list[dict], *, malformed: str | None = None) -> None:
    directory.mkdir(parents=True, exist_ok=True)
    events = [
        {"eventType": "session-start", "details": {"processId": pid, "profileId": "profile-a", "initialView": "map-data"}},
        *body,
        {"eventType": "session-end", "details": {"processId": pid}},
    ]
    lines = [json.dumps(event) for event in events]
    if malformed is not None:
        lines.insert(1, malformed)
    (directory / f"ui-session-{pid}.jsonl").write_text("\n".join(lines) + "\n", encoding="utf-8")


def post_from_ui(ui: dict, *, healthy: bool | None = None, app_exit: int = 0) -> dict:
    evidence_healthy = ui.get("healthy") is True if healthy is None else healthy
    return {
        "appExitCode": app_exit,
        "evidenceHealthy": evidence_healthy,
        "evidenceHealthErrors": [] if evidence_healthy else ["isolated fixture evidence failure"],
        "completeSearchRender": ui.get("latestSearchCorrelated") is True,
        "completeNoSavedContext": False,
        "runtimeIntegrityMatchesPreflight": True,
        "cleanupClean": True,
        "profileStore": {"profileId": "profile-a"},
        "uiEvidence": ui,
    }


def main() -> int:
    checks: dict[str, bool] = {}
    with tempfile.TemporaryDirectory(prefix="lwbridge-pm15-owner-evidence-") as tmp:
        root = Path(tmp)

        # Session one has valid proof.
        s1 = root / "ui" / "session-1-a"
        write_session(s1, 9000, [
            {"eventType": "resource-search-response", "requestId": "s1-search", "result": SEARCH_RESULT},
            {"eventType": "resource-render-observation", "requestId": "s1-search", "correlated": True},
        ])
        ui1 = collector.collect_ui_events(s1, 9000)
        first = post_from_ui(ui1)
        assert first["completeSearchRender"] is True

        # PM15-01: second session opens/closes without Search. Session one must not satisfy it.
        s2_none = root / "ui" / "session-2-no-search"
        write_session(s2_none, 1000, [])
        ui2_none = collector.collect_ui_events(s2_none, 1000)
        second_none = post_from_ui(ui2_none)
        outcome_none = collector.evaluate_reopen(first, second_none)
        assert ui2_none["healthy"] is True and ui2_none["searchCount"] == 0
        assert second_none["completeSearchRender"] is False and outcome_none["complete"] is False
        assert collector.completion_failure_reasons(second_none)
        checks["secondSessionWithoutSearchRejected"] = True

        # PM15-01: second session has a Search but its rendered observation is uncorrelated/mismatching.
        s2_bad = root / "ui" / "session-2-bad"
        write_session(s2_bad, 800, [
            {"eventType": "resource-search-response", "requestId": "s2-bad", "result": OTHER_RESULT},
            {"eventType": "resource-render-observation", "requestId": "s2-bad", "correlated": False, "reason": "mismatching row"},
        ])
        ui2_bad = collector.collect_ui_events(s2_bad, 800)
        second_bad = post_from_ui(ui2_bad)
        assert ui2_bad["latestSearchCorrelated"] is False
        assert collector.evaluate_reopen(first, second_bad)["complete"] is False
        checks["uncorrelatedOrMismatchingSecondEvidenceRejected"] = True

        # PM15-01: PID/file ordering and PID reuse cannot leak old proof because directories are session-owned.
        reused_pid = 777
        s1_reuse = root / "ui" / "session-1-reuse"
        s2_reuse = root / "ui" / "session-2-reuse"
        write_session(s1_reuse, reused_pid, [
            {"eventType": "resource-search-response", "requestId": "old", "result": SEARCH_RESULT},
            {"eventType": "resource-render-observation", "requestId": "old", "correlated": True},
        ])
        write_session(s2_reuse, reused_pid, [])
        assert collector.collect_ui_events(s1_reuse, reused_pid)["latestSearchCorrelated"] is True
        assert collector.collect_ui_events(s2_reuse, reused_pid)["latestSearchCorrelated"] is False
        low_pid_dir = root / "ui" / "session-2-low-pid"
        write_session(low_pid_dir, 12, [
            {"eventType": "resource-search-response", "requestId": "new-low-pid", "result": SEARCH_RESULT},
            {"eventType": "resource-render-observation", "requestId": "new-low-pid", "correlated": True},
        ])
        low_pid_ui = collector.collect_ui_events(low_pid_dir, 12)
        assert low_pid_ui["latestRequestId"] == "new-low-pid"
        checks["fileOrderAndPidReuseCannotSelectOldProof"] = True

        # PM15-01 positive: distinct second session with its own valid request/render can pass same-row reopen.
        s2_good = root / "ui" / "session-2-good"
        write_session(s2_good, 500, [
            {"eventType": "resource-search-response", "requestId": "s2-search", "result": SEARCH_RESULT},
            {"eventType": "resource-render-observation", "requestId": "s2-search", "correlated": True},
        ])
        ui2_good = collector.collect_ui_events(s2_good, 500)
        second_good = post_from_ui(ui2_good)
        outcome_good = collector.evaluate_reopen(first, second_good)
        assert ui2_good["latestRequestId"] == "s2-search"
        assert outcome_good["complete"] is True and outcome_good["sameSavedRow"] is True
        checks["distinctValidSecondSessionCanPass"] = True

        # PM15-02: process query command failure, malformed output and legitimate empty are distinct.
        proc_fail = collector.parse_process_snapshot(9, "", "CIM failure")
        proc_bad = collector.parse_process_snapshot(0, "{not-json", "")
        proc_empty = collector.parse_process_snapshot(0, "", "")
        assert proc_fail["ok"] is False and proc_fail["error"]["kind"] == "PROCESS_QUERY_FAILED"
        assert proc_fail["diagnostic"]["exitCode"] == 9 and "CIM failure" in proc_fail["diagnostic"]["stderr"]
        assert proc_bad["ok"] is False and proc_bad["error"]["kind"] == "PROCESS_QUERY_INVALID_JSON"
        assert proc_empty == {"ok": True, "rows": [], "error": None, "diagnostic": {"exitCode": 0, "stdout": "", "stderr": ""}}
        assert collector.process_observation_blocker(proc_fail)
        assert collector.process_observation_blocker(proc_bad)
        assert collector.process_observation_blocker(proc_empty) is None
        checks["processObservationFailureAndEmptySeparated"] = True

        # PM15-02: missing/malformed current-session logs are unhealthy.
        missing_dir = root / "ui" / "session-missing"
        missing_dir.mkdir(parents=True)
        missing_ui = collector.collect_ui_events(missing_dir, 321)
        assert missing_ui["healthy"] is False and missing_ui["errors"]
        malformed_dir = root / "ui" / "session-malformed"
        write_session(malformed_dir, 654, [], malformed="not-json")
        malformed_ui = collector.collect_ui_events(malformed_dir, 654)
        assert malformed_ui["healthy"] is False and malformed_ui["errors"]
        checks["missingOrMalformedCurrentSessionEvidenceRejected"] = True

        # PM15-02: an app/evidence failure after an earlier valid session cannot become COMPLETE.
        failed_second = post_from_ui(ui2_good, healthy=False, app_exit=5)
        failed_second["completeSearchRender"] = True
        failed_second["evidenceHealthErrors"] = ["LWBridge exited with code 5"]
        assert collector.completion_failure_reasons(failed_second)
        assert collector.evaluate_reopen(first, failed_second)["complete"] is False
        checks["appOrEvidenceFailureAfterPriorSuccessRejected"] = True

    print(json.dumps({"ok": all(checks.values()), "checks": checks}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
