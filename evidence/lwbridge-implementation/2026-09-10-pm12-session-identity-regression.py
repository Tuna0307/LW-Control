"""LWB-PM12-003 isolated selected-process/session-identity regression.

This uses disposable files and mocked launcher/process/result boundaries only.
It does not inspect, launch, stop, or modify the installed Last War client.
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
    "pm12_identity_helper", repo / "tools" / "run_live_resource_probe.py"
)
helper = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(helper)


root = Path(tempfile.mkdtemp(prefix="lwbridge-pm12-identity-"))
try:
    selected_root = root / "selected"
    selected_game = selected_root / "Game" / "LastWar.exe"
    foreign_game = root / "foreign" / "Game" / "LastWar.exe"
    selected_game.parent.mkdir(parents=True)
    foreign_game.parent.mkdir(parents=True)

    inventory = json.dumps([
        {"pid": 4101, "path": str(foreign_game), "startedAtUtc": "2026-09-10T06:00:00Z"},
        {"pid": 4102, "path": str(selected_game), "startedAtUtc": "2026-09-10T06:00:01Z"},
    ])
    parsed = helper.parse_selected_game_processes(inventory, selected_game)
    process_scope = {
        "foreignInstallationExcluded": len(parsed) == 1 and parsed[0]["pid"] == 4102,
        "selectedPid": parsed[0]["pid"] if parsed else None,
        "selectedPath": parsed[0]["path"] if parsed else None,
    }

    p: dict[str, Path] = {
        "launcher": selected_root / "LastWarLauncher.exe",
        "game": selected_game,
        "runtime": root / "runtime",
        "backup_root": root / "backups",
    }
    candidate = root / "candidate"
    candidate.mkdir()
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

    result_path = root / "runtime" / "results" / "identity-request.json"
    result_path.parent.mkdir(parents=True)
    result_path.write_text("{}", encoding="utf-8")

    class FakeLauncher:
        pid = 4201

    def fake_verify(paths: dict[str, Path]) -> dict[str, object]:
        return {"packageSha256": helper.sha256_file(paths["data"]), "dummy": True}

    owned_game = {
        "pid": 4202,
        "path": str(selected_game),
        "startedAtUtc": "2026-09-10T06:10:00Z",
    }
    with patch.object(helper, "paths", return_value=p), \
         patch.object(helper, "selected_game_processes", return_value=[]), \
         patch.object(helper, "verify_current", side_effect=fake_verify), \
         patch.object(helper, "make_candidate", return_value={"dummy": True}), \
         patch.object(helper.tempfile, "mkdtemp", return_value=str(candidate)), \
         patch.object(helper.subprocess, "Popen", return_value=FakeLauncher()), \
         patch.object(helper, "await_owned_game_process", return_value=owned_game), \
         patch.object(helper, "await_result", return_value=({"requestId": "identity-request"}, result_path)):
        run_result = helper.run(
            "identity-request",
            10,
            game_root=selected_root,
            profile_id="profile-test",
        )

    command_text = (p["runtime"] / "command.txt").read_text(encoding="utf-8")
    launch_session_id = run_result.get("launchSessionId")
    owned_launch = {
        "profilePropagated": run_result.get("profileId") == "profile-test",
        "positiveExactPidPropagated": run_result.get("gamePid") == 4202,
        "selectedPathPropagated": helper._normalized_process_path(run_result.get("gamePath", ""))
            == helper._normalized_process_path(selected_game),
        "launcherPidRecorded": run_result.get("launcherPid") == 4201,
        "launchSessionGenerated": isinstance(launch_session_id, str) and len(launch_session_id) == 32,
        "commandContainsProfile": "profileId=profile-test\n" in command_text,
        "commandContainsExactPid": "gamePid=4202\n" in command_text,
        "commandContainsLaunchSession": isinstance(launch_session_id, str)
            and f"launchSessionId={launch_session_id}\n" in command_text,
        "originalTripletRestored": all(p[key].read_bytes() == value for key, value in originals.items()),
        "broadStopRemoved": not hasattr(helper, "stop_game"),
    }

    with patch.object(helper, "selected_game_processes", return_value=[owned_game]):
        try:
            helper.require_no_selected_game_process(p)
            unmanaged_rejected = False
        except helper.LiveResourceError as exc:
            unmanaged_rejected = "helper-owned launch session" in str(exc) and "4202" in str(exc)

    report = {
        "schemaVersion": 1,
        "findingId": "LWB-PM12-003",
        "scope": "isolated disposable files and mocked launcher/process boundaries only; no installed game/process operation",
        "processScope": process_scope,
        "ownedLaunchIdentity": owned_launch,
        "alreadyRunningSelectedInstallRejected": unmanaged_rejected,
    }
    report["ok"] = (
        all(process_scope.values())
        and all(owned_launch.values())
        and unmanaged_rejected
    )
    print(json.dumps(report, indent=2))
    raise SystemExit(0 if report["ok"] else 1)
finally:
    shutil.rmtree(root, ignore_errors=True)
