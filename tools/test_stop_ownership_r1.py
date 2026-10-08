"""HOME 009 R1 (LEAD009-01/02) Stop ownership/restoration tests through the actual run_stop handler.

Scope: inert. Actual production ``run_overview_bridge.run_stop`` / ``run_live_resource_probe`` code with a
scripted process-incarnation table (home009_process_table.py), an isolated runtime/journal and stub
restore/clear seams.  No real process is opened, terminated or restored; no desktop/live access.

Environment ``HOME009_TOOLS_DIR`` selects the tools directory that provides run_overview_bridge.py and
run_live_resource_probe.py; this lets the same scenarios be replayed against an archived pre-R1
revision (see home009_r1_before_after.py).  With ``--json`` the per-case record is printed.
"""
from __future__ import annotations

import json
import os
import sys
import tempfile
from pathlib import Path
from unittest.mock import patch

TOOLS = Path(os.environ.get("HOME009_TOOLS_DIR") or Path(__file__).resolve().parent)
HERE = Path(__file__).resolve().parent
for entry in (str(HERE / "lwbridge317"), str(TOOLS)):
    if entry not in sys.path:
        sys.path.insert(0, entry)
import run_overview_bridge as ov  # noqa: E402
import home009_process_table as pt  # noqa: E402

PROFILE = "overview_test_profile"
SESSION = "session_123"
PID = 42420
OLD, NEW = pt.OLD_STARTED, pt.NEW_STARTED


class LegacyPidApi:
    """Pre-R1 process API shape (PID-addressed open_terminate) over the same incarnation table."""

    def __init__(self, table: pt.ProcessTable):
        self.table = table

    def snapshot_pid_exists(self, pid):
        return self.table.snapshot_pid_exists(pid)

    def image_path(self, pid):
        return self.table.image_path(pid)

    def open_terminate(self, pid):
        return self.table.open_process(pid, pt.TERMINATE)

    def terminate(self, handle, code):
        return self.table.terminate(handle, code)

    def close_handle(self, handle):
        return self.table.close_handle(handle)


def make_state(runtime: Path, game: Path, backup: Path, *, started=OLD, stage="active_ready_deferred_restore"):
    runtime.mkdir(parents=True, exist_ok=True)
    state = {"schemaVersion": 1, "requestId": SESSION, "sessionId": SESSION, "profileId": PROFILE,
             "gamePid": PID, "gamePath": str(game), "backupPath": str(backup), "stage": stage, "originalFiles": {}}
    if started is not None:
        state["gameStartedAtUtc"] = started
    (runtime / "recovery.json").write_text(json.dumps(state), encoding="utf-8")


