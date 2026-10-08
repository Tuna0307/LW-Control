"""Scenario world model + oracle runner for Home 009 monitor/recovery comparisons.

A scenario is plain JSON (shared verbatim with the C# trace harness).  Both sides implement the
same tiny world:

* Game *instances* (pid, start time).  Instance 0 starts alive at t=0 and uses the absolute
  piecewise signals below; instances created by a successful launch use the ``launch`` block
  (relative times).
* Signals are piecewise-constant lists ``[[t, value], ...]`` (value in force from time t).
* ``logs`` entries append text to a log file at time ``t``; readers only see text appended after
  they were initialised (init offset = size at recovery start).

Event trace (``kind``): terminate, killUpdaters, launch, status.
"""
from __future__ import annotations

import json
import random
import sys
from dataclasses import asdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
if str(HERE) not in sys.path:
    sys.path.insert(0, str(HERE))

import home009_recovery_oracle as o

BASE_PID = 41000


def piece(signal, t, default=None):
    value = default
    for at, v in signal:
        if at <= t:
            value = v
        else:
            break
    return value


# ---- log classifier (0x419e2a) ------------------------------------------------------------------
PLAYER_UPDATE_MARKERS = ["downloadupdatestate", "download_start", "download_finish", "dll version changed"]
LAUNCHER_UPDATE_MARKERS = ["updating from path", "installing from path", "copying launcher"]
PLAYER_ERROR_CONTAINS = ["downloadupdate error", "disk full"]
PLAYER_ERROR_TOKENS = ["e115", "e123", "e900"]
LAUNCHER_ERROR_CONTAINS = ["download failed", "update failed", "disk full"]
UPDATER_ERROR_CONTAINS = ["update failed", "install failed", "disk full"]


def has_token(hay: str, needle: str) -> bool:
    """0x41d0df: case-sensitive occurrence whose neighbours are not ASCII alphanumeric."""
    start = 0
    while True:
        i = hay.find(needle, start)
        if i < 0:
            return False
        before = hay[i - 1] if i > 0 else ""
        after = hay[i + len(needle)] if i + len(needle) < len(hay) else ""
        if not (before.isascii() and before.isalnum()) and not (after.isascii() and after.isalnum()):
            return True
        start = i + 1


def classify(player: str, launcher: str, updater: str):
    update = (any(m in player for m in PLAYER_UPDATE_MARKERS) or has_token(player, "e999")
              or any(m in launcher for m in LAUNCHER_UPDATE_MARKERS))
    maint = ("connect to server failed" in player) or ("loading error" in player and has_token(player, "e109"))
    err = (any(m in player for m in PLAYER_ERROR_CONTAINS)
           or any(has_token(player, c) for c in PLAYER_ERROR_TOKENS)
           or any(m in launcher for m in LAUNCHER_ERROR_CONTAINS)
           or any(m in updater for m in UPDATER_ERROR_CONTAINS))
    activity = update or len(launcher) != 0 or len(updater) != 0
    return activity, update, maint, (o.LOG_ERROR_TEXT if err else None)


