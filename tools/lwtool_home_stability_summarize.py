"""Summarize a sanitized Home stability sampler report without exposing owner IDs."""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path
from statistics import mean, median
from typing import Any, Iterable


def numbers(values: Iterable[Any]) -> list[float]:
    return [float(value) for value in values if isinstance(value, (int, float))]


def distribution(values: Iterable[Any]) -> dict[str, float | int] | None:
    items = sorted(numbers(values))
    if not items:
        return None

    def percentile(fraction: float) -> float:
        index = min(len(items) - 1, max(0, math.ceil(fraction * len(items)) - 1))
        return items[index]

    return {
        "count": len(items),
        "min": items[0],
        "p50": percentile(0.50),
        "p95": percentile(0.95),
        "p99": percentile(0.99),
        "max": items[-1],
        "mean": mean(items),
    }


def path_value(value: dict[str, Any], *path: str) -> Any:
    current: Any = value
    for key in path:
        if not isinstance(current, dict):
            return None
        current = current.get(key)
    return current


def process_summary(samples: list[dict[str, Any]], label: str) -> dict[str, Any]:
    live = [
        path_value(sample, "processes", label)
        for sample in samples
        if path_value(sample, "processes", label, "alive") is True
    ]
    live = [value for value in live if isinstance(value, dict)]

    def series(*path: str) -> list[float]:
        return numbers(path_value(value, *path) for value in live)

    def first_last(*path: str) -> dict[str, float] | None:
        values = series(*path)
        return {"first": values[0], "last": values[-1]} if values else None

    read_delta = series("io", "delta", "read_bytes")
    write_delta = series("io", "delta", "write_bytes")
    return {
        "aliveSamples": len(live),
        "cpuAllLogicalPercent": distribution(series("cpu", "percent_all_logical_cpus")),
        "workingSetBytes": {
            "distribution": distribution(series("memory", "working_set_bytes")),
            "endpoints": first_last("memory", "working_set_bytes"),
        },
        "privateBytes": {
            "distribution": distribution(series("memory", "private_bytes")),
            "endpoints": first_last("memory", "private_bytes"),
        },
        "handles": {
            "distribution": distribution(series("handles")),
            "endpoints": first_last("handles"),
        },
        "threads": {
            "distribution": distribution(series("threads")),
            "endpoints": first_last("threads"),
        },
        "ioReadBytesDeltaTotal": sum(read_delta),
        "ioWriteBytesDeltaTotal": sum(write_delta),
        "ioReadBytesDelta": distribution(read_delta),
        "ioWriteBytesDelta": distribution(write_delta),
    }


def summarize(report: dict[str, Any]) -> dict[str, Any]:
    samples = report.get("samples") or []
    heartbeat_ages = [path_value(sample, "heartbeat", "ageSeconds") for sample in samples]
    route_false = sum(
        1 for sample in samples
        if path_value(sample, "monitor", "freshWithin4Seconds") is True
        and path_value(sample, "monitor", "authenticatedRouteObserved") is False
    )
    bridge_false = sum(
        1 for sample in samples
        if path_value(sample, "monitor", "freshWithin4Seconds") is True
        and path_value(sample, "monitor", "bridgeOnline") is False
    )
    game_unhealthy = sum(
        1 for sample in samples
        if path_value(sample, "monitor", "freshWithin4Seconds") is True
        and path_value(sample, "monitor", "gameHealthy") is False
    )
    direct_active = sum(
        1 for sample in samples if path_value(sample, "transport", "directActive") is True
    )
    direct_capable = sum(
        1 for sample in samples if path_value(sample, "transport", "directCapable") is True
    )
    return {
        "schemaVersion": 1,
        "sourceTool": report.get("tool"),
        "startedUtc": report.get("startedUtc"),
        "endedUtc": report.get("endedUtc"),
        "requestedDurationSeconds": report.get("requestedDurationSeconds"),
        "sampleCount": report.get("sampleCount", len(samples)),
        "observedGamePidCount": len(set(report.get("observedGamePids") or [])),
        "naturalFailureObserved": report.get("naturalFailureObserved"),
        "firstFailure": report.get("firstFailure"),
        "failureLayerFirstSeen": report.get("failureLayerFirstSeen"),
        "heartbeatAgeSeconds": distribution(heartbeat_ages),
        "freshMonitorRouteFalseSamples": route_false,
        "freshMonitorBridgeOfflineSamples": bridge_false,
        "freshMonitorGameUnhealthySamples": game_unhealthy,
        "directCapableSamples": direct_capable,
        "directActiveSamples": direct_active,
        "maxInboundMailboxBacklog": max(
            [int(path_value(sample, "mailbox", "inboundCount") or 0) for sample in samples], default=0),
        "maxOutboundMailboxBacklog": max(
            [int(path_value(sample, "mailbox", "outboundCount") or 0) for sample in samples], default=0),
        "playerLogMarkerTotals": report.get("playerLogMarkerTotals") or {},
        "processes": {
            "host": process_summary(samples, "host"),
            "game": process_summary(samples, "game"),
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    summary = summarize(json.loads(args.input.read_text(encoding="utf-8")))
    text = json.dumps(summary, indent=2, sort_keys=True) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(text, encoding="utf-8")
    else:
        print(text, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