def run_case(setup, *, journal_started=OLD, request_started=OLD, restore_fails_first=False,
             legacy=False, stage="active_ready_deferred_restore"):
    """Run production run_stop once (twice for the restore-failure retry case) against a table."""
    legacy = legacy or os.environ.get("HOME009_LEGACY_API") == "1"
    with tempfile.TemporaryDirectory(prefix="home009-r1-stop-") as td:
        root = Path(td)
        runtime, backup, game = root / "runtime", root / "backup", root / "Game" / "LastWar.exe"
        game.parent.mkdir(parents=True)
        game.write_bytes(b"fake")
        backup.mkdir()
        paths = {"runtime": runtime, "game": game, "evidence_root": root / "evidence"}
        make_state(runtime, game, backup, started=journal_started, stage=stage)
        table = pt.ProcessTable()
        setup(table, str(game))
        record = {"restoreCalls": 0, "restoreSawLivePid": [], "journalStages": []}
        failures = {"restore": 1 if restore_fails_first else 0}

        def selected(_):
            rows = []
            for inc in table.incarnations:
                if table.alive(inc) and table._query_ok(inc) and pt.ProcessTable and inc.image.lower() == str(game).lower():
                    rows.append({"pid": inc.pid, "path": str(game), "startedAtUtc": inc.created})
            return rows

        def restore(*_):
            record["restoreCalls"] += 1
            record["restoreSawLivePid"].append(any(table.alive(i) for i in table.incarnations
                                                   if i.image.lower() == str(game).lower()))
            if failures["restore"] > 0:
                failures["restore"] -= 1
                raise ov.lr.LiveResourceError("restore failed (scripted)")
            return {"restored": True, "packageSha256": "inert-original"}

        def clear(_, __):
            (runtime / "recovery.json").unlink(missing_ok=True)

        def update_stage(p, state, value):
            state["stage"] = value
            record["journalStages"].append(value)
            (runtime / "recovery.json").write_text(json.dumps(state), encoding="utf-8")

        api = LegacyPidApi(table) if legacy else table
        outcome: dict = {}

        def call():
            return ov.run_stop(PROFILE, SESSION, PID, str(game), None, request_started)

        with patch.object(ov, "overview_paths", return_value=paths), \
                patch.object(ov.lr, "selected_game_processes", side_effect=selected), \
                patch.object(ov.lr, "process_api", return_value=api), \
                patch.object(ov.lr, "stop_sleep_milliseconds", side_effect=table.sleep_ms), \
                patch.object(ov.lr, "restore_backup", side_effect=restore), \
                patch.object(ov.lr, "clear_recovery", side_effect=clear), \
                patch.object(ov.lr, "update_recovery_stage", side_effect=update_stage):
            outcomes = []
            for attempt in range(2 if restore_fails_first else 1):
                try:
                    value = call()
                    outcomes.append({"ok": True, "close": value.get("close"), "gameRunning": value.get("gameRunning")})
                except (ov.OverviewBridgeError, ov.lr.LiveResourceError) as exc:
                    outcomes.append({"ok": False, "error": str(exc), "errorType": type(exc).__name__,
                                     "code": getattr(exc, "code", None)})
            outcome = outcomes[-1]
        journal = runtime / "recovery.json"
        journal_state = json.loads(journal.read_text(encoding="utf-8")) if journal.exists() else None
        return {"outcomes": outcomes, "outcome": outcome, "record": record, "table": table, "game": str(game),
                "journalPresent": journal.exists(), "journalState": journal_state}


# ---- scenarios -------------------------------------------------------------------------------

def live(game, **kw):
    return pt.Incarnation(pid=PID, image=game, created=OLD, label="captured", **kw)


def expect(condition, message):
    if not condition:
        raise AssertionError(message)


CASES = []


def case(fn):
    CASES.append(fn)
    return fn


@case
def lead009_01_poll_query_denied_while_pid_survives():
    """Lead inverse: terminate accepted, process survives, image query denied from 300 ms."""
    r = run_case(lambda t, g: t.add(live(g, exit_delay_after_terminate=None, query_denied_from=300)))
    o = r["outcome"]
    expect(not o["ok"] and o["code"] == "OWNED_EXIT_UNPROVEN", f"restoration must be withheld: {o}")
    expect(r["record"]["restoreCalls"] == 0 and r["journalPresent"], "restore/journal-clear while PID survives")
    expect(r["journalState"]["stage"] == "closing_owned_game_for_restore", r["journalState"]["stage"])
    expect(r["journalState"]["stopOutcome"]["code"] == "OWNED_EXIT_UNPROVEN", "truthful journal outcome missing")
    expect(r["table"].open_handles() == 0, "handle leaked")
    return {"finding": "LEAD009-01", "outcome": o["code"] if "code" in o else o}


@case
def delayed_exit_after_denied_query_is_proven_by_handle():
    r = run_case(lambda t, g: t.add(live(g, exit_delay_after_terminate=3000, query_denied_from=300)))
    o = r["outcome"]
    expect(o["ok"], o)
    expect(o["close"]["exitProof"]["method"] == "process-handle-signalled", o["close"]["exitProof"])
    expect(r["record"]["restoreCalls"] == 1 and not r["journalPresent"], "restoration expected after proven exit")
    expect(r["record"]["restoreSawLivePid"] == [False], "restore must happen after captured exit")
    return {"finding": "LEAD009-01"}


@case
def final_poll_boundary_denied_at_check_100_exit_within_budget():
    # poll ends at the 100th check (clock 9900); the remaining proof budget is the 1000 ms minimum.
    r = run_case(lambda t, g: t.add(live(g, exit_delay_after_terminate=10400, query_denied_from=9900)))
    expect(r["outcome"]["ok"] and r["record"]["restoreCalls"] == 1, r["outcome"])
    return {}


