"""PM12-A isolated recovery/ownership regression.

This script uses disposable dummy files only. It does not inspect, launch, stop,
or modify Last War. Run from the repository root with:

    python evidence/lwbridge-implementation/2026-09-10-pm12-recovery-regression.py
"""

from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import time
import uuid
from unittest.mock import patch


repo = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location(
    "pm12_helper", repo / "tools" / "run_live_resource_probe.py"
)
helper = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(helper)


def make_dummy() -> tuple[Path, dict[str, Path], Path, dict[str, bytes]]:
    root = Path(tempfile.mkdtemp(prefix="lwbridge-pm12-recovery-"))
    candidate = root / "candidate"
    candidate.mkdir()
    p: dict[str, Path] = {
        "runtime": root / "runtime",
        "backup_root": root / "backups",
    }
    originals: dict[str, bytes] = {}
    for key, name in (
        ("data", "LWScripts.data"),
        ("metadata", "LWScripts.txt"),
        ("version", "version.txt"),
    ):
        p[key] = root / name
        originals[key] = f"ORIGINAL-{key}".encode()
        p[key].write_bytes(originals[key])
        (candidate / name).write_bytes(f"CANDIDATE-{key}".encode())
    return root, p, candidate, originals


def all_original(p: dict[str, Path], originals: dict[str, bytes]) -> bool:
    return all(p[key].read_bytes() == value for key, value in originals.items())


def fake_verify(_p: dict[str, Path]) -> dict[str, object]:
    return {"packageSha256": helper.sha256_file(_p["data"]), "dummy": True}


install_cases: list[dict[str, object]] = []
for fail_at in (1, 2, 3):
    root, p, candidate, originals = make_dummy()
    calls = 0
    real_copy = helper.copy_atomic

    def failing_copy(source: Path, destination: Path) -> None:
        nonlocal_calls[0] += 1
        if nonlocal_calls[0] == fail_at:
            raise OSError(f"injected install failure {fail_at}")
        real_copy(source, destination)

    nonlocal_calls = [0]
    try:
        with patch.object(helper, "paths", return_value=p), \
             patch.object(helper, "process_running", return_value=False), \
             patch.object(helper, "verify_current", side_effect=fake_verify), \
             patch.object(helper, "make_candidate", return_value={"dummy": True}), \
             patch.object(helper.tempfile, "mkdtemp", return_value=str(candidate)), \
             patch.object(helper, "copy_atomic", side_effect=failing_copy), \
             patch.object(helper.subprocess, "Popen", side_effect=AssertionError("must not launch")):
            try:
                helper.run(f"install-fail-{fail_at}", 10, False)
                error = None
            except OSError as exc:
                error = str(exc)
        install_cases.append({
            "failureAtInstallCopy": fail_at,
            "error": error,
            "filesRestored": all_original(p, originals),
            "pendingRecovery": (p["runtime"] / "recovery.json").exists(),
        })
    finally:
        shutil.rmtree(root, ignore_errors=True)


restore_cases: list[dict[str, object]] = []
for fail_at in (1, 2, 3):
    root, p, candidate, originals = make_dummy()
    try:
        with patch.object(helper, "verify_current", side_effect=fake_verify):
            backup = helper.make_backup(p)
            state = helper.arm_recovery(p, backup, f"restore-fail-{fail_at}")
            helper.install_candidate(p, candidate)
            real_copy = helper.copy_atomic
            copy_calls = [0]

            def failing_restore_copy(source: Path, destination: Path) -> None:
                copy_calls[0] += 1
                if copy_calls[0] == fail_at:
                    raise OSError(f"injected restore failure {fail_at}")
                real_copy(source, destination)

            try:
                with patch.object(helper, "copy_atomic", side_effect=failing_restore_copy):
                    helper.update_recovery_stage(p, state, "restoring_test")
                    helper.restore_backup(p, backup)
                first_error = None
            except OSError as exc:
                first_error = str(exc)

            pending_after_failure = (p["runtime"] / "recovery.json").exists()
            recovered = helper.recover_pending(p)
            restore_cases.append({
                "failureAtRestoreCopy": fail_at,
                "error": first_error,
                "pendingStatePreserved": pending_after_failure,
                "recoveredOnNextAttempt": recovered is not None and all_original(p, originals),
                "pendingRecoveryAfterRetry": (p["runtime"] / "recovery.json").exists(),
            })
    finally:
        shutil.rmtree(root, ignore_errors=True)


