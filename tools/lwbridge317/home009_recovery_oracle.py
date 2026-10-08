"""Reconstructed LWBridge 0.3.17 health-monitor and recovery-run decision contract (Home 009 C/D).

RECONSTRUCTION from static disassembly of the hash-gated reference; NOT execution of the original.
Each rule cites the RVA it was read from.  Constants are asserted against the image by
``home009_contract.verify_recovery_contract``.

World interface (what the original reads/does through helper functions) is abstracted as ``Env``;
the harness decides what those helpers return.  Effects the original performs through helpers
(terminate game, kill updaters, launch) are *requests* returned to the harness together with the
decision, whose result is fed back through ``Env`` callbacks.

Clock: ``0x2c9034`` = Unix epoch milliseconds from GetSystemTimePreciseAsFileTime (wall clock).
"""
from __future__ import annotations

from dataclasses import dataclass, field, replace
from typing import Callable, Optional

# ---- constants (verified by home009_contract) -------------------------------------------------
TICK_MS = 2000                                   # 0xe5650/0xe69e3 edx=2 r8d=0
NORMAL_RETRY_MS = (15000, 30000, 60000, 120000, 300000)   # 0xd67b08
MAINTENANCE_RETRY_MS = (120000, 300000, 600000)           # 0xd681b0
UPDATE_STALL_MS = 900000                         # 0xe6096 cmp rax,0xdbb9f ; jle  => elapsed >= 900000
STABLE_VERIFY_MS = 15000                         # 0xe6285 cmp eax,0x3a98 ; jl
DISCONNECT_WAIT_MS = 60000                       # 0xe654f cmp rax,0xea60 ; jge
LOGIN_UNAVAILABLE_MS = 180000                    # 0xe6309 cmp rax,0x2bf1f ; jle  => elapsed >= 180000
MON_HANG_MS = 30000                              # 0x41abf8 cmp rdx,0x7530 ; jl / 0x41ac0b cmp rcx,0x752f ; jle
MON_DISCONNECT_MS = 60000                        # 0x41ac34 cmp rdx,0xea5f ; jg
MON_LOGIN_MS = 180000                            # 0x41ac53 cmp rsi,0x2bf20 ; setge
MON_MISSING_OBSERVATIONS = 2                     # 0x41aafe cmp esi,1 ; jbe  => needs counter >= 2
UPDATE_STALL_TEXT = "game update had no activity for 15 minutes"   # 0xe61d1
LOG_ERROR_TEXT = "game update reported an error"                   # 0x41a0db/0x41a0e6


def retry_delay(table, counter_before: int) -> int:
    return table[min(counter_before, len(table) - 1)]


# ---- status record (0x41c0ab payload) ------------------------------------------------------------
@dataclass(frozen=True)
class Status:
    state: str = "idle"
    reason: Optional[str] = None
    update_detected: bool = False
    restarted: bool = False
    started_at: int = 0
    completed_at: Optional[int] = None
    attempts: int = 0
    next_retry_at: Optional[int] = None
    error: Optional[str] = None
    notice_id: int = 0
    notice_visible: bool = False


ACTIVE_STATES = frozenset({"waiting", "repairing", "launching", "verifying", "updating", "maintenance"})  # 0x4199ba


@dataclass
class Store:
    """Shared status store with the original's emission rules (events recorded in ``events``)."""

    status: Status = field(default_factory=Status)
    events: list = field(default_factory=list)

    def emit(self, st: Status) -> None:
        self.status = st
        self.events.append(st)

    # 0x41ce45 set_state: dedupe on (state, attempts)
    def set_state(self, label: str, attempts: int) -> None:
        s = self.status
        if s.state == label and s.attempts == attempts:
            return
        self.emit(replace(s, state=label, attempts=attempts, next_retry_at=None, error=None))

    # 0x41cacb retry scheduled -> state "waiting"
    def retry_scheduled(self, attempts: int, next_retry_at: int, error: Optional[str]) -> None:
        self.emit(replace(self.status, state="waiting", attempts=attempts,
                          next_retry_at=next_retry_at, error=error))

    # 0x41b59e set maintenance
    def set_maintenance(self, attempts: int, next_retry_at: int) -> None:
        self.emit(replace(self.status, state="maintenance", attempts=attempts,
                          next_retry_at=next_retry_at, notice_visible=True))

    def mark_update_detected(self) -> None:           # 0x41b89f
        if not self.status.update_detected:
            self.emit(replace(self.status, update_detected=True))

    def mark_restarted(self) -> None:                 # 0x41ae75
        if not self.status.restarted:
            self.emit(replace(self.status, restarted=True))

    def finish(self, success: bool, now: int, message: str) -> None:   # 0x41c448
        self.emit(replace(self.status, state="succeeded" if success else "failed", completed_at=now,
                          next_retry_at=None, error=None if success else message, notice_visible=True))

    def finish_stopped(self) -> None:                 # 0x41c201: fresh idle record keeping the notice id
        self.emit(Status(notice_id=self.status.notice_id))

    def start(self, reason: str, update_detected: bool, now: int) -> None:   # 0x41b03a
        self.emit(Status(state="waiting", reason=reason, update_detected=update_detected, restarted=False,
                         started_at=now, completed_at=None, attempts=0, next_retry_at=None, error=None,
                         notice_id=self.status.notice_id + 1, notice_visible=True))


