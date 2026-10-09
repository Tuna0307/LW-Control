"""Reconcile the 47 original Home obligations without importing the research archive.

Source: origin/research/offline-controller, immutable Home obligation ledger.
Rows are inherited research assertions; this report never upgrades source/clone
tests to original-runtime or actual native proof.
"""

from __future__ import annotations

import json
import subprocess
from pathlib import Path


REPO = Path(__file__).resolve().parents[1]
SOURCE = (
    "origin/research/offline-controller:"
    "evidence/lwbridge-0.3.17/functions/"
    "LWB317-FUNCTION-HOME-MAP-COMPLETION-010/obligations/home-obligations.json"
)
DESTINATION = REPO / "docs" / "HOME_004_CONTRACT_MATRIX.md"


def compact(value: object) -> str:
    return " ".join(str(value or "").split())


def field(value: object) -> str:
    return compact(value).replace("|", "\\|").replace("`", "'")


def main() -> None:
    raw = subprocess.check_output(["git", "show", SOURCE], cwd=REPO)
    rows = json.loads(raw)["rows"]
    expected = [f"H-{index:02}" for index in range(1, 48)]
    assert [row["id"] for row in rows] == expected, "Home obligation IDs changed"

    lines = [
        "# HOME-COMPLETE-DELIVERY-004 — 47-row original contract audit",
        "",
        "Reference: LWBridge 0.3.17 SHA-256 "
        "`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.",
        "Source authority: `" + SOURCE + "` (research, not product code).",
        "This matrix retains all 47 rows, including exclusions and unclosed dependencies. "
        "Its inherited proofs are trace locators and must be inspected directly; "
        "they are not new packaged native observations. The original protected "
        "licensed runtime has not been executed for this delivery.",
        "",
        "## In-scope checklist",
        "",
        "For every row, **input/state** identifies the control under test; "
        "**reference outcome** is the earlier recovered 0.3.17 assertion and locator; "
        "**path** is the inherited production mapping (not a certification of all callers); "
        "**distinguishing check** preserves the negative or test needed; "
        "**proof** names recorded earlier evidence; **gap** is the remaining "
        "dependency. No entry is marked whole-Home complete.",
        "",
        "| ID / input or state | Reference outcome / original locator | Production path "
        "| Distinguishing check | Actual proof kind / locator | Remaining gap |",
        "| --- | --- | --- | --- | --- | --- |",
    ]

    for row in rows:
        check = compact(row.get("demonstratedDifference"))
        needed = compact(row.get("requiredCorrection"))
        if needed and needed.lower() != "none":
            check = f"{check}; verify: {needed}" if check else needed
        if not check:
            check = "No independent distinguishing case recorded; compare equivalent states"
        proof = compact(row.get("proofAvailable")) or "NO PRIOR PROOF IDENTIFIED"
        gap = compact(row.get("remainingDependency"))
        if gap.lower() == "none" and row.get("readiness") == "LIVE":
            gap = "Native adverse-case replay needed"
        if row["id"] == "H-02":
            check = (
                "BEFORE: extra local_game_launch_status gate and 360 s UI cancellation; "
                "AFTER: original root/busy gate and no frontend lifecycle deadline; "
                "test current Home bridge dispatch and native package"
            )
            proof += "; DELIVERY-004 controlled frontend Home integration PASS"
            gap = "Actual packaged positive/adverse Home replay required"
        if row["id"] == "H-03":
            check += "; DELIVERY-004: root/running errors precede missing packaged helper"
            proof += "; DELIVERY-004 source ordering change; native checks pending"
        if row["id"] == "H-21":
            check += "; DELIVERY-004: non-object reconcile payload defaults to true instead of throwing"
            proof += "; DELIVERY-004 controlled native regression"
        if row["id"] == "H-20":
            check += "; DELIVERY-004: original error returned from outdated-build repair uses camelCase restart result"
            proof += "; DELIVERY-004 actual serializer result roundtrip"
        if row["id"] == "H-05":
            gap = "Protected original response unavailable; cannot fabricate entitlement"
        if row["id"] == "H-38":
            gap = "Independent simultaneous multi-profile owner runtimes and original protected capacity"

        original = compact(row.get("recoveredContract"))
        locator = compact(row.get("originalLocator"))
        path = compact(row.get("currentImplementation"))
        kind = f"archived {row.get('status', 'UNKNOWN')} / {row.get('readiness', 'UNKNOWN')}"
        lines.append(
            "| " + field(f"{row['id']}: {row['control']}")
            + " | " + field(f"{original} ({locator})")
            + " | " + field(path)
            + " | " + field(check)
            + " | " + field(f"{kind}: {proof}")
            + " | " + field(gap or "No recorded external dependency; exact native proof still required")
            + " |"
        )

    lines += [
        "",
        "## Acceptance boundary",
        "",
        "- Existing lead-accepted Home subset remains single-profile "
        "Launch → authenticated Connected → optional-ID Close with exact restoration "
        "(docs/HOME_LAUNCH_LEAD_ACCEPTANCE.md).",
        "- The HOME-004 UI/transport and Start precedence changes require an actual "
        "packaged regression; a frontend test alone cannot accept the native experience.",
        "- The remaining protected ticket/lease/finalizer input, original-runtime "
        "failure outcomes, independent multi-profile admission, and unreplayed "
        "recovery/repair/adoption adversity prevent whole Home A-to-A certification.",
        "- Owner data, installed scripts, recovery journals, and stored Map datasets "
        "remain outside this document's modification scope.",
        "",
    ]
    DESTINATION.write_text("\n".join(lines), encoding="utf-8")
    print(f"HOME_004_MATRIX_OK rows={len(rows)} file={DESTINATION}")


if __name__ == "__main__":
    main()
