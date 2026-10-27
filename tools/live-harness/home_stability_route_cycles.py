"""Exercise exact same-game authenticated-route reconnects in an isolated run.

Requires a task-owned LW-Control process started with
LWBRIDGE_HOME004_ISOLATED_ROUTE_FAULT=1. The product's isolated fault hook
validates the exact profile/session/PID before it closes only the current pipe
transport while retaining the claimed registration. This controller never
prints or persists the session/profile identity in its output.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
from pathlib import Path
import time
from typing import Any

from home_stability_sampler import actual_process_identity, safe_json, safe_text


OPERATION = "drop-exact-authenticated-route-retain-registration"


def now_iso() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat()


def healthy_heartbeat(runtime: Path, expected_pid: int) -> bool:
    value, status = safe_json(runtime / "heartbeat.json")
    if status != "ok" or value is None:
        return False
    try:
        age = time.time() - (runtime / "heartbeat.json").stat().st_mtime
    except OSError:
        return False
    return (
        age <= 5
        and value.get("gamePid") == expected_pid
        and value.get("ready") is True
        and value.get("gameStateObserved") is True
        and value.get("gameReady") is True
        and value.get("loggedIn") is True
        and value.get("connected") is True
        and value.get("connecting") is False
        and isinstance(value.get("serverId"), int)
        and value["serverId"] > 0
    )


def transport_snapshot(runtime: Path) -> tuple[str | None, bool | None, str | None]:
    value, status = safe_json(runtime / "pipe-transport.json")
    adapter, _ = safe_text(runtime / "pipe-adapter-state.txt")
    if status != "ok" or value is None:
        return None, None, adapter.strip() if adapter else None
    return (
        value.get("state") if isinstance(value.get("state"), str) else None,
        value.get("clientConnected") if isinstance(value.get("clientConnected"), bool) else None,
        adapter.strip() if adapter else None,
    )


def wait_result(path: Path, timeout: float) -> dict[str, Any]:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        value, status = safe_json(path)
        if status == "ok" and value is not None:
            return value
        time.sleep(0.025)
    raise TimeoutError("isolated route-fault receipt was not produced")


def run(args: argparse.Namespace) -> dict[str, Any]:
    root = args.isolated_root.expanduser().resolve(strict=True)
    runtime = root / "overview-bridge"
    if not runtime.is_dir():
        raise RuntimeError("isolated root does not contain overview-bridge")
    output = args.output.expanduser().resolve()
    if root == output or root in output.parents:
        raise RuntimeError("sanitized output must be outside the isolated runtime root")

    adoption, status = safe_json(runtime / "adoption.json")
    if status != "ok" or adoption is None:
        raise RuntimeError("exact adoption record is unavailable")
    profile = adoption.get("profileId")
    instance = adoption.get("instanceId") or adoption.get("sessionId")
    pid = adoption.get("pid")
    started = adoption.get("processCreatedAt")
    game_path = adoption.get("gameExecutable")
    if not isinstance(profile, str) or not profile or not isinstance(instance, str) or not instance:
        raise RuntimeError("adoption record lacks exact profile/session identity")
    if not isinstance(pid, int) or pid <= 0 or not isinstance(game_path, str):
        raise RuntimeError("adoption record lacks exact game process identity")
    expected_path = Path(game_path)
    before_identity = actual_process_identity(pid, expected_path, started)
    if not (
        before_identity.get("alive") is True
        and before_identity.get("imageMatchesExpected") is True
        and before_identity.get("creationMatchesAdoption") is True
        and healthy_heartbeat(runtime, pid)
    ):
        raise RuntimeError("game is not a fresh healthy exact owner before route cycling")

    request = root / "home004-drop-route.request.json"
    consumed = Path(str(request) + ".consumed")
    receipt_path = Path(str(consumed) + ".result.json")
    cycles: list[dict[str, Any]] = []
    original_pid = pid

    for index in range(1, args.cycles + 1):
        if index > 1:
            time.sleep(args.between)
        adoption_now, adoption_status = safe_json(runtime / "adoption.json")
        if adoption_status != "ok" or adoption_now is None or adoption_now.get("pid") != original_pid:
            raise RuntimeError(f"cycle {index}: exact game PID changed before fault")
        if adoption_now.get("processCreatedAt") != started:
            raise RuntimeError(f"cycle {index}: exact game creation identity changed before fault")
        if not healthy_heartbeat(runtime, original_pid):
            raise RuntimeError(f"cycle {index}: game heartbeat was not healthy before fault")

        for stale in (request, consumed, receipt_path):
            try:
                stale.unlink()
            except FileNotFoundError:
                pass
        payload = {
            "operation": OPERATION,
            "profileId": profile,
            "instanceId": instance,
            "gamePid": original_pid,
        }
        temporary = request.with_name(request.name + f".tmp-{os.getpid()}")
        temporary.write_text(json.dumps(payload), encoding="utf-8")
        os.replace(temporary, request)
        requested_at = now_iso()

        receipt = wait_result(receipt_path, args.timeout)
        if receipt.get("succeeded") is not True or receipt.get("retainedRegistration") is not True:
            raise RuntimeError(f"cycle {index}: route fault was not accepted")
        if receipt.get("gamePid") != original_pid:
            raise RuntimeError(f"cycle {index}: receipt game PID mismatch")

        saw_disconnected = False
        saw_adapter_non_connected = False
        fault_observed_at: str | None = None
        first_observed_layer: str | None = None
        recovered_at: str | None = None
        deadline = time.monotonic() + args.timeout
        while time.monotonic() < deadline:
            state, connected, adapter = transport_snapshot(runtime)
            if connected is False:
                saw_disconnected = True
                if fault_observed_at is None:
                    fault_observed_at = now_iso()
                    first_observed_layer = "pipe_transport"
            if adapter is not None and adapter != "connected":
                saw_adapter_non_connected = True
                if fault_observed_at is None:
                    fault_observed_at = now_iso()
                    first_observed_layer = "managed_adapter"
            identity = actual_process_identity(original_pid, expected_path, started)
            if not (
                identity.get("alive") is True
                and identity.get("imageMatchesExpected") is True
                and identity.get("creationMatchesAdoption") is True
            ):
                raise RuntimeError(f"cycle {index}: exact game changed during local route recovery")
            # Closing the server-side session is asynchronous from the in-game
            # worker's point of view. Immediately after the fault receipt the
            # old adapter state can still say "connected" until its next pipe
            # operation observes EOF/PeekNamedPipe failure. Never count that
            # stale pre-fault state as recovery. First require an observed local
            # transport failure, then require a genuinely reconnected adapter
            # plus a still-healthy exact-game heartbeat.
            if (
                fault_observed_at is not None
                and state == "connected"
                and connected is True
                and adapter == "connected"
                and healthy_heartbeat(runtime, original_pid)
            ):
                recovered_at = now_iso()
                break
            time.sleep(0.025)
        if fault_observed_at is None:
            raise TimeoutError(f"cycle {index}: local route fault was accepted but not observed by the adapter")
        if recovered_at is None:
            raise TimeoutError(f"cycle {index}: exact same-game route did not recover")

        cycles.append({
            "cycle": index,
            "requestedUtc": requested_at,
            "faultReceiptUtc": receipt.get("utc"),
            "faultObservedUtc": fault_observed_at,
            "firstObservedLayer": first_observed_layer,
            "recoveredUtc": recovered_at,
            "sameExactGame": True,
            "retainedRegistration": True,
            "transportDisconnectObserved": saw_disconnected,
            "adapterNonConnectedObserved": saw_adapter_non_connected,
            "heartbeatHealthyAfterRecovery": True,
        })

    final_identity = actual_process_identity(original_pid, expected_path, started)
    result = {
        "schemaVersion": 1,
        "tool": "home_stability_route_cycles",
        "operation": OPERATION,
        "cycleCount": len(cycles),
        "sameExactGameAcrossAllCycles": (
            final_identity.get("alive") is True
            and final_identity.get("imageMatchesExpected") is True
            and final_identity.get("creationMatchesAdoption") is True
        ),
        "gamePid": original_pid,
        "cycles": cycles,
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    for raw in (request, consumed, receipt_path):
        try:
            raw.unlink()
        except FileNotFoundError:
            pass
    return result


def parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--isolated-root", required=True, type=Path)
    p.add_argument("--output", required=True, type=Path)
    p.add_argument("--cycles", type=int, default=5)
    p.add_argument("--between", type=float, default=3.0)
    p.add_argument("--timeout", type=float, default=10.0)
    return p


def main() -> int:
    args = parser().parse_args()
    if os.name != "nt":
        raise SystemExit("Windows only")
    if args.cycles < 1 or args.between < 0 or args.timeout <= 0:
        raise SystemExit("invalid cycle/between/timeout arguments")
    result = run(args)
    print(json.dumps({
        "tool": result["tool"],
        "cycleCount": result["cycleCount"],
        "sameExactGameAcrossAllCycles": result["sameExactGameAcrossAllCycles"],
        "gamePid": result["gamePid"],
        "output": str(args.output),
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