class World(o.Env):
    def __init__(self, scenario: dict):
        self.sc = scenario
        self.t = 0
        self.events: list[dict] = []
        self.instances = [{"pid": BASE_PID, "start": 0, "alive": True, "rel": False,
                           "crash_at": scenario.get("initial", {}).get("crashAt")}]
        self.launches = 0
        self.updaters_killed = False
        self.log_consumed = {"player": 0, "launcher": 0, "updater": 0}
        self.log_text = {"player": "", "launcher": "", "updater": ""}
        self.log_pending = sorted(scenario.get("logs", []), key=lambda e: e[0])
        self.log_reader_started = False
        self.run_id = 0
        self.config_trail = scenario.get("autoReconnect", [[0, True]])

    # time/log plumbing
    def set_time(self, t: int) -> None:
        self.t = t
        for inst in self.instances:
            if inst["alive"] and inst.get("crash_at") is not None and t >= inst["crash_at"]:
                inst["alive"] = False
        while self.log_pending and self.log_pending[0][0] <= t:
            _, which, text = self.log_pending.pop(0)
            self.log_text[which] += text

    def current(self):
        return self.instances[-1]

    # ---- Env
    def now(self): return self.t
    def config_reconnect(self): return bool(piece(self.config_trail, self.t, True))
    def desired(self): return bool(piece(self.sc.get("desired", [[0, True]]), self.t, True))
    def armed(self): return True
    def run_id_matches(self, run_id): return run_id == self.run_id
    def root_available(self): return bool(piece(self.sc.get("rootAvailable", [[0, True]]), self.t, True))
    def tracked_pid(self): return self.current()["pid"]

    def find_pid(self):
        inst = self.current()
        return inst["pid"] if inst["alive"] else 0

    def _signal(self, name: str, default):
        inst = self.current()
        if not inst["rel"]:
            return piece(self.sc.get("initial", {}).get(name, [[0, default]]), self.t, default)
        rel = self.t - inst["start"]
        return piece(self.sc.get("launch", {}).get(name, [[0, default]]), rel, default)

    def online(self): return self.find_pid() != 0 and bool(self._signal("online", True))
    def healthy(self, pid): return self.find_pid() == pid and bool(self._signal("healthy", True))
    def hung(self, pid): return self.find_pid() == pid and bool(self._signal("hung", False))

    def update_running(self):
        return (not self.updaters_killed) and bool(piece(self.sc.get("updating", [[0, False]]), self.t, False))

    def fingerprint(self): return piece(self.sc.get("fingerprint", [[0, 0]]), self.t, 0)

    def classify_logs(self):
        texts = {}
        for k in ("player", "launcher", "updater"):
            texts[k] = self.log_text[k][self.log_consumed[k]:] if self.log_reader_started else ""
            self.log_consumed[k] = len(self.log_text[k])
        self.log_reader_started = True
        return classify(texts["player"], texts["launcher"], texts["updater"])

    def start_log_readers(self):
        # 0x41a18d/0x41e950: initial offset = current size
        for k in ("player", "launcher", "updater"):
            self.log_consumed[k] = len(self.log_text[k])
        self.log_reader_started = True

    def terminate_game(self, pid):
        self.events.append({"t": self.t, "kind": "terminate", "pid": pid})
        if bool(piece(self.sc.get("terminateFails", [[0, False]]), self.t, False)):
            return False
        for inst in self.instances:
            if inst["pid"] == pid:
                inst["alive"] = False
        return True

    def kill_updaters(self):
        self.events.append({"t": self.t, "kind": "killUpdaters"})
        if self.sc.get("killUpdatersStopsUpdating", True):
            self.updaters_killed = True

    def launch(self):
        self.launches += 1
        fails = self.sc.get("launch", {}).get("fails", [])
        self.events.append({"t": self.t, "kind": "launch", "n": self.launches})
        if self.launches <= len(fails) and fails[self.launches - 1]:
            return "err"
        crash = self.sc.get("launch", {}).get("crashAfterMs")
        self.instances.append({"pid": BASE_PID + self.launches, "start": self.t, "alive": True, "rel": True,
                               "crash_at": (self.t + crash) if crash is not None else None})
        return "ok"


def status_event(t: int, st: o.Status) -> dict:
    return {"t": t, "kind": "status", "state": st.state, "reason": st.reason, "attempts": st.attempts,
            "nextRetryAt": st.next_retry_at, "error": st.error, "updateDetected": st.update_detected,
            "restarted": st.restarted, "completedAt": st.completed_at, "noticeId": st.notice_id}


