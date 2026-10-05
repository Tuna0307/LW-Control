from __future__ import annotations

import json
import math
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw


HERE = Path(__file__).resolve().parent
REPO = HERE.parents[4]
UNIT_B = REPO / "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b"
BROWSER = UNIT_B / "browser"
MEASUREMENTS = json.loads((BROWSER / "measurements.json").read_text(encoding="utf-8"))
COMPARISONS = json.loads((BROWSER / "comparison-results.json").read_text(encoding="utf-8"))

records = {(entry["pairId"], entry["side"]): entry for entry in MEASUREMENTS["records"]}
comparisons = {entry["pairId"]: entry for entry in COMPARISONS["pairs"]}
accepted_non_exact = {
    "scheduled-empty-en-light": "runtime native Start Scan provider fence",
    "scheduled-conditional-en-dark": "runtime native Start Scan provider fence",
}
superseded_pair = "city-error-en-dark"


def rect_box(rect: dict) -> tuple[int, int, int, int]:
    return (
        math.floor(rect["x"]),
        math.floor(rect["y"]),
        math.ceil(rect["x"] + rect["width"]),
        math.ceil(rect["y"] + rect["height"]),
    )


def changed_mask(diff: Image.Image) -> Image.Image:
    r, g, b = diff.split()
    return ImageChops.lighter(ImageChops.lighter(r, g), b).point(lambda value: 255 if value else 0)


def changed_count(mask: Image.Image) -> int:
    return sum(1 for value in mask.get_flattened_data() if value)


results = []
for pair_id, comparison in comparisons.items():
    if pair_id == superseded_pair:
        continue
    original = Image.open(BROWSER / f"{pair_id}-original.png").convert("RGB")
    current = Image.open(BROWSER / f"{pair_id}-current.png").convert("RGB")
    assert original.size == current.size, f"{pair_id}: screenshot dimensions"
    changed = changed_mask(ImageChops.difference(original, current))
    count = changed_count(changed)
    allowed = Image.new("L", original.size, 0)
    reason = None
    boxes = []
    if pair_id in accepted_non_exact:
        reason = accepted_non_exact[pair_id]
        start_rect = records[(pair_id, "current")]["measurement"]["anchors"]["startScan"]["rect"]
        box = rect_box(start_rect)
        ImageDraw.Draw(allowed).rectangle(box, fill=255)
        boxes.append(box)
    outside_count = changed_count(ImageChops.subtract(changed, allowed))
    if pair_id in accepted_non_exact:
        assert count > 0, f"{pair_id}: provider-fence difference disappeared; reclassify the contract"
        assert outside_count == 0, f"{pair_id}: {outside_count} pixels outside Start Scan fence"
        assert not comparison["exactScreenshotBytes"], f"{pair_id}: expected non-exact saved pair"
    else:
        assert count == 0, f"{pair_id}: unexpected {count} changed pixels"
        assert comparison["exactScreenshotBytes"], f"{pair_id}: byte-exact saved flag"
    results.append({
        "pairId": pair_id,
        "changedPixels": count,
        "acceptedReason": reason,
        "allowedBoxes": [list(box) for box in boxes],
        "outsideAcceptedPixels": outside_count,
    })

assert len(results) == 15
assert sum(1 for entry in results if entry["changedPixels"] == 0) == 13
assert {entry["pairId"] for entry in results if entry["changedPixels"]} == set(accepted_non_exact)
assert sum(entry["outsideAcceptedPixels"] for entry in results) == 0

print(json.dumps({
    "marker": "LWB317_REMAINING_M5_MAP_PIXELS_READ_ONLY_OK",
    "historicalPairsReplayed": len(results),
    "pixelExactPairs": 13,
    "acceptedDifferencePairs": 2,
    "outsideAcceptedPixels": 0,
    "supersededHistoricalPair": superseded_pair,
}, indent=2))
