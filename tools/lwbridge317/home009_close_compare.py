"""Home 009 checkpoint A comparator: reconstructed 0.3.17 Stop contract vs the production Stop handler.

The oracle is ``home009_oracle.original_stop_close`` (a reconstruction bound to RVAs by
``home009_contract.verify_close_contract``).  The production side is the real
``run_overview_bridge.run_stop`` handler with inert seams (fake process table, fake restore,
virtual clock).  No process is created, closed or terminated; no desktop/live access.

Usage:  python home009_close_compare.py [--out FILE]   (exit status 1 on any mismatch)
"""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
import tempfile
from pathlib import Path
from unittest.mock import patch

HERE = Path(__file__).resolve().parent
TOOLS = HERE.parent
for candidate in (HERE, TOOLS):
    if str(candidate) not in sys.path:
        sys.path.insert(0, str(candidate))

import home009_contract
import home009_oracle as oracle
import home009_process_table as ptable
import run_overview_bridge as ov
import test_overview_bridge_lifecycle as base

PID = base.PID
STARTED = base.OLD_STARTED


def scenarios() -> list[dict]:
    rows = []

    def add(name, model=None, accepts_close=True, window_exit_ms=None, note=""):
        rows.append({"name": name, "model": model or {}, "acceptsClose": accepts_close,
                     "windowExitMs": window_exit_ms, "note": note})

    add("already-absent", {"gone_at": 0})
    add("image-reused-by-other-process", {"image_override": "C:\\Other\\LastWar.exe", "dies_on_terminate_after": 0})
    for ms in (0, 100, 5000, 9800, 9899, 9900, 9901, 9950, 9999, 10000, 10001, 15000):
        add(f"terminate-exits-after-{ms}ms", {"dies_on_terminate_after": ms}, accepts_close=True, window_exit_ms=ms)
    add("never-exits", {"dies_on_terminate_after": None}, accepts_close=True, window_exit_ms=None)
    add("game-ignores-normal-close", {"dies_on_terminate_after": 0}, accepts_close=False)
    add("image-path-unreadable-while-present", {"path_unavailable": True})
    add("open-terminate-denied-present", {"open_terminate_fails": True})
    add("terminate-call-fails-present", {"terminate_fails": True})
    add("open-race-process-vanishes", {"vanish_on_open": True})
    add("poll-query-denied-after-terminate", {"dies_on_terminate_after": None, "query_denied_during_poll_at": 300})
    return rows


def build_model(spec: dict, game: Path) -> oracle.VirtualProcessModel:
    kwargs = dict(spec)
    image = kwargs.pop("image_override", str(game))
    vanish_on_open = kwargs.pop("vanish_on_open", False)
    model = oracle.VirtualProcessModel(pid=PID, image=image, **kwargs)
    model.vanish_on_open = vanish_on_open  # type: ignore[attr-defined]
    if vanish_on_open:
        original_open = model.open_terminate

        def open_and_vanish(pid):
            model.gone_at = model.now
            return original_open(pid)

        model.open_terminate = open_and_vanish  # type: ignore[method-assign]
    return model


def run_oracle(spec: dict, game: Path) -> dict:
    model = build_model(spec["model"], game)
    out = oracle.original_stop_close(model, model.sleep_ms, PID, str(game))
    return {"kind": out.kind, "error": out.error_kind, "terminated": out.terminated,
            "terminateCalls": model.terminate_calls, "elapsedMs": model.now}


def classify_error(text: str) -> str:
    if oracle.CLOSE_TIMEOUT_TEXT in text:
        return "CLOSE_TIMEOUT"
    if "OWNED_EXIT_UNPROVEN" in text or "OWNED_INSTALLATION_STILL_RUNNING" in text:
        return "RESTORATION_WITHHELD"
    if "creation identity" in text:
        return "IDENTITY_REFUSED"
    if "PROCESS_QUERY_FAILED" in text:
        return "PROCESS_QUERY_FAILED"
    if "open target process" in text or "terminate target process" in text:
        return "TERMINATE_FAILED"
    return "OTHER:" + text[:80]


def build_table(spec: dict, game: Path) -> ptable.ProcessTable:
    """Production-side process table (handle API) scripted from the same scenario spec as the oracle."""
    m = spec["model"]
    inc = ptable.Incarnation(
        pid=PID, image=m.get("image_override", str(game)), created=STARTED,
        exit_at=m.get("gone_at"),
        exit_delay_after_terminate=m.get("dies_on_terminate_after"),
        query_denied_from=m.get("query_denied_during_poll_at"),
        query_always_denied=bool(m.get("path_unavailable")),
        terminate_open_denied=bool(m.get("open_terminate_fails")),
        terminate_call_fails=bool(m.get("terminate_fails")))
    table = ptable.ProcessTable()
    table.add(inc)
    if m.get("vanish_on_open"):
        def vanish(t):
            inc.exit_at = t.clock()
        table.hooks["open_process"] = vanish
    return table