def run_oracle(scenario: dict) -> list[dict]:
    world = World(scenario)
    store = o.Store()
    monitor = o.Monitor()
    run = None
    next_run_tick = None
    duration = scenario["durationMs"]
    seen_status = 0

    def flush(t):
        nonlocal seen_status
        while seen_status < len(store.events):
            world.events.append(status_event(t, store.events[seen_status]))
            seen_status += 1

    t = 0
    while t <= duration:
        due = [t]
        # a monitor tick and a run tick may both be due at t; the monitor ticks first (arbitrary but fixed)
        world.set_time(t)
        if run is not None and run.finished:
            run = None
        decision = monitor.tick(world, store)
        flush(t)
        if decision is not None and run is None:
            reason, update_flag = decision
            world.run_id += 1
            store.start(reason, update_flag, t)
            flush(t)
            run = o.Run(world.run_id, reason, t)
            world.start_log_readers()
            run.init(world, store)
            flush(t)
            next_run_tick = t + o.TICK_MS
        if run is not None and not run.finished and next_run_tick is not None and next_run_tick <= t:
            run.tick(world, store)
            flush(t)
            next_run_tick = t + o.TICK_MS
        t += o.TICK_MS
    return world.events


# ---- scenario catalogue ---------------------------------------------------------------------------
def base(name, **kw):
    sc = {"name": name, "durationMs": 400000, "autoReconnect": [[0, True]],
          "initial": {"online": [[0, True]], "healthy": [[0, True]], "hung": [[0, False]]},
          "updating": [[0, False]], "fingerprint": [[0, 0]], "logs": [],
          "launch": {"onlineAfterMs": 0, "online": [[0, True]], "healthy": [[0, True]], "fails": []},
          "terminateFails": [[0, False]]}
    for k, v in kw.items():
        if isinstance(v, dict) and isinstance(sc.get(k), dict):
            sc[k] = {**sc[k], **v}
        else:
            sc[k] = v
    return sc


def catalogue() -> list[dict]:
    S = []
    S.append(base("idle-healthy"))
    S.append(base("process-exit-at-30s", initial={"crashAt": 30000}))
    S.append(base("process-exit-immediately", initial={"crashAt": 2000}))
    S.append(base("bridge-offline-from-10s",
                  initial={"online": [[0, True], [10000, False]], "healthy": [[0, True], [10000, False]]}))
    S.append(base("bridge-offline-then-back-at-100s",
                  initial={"online": [[0, True], [10000, False], [100000, True]],
                           "healthy": [[0, True], [10000, False], [100000, True]]}))
    S.append(base("bridge-offline-back-at-140s",
                  initial={"online": [[0, True], [10000, False], [140000, True]],
                           "healthy": [[0, True], [10000, False], [140000, True]]}))
    S.append(base("hang-from-10s",
                  initial={"online": [[0, True], [10000, False]], "healthy": [[0, True], [10000, False]],
                           "hung": [[0, False], [10000, True]]}))
    S.append(base("login-unavailable-from-20s",
                  initial={"online": [[0, True]], "healthy": [[0, True], [20000, False]]}))
    S.append(base("login-unavailable-never-recovers-launch-ok-unhealthy",
                  initial={"online": [[0, True]], "healthy": [[0, True], [20000, False]]},
                  launch={"healthy": [[0, False]], "online": [[0, True]]}, durationMs=900000))
    S.append(base("disabled-reconnect-offline",
                  autoReconnect=[[0, False]],
                  initial={"online": [[0, True], [10000, False]], "healthy": [[0, True], [10000, False]]}))
    S.append(base("reconnect-enabled-late-offline-stale",
                  autoReconnect=[[0, False], [200000, True]],
                  initial={"online": [[0, True], [10000, False]], "healthy": [[0, True], [10000, False]]}))
    S.append(base("update-process-running-offline",
                  initial={"online": [[0, True], [10000, False]], "healthy": [[0, True], [10000, False]]},
                  updating=[[0, False], [10000, True]]))
    S.append(base("launch-fails-forever",
                  initial={"crashAt": 30000}, launch={"fails": [True] * 12}, durationMs=1500000))
    S.append(base("launch-fails-three-times-then-ok",
                  initial={"crashAt": 30000}, launch={"fails": [True, True, True, False]}, durationMs=600000))
    S.append(base("terminate-fails-hang",
                  initial={"online": [[0, True], [10000, False]], "healthy": [[0, True], [10000, False]],
                           "hung": [[0, False], [10000, True]]},
                  terminateFails=[[0, True], [90000, False]]))
    S.append(base("server-unavailable-log-after-exit",
                  initial={"crashAt": 30000},
                  logs=[[34000, "player", "connect to server failed\n"]], durationMs=900000))
    S.append(base("update-log-error", initial={"online": [[0, True], [10000, False]],
                                                "healthy": [[0, True], [10000, False]]},
                  logs=[[80000, "launcher", "update failed\n"]], durationMs=600000))
    S.append(base("update-stall-15min",
                  initial={"crashAt": 30000}, updating=[[0, False], [36000, True]], durationMs=1500000))
    S.append(base("update-activity-keeps-alive",
                  initial={"crashAt": 30000}, updating=[[0, False], [36000, True]],
                  fingerprint=[[0, 0]] + [[40000 + i * 600000, i + 1] for i in range(3)], durationMs=1500000))
    S.append(base("reconnect-disabled-mid-recovery",
                  autoReconnect=[[0, True], [45000, False]], initial={"crashAt": 30000}, durationMs=300000))
    S.append(base("launch-then-unhealthy-after-2s-flap",
                  initial={"crashAt": 30000},
                  launch={"healthy": [[0, False], [4000, True], [14000, False], [16000, True]]}, durationMs=300000))
    S.append(base("crash-loop", initial={"crashAt": 30000}, launch={"crashAfterMs": 20000}, durationMs=900000))
    return S