# ---- environment ------------------------------------------------------------------------------
class Env:
    """Override in the harness. Methods map to original helpers (RVA in comments)."""

    def now(self) -> int: ...                                      # 0x2c9034
    def config_reconnect(self) -> bool: ...                        # 0x41b7ef  auto_force_update_reload
    def desired(self) -> bool: ...                                 # ctx+0x124
    def armed(self) -> bool: ...                                   # ctx+0x126
    def run_id_matches(self, run_id: int) -> bool: ...             # ctx+0x118
    def root_available(self) -> bool: ...                          # 0x41d2ce
    def tracked_pid(self) -> int: ...                              # ctx+0x120
    def find_pid(self) -> int: ...                                 # 0x41b451 (0 when absent)
    def online(self) -> bool: ...                                  # 0x2d47a6
    def healthy(self, pid: int) -> bool: ...                       # 0x41ba64 (record keyed by pid, persists while offline)
    def reset_health_record(self) -> None: ...                     # 0x41c201 clears [+0x88]/[+0x8c]
    def event(self): ...                                           # 0x42c39d game.recovery_requested (reason, update) | None
    def update_running(self) -> bool: ...                          # 0x41dfb1
    def hung(self, pid: int) -> bool: ...                          # 0x41a472 via EnumWindows
    def fingerprint(self): ...                                     # 0x41dad8
    def classify_logs(self): ...                                   # 0x419e2a -> (activity, update, maintenance, error|None)
    def terminate_game(self, pid: int) -> bool: ...                # 0x41e543 (False = error)
    def kill_updaters(self) -> None: ...                           # 0x41e613
    def launch(self) -> str: ...                                   # e6bf7 (+fallback 0x2deee9): "ok" | "err"


# ---- monitor ----------------------------------------------------------------------------------
@dataclass
class Monitor:
    """Observation state ``M`` (ctx+0x60 guarded record, fields +0x90..+0xb4)."""

    missing: int = 0           # +0xac
    offline_pid: int = 0       # +0xb0
    offline_since: int = 0     # +0x98
    unhealthy_pid: int = 0     # +0xb4
    unhealthy_since: int = 0   # +0xa0
    hung_pid: int = 0          # +0xa8
    hung_since: int = 0        # +0x90

    def tick(self, env: Env, store: Store) -> Optional[tuple[str, bool]]:
        """0x41a8a0.  Returns (reason, update_flag) when a recovery is requested."""
        if not (env.config_reconnect() and env.desired() and env.armed()):   # 0x41a8c0-0x41a8e5
            return None
        ev = env.event()                                                     # 0x42c8ff -> 0x41b03a
        if ev is not None:
            return ev
        if store.status.state in ACTIVE_STATES:                              # 0x41a932-0x41a951
            return None
        if not env.root_available():                                         # 0x41a974-0x41a97c
            return None
        if env.tracked_pid() == 0:                                           # 0x41a99c
            return None
        pid = env.find_pid()
        now = env.now()
        if pid == 0:                                                         # 0x41aa30-0x41ab07
            self.offline_pid = 0                                             # [rax+0xb0] = 0
            self.missing = min(self.missing + 1, 0xFFFFFFFF)
            if self.missing <= 1:
                return None
            return ("processExit", False)
        online = env.online()
        healthy = env.healthy(pid)
        updating = env.update_running()
        either = updating or online
        hung = False if either else env.hung(pid)
        self.missing = 0                                                     # 0x41ab51
        if either:                                                           # 0x41ab5b
            self.offline_pid, self.offline_since = 0, 0
        elif self.offline_pid != pid:
            self.offline_pid, self.offline_since = pid, now
        if healthy:                                                          # 0x41ab8c
            self.unhealthy_pid, self.unhealthy_since = 0, 0
        elif self.unhealthy_pid != pid:
            self.unhealthy_pid, self.unhealthy_since = pid, now
        if hung:                                                             # 0x41abb3
            if self.hung_pid == pid:
                hung_since = self.hung_since
            else:
                self.hung_pid, self.hung_since = pid, now
                hung_since = now
            if now - hung_since >= MON_HANG_MS and now - self.offline_since >= MON_HANG_MS:
                return ("hang", False)                                       # 0x41ac14
        else:
            self.hung_pid, self.hung_since = 0, 0
        if not either and now - self.offline_since >= MON_DISCONNECT_MS:     # 0x41ac22-0x41ac3b
            return ("disconnect", False)
        if (self.unhealthy_since > 0 and not healthy and online
                and now - self.unhealthy_since >= MON_LOGIN_MS):             # 0x41ac3d-0x41ac63
            return ("disconnect", False)
        return None