@case
def final_poll_boundary_denied_at_check_100_exit_after_budget():
    r = run_case(lambda t, g: t.add(live(g, exit_delay_after_terminate=11500, query_denied_from=9900)))
    o = r["outcome"]
    expect(not o["ok"] and o["code"] == "OWNED_EXIT_UNPROVEN" and r["record"]["restoreCalls"] == 0
           and r["journalPresent"], o)
    return {}


@case
def timeout_when_game_visibly_survives_keeps_journal():
    r = run_case(lambda t, g: t.add(live(g, exit_delay_after_terminate=None)))
    o = r["outcome"]
    expect(not o["ok"] and "did not close in time" in o["error"], o)
    expect(r["record"]["restoreCalls"] == 0 and r["journalPresent"], "restore/clear after timeout")
    return {}


@case
def already_absent_restores_without_touching_a_process():
    r = run_case(lambda t, g: t.add(live(g, exit_at=0)))
    o = r["outcome"]
    expect(o["ok"] and o["close"]["alreadyExited"] and o["close"]["exitProof"]["method"] == "pid-absent", o)
    expect(r["table"].handles == [] and r["record"]["restoreCalls"] == 1 and not r["journalPresent"], "state")
    return {}


@case
def denial_before_termination_open_denied_with_pid_present():
    r = run_case(lambda t, g: t.add(live(g, terminate_open_denied=True)))
    o = r["outcome"]
    expect(not o["ok"] and "open target process failed" in o["error"], o)
    expect(r["record"]["restoreCalls"] == 0 and r["journalPresent"] and r["table"].open_handles() == 0, "state")
    return {}


@case
def image_unreadable_while_pid_present_is_query_failure_not_exit():
    r = run_case(lambda t, g: t.add(live(g, query_always_denied=True)))
    o = r["outcome"]
    expect(not o["ok"] and "PROCESS_QUERY_FAILED" in o["error"], o)
    expect(r["record"]["restoreCalls"] == 0 and r["journalPresent"] and r["table"].open_handles() == 0, "state")
    return {}


@case
def denial_after_termination_terminate_call_fails_while_alive():
    r = run_case(lambda t, g: t.add(live(g, terminate_call_fails=True)))
    o = r["outcome"]
    expect(not o["ok"] and "terminate target process failed" in o["error"], o)
    expect(r["record"]["restoreCalls"] == 0 and r["journalPresent"] and r["table"].open_handles() == 0, "state")
    return {}


@case
def terminate_call_fails_because_process_already_exited_restores():
    def setup(t, g):
        inc = t.add(live(g, terminate_call_fails=True))
        t.hooks["terminate"] = lambda tab: setattr(inc, "exit_at", tab.clock())
    r = run_case(setup)
    expect(r["outcome"]["ok"] and r["record"]["restoreCalls"] == 1, r["outcome"])
    return {}


@case
def surviving_second_installation_process_withholds_restoration():
    def setup(t, g):
        t.add(live(g, exit_delay_after_terminate=500))
        # A second process of the selected installation appears while the owned one is exiting.
        t.hooks["handle_wait_exit"] = lambda tab: tab.add(pt.Incarnation(pid=777, image=g, created=NEW, label="other"))
    r = run_case(setup)
    o = r["outcome"]
    expect(not o["ok"] and o["code"] == "OWNED_INSTALLATION_STILL_RUNNING", o)
    expect(r["record"]["restoreCalls"] == 0 and r["journalPresent"], "restore under surviving installation process")
    return {"finding": "LEAD009-01"}


@case
def restoration_failure_keeps_journal_and_retry_succeeds():
    r = run_case(lambda t, g: t.add(live(g)), restore_fails_first=True)
    first, second = r["outcomes"]
    expect(not first["ok"] and "restore failed" in first["error"], first)
    expect(second["ok"] and second["close"]["alreadyExited"], second)
    expect(r["record"]["restoreCalls"] == 2 and not r["journalPresent"], "retry should finish restoration")
    expect("restoring_after_owned_game_exit" in r["record"]["journalStages"], r["record"]["journalStages"])
    return {}


@case
def lead009_02_same_path_replacement_before_open_is_not_terminated():
    def setup(t, g):
        t.add(live(g))
        t.hooks["open_process"] = lambda tab: (tab.hooks.pop("open_process"),
                                               tab.replace(PID, g, NEW, label="replacement"))[1]
    r = run_case(setup)
    o = r["outcome"]
    expect(not o["ok"] and "creation identity changed before termination" in o["error"], o)
    expect("replacement" not in r["table"].terminated(), "replacement incarnation was terminated")
    expect(r["record"]["restoreCalls"] == 0 and r["journalPresent"] and r["table"].open_handles() == 0, "state")
    return {"finding": "LEAD009-02"}


