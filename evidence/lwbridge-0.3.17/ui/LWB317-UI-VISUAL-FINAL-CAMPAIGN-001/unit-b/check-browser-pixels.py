from __future__ import annotations

import json
import math
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw


HERE = Path(__file__).resolve().parent
BROWSER = HERE / "browser"
MEASUREMENTS = json.loads((BROWSER / "measurements.json").read_text(encoding="utf-8"))
COMPARISONS = json.loads((BROWSER / "comparison-results.json").read_text(encoding="utf-8"))

records = {(entry["pairId"], entry["side"]): entry for entry in MEASUREMENTS["records"]}
comparisons = {entry["pairId"]: entry for entry in COMPARISONS["pairs"]}

accepted_non_exact = {
    "city-error-en-dark": "accepted documented canonical query-error banner",
    "scheduled-empty-en-light": "runtime native Start Scan provider fence",
    "scheduled-conditional-en-dark": "runtime native Start Scan provider fence",
}


def rect_box(rect: dict, pad: int = 0) -> tuple[int, int, int, int]:
    return (
        math.floor(rect["x"]) - pad,
        math.floor(rect["y"]) - pad,
        math.ceil(rect["x"] + rect["width"]) + pad,
        math.ceil(rect["y"] + rect["height"]) + pad,
    )


def changed_mask(diff: Image.Image) -> Image.Image:
    r, g, b = diff.split()
    merged = ImageChops.lighter(ImageChops.lighter(r, g), b)
    return merged.point(lambda value: 255 if value else 0)


def changed_count(mask: Image.Image) -> int:
    return sum(1 for value in mask.getdata() if value)


results = []
for pair_id, comparison in comparisons.items():
    original = Image.open(BROWSER / f"{pair_id}-original.png").convert("RGB")
    current = Image.open(BROWSER / f"{pair_id}-current.png").convert("RGB")
    assert original.size == current.size, f"{pair_id}: screenshot dimensions"
    diff = ImageChops.difference(original, current)
    changed = changed_mask(diff)
    count = changed_count(changed)
    bbox = changed.getbbox()

    allowed = Image.new("L", original.size, 0)
    reason = None
    boxes = []
    if pair_id in accepted_non_exact:
        reason = accepted_non_exact[pair_id]
        draw = ImageDraw.Draw(allowed)
        if pair_id.startswith("scheduled-"):
            start_rect = records[(pair_id, "current")]["measurement"]["anchors"]["startScan"]["rect"]
            box = rect_box(start_rect)
            draw.rectangle(box, fill=255)
            boxes.append(box)
        else:
            current_measurement = records[(pair_id, "current")]["measurement"]
            panel = current_measurement["anchors"]["panel"]["rect"]
            alert = current_measurement["anchors"]["scanError"]["rect"]
            # The accepted current-only query alert inserts one grid row and therefore shifts the
            # table, panel bottom border and panel shadow below it. Keep the mask bounded to that
            # downstream portion of the Map panel; toolbar/search controls above the alert remain strict.
            box = (
                max(0, math.floor(panel["x"]) - 30),
                math.floor(alert["y"]),
                min(original.size[0], math.ceil(panel["x"] + panel["width"]) + 30),
                min(original.size[1], math.ceil(panel["y"] + panel["height"]) + 22),
            )
            draw.rectangle(box, fill=255)
            boxes.append(box)

    outside = ImageChops.subtract(changed, allowed)
    outside_count = changed_count(outside)
    if pair_id in accepted_non_exact:
        assert count > 0, f"{pair_id}: positive-control difference disappeared; reclassify the contract"
        assert outside_count == 0, f"{pair_id}: {outside_count} changed pixels outside accepted contract"
        assert not comparison["exactScreenshotBytes"], f"{pair_id}: comparison should be non-exact"
    else:
        assert count == 0, f"{pair_id}: unexpected {count} changed pixels"
        assert outside_count == 0
        assert comparison["exactScreenshotBytes"], f"{pair_id}: byte exact flag"

    if count:
        preview = Image.new("RGB", original.size, "white")
        preview.paste((220, 220, 220), mask=changed)
        if boxes:
            preview_draw = ImageDraw.Draw(preview)
            for box in boxes:
                preview_draw.rectangle(box, outline="black", width=1)
        preview.save(BROWSER / f"{pair_id}-diff-mask.png")

    results.append({
        "pairId": pair_id,
        "changedPixels": count,
        "changedBBox": list(bbox) if bbox else None,
        "acceptedReason": reason,
        "allowedBoxes": [list(box) for box in boxes],
        "outsideAcceptedPixels": outside_count,
    })

assert len(results) == 16
assert sum(1 for entry in results if entry["changedPixels"] == 0) == 13
assert {entry["pairId"] for entry in results if entry["changedPixels"]} == set(accepted_non_exact)

report = {
    "task": "LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b",
    "marker": "LWB317_VISUAL_FINAL_UNIT_B_PIXELS_OK",
    "pairs": len(results),
    "pixelExactPairs": sum(1 for entry in results if entry["changedPixels"] == 0),
    "acceptedDifferencePairs": len(accepted_non_exact),
    "outsideAcceptedPixels": sum(entry["outsideAcceptedPixels"] for entry in results),
    "results": results,
}
(HERE / "pixel-results.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(json.dumps({key: report[key] for key in ("marker", "pairs", "pixelExactPairs", "acceptedDifferencePairs", "outsideAcceptedPixels")}, indent=2))
