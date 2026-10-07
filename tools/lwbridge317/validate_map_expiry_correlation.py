#!/usr/bin/env python3
"""Final repeatable validator for LWB317-FUNCTION-MAP-EXPIRY-CORRELATION-005."""
from __future__ import annotations

import hashlib
import importlib.util
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / "evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-MAP-EXPIRY-CORRELATION-005"
HISTORICAL = (
    ROOT
    / "evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-MAP-BLOCKER-BOUNDARIES-004"
    / "lead-review-2026-10-07/expiry-results.json"
)
HISTORICAL_SHA256 = "77fb89fc7bf4325e5ce656dfc6451e5209a0666367322ff7abe880424978a7d8"

PROVIDER = ROOT / "src/LWBridge.Desktop/CurrentClientMap317ActionProvider.cs"
SEMANTIC_TESTS = ROOT / "tests/LWBridge.Desktop.Checks/MapProviderSemanticsChecks.cs"
OVERVIEW_BRIDGE = ROOT / "tools/current_overview_bridge.lua"
CORRELATION_TOOL = ROOT / "tools/lwbridge317/inspect_map_expiry_correlation.py"


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_correlation() -> dict:
    spec = importlib.util.spec_from_file_location("expiry_corr_final", CORRELATION_TOOL)
    if spec is None or spec.loader is None:
        raise RuntimeError("cannot load managed-correlation inspector")
    mod = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = mod
    spec.loader.exec_module(mod)
    return mod.inspect()


def main() -> int:
    require(HISTORICAL.is_file(), "historical lead expiry evidence missing")
    require(sha256(HISTORICAL) == HISTORICAL_SHA256,
            "historical failing expiry evidence changed")

    corrected = json.loads(
        (EVIDENCE / "a-expiry-results-corrected.json").read_text(encoding="utf-8-sig")
    )
    cases = {row["name"]: row for row in corrected["cases"]}
    required = {
        "positive_after_plunder": ("ACCEPTED", "ACCEPTED"),
        "zero": ("ACCEPTED", "ACCEPTED"),
        "negative": ("ACCEPTED", "ACCEPTED"),
        "missing": ("ACCEPTED", "ACCEPTED"),
        "positive_equal_plunder": ("REJECTED", "REJECTED"),
        "positive_before_plunder": ("REJECTED", "REJECTED"),
    }
    for name, expected in required.items():
        row = cases[name]
        require((row["host"], row["helper"]) == expected,
                f"corrected expiry case changed: {name}")
        if expected == ("ACCEPTED", "ACCEPTED"):
            require(row.get("helperPreservedRawRow") is True,
                    f"accepted helper row was not preserved: {name}")

    for name in ("numeric_string_after_plunder",
                 "malformed_string_reader_rejects_string_kind"):
        row = cases[name]
        require(row["helper"] == "REJECTED", f"reader contract broadened: {name}")
        require(row.get("helperError") == "System.InvalidOperationException",
                f"reader exception contract changed: {name}")

    provider = PROVIDER.read_text(encoding="utf-8")
    require(
        "(taskExpireTime > 0 && plunderAt >= taskExpireTime)" in provider,
        "conditional Ghost expiry predicate missing",
    )
    helper_start = provider.index("internal static IReadOnlyList<JsonElement> PrepareGhostPlunderRows")
    helper_end = provider.index("private static long ReadInteger", helper_start)
    helper = provider[helper_start:helper_end]
    require("taskExpireTime <= 0" not in helper,
            "over-strict non-positive expiry rejection returned")
    require("ReadInteger(row, \"taskExpireTime\")" in helper,
            "expiry reader boundary changed")
    require("stolenCount != stealListCount" in helper
            and "maxStealCount != stealMaxTimes" in helper,
            "counter alias guard changed")

    public_start = provider.index(
        "public ValueTask<IReadOnlyList<JsonElement>> PrepareGhostPlunderTasksAsync"
    )
    public_end = provider.index("internal static IReadOnlyList<JsonElement> PrepareGhostPlunderRows",
                                public_start)
    public = provider[public_start:public_end]
    require("ValueTask.FromException" in public and "ProviderUnavailable(" in public,
            "public Ghost preparation fence changed")
    require("row transformation/rejection semantics are incomplete" in public,
            "preparation uncertainty explanation missing")
    require("terminal-result correlation is separately unresolved" in public,
            "execution-correlation separation missing")

    tests = SEMANTIC_TESTS.read_text(encoding="utf-8")
    for token in (
        "GhostPreparationMatchesExpiryBoundary",
        "GhostPreparationPreservesExpiryReaderContract",
        "positive expiry after plunder",
        "zero expiry",
        "negative expiry",
        "missing expiry",
    ):
        require(token in tests, f"expiry production test marker missing: {token}")

    bridge = OVERVIEW_BRIDGE.read_text(encoding="utf-8")
    begin = bridge.index("function dispatch_plunder_runtime.begin(request)")
    send = bridge.index("function dispatch_plunder_runtime.send(request, server_time)", begin)
    begin_body = bridge[begin:send]
    guard = begin_body.index('if request.taskKind == "ghost" then')
    pending = begin_body.index("dispatch_plunder_runtime.pendingByTask", guard)
    require(guard < pending, "Ghost guard no longer precedes pending/runtime setup")
    require("DISPATCH_PLUNDER_MANAGER_UNAVAILABLE" in begin_body[guard:pending],
            "Ghost-before-Dispatch fail-closed result changed")

    correlation = load_correlation()
    corr = correlation["correlation"]
    require(correlation["ok"] is True, "managed correlation inspector failed")
    require(corr["disposition"] == "PARTIAL_SOURCE_PROVEN_MANAGED_CORRELATION",
            "managed correlation disposition changed")
    require("Dictionary<int,msgSendInfo>" in corr["pendingStore"],
            "managed keyed pending-store proof changed")
    require("reset" in corr["reuse"].lower(),
            "managed future reuse/reset proof changed")
    require("key-based" in corr["ordering"],
            "managed ordering proof changed")
    require(corr["directGhostFutureKeyEmission"].startswith("UNKNOWN:"),
            "direct Ghost future-key emission was improperly promoted")

    result = {
        "ok": True,
        "workItem": "LWB317-FUNCTION-MAP-EXPIRY-CORRELATION-005",
        "historicalExpiryEvidencePreserved": True,
        "expiryCorrection": {
            "sixRequiredBoundaryCases": "PASS",
            "acceptedRowsPreserved": True,
            "numericParsingBroadened": False,
            "publicPreparationEnabled": False,
        },
        "managedCorrelation": {
            "fuidPendingIdentityWhenResponseKeyPresent": True,
            "connectionResetAllowsReuse": True,
            "matching": "key-based",
            "directGhostFutureKeyEmission": "UNKNOWN",
            "productionCorrelatorAdded": False,
        },
        "ghostBeforeDispatchFencePreserved": True,
        "globalAcceptanceUpgraded": False,
    }
    print(json.dumps(result, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
