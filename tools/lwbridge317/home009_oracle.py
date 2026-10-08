"""Reconstructed LWBridge 0.3.17 behaviour contracts for Home 009 (no execution of the original).

Every function here is a *reconstruction* of a control flow read from the hash-gated
reference executable (see ``home009_contract.py`` for the byte assertions that bind each
constant to an RVA).  They are used as independent comparators for production code; they are
NOT the original runtime and must not be described as such.

Close/stop contract (checkpoint A)
----------------------------------
* ``0x41d84b`` terminate(pid, expected_image_path) -> Ok(True)=terminated | Ok(False)=nothing done
* ``0x41e3cf`` present(pid, expected_image_path)   -> bool (image path query only)
* ``0xe5725``  wait-gone loop: up to 100 checks, 100 ms timer between checks, cap tested before
  the next process check, error "The game did not close in time." when 100 checks all see it.
"""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import Callable, Protocol

CLOSE_MAX_CHECKS = 100            # 0xe578f  mov ecx,0x64   (0x1ddcee in reconcile helper)
CLOSE_POLL_MS = 100               # 0xe57cb  mov r8d,0x5f5e100 ns
CLOSE_TIMEOUT_TEXT = "The game did not close in time."  # 0xe5821 / 0x1ddd80


class OriginalError(Exception):
    """Error produced by the reconstructed original contract."""

    def __init__(self, kind: str, detail: str = ""):
        super().__init__(f"{kind}: {detail}" if detail else kind)
        self.kind = kind
        self.detail = detail


class ProcessApi(Protocol):
    """The Win32 surface used by 0x41d84b / 0x41e3cf / 0xe5725."""

    def snapshot_pid_exists(self, pid: int) -> bool: ...        # 0x4198e5 Toolhelp scan
    def image_path(self, pid: int) -> str | None: ...           # 0x4197d8 OpenProcess(0x1000)+QueryFullProcessImageNameW
    def open_terminate(self, pid: int) -> object | None: ...    # OpenProcess(1,0,pid)
    def terminate(self, handle: object, exit_code: int) -> bool: ...
    def close_handle(self, handle: object) -> None: ...


def ascii_ci_equal(left: str, right: str) -> bool:
    """0x41d946-0x41d98e: equal byte length, then A-Z folded to a-z, other bytes exact."""
    a, b = left.encode("utf-8"), right.encode("utf-8")
    if len(a) != len(b):
        return False

    def fold(x: int) -> int:
        return x | 0x20 if 0x41 <= x <= 0x5A else x

    return all(fold(x) == fold(y) for x, y in zip(a, b))


def original_terminate(api: ProcessApi, pid: int, expected_path: str) -> bool:
    """Reconstruction of 0x41d84b. Returns True when TerminateProcess succeeded."""
    if not api.snapshot_pid_exists(pid):
        return False
    actual = api.image_path(pid)
    if actual is None:
        if not api.snapshot_pid_exists(pid):
            return False
        raise OriginalError("PROCESS_QUERY_FAILED", "Unable to verify the target process path.")
    if not ascii_ci_equal(actual, expected_path):
        return False
    handle = api.open_terminate(pid)
    if handle is None:
        if not api.snapshot_pid_exists(pid):
            return False
        raise OriginalError("TERMINATE_FAILED", "open target process")
    ok = api.terminate(handle, 1)
    api.close_handle(handle)
    if not ok:
        if not api.snapshot_pid_exists(pid):
            return False
        raise OriginalError("TERMINATE_FAILED", "terminate target process")
    return True


def original_present(api: ProcessApi, pid: int, expected_path: str) -> bool:
    """Reconstruction of 0x41e3cf: image path query only; unavailable path counts as absent."""
    actual = api.image_path(pid)
    return actual is not None and ascii_ci_equal(actual, expected_path)


@dataclass
class CloseOutcome:
    kind: str                       # "ok" | "error"
    error_kind: str = ""
    terminated: bool = False
    checks: int = 0
    sleeps: int = 0


def original_stop_close(api: ProcessApi, sleep_ms: Callable[[int], None], pid: int,
                        expected_path: str) -> CloseOutcome:
    """0x19a094 terminate then 0x19a0de wait loop (profile_instance_stop)."""
    out = CloseOutcome("ok")
    try:
        out.terminated = original_terminate(api, pid, expected_path)
    except OriginalError as exc:
        return CloseOutcome("error", exc.kind)
    count = 0
    while True:
        if count >= CLOSE_MAX_CHECKS:
            out.kind, out.error_kind = "error", "CLOSE_TIMEOUT"
            return out
        count += 1
        out.checks = count
        if not original_present(api, pid, expected_path):
            return out
        sleep_ms(CLOSE_POLL_MS)
        out.sleeps += 1


@dataclass
class VirtualProcessModel:
    """A scripted Windows process table with a virtual clock (milliseconds)."""

    pid: int
    image: str
    gone_at: int | None = None            # time the process leaves the table (None: never by itself)
    dies_on_terminate_after: int | None = None  # ms after a successful TerminateProcess
    path_unavailable: bool = False        # QueryFullProcessImageNameW fails while process exists
    open_terminate_fails: bool = False
    terminate_fails: bool = False
    query_denied_during_poll_at: int | None = None
    now: int = 0
    terminate_calls: int = 0
    open_calls: int = 0
    _terminated_at: int | None = None
    log: list = field(default_factory=list)

    def _exists(self) -> bool:
        if self._terminated_at is not None and self.dies_on_terminate_after is not None:
            if self.now >= self._terminated_at + self.dies_on_terminate_after:
                return False
        if self.gone_at is not None and self.now >= self.gone_at:
            return False
        return True

    # ---- ProcessApi
    def snapshot_pid_exists(self, pid: int) -> bool:
        return pid == self.pid and self._exists()

    def image_path(self, pid: int) -> str | None:
        if pid != self.pid or not self._exists() or self.path_unavailable:
            return None
        if self.query_denied_during_poll_at is not None and self.now >= self.query_denied_during_poll_at:
            return None
        return self.image

    def open_terminate(self, pid: int):
        self.open_calls += 1
        if pid != self.pid or not self._exists() or self.open_terminate_fails:
            return None
        return object()

    def terminate(self, handle, exit_code: int) -> bool:
        self.terminate_calls += 1
        if self.terminate_fails:
            return False
        assert exit_code == 1
        if self._terminated_at is None:
            self._terminated_at = self.now
        return True

    def close_handle(self, handle) -> None:
        return None

    def sleep_ms(self, ms: int) -> None:
        self.now += ms