def random_scenarios(seed: int, count: int) -> list[dict]:
    rng = random.Random(seed)
    out = []
    for i in range(count):
        dur = rng.choice([300000, 600000, 1200000])
        sc = base(f"random-{seed}-{i}", durationMs=dur)
        def pw(vals, n):
            ts = sorted(rng.sample(range(2000, dur - 2000, 2000), k=min(n, 20)))
            cur = vals[0]
            sig = [[0, cur]]
            for t in ts:
                cur = rng.choice(vals)
                sig.append([t, cur])
            return sig
        sc["initial"] = {"online": pw([True, False], rng.randint(0, 6)),
                         "healthy": pw([True, False], rng.randint(0, 6)),
                         "hung": pw([False, False, True], rng.randint(0, 3)),
                         "crashAt": rng.choice([None, None, rng.randrange(4000, dur // 2, 2000)])}
        sc["updating"] = pw([False, False, True], rng.randint(0, 4))
        sc["fingerprint"] = [[0, 0]] + [[t, rng.randint(0, 3)] for t in sorted(rng.sample(range(2000, dur, 2000), k=rng.randint(0, 3)))]
        sc["autoReconnect"] = pw([True, True, False], rng.randint(0, 2))
        sc["terminateFails"] = pw([False, False, True], rng.randint(0, 2))
        sc["launch"] = {"fails": [rng.random() < 0.4 for _ in range(rng.randint(0, 6))],
                        "online": pw([True, False], rng.randint(0, 2)), "healthy": pw([True, False], rng.randint(0, 3)),
                        "crashAfterMs": rng.choice([None, None, rng.randrange(6000, 60000, 2000)])}
        texts = ["connect to server failed\n", "Loading error : E109\n", "update failed\n", "disk full\n",
                 "downloadupdate error\n", "updating from path\n", "E115\n", "noise\n"]
        files = ["player", "launcher", "updater"]
        sc["logs"] = sorted([[rng.randrange(2000, dur - 2000, 2000), rng.choice(files), rng.choice(texts)]
                             for _ in range(rng.randint(0, 5))])
        out.append(sc)
    return out


def main() -> int:
    import argparse
    ap = argparse.ArgumentParser()
    ap.add_argument("--emit-scenarios")
    ap.add_argument("--emit-oracle")
    ap.add_argument("--seed", type=int, default=9)
    ap.add_argument("--random", type=int, default=200)
    a = ap.parse_args()
    scs = catalogue() + random_scenarios(a.seed, a.random)
    if a.emit_scenarios:
        Path(a.emit_scenarios).write_text(json.dumps(scs, indent=1), encoding="utf-8")
    if a.emit_oracle:
        res = {sc["name"]: run_oracle(sc) for sc in scs}
        Path(a.emit_oracle).write_text(json.dumps(res, indent=1), encoding="utf-8")
    print(len(scs), "scenarios")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