# ---- recovery run -------------------------------------------------------------------------------
@dataclass
class Run:
    run_id: int
    reason: str
    started_at: int
    stable_since: int = 0           # [+0x68]
    last_activity: int = 0          # [+0x70]
    login_since: int = 0            # [+0x78]
    deadline: int = 0               # [+0x80]
    run_pid: int = 0                # [+0x108]
    last_pid: int = 0               # [+0x10c]
    normal_attempts: int = 0        # [+0x110]
    maint_attempts: int = 0         # [+0x114]
    maintenance: bool = False       # [+0x118]
    last_fp: object = None          # [+0x20/0x28]
    launch_attempt: int = 0         # [+0x120]
    finished: bool = False
    log: list = field(default_factory=list)

    # active counter (0x110 + flag*4)
    @property
    def active_attempts(self) -> int:
        return self.maint_attempts if self.maintenance else self.normal_attempts

    def init(self, env: Env, store: Store) -> None:
        """0xe58e1-0xe5bf8: runs once when the future first polls; no tick delay before it."""
        now = env.now()
        if not env.root_available():
            store.finish(False, now, "game root is unavailable")                # 0xe5976
            self.finished = True
            return
        pid = env.find_pid()
        self.run_pid = self.last_pid = pid
        self.stable_since = 0
        self.last_activity = self.login_since = self.deadline = now
        self.normal_attempts = self.maint_attempts = 0
        self.maintenance = False
        self.last_fp = env.fingerprint()
        if self.reason == "hang" and pid != 0:                                   # 0xe5b73-0xe5bf3
            if env.terminate_game(pid):
                self.log.append(("terminated_unresponsive", pid))
            else:
                n = self.normal_attempts
                self.normal_attempts = n + 1
                delay = retry_delay(NORMAL_RETRY_MS, n)
                self.deadline = now + delay
                store.retry_scheduled(n + 1, self.deadline, "terminate_error")
                self.log.append(("retry_scheduled", n + 1))

    def _continue_allowed(self, env: Env) -> bool:
        """0xe6987-0xe69cc end-of-iteration gate."""
        return env.armed() and env.run_id_matches(self.run_id) and env.desired() and env.config_reconnect()

    def tick(self, env: Env, store: Store) -> None:
        """One loop iteration (0xe5ca7-0xe6987). The caller waits TICK_MS between ticks."""
        if self.finished:
            return
        now = env.now()
        st = store.status
        if st.next_retry_at is not None and st.next_retry_at < self.deadline:     # 0xe5cfc-0xe5d1c
            self.deadline = st.next_retry_at
        activity, update, maint_flag, error = env.classify_logs()                  # 0xe5d2d-0xe5d3e
        if activity:
            self.last_activity = now                                               # 0xe5d4c
        if update:
            store.mark_update_detected()                                           # 0xe5d57-0xe5d6c
        fp = env.fingerprint()                                                     # 0xe5d71-0xe5db0
        if fp != self.last_fp:
            self.last_fp = fp
            self.last_activity = now
            store.mark_update_detected()
        pid = env.find_pid()                                                       # 0xe5dd0
        if pid != 0:                                                               # 0xe5deb
            if pid != self.last_pid:
                self.last_pid = pid
                self.login_since = now
                self.stable_since = 0
            if self.run_pid != 0 and pid != self.run_pid:                          # 0xe5e0e-0xe5e2e
                store.mark_restarted()
        upd = env.update_running()                                                 # 0xe5e3b
        if upd:                                                                    # 0xe5e44-0xe5e85
            self.login_since = now
            store.mark_update_detected()
            store.set_state("updating", self.normal_attempts)
        if error is not None:                                                      # 0xe5fa3
            if pid != 0:
                env.terminate_game(pid)
            env.kill_updaters()
            n = self.normal_attempts
            self.normal_attempts = n + 1
            self.deadline = now + retry_delay(NORMAL_RETRY_MS, n)
            store.retry_scheduled(n + 1, self.deadline, error)
            self.last_activity = now
            return self._end(env, store)
        if maint_flag and not upd:                                                 # 0xe5e95-0xe5f9e
            if pid != 0:
                env.terminate_game(pid)
            self.maintenance = True
            self.deadline = now + retry_delay(MAINTENANCE_RETRY_MS, self.maint_attempts)
            store.set_maintenance(self.maint_attempts, self.deadline)
            self.last_activity = now
            return self._end(env, store)
        if upd:                                                                    # 0xe608b-0xe6444
            if now - self.last_activity < UPDATE_STALL_MS:
                return self._end(env, store)
            env.kill_updaters()
            n = self.normal_attempts
            self.normal_attempts = n + 1
            self.deadline = now + retry_delay(NORMAL_RETRY_MS, n)
            store.retry_scheduled(n + 1, self.deadline, UPDATE_STALL_TEXT)
            self.last_activity = now
            return self._end(env, store)
        # 0xe61f8
        online = env.online()
        healthy_game = env.healthy(pid) if pid != 0 else False
        if online and pid != 0 and healthy_game and pid == self.last_pid:          # 0xe6223-0xe6233
            store.set_state("verifying", self.active_attempts)
            if self.stable_since == 0:
                self.stable_since = now
            if now - self.stable_since >= STABLE_VERIFY_MS:                        # 0xe6285
                store.finish(True, now, "")
                self.finished = True
                return
            return self._end(env, store)
        self.stable_since = 0                                                      # 0xe62b9
        if pid == 0:                                                               # 0xe64e0
            if now < self.deadline:
                return self._end(env, store)
            store.set_state("repairing", self.active_attempts)
            if self.maintenance:
                self.maint_attempts += 1
                attempt = self.maint_attempts
            else:
                self.normal_attempts += 1
                attempt = self.normal_attempts
            self.launch_attempt = attempt
            store.set_state("launching", attempt)
            result = env.launch()                                                  # e6bf7 (+ fallback 0x2deee9 when it returns Ok(false))
            if result == "ok":                                                     # 0xe6751
                store.mark_restarted()
                self.last_activity = now
                table = MAINTENANCE_RETRY_MS if self.maintenance else NORMAL_RETRY_MS
                self.deadline = now + retry_delay(table, attempt - 1)
            else:                                                                  # 0xe67a4
                if self.maintenance:
                    d = now + retry_delay(MAINTENANCE_RETRY_MS, attempt - 1)
                    self.deadline = d
                    store.set_maintenance(attempt, d)
                else:
                    d = now + retry_delay(NORMAL_RETRY_MS, attempt - 1)
                    self.deadline = d
                    store.retry_scheduled(attempt, d, "launch_error")
                # no store to [rbx+0x70] on the Err path (0xe67a4-0xe6987)
            return self._end(env, store)
        if not online:                                                             # 0xe6544
            if now - self.last_activity < DISCONNECT_WAIT_MS:
                store.set_state("waiting", self.normal_attempts)
                return self._end(env, store)
            ok = env.terminate_game(pid)
            if not ok:                                                             # 0xe6ab3
                n = self.normal_attempts
                self.normal_attempts = n + 1
                d = now + retry_delay(NORMAL_RETRY_MS, n)
                self.deadline = d
                store.retry_scheduled(n + 1, d, "terminate_error")
            else:
                if self.maintenance:                                               # 0xe6619
                    d = now + retry_delay(MAINTENANCE_RETRY_MS, self.maint_attempts)
                    self.deadline = d
                    store.set_maintenance(self.maint_attempts, d)
                else:
                    self.deadline = now                                            # 0xe6bb2
            self.last_activity = now
            return self._end(env, store)
        # online but unhealthy / pid mismatch: 0xe62d2
        store.set_state("verifying", self.active_attempts)
        if now - self.login_since < LOGIN_UNAVAILABLE_MS:
            return self._end(env, store)
        env.terminate_game(pid)                                                    # result ignored 0xe632c
        self.maintenance = True
        self.deadline = now + retry_delay(MAINTENANCE_RETRY_MS, self.maint_attempts)
        store.set_maintenance(self.maint_attempts, self.deadline)
        self.last_activity = self.login_since = now
        return self._end(env, store)

    def _end(self, env: Env, store: Store) -> None:
        if not self._continue_allowed(env):
            store.finish_stopped()
            env.reset_health_record()
            self.finished = True
