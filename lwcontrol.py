"""Independent, offline controller model. No game protocol or live transport."""

from __future__ import annotations

import argparse
import json
import math
from dataclasses import dataclass, replace
from pathlib import Path
from typing import Protocol


FEATURES = frozenset({"claim_daily", "collect_resources"})


def valid_time(value: float, label: str) -> None:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{label} must be a finite nonnegative number")
    if not math.isfinite(value) or value < 0:
        raise ValueError(f"{label} must be a finite nonnegative number")


@dataclass(frozen=True)
class FeatureConfig:
    name: str
    enabled: bool = False
    cooldown_seconds: float = 60

    def __post_init__(self) -> None:
        if self.name not in FEATURES:
            raise ValueError(f"Unknown feature: {self.name}")
        if type(self.enabled) is not bool:
            raise ValueError("enabled must be a boolean")
        valid_time(self.cooldown_seconds, "cooldown_seconds")
        if self.cooldown_seconds == 0:
            raise ValueError("cooldown_seconds must be positive")


@dataclass(frozen=True)
class Snapshot:
    observed_at: float
    connected: bool
    daily_available: bool
    resource_batches: int

    def __post_init__(self) -> None:
        valid_time(self.observed_at, "observed_at")
        if type(self.connected) is not bool or type(self.daily_available) is not bool:
            raise ValueError("snapshot flags must be booleans")
        if type(self.resource_batches) is not int or self.resource_batches < 0:
            raise ValueError("resource_batches must be a nonnegative integer")


@dataclass(frozen=True)
class Command:
    request_id: str
    feature: str


@dataclass(frozen=True)
class Result:
    request_id: str
    success: bool
    detail: str


class Bridge(Protocol):
    """Invented application boundary, not either uploaded tool's wire format.

    Calls must complete or raise an exception. A future adapter needs its own
    bounded I/O timeouts; this synchronous prototype does not enforce deadlines.
    """

    def snapshot(self, now: float) -> Snapshot: ...
    def execute(self, command: Command, now: float) -> Result: ...


class MockBridge:
    """Mutable synthetic world; never performs network or process operations."""

    def __init__(self, state: Snapshot):
        self.state = state
        self.history: list[Command] = []

    def snapshot(self, now: float) -> Snapshot:
        return self.state

    def execute(self, command: Command, now: float) -> Result:
        self.history.append(command)
        if not self.state.connected:
            return Result(command.request_id, False, "mock disconnected")
        if command.feature == "claim_daily" and self.state.daily_available:
            self.state = replace(self.state, daily_available=False, observed_at=now)
        elif command.feature == "collect_resources" and self.state.resource_batches > 0:
            self.state = replace(
                self.state, resource_batches=self.state.resource_batches - 1,
                observed_at=now,
            )
        else:
            return Result(command.request_id, False, "mock precondition failed")
        return Result(command.request_id, True, "simulated action")


class Controller:
    """Single-threaded scheduler, one action per tick, with explicit stop/reset.

    Cooldowns apply to attempts, including failures. Transport/result ambiguity
    pauses further actions until resume() is called. State is process-local.
    """

    def __init__(self, bridge: Bridge, features: list[FeatureConfig], max_age: float = 5):
        valid_time(max_age, "max_age")
        if len({f.name for f in features}) != len(features):
            raise ValueError("Duplicate feature configuration")
        self.bridge = bridge
        self.features = tuple(features)
        self.max_age = max_age
        self.next_due: dict[str, float] = {}
        self.paused = False
        self.sequence = 0
        self.last_tick = -1.0
        self.events: list[dict] = []

    def stop(self) -> None:
        self.paused = True

    def resume(self) -> None:
        self.paused = False

    def _event(self, now: float, status: str, **fields) -> dict:
        event = {"at": now, "mode": "offline", "status": status, **fields}
        self.events.append(event)
        return event

    def _healthy(self, state: Snapshot, now: float) -> bool:
        return state.connected and 0 <= now - state.observed_at <= self.max_age

    def _fault(self, now: float, detail: str, **fields) -> dict:
        self.paused = True
        return self._event(now, "fault", detail=detail, **fields)

    def tick(self, now: float) -> dict:
        valid_time(now, "tick time")
        if now < self.last_tick:
            raise ValueError("tick time must be monotonic")
        self.last_tick = now
        if self.paused:
            return self._event(now, "paused")
        try:
            before = self.bridge.snapshot(now)
        except Exception:
            return self._fault(now, "snapshot failed")
        if not self._healthy(before, now):
            return self._event(now, "blocked", detail="bridge disconnected or snapshot stale/future")

        for feature in self.features:
            if not feature.enabled or now < self.next_due.get(feature.name, 0):
                continue
            available = (
                before.daily_available if feature.name == "claim_daily"
                else before.resource_batches > 0
            )
            if not available:
                continue
            self.sequence += 1
            command = Command(f"offline-{self.sequence:04d}", feature.name)
            self.next_due[feature.name] = now + feature.cooldown_seconds
            fields = {"request_id": command.request_id, "feature": feature.name}
            try:
                result = self.bridge.execute(command, now)
                if result.request_id != command.request_id:
                    return self._fault(now, "result correlation mismatch", **fields)
                if result.success is not True:
                    return self._fault(now, "adapter reported failure", **fields)
                after = self.bridge.snapshot(now)
            except Exception:
                return self._fault(now, "adapter call failed; outcome uncertain", **fields)
            verified = (
                before.daily_available and not after.daily_available
                if feature.name == "claim_daily"
                else after.resource_batches == before.resource_batches - 1
            )
            if not self._healthy(after, now) or after.observed_at < before.observed_at or not verified:
                return self._fault(now, "effect not verified", **fields)
            return self._event(now, "completed", detail="mock effect verified", **fields)
        return self._event(now, "idle")


def run_scenario(data: dict) -> list[dict]:
    """Validate the complete fixture before simulating any steps."""
    if set(data) != {"features", "initial_state", "steps"}:
        raise ValueError("scenario requires features, initial_state, and steps")
    features = [FeatureConfig(**item) for item in data["features"]]
    initial = Snapshot(**data["initial_state"])
    prepared = []
    state = initial
    previous = -1.0
    for step in data["steps"]:
        if set(step) - {"at", "state", "control"}:
            raise ValueError("unknown step field")
        now = step["at"]
        valid_time(now, "step time")
        if now < previous:
            raise ValueError("scenario steps must be monotonic")
        previous = now
        control = step.get("control")
        if control not in (None, "stop", "resume"):
            raise ValueError("control must be stop or resume")
        patch = step.get("state", {})
        state = replace(state, **patch)  # validate keys and field types
        prepared.append((now, patch, control))
    bridge = MockBridge(initial)
    controller = Controller(bridge, features)
    for now, patch, control in prepared:
        bridge.state = replace(bridge.state, **patch)
        if control == "stop":
            controller.stop()
        elif control == "resume":
            controller.resume()
        controller.tick(now)
    return controller.events


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scenario", type=Path, required=True, help="Synthetic JSON fixture")
    args = parser.parse_args()
    try:
        events = run_scenario(json.loads(args.scenario.read_text(encoding="utf-8")))
    except (OSError, ValueError, TypeError, KeyError) as exc:
        parser.error(str(exc))
    for event in events:
        print(json.dumps(event, sort_keys=True))


if __name__ == "__main__":
    main()
