"""LWB-PM12-006 isolated exact-PID normal-close/restoration regression.

This uses disposable files and mocked process/launcher boundaries only. It does
not inspect, launch, close, or modify the installed Last War client.
"""

from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import shutil
import tempfile
from unittest.mock import patch


repo = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location(
    "pm12_scoped_close_helper", repo / "tools" / "run_live_resource_probe.py"
)
helper = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(helper)


class Completed:
    def __init__(self, returncode: int = 0, stdout: str = "", stderr: str = ""):
        self.returncode = returncode
        self.stdout = stdout
        self.stderr = stderr


root = Path(tempfile.mkdtemp(prefix="lwbridge-pm12-scoped-close-"))
try:
    selected_root = root / "selected"
    selected_game = selected_root / "Game" / "LastWar.exe"
    selected_game.parent.mkdir(parents=True)
    selected_game.write_bytes(b"dummy-game")
    p: dict[str, Path] = {
        "launcher": selected_root / "LastWarLauncher.exe",
        "game": selected_game,
        "runtime": root / "runtime",
        "backup_root": root / "backups",
    }
    for key, name in (
        ("data", "LWScripts.data"),
        ("metadata", "LWScripts.txt"),
        ("version", "version.txt"),
    ):
        p[key] = root / name
        p[key].write_bytes(f"ORIGINAL-{key}".encode())

    owned = {
        "pid": 5202,
        "path": str(selected_game),
        "startedAtUtc": "2026-09-10T07:30:00Z",
    }

    close_call: dict[str, object] = {}

    def fake_close_run(args, **kwargs):
        close_call["args"] = args
        close_call["env"] = kwargs.get("env")
        return Completed()

    with patch.object(helper, "selected_game_processes", side_effect=[[owned], []]), \
         patch.object(helper.subprocess, "run", side_effect=fake_close_run):
        close_result = helper.close_owned_game_process_for_restore(p, owned, wait_milliseconds=250)

    close_env = close_call.get("env") if isinstance(close_call.get("env"), dict) else {}
    close_args = close_call.get("args") if isinstance(close_call.get("args"), list) else []
    close_script = close_args[-1] if close_args else ""
    exact_close = {
        "pidScoped": close_result.get("pid") == 5202 and close_env.get("LWBRIDGE_OWNED_GAME_PID") == "5202",
        "pathScoped": helper._normalized_process_path(close_result.get("path", ""))
            == helper._normalized_process_path(selected_game)
            and helper._normalized_process_path(close_env.get("LWBRIDGE_OWNED_GAME_PATH", ""))
            == helper._normalized_process_path(selected_game),
        "boundedWait": close_result.get("waitMilliseconds") == 250
            and close_env.get("LWBRIDGE_NORMAL_CLOSE_WAIT_MS") == "250",
        "normalCloseOnly": "CloseMainWindow" in close_script
            and "Stop-Process" not in close_script
            and ".Kill(" not in close_script
            and "TerminateProcess" not in close_script,
        "exitVerified": close_result.get("accepted") is True and close_result.get("processExited") is True,
    }

    changed_identity = dict(owned)
    changed_identity["pid"] = 5203
    try:
        with patch.object(helper, "selected_game_processes", return_value=[changed_identity]), \
             patch.object(helper.subprocess, "run", side_effect=AssertionError("close must not run")):
            helper.close_owned_game_process_for_restore(p, owned, wait_milliseconds=250)
        identity_rejected = False
    except helper.LiveResourceError as exc:
        identity_rejected = "identity changed" in str(exc)

    try:
        with patch.object(helper, "selected_game_processes", return_value=[owned]), \
             patch.object(helper.subprocess, "run", return_value=Completed(returncode=41)):
            helper.close_owned_game_process_for_restore(p, owned, wait_milliseconds=250)
        rejected_normal_close = False
    except helper.LiveResourceError as exc:
        rejected_normal_close = "did not accept a normal main-window close" in str(exc)

    result_path = p["runtime"] / "results" / "scoped-close-request.json"
    result_path.parent.mkdir(parents=True, exist_ok=True)
    result_path.write_text("{}", encoding="utf-8")
    candidate_root = root / "candidate"
    candidate_root.mkdir()

    class FakeLauncher:
        pid = 5201

    def fake_verify(paths: dict[str, Path]) -> dict[str, object]:
        return {"packageSha256": helper.sha256_file(paths["data"]), "dummy": True}

    restored_value = {
        "restored": True,
        "packageSha256": "dummy",
        "originalFiles": {},
        "restoredFiles": {},
    }
    close_flow_value = {
        "method": "Process.CloseMainWindow",
        "pid": 5202,
        "path": str(selected_game),
        "waitMilliseconds": 10000,
        "accepted": True,
        "processExited": True,
    }
    with patch.object(helper, "paths", return_value=p), \
         patch.object(helper, "selected_game_processes", return_value=[]), \
         patch.object(helper, "verify_current", side_effect=fake_verify), \
         patch.object(helper, "make_candidate", return_value={"dummy": True}), \
         patch.object(helper, "install_candidate", return_value=None), \
         patch.object(helper.tempfile, "mkdtemp", return_value=str(candidate_root)), \
         patch.object(helper.subprocess, "Popen", return_value=FakeLauncher()), \
         patch.object(helper, "await_owned_game_process", return_value=owned), \
         patch.object(helper, "await_result", return_value=({"requestId": "scoped-close-request"}, result_path)), \
         patch.object(helper, "restore_backup", side_effect=[OSError("locked while running"), restored_value]), \
         patch.object(helper, "close_owned_game_process_for_restore", return_value=close_flow_value) as close_mock:
        run_result = helper.run(
            "scoped-close-request",
            10,
            game_root=selected_root,
            profile_id="profile-test",
        )

    restore_retry = {
        "closeCalledOnce": close_mock.call_count == 1,
        "successfulAfterClose": run_result.get("ok") is True,
        "closeEvidenceReturned": run_result.get("restore", {}).get("normalClose") == close_flow_value,
        "recoveryCleared": not (p["runtime"] / "recovery.json").exists(),
    }

    report = {
        "schemaVersion": 1,
        "findingId": "LWB-PM12-006",
        "scope": "isolated disposable files and mocked process boundaries only; no installed game/process operation",
        "exactOwnedNormalClose": exact_close,
        "changedIdentityRejectedBeforeClose": identity_rejected,
        "normalCloseRefusalFailsClosed": rejected_normal_close,
        "restoreRetryAfterOwnedClose": restore_retry,
    }
    report["ok"] = (
        all(exact_close.values())
        and identity_rejected
        and rejected_normal_close
        and all(restore_retry.values())
    )
    print(json.dumps(report, indent=2))
    raise SystemExit(0 if report["ok"] else 1)
finally:
    shutil.rmtree(root, ignore_errors=True)