def alive_after_budget(table: ptable.ProcessTable, game: Path) -> bool:
    """Independent process-table truth: is the captured incarnation still alive after the Stop budget?

    A scenario whose only incarnation runs a different image models "PID reused by another program":
    the captured game process is then already gone.
    """
    inc = table.incarnations[0]
    if not oracle.ascii_ci_equal(inc.image, str(game)):
        return False
    horizon = table.clock() + max(1000, (oracle.CLOSE_MAX_CHECKS * oracle.CLOSE_POLL_MS))
    return inc.exit_at is None or horizon < inc.exit_at


def run_production(spec: dict) -> dict:
    with tempfile.TemporaryDirectory(prefix="lwbridge-home009-stop-") as td:
        _, runtime, backup, game, paths = base.fixture_root(td)
        base.write_state(runtime, game, backup)
        table = build_table(spec, game)

        def selected(_):
            inc = table.by_pid(PID)
            if inc is not None and table._query_ok(inc) and oracle.ascii_ci_equal(inc.image, str(game)):
                return [{"pid": PID, "path": str(game), "startedAtUtc": STARTED}]
            return []

        def clear_recovery(p, state):
            (runtime / "recovery.json").unlink(missing_ok=True)

        restore_calls = {"count": 0}

        def restore(*_):
            restore_calls["count"] += 1
            return {"restored": True, "packageSha256": "original"}

        result: dict
        with patch.object(ov, "overview_paths", return_value=paths),              patch.object(ov.lr, "selected_game_processes", side_effect=selected),              patch.object(ov.lr, "restore_backup", side_effect=restore),              patch.object(ov.lr, "clear_recovery", side_effect=clear_recovery),              patch.object(ov.lr, "update_recovery_stage", side_effect=lambda p, state, value: state.__setitem__("stage", value)),              patch.object(ov.lr, "process_api", return_value=table),              patch.object(ov.lr, "stop_sleep_milliseconds", side_effect=table.sleep_ms),              patch.object(ov.lr.subprocess, "run", side_effect=AssertionError("Stop must not use a graceful PowerShell closer")):
            try:
                value = ov.run_stop(base.PROFILE, base.SESSION, PID, str(game), None, STARTED)
            except ov.StopRestorationWithheld as exc:
                # Original close contract completed; the restoration gate (technical adaptation) withheld.
                close = exc.close
                result = {"kind": "ok", "error": "", "terminated": bool(close.get("accepted"))
                          and close.get("method") == "TerminateProcess", "restorationWithheld": exc.code}
            except (ov.OverviewBridgeError, ov.lr.LiveResourceError) as exc:
                result = {"kind": "error", "error": classify_error(str(exc)), "terminated": False}
            else:
                close = value["close"]
                result = {"kind": "ok", "error": "", "terminated": bool(close.get("accepted"))
                          and close.get("method") == "TerminateProcess"}
        result["terminateCalls"] = sum(i.terminated_calls for i in table.incarnations)
        result["elapsedMs"] = table.now
        result["restoreCalls"] = restore_calls["count"]
        result["journalPreserved"] = (runtime / "recovery.json").exists()
        result["aliveAtEnd"] = alive_after_budget(table, game)
        result["openHandlesLeaked"] = table.open_handles()
        return result


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--out")
    args = parser.parse_args()
    facts = home009_contract.verify_close_contract()
    rows, mismatches = [], []
    for spec in scenarios():
        with tempfile.TemporaryDirectory() as td:
            game = Path(td) / "Game" / "LastWar.exe"
            expected = run_oracle(spec, game)
        actual = run_production(spec)
        # An error carries no `terminated` value in production (an exception, not a result record),
        # so that key is compared only for successful outcomes. terminateCalls/elapsedMs always.
        compare_keys = ("kind", "error", "terminateCalls", "elapsedMs") + (
            ("terminated",) if expected["kind"] == "ok" else ())
        same = all(expected[k] == actual.get(k) for k in compare_keys)
        # Technical restoration gate (R1 A): for an original-ok close the installation may only be
        # restored (journal cleared) when the independently scripted process table shows the
        # captured process gone; otherwise restoration is withheld and the journal is preserved.
        if expected["kind"] == "ok":
            gate_ok = (actual["restoreCalls"] == (0 if actual["aliveAtEnd"] else 1)
                       and actual["journalPreserved"] == actual["aliveAtEnd"]
                       and bool(actual.get("restorationWithheld")) == actual["aliveAtEnd"])
        else:
            gate_ok = actual["restoreCalls"] == 0 and actual["journalPreserved"]
        same = same and gate_ok and actual["openHandlesLeaked"] == 0
        rows.append({"scenario": spec["name"], "oracle": expected, "production": actual, "equal": same})
        if not same:
            mismatches.append(spec["name"])
    report = {
        "workItem": "LWB317-FUNCTION-HOME-PARITY-CLOSEOUT-009", "checkpoint": "A",
        "oracle": "home009_oracle.original_stop_close (RECONSTRUCTED from 0.3.17 RVAs, not original execution)",
        "contractFacts": facts, "scenarioCount": len(rows), "mismatches": mismatches, "rows": rows,
    }
    text = json.dumps(report, indent=2, sort_keys=True)
    if args.out:
        Path(args.out).write_text(text + "\n", encoding="utf-8")
    print(text if not args.out else f"{len(rows)} scenarios, {len(mismatches)} mismatches")
    return 1 if mismatches else 0


if __name__ == "__main__":
    raise SystemExit(main())