@case
def lead009_02_replacement_after_validation_before_terminate_hits_only_the_opened_handle():
    def setup(t, g):
        t.add(live(g, exit_delay_after_terminate=0))
        t.hooks["terminate"] = lambda tab: (tab.hooks.pop("terminate"), tab.replace(PID, g, NEW, label="replacement"))[1]
    r = run_case(setup)
    o = r["outcome"]
    # The opened handle is bound to the captured incarnation, which is already replaced when TerminateProcess
    # runs: the call hits the dead captured object.  The replacement must survive and restoration is withheld.
    expect("replacement" not in r["table"].terminated(), "replacement was terminated through a PID re-open")
    expect(r["record"]["restoreCalls"] == 0 and r["journalPresent"], (o, r["record"]))
    return {"finding": "LEAD009-02"}


@case
def different_path_replacement_is_not_terminated_and_captured_exit_is_proven():
    def setup(t, g):
        t.add(live(g))
        t.hooks["open_process"] = lambda tab: (tab.hooks.pop("open_process"),
                                               tab.replace(PID, "C:\\Other\\Tool.exe", NEW, label="other-program"))[1]
    r = run_case(setup)
    o = r["outcome"]
    expect(o["ok"] and o["close"]["exitProof"]["method"] == "foreign-image", o)
    expect(r["table"].terminated() == [] and r["record"]["restoreCalls"] == 1, "foreign process touched")
    return {"finding": "LEAD009-02"}


@case
def handle_creation_time_unreadable_is_refused():
    def setup(t, g):
        t.add(live(g))
        t.handle_creation_utc = lambda handle: None  # access failure reading the handle's creation time
    r = run_case(setup)
    o = r["outcome"]
    expect(not o["ok"] and "could not be read from the process handle" in o["error"], o)
    expect(r["table"].terminated() == [] and r["record"]["restoreCalls"] == 0 and r["journalPresent"], "state")
    return {}


@case
def journal_without_creation_identity_never_terminates_a_live_pid():
    r = run_case(lambda t, g: t.add(live(g)), journal_started=None, request_started=None)
    o = r["outcome"]
    expect(not o["ok"], o)
    expect(r["table"].terminated() == [] and r["record"]["restoreCalls"] == 0 and r["journalPresent"], "state")
    return {}


@case
def inventory_replacement_still_refused_before_any_open():
    def setup(t, g):
        t.add(pt.Incarnation(pid=PID, image=g, created=NEW, label="replacement"))
    r = run_case(setup)
    o = r["outcome"]
    expect(not o["ok"] and "creation identity changed" in o["error"], o)
    expect(r["table"].calls.get("open_process", 0) == 0 and r["record"]["restoreCalls"] == 0, "opened a process")
    return {}


@case
def legitimate_termination_restores_after_handle_confirmed_exit():
    r = run_case(lambda t, g: t.add(live(g, exit_delay_after_terminate=250)))
    o = r["outcome"]
    expect(o["ok"] and o["close"]["method"] == "TerminateProcess" and o["close"]["exitProof"]["established"], o)
    expect(r["table"].terminated() == ["captured"] and r["record"]["restoreCalls"] == 1, "state")
    expect(r["record"]["restoreSawLivePid"] == [False] and r["table"].open_handles() == 0, "restore before exit")
    return {}


def main() -> int:
    emit_json = "--json" in sys.argv
    results, failures = [], []
    for fn in CASES:
        try:
            info = fn() or {}
            results.append({"case": fn.__name__, "pass": True, **info})
        except Exception as exc:  # an unexpected shape (e.g. a missing exit proof) is a failed obligation
            failures.append(fn.__name__)
            results.append({"case": fn.__name__, "pass": False,
                            "assertion": f"{type(exc).__name__}: {str(exc)[:400]}"})
    report = {"toolsDir": str(TOOLS), "total": len(results), "failed": failures, "results": results}
    if emit_json:
        print(json.dumps(report, indent=2))
    else:
        print(json.dumps({"ok": not failures, "total": len(results), "failed": failures}, separators=(",", ":")))
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
