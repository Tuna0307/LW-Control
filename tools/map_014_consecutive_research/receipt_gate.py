"""Offline checks of archived MAP-014 consecutive native diagnostics.

This is a research gate, not a production source or proof of app publication.
It reads archival JSON only and intentionally cannot contact the game.
"""

from __future__ import annotations

from collections import Counter
from pathlib import Path
import argparse
import json
import math
from typing import Any


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def _stage(receipt: dict[str, Any], name: str) -> dict[str, Any]:
    matches = [s for s in receipt.get("map014SubscriptionStages", []) if s.get("stage") == name]
    _require(len(matches) == 1, f"exactly one {name} native subscription stage required")
    return matches[0]


def _rectangle(indices: list[int]) -> bool:
    if any(type(i) is not int or not 0 <= i < 10000 for i in indices):
        return False
    if not indices or len(indices) != len(set(indices)):
        return False
    rows = Counter(i // 100 for i in indices)
    cols = Counter(i % 100 for i in indices)
    ys, xs = sorted(rows), sorted(cols)
    return (
        ys == list(range(ys[0], ys[-1] + 1))
        and xs == list(range(xs[0], xs[-1] + 1))
        and len(indices) == len(ys) * len(xs)
        and all(n == len(xs) for n in rows.values())
        and all(n == len(ys) for n in cols.values())
    )


def _matching_snapshot(original: Any, restored: Any) -> bool:
    """Independently compare serialized camera checkpoints, allowing float jitter."""
    if type(original) is not type(restored):
        return False
    if isinstance(original, dict):
        return original.keys() == restored.keys() and all(
            _matching_snapshot(original[key], restored[key]) for key in original
        )
    if isinstance(original, list):
        return len(original) == len(restored) and all(
            _matching_snapshot(left, right) for left, right in zip(original, restored)
        )
    if isinstance(original, float):
        return math.isfinite(original) and math.isfinite(restored) and math.isclose(
            original, restored, rel_tol=1e-5, abs_tol=1e-4
        )
    return original == restored


def validate_region(receipt: dict[str, Any], *, x: int, y: int, final: bool) -> None:
    """Require a correlated native B/C response and native AOI, plus final restoration."""
    _require(receipt.get("state") == "proven", "region did not finish proven")
    _require(receipt.get("requestMode") == "coverage", "wrong native request mode")
    # Private experimental JSON omits coverageFov from the response envelope;
    # the *native post-lens stage* records the actual Unity FOV applied.
    _require((receipt.get("targetTileX"), receipt.get("targetTileY")) == (x, y),
             "target differs from requested tile")
    _require(receipt.get("map013NativeReplyState") == "matched", "no correlated native reply")
    _require(receipt.get("responseFlagsTransitioned") is True, "reply flags did not transition")
    _require(receipt.get("postServerLod") == 0, "unexpected native LOD")
    _require(receipt.get("postBlockSize") == 10, "unexpected AOI cell width")
    _require(receipt.get("postBlockCount") == 100, "unexpected AOI grid")
    _require(receipt.get("positionRestoredBeforeResponse") is False,
             "remote view was restored before target reply")
    index = (y // 10) * 100 + x // 10
    _require(receipt.get("targetAoiIndex") == index, "target AOI echo differs from geometry")
    indices = receipt.get("requestedIndices")
    _require(isinstance(indices, list) and _rectangle(indices), "invalid native AOI rectangle")
    _require(index in indices, "native footprint omits requested target")
    _require(receipt.get("nativeCurrentSetCount") == len(indices),
             "native footprint count disagrees with copied indices")
    after = _stage(receipt, "post-update")
    actual = after.get("currentIndices")
    _require(isinstance(actual, list) and set(actual) == set(indices),
             "post-update _curViewIndex differs from accepted AOI footprint")
    _require((after.get("tileX"), after.get("tileY")) == (x, y),
             "post-update physical camera does not reach target")
    _require(after.get("flags", {}).get("isRecvViewPoints") is False,
             "post-update reply flag was not armed for fresh response")
    lens = _stage(receipt, "post-lens")
    _require(lens.get("geometry", {}).get("fov") == 20,
             "post-lens native FOV differs from intended 20")
    if final:
        for key in ("positionRestoredAfterResponse", "cameraTileStable",
                    "cameraGeometryRestored", "cameraGeometryConfirmed",
                    "cameraProjectionRestored"):
            _require(receipt.get(key) is True, f"final {key} confirmation absent")
        _require(
            (receipt.get("restoredTileX"), receipt.get("restoredTileY")) ==
            (receipt.get("homeTileX"), receipt.get("homeTileY")),
            "final camera tile differs from original home",
        )
        original, restored = receipt.get("originalCameraGeometry"), receipt.get("restoredCameraGeometry")
        _require(isinstance(original, dict) and isinstance(restored, dict)
                 and _matching_snapshot(original, restored),
                 "final native camera checkpoint differs from original home")
    else:
        _require(receipt.get("map013PositionRetained") is True,
                 "intermediate region did not retain the camera for the next request")
        _require(receipt.get("positionRestoredAfterResponse") is False,
                 "intermediate region unexpectedly ended the group")


def validate_group(regions: list[dict[str, Any]]) -> None:
    _require(len(regions) == 3, "consecutive group requires exactly three region receipts")
    identity_fields = ("profileId", "launchSessionId", "gamePid", "challenge")
    for field in identity_fields:
        _require(len({r.get(field) for r in regions}) == 1 and regions[0].get(field),
                 f"group changed native owner field: {field}")
    request_ids = [r.get("requestId") for r in regions]
    _require(all(isinstance(rid, str) and rid for rid in request_ids)
             and len(set(request_ids)) == 3, "native region request IDs are not unique")
    home = (regions[0].get("homeTileX"), regions[0].get("homeTileY"))
    _require(all((r.get("homeTileX"), r.get("homeTileY")) == home for r in regions),
             "original home tile changed within group")
    _require((regions[0].get("preTileX"), regions[0].get("preTileY")) == home,
             "first native region did not start at the original home")
    validate_region(regions[0], x=455, y=425, final=False)
    validate_region(regions[1], x=545, y=495, final=False)
    validate_region(regions[2], x=655, y=565, final=True)
    for earlier, later in zip(regions, regions[1:]):
        before = _stage(later, "before-shift")
        _require(
            (before.get("tileX"), before.get("tileY")) ==
            (earlier.get("targetTileX"), earlier.get("targetTileY")),
            "physical camera continuity lost between retained regions",
        )


def validate_abc(directory: Path) -> None:
    regions = [json.loads((directory / f"sequence-region-{n}.json").read_text(encoding="utf-8"))
               for n in (1, 2, 3)]
    validate_group(regions)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path, help="path to a native-causal archive")
    args = parser.parse_args()
    try:
        validate_abc(args.archive)
    except (OSError, ValueError, KeyError, json.JSONDecodeError) as error:
        print(f"REJECTED {args.archive.name}: {error}")
        return 1
    print(f"ACCEPTED DIAGNOSTIC A/B/C {args.archive.name}; engine, Stop and publication untested")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