root, p, candidate, originals = make_dummy()
try:
    with patch.object(helper, "verify_current", side_effect=fake_verify):
        backup = helper.make_backup(p)
        helper.arm_recovery(p, backup, "interrupted")
        helper.install_candidate(p, candidate)
        interrupted_before = not all_original(p, originals)
        recovered = helper.recover_pending(p)
        interrupted_case = {
            "candidateWasInstalled": interrupted_before,
            "recovered": recovered is not None and all_original(p, originals),
            "pendingRecoveryAfterRecovery": (p["runtime"] / "recovery.json").exists(),
        }
finally:
    shutil.rmtree(root, ignore_errors=True)


lease_root = Path(tempfile.mkdtemp(prefix="lwbridge-pm12-lease-"))
try:
    ready_path = lease_root / "child-ready.txt"
    child_path = lease_root / "lease-child.py"
    helper_path = repo / "tools" / "run_live_resource_probe.py"
    child_path.write_text(f"""
import importlib.util
from pathlib import Path
import sys
import time

spec = importlib.util.spec_from_file_location("lease_helper", {str(helper_path)!r})
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
runtime = Path(sys.argv[1])
ready = Path(sys.argv[2])
with module.OperationLease(runtime, {{"requestId": "child"}}):
    ready.write_text("ready", encoding="utf-8")
    time.sleep(1.0)
""", encoding="utf-8")
    child = subprocess.Popen([sys.executable, str(child_path), str(lease_root), str(ready_path)])
    deadline = time.monotonic() + 3
    while not ready_path.exists() and child.poll() is None and time.monotonic() < deadline:
        time.sleep(0.02)
    child_ready = ready_path.exists()
    try:
        helper.OperationLease(lease_root, {"requestId": "parent-overlap"})
        rejected = False
    except helper.LiveResourceError:
        rejected = True
    child_exit = child.wait(timeout=4)
    second = helper.OperationLease(lease_root, {"requestId": "after-release"})
    second.close()
    lease_case = {
        "childAcquiredLease": child_ready,
        "crossProcessOverlapRejected": rejected,
        "childExitCode": child_exit,
        "leaseReusableAfterRelease": True,
    }
finally:
    shutil.rmtree(lease_root, ignore_errors=True)


root, p, candidate, originals = make_dummy()
try:
    with patch.object(helper, "paths", return_value=p), \
         patch.object(helper, "process_running", return_value=False), \
         patch.object(helper, "verify_current", side_effect=fake_verify), \
         patch.object(helper, "make_candidate", side_effect=RuntimeError("primary failure")), \
         patch.object(helper, "restore_backup", side_effect=OSError("cleanup failure")):
        try:
            helper.run("dual-error", 10, False)
            dual_error = None
        except helper.LiveResourceError as exc:
            dual_error = str(exc)
    dual_error_case = {
        "message": dual_error,
        "preservesPrimary": dual_error is not None and "primary failure" in dual_error,
        "preservesCleanup": dual_error is not None and "cleanup failure" in dual_error,
        "pendingRecoveryPreserved": (p["runtime"] / "recovery.json").exists(),
    }
finally:
    shutil.rmtree(root, ignore_errors=True)


report = {
    "scope": "isolated dummy files only; no game/process operations",
    "findingId": "LWB-PM12-001",
    "installFailureCases": install_cases,
    "restoreFailureCases": restore_cases,
    "interruptedRecovery": interrupted_case,
    "sharedLease": lease_case,
    "dualError": dual_error_case,
}
report["ok"] = (
    all(case["filesRestored"] and not case["pendingRecovery"] for case in install_cases)
    and all(
        case["pendingStatePreserved"]
        and case["recoveredOnNextAttempt"]
        and not case["pendingRecoveryAfterRetry"]
        for case in restore_cases
    )
    and interrupted_case["candidateWasInstalled"]
    and interrupted_case["recovered"]
    and not interrupted_case["pendingRecoveryAfterRecovery"]
    and lease_case["childAcquiredLease"]
    and lease_case["crossProcessOverlapRejected"]
    and lease_case["childExitCode"] == 0
    and dual_error_case["preservesPrimary"]
    and dual_error_case["preservesCleanup"]
    and dual_error_case["pendingRecoveryPreserved"]
)
print(json.dumps(report, indent=2))
raise SystemExit(0 if report["ok"] else 1)
