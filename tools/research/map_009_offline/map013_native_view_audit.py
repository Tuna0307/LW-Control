"""Fail-closed audit of first/second v24 map-view receipts.

This is an offline source-lifetime analysis, not a live native reply hook.
Local _curViewIndex / _addViewIndex do not identify server-returned AOI
segments. _msgViewIndex is a manager observation, NOT a server-echoed
generation. Distinguish the first *observed* failure from its cause.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
from typing import Any


def audit_receipt(receipt: dict[str, Any], *, expected_aoi: int = 4954) -> dict[str, Any]:
    stages = receipt.get("sequenceSnapshots")
    if not isinstance(stages, list) or len(stages) != 2:
        raise ValueError("two real source snapshots required")
    first, second = stages
    observations = receipt.get("secondReceiverObservations")
    if not isinstance(observations, list) or not observations:
        raise ValueError("second source view-manager observations absent")

    def counts(stage: dict[str, Any]) -> dict[str, int]:
        return {
            kind: len(stage.get(field, [])) for kind, field in (
                ("point", "pointRecords"), ("monster", "monsterMarchRecords"),
                ("train", "trainMarchRecords"))
        }

    prior = counts(first)
    after = counts(second)
    aoi = second.get("requestedIndices", [])
    footprint = expected_aoi in aoi
    local_view = all(
        sample.get("hasTarget") is True and
        sample.get("addHasTarget") is True and
        sample.get("currentCount", 0) > 0
        for sample in observations
    )
    msg_has_target = any(sample.get("msgHasTarget") is True for sample in observations)
    flags_ready = any(sample.get("flagsReady") is True for sample in observations)
    all_positive = sum(after.values()) > 0
    stale_cache = receipt.get("resourceCompleteness", {})
    rejected = stale_cache.get("rejectionReasonCounts", {}).get("outside_selected_aoi", 0)
    result = {
        "firstRegion": {
            "tile": [first.get("targetTileX"), first.get("targetTileY")],
            "sourceRows": prior,
            "hasPositiveSourceWitness": sum(prior.values()) > 0,
        },
        "secondRegion": {
            "tile": [second.get("targetTileX"), second.get("targetTileY")],
            "sourceRows": after,
            "localTargetFootprintPresent": footprint,
            "localAddAndCurrentTargetPresent": local_view,
            "msgViewTargetObserved": msg_has_target,
            "flagsReadyObserved": flags_ready,
            "msgViewObservedCounts": [o.get("msgCount") for o in observations],
            "rejectedOutsideTargetAoiPointRows": rejected,
            "nativeTargetResponseSegmentsAttributed": "unobserved",
            "nativeSourceServerAndGenerationAttributed": "unobserved",
        },
        "observedBoundary": (
            "second_source_rows_present_without_response_attribution"
            if all_positive
            else "local_target_aoi_does_not_match_native_message_view_and_second_source_rows_empty"
            if footprint and local_view and not msg_has_target
            else "other_unqualified_second_region"
        ),
        "qualifiesConsecutiveAcquisition": False,
        "reason": "native response/segments, server ownership and exact final manager restoration not positively attested; not evidence of packet absence",
    }
    # Even a positive count without packet+response+cache ownership is not a
    # positive alternative, so there is deliberately no success branch here.
    return result


def load_and_audit(path: Path) -> dict[str, Any]:
    raw = path.read_bytes()
    report = audit_receipt(json.loads(raw))
    report["evidence"] = {
        "sha256": hashlib.sha256(raw).hexdigest(),
        "source": str(path),
        "observationType": "previous live private native diagnostic, NOT a fresh new game network trace",
    }
    return report


if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser()
    parser.add_argument("--r15", type=Path, required=True)
    parser.add_argument("--r16", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if "artifacts" not in args.output.parts:
        parser.error("do not put raw game observations in Git or releases")
    outputs = {"R15": load_and_audit(args.r15), "R16": load_and_audit(args.r16)}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(outputs, indent=2) + "\n", encoding="utf-8")
    for name, report in outputs.items():
        print(name, report["observedBoundary"], "qualifies", report["qualifiesConsecutiveAcquisition"])
