"""Inert scripted Windows process table for the Stop/ownership tests (HOME 009 R1).

This is a TEST DOUBLE for ``run_live_resource_probe.Win32ProcessApi``.  It models process
*incarnations* (a PID can be reused by a new incarnation with a different creation time), real
handle semantics (a handle stays bound to the incarnation it opened, even after PID reuse),
a virtual clock, scripted access denial and hook points that let a test change the world at an
exact production API call.  It never touches a real process.
"""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import Callable

OLD_STARTED = "2026-09-12T01:00:00.0000000Z"
NEW_STARTED = "2026-09-12T02:00:00.0000000Z"

QUERY = 0x1000
TERMINATE = 0x1
SYNCHRONIZE = 0x100000


@dataclass
class Incarnation:
    pid: int
    image: str
    created: str = OLD_STARTED
    exit_at: int | None = None                  # absolute virtual ms at which it leaves the table
    exit_delay_after_terminate: int | None = 0  # None: ignores TerminateProcess
    query_denied_from: int | None = None        # image/handle queries fail from this virtual ms (still alive)
    query_always_denied: bool = False
    terminate_open_denied: bool = False         # OpenProcess with TERMINATE right is denied
    terminate_call_fails: bool = False
    terminated_calls: int = 0
    label: str = ""


@dataclass
class _Handle:
    inc: Incarnation
    access: int
    closed: bool = False


@dataclass
class ProcessTable:
    incarnations: list[Incarnation] = field(default_factory=list)
    now: int = 0                 # advanced only by sleep_ms (the original poll clock)
    waited: int = 0              # time spent in handle waits (does not move ``now``)
    hooks: dict[str, Callable[["ProcessTable"], None]] = field(default_factory=dict)
    calls: dict[str, int] = field(default_factory=dict)
    handles: list[_Handle] = field(default_factory=list)
    log: list[str] = field(default_factory=list)

    # ---- helpers
    def clock(self) -> int:
        return self.now + self.waited

    def _fire(self, name: str) -> None:
        self.calls[name] = self.calls.get(name, 0) + 1
        hook = self.hooks.get(name)
        if hook is not None:
            hook(self)

    def alive(self, inc: Incarnation) -> bool:
        return inc.exit_at is None or self.clock() < inc.exit_at

    def by_pid(self, pid: int) -> Incarnation | None:
        live = [i for i in self.incarnations if i.pid == pid and self.alive(i)]
        return live[-1] if live else None

    def add(self, inc: Incarnation) -> Incarnation:
        self.incarnations.append(inc)
        return inc

    def replace(self, pid: int, image: str, created: str = NEW_STARTED, **kw) -> Incarnation:
        """Old incarnation of ``pid`` exits now; a new one takes the PID (Windows PID reuse)."""
        old = self.by_pid(pid)
        if old is not None:
            old.exit_at = self.clock()
        return self.add(Incarnation(pid=pid, image=image, created=created, **kw))

    def _query_ok(self, inc: Incarnation) -> bool:
        if inc.query_always_denied:
            return False
        return not (inc.query_denied_from is not None and self.clock() >= inc.query_denied_from)

    # ---- Win32ProcessApi surface
    def snapshot_pid_exists(self, pid: int) -> bool:
        self._fire("snapshot_pid_exists")
        return self.by_pid(pid) is not None

    def image_path(self, pid: int) -> str | None:
        self._fire("image_path")
        inc = self.by_pid(pid)
        if inc is None or not self._query_ok(inc):
            return None
        return inc.image

    def open_process(self, pid: int, access: int):
        self._fire("open_process")
        inc = self.by_pid(pid)
        if inc is None:
            return None
        if access & TERMINATE and inc.terminate_open_denied:
            return None
        if access == QUERY and inc.query_always_denied:
            return None
        handle = _Handle(inc, access)
        self.handles.append(handle)
        return handle

    def handle_image_path(self, handle) -> str | None:
        self._fire("handle_image_path")
        inc = handle.inc
        if not self.alive(inc) or not self._query_ok(inc):
            return None
        return inc.image

    def handle_creation_utc(self, handle) -> str | None:
        self._fire("handle_creation_utc")
        return handle.inc.created

    def handle_wait_exit(self, handle, timeout_milliseconds: int) -> bool:
        self._fire("handle_wait_exit")
        assert handle.access & SYNCHRONIZE, "wait requires the SYNCHRONIZE right"
        inc = handle.inc
        if not self.alive(inc):
            return True
        if inc.exit_at is not None and self.clock() + timeout_milliseconds >= inc.exit_at:
            self.waited += max(0, inc.exit_at - self.clock())
            return True
        self.waited += max(0, int(timeout_milliseconds))
        return False

    def terminate(self, handle, exit_code: int) -> bool:
        self._fire("terminate")
        assert exit_code == 1 and handle.access & TERMINATE, "terminate requires the TERMINATE right"
        inc = handle.inc
        inc.terminated_calls += 1
        self.log.append(f"terminate:{inc.label or inc.created}")
        if inc.terminate_call_fails or not self.alive(inc):
            return False
        if inc.exit_delay_after_terminate is not None and inc.exit_at is None:
            inc.exit_at = self.clock() + inc.exit_delay_after_terminate
        return True

    def close_handle(self, handle) -> None:
        self._fire("close_handle")
        handle.closed = True

    def sleep_ms(self, ms: int) -> None:
        self._fire("sleep")
        self.now += ms

    # ---- assertions helpers
    def terminated(self) -> list[str]:
        return [i.label or i.created for i in self.incarnations if i.terminated_calls]

    def open_handles(self) -> int:
        return sum(1 for h in self.handles if not h.closed)
