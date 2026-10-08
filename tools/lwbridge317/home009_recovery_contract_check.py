"""HOME 009 C/D contract assertions over the recorded oracle and production traces.

Pure offline JSON checks (no executable, no process, no desktop). Scenario inputs:
recovery-scenarios.json; traces: recovery-oracle-trace.json (reconstructed original) and
after-C-recovery-production-trace.json (production OverviewLifecycleService on a virtual clock,
recorded on Windows). Asserts the contract points the retired Program.cs block used to contradict.
Reconstructed contract, NOT original execution.
"""
import json, sys
from pathlib import Path

D = Path(__file__).resolve().parents[2] / "evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-PARITY-CLOSEOUT-009"
TRACES = {"oracle": D / "recovery-oracle-trace.json", "production": D / "after-C-recovery-production-trace.json"}
fails = []

def need(ok, label):
    if not ok:
        fails.append(label)
    print(("PASS " if ok else "FAIL ") + label)

def ev(t, kind, **kw):
    return [e for e in t if e["kind"] == kind and all(e.get(k) == v for k, v in kw.items())]

def st(t, state, reason=None):
    return [e for e in t if e["kind"] == "status" and e["state"] == state and (reason is None or e.get("reason") == reason)]

for who, path in TRACES.items():
    tr = json.load(open(path))
    p = lambda s: f"[{who}] {s}"
    # immediate (event-origin / exit) start: process exit needs two 2 s observations, waiting then first launch 2 s later
    a = tr["process-exit-immediately"]
    need(st(a, "waiting", "processExit")[0]["t"] == 4000 and ev(a, "launch")[0]["t"] == 6000, p("process exit: two observations then launch next run tick"))
    # disconnect: 60 s offline -> waiting, further 60 s -> terminate, then relaunch next tick
    b = tr["bridge-offline-from-10s"]
    need(st(b, "waiting", "disconnect")[0]["t"] == 70000 and ev(b, "terminate")[0]["t"] == 130000 and ev(b, "launch")[0]["t"] == 132000,
         p("disconnect: 60 s + 60 s before terminate, relaunch next tick"))
    # login unavailable: 180 s + 180 s, then maintenance (not an immediate relaunch)
    c = tr["login-unavailable-from-20s"]
    need(st(c, "waiting", "disconnect")[0]["t"] == 200000 and ev(c, "terminate")[0]["t"] == 380000 and st(c, "maintenance")[0]["nextRetryAt"] == 500000,
         p("login unavailable: 180 s + 180 s, maintenance first retry 120 s"))
    # unknown game state counts unhealthy (escalation after events)
    d = tr["event-disconnect-unhealthy-escalates"]
    need(len(ev(d, "terminate")) == 1 and st(d, "maintenance"), p("unhealthy after event-origin disconnect escalates to terminate+maintenance"))
    # hang: 30 s
    h = tr["hang-from-10s"]
    need(st(h, "waiting", "hang")[0]["t"] == 40000 and ev(h, "terminate")[0]["t"] == 40000, p("hang: 30 s then terminate"))
    # retry table: 15s, then 30s, then 60s on launch failures
    f = [e for e in tr["launch-fails-forever"] if e["kind"] == "status" and e.get("nextRetryAt")]
    need([e["nextRetryAt"] - e["t"] for e in f[:3]] == [15000, 30000, 60000], p("normal retry table 15 s/30 s/60 s"))
    # reconnect disabled mid-recovery ends idle
    r = tr["reconnect-disabled-mid-recovery"]
    need(r[-1]["state"] == "idle", p("auto reconnect disabled finishes idle"))
    # ignored event forms start nothing
    for n in ("event-unconfirmed-ignored", "event-ambiguous-ignored", "event-unsupported-reason-ignored", "event-while-reconnect-disabled"):
        need(not ev(tr[n], "launch") and not ev(tr[n], "terminate"), p(f"{n} starts nothing"))
    need(not tr["idle-healthy"], p("healthy idle emits no effects"))

print(f"{len(fails)} failed")
sys.exit(1 if fails else 0)
