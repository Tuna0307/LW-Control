"""Independently decode pixels; container geometry equality cannot prove text equality."""
import argparse
import hashlib
import json
import math
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

parser = argparse.ArgumentParser()
parser.add_argument("--record", action="store_true")
parser.add_argument("--expect-submitted-defect", action="store_true")
parser.add_argument("--measurements", type=Path)
args = parser.parse_args()
here = Path(__file__).resolve().parent
repo = here.parents[3]
measurements_file = args.measurements or repo / "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-TOOLBAR-VISUAL-001/milestone-c/browser/measurements.json"
manifest = json.loads(measurements_file.read_text(encoding="utf-8"))
records = manifest["records"]
results = []
for pair_id in dict.fromkeys(record["pairId"] for record in records):
    sides = {record["side"]: record for record in records if record["pairId"] == pair_id}
    images = {}
    for side, record in sides.items():
        image_file = repo / record["screenshot"]
        assert hashlib.sha256(image_file.read_bytes()).hexdigest() == record["screenshotSha256"]
        images[side] = Image.open(image_file).convert("RGB")
    assert images["original"].size == images["current"].size
    diff = ImageChops.difference(images["original"], images["current"])
    channels = diff.split()
    changed = ImageChops.lighter(ImageChops.lighter(channels[0], channels[1]), channels[2]).point(lambda value: 255 if value else 0)
    allowed = Image.new("L", changed.size, 0)
    draw = ImageDraw.Draw(allowed)
    fences = []
    original_anchors = sides["original"]["measurement"]["anchors"]
    current_anchors = sides["current"]["measurement"]["anchors"]
    for name, anchor in current_anchors.items():
        if not name.startswith("searchChild") or not anchor or anchor["tag"] != "BUTTON":
            continue
        original_anchor = original_anchors.get(name)
        original_attributes = dict(original_anchor["attrs"]) if original_anchor else {}
        if "disabled" not in dict(anchor["attrs"]) or "disabled" in original_attributes:
            continue
        assert anchor["rect"] == original_anchor["rect"], "availability mask requires equal geometry"
        rect = anchor["rect"]
        box = [math.floor(rect["x"]), math.floor(rect["y"]), math.ceil(rect["x"] + rect["width"]) - 1, math.ceil(rect["y"] + rect["height"]) - 1]
        draw.rectangle(box, fill=255)
        fences.append({"anchor": name, "text": anchor["text"], "box": box})
    outside = ImageChops.subtract(changed, allowed)
    results.append({
        "pairId": pair_id,
        "changedPixels": changed.histogram()[255],
        "changedPixelsOutsideDisabledActionBoxes": outside.histogram()[255],
        "outsideBoundingBox": outside.getbbox(),
        "allowedFences": fences,
    })

report = {"marker": "LWB317_INDEPENDENT_TOOLBAR_PIXEL_MASK", "measurementsSha256": hashlib.sha256(measurements_file.read_bytes()).hexdigest(), "method": "Any RGB-channel difference; only newly disabled direct toolbar buttons masked, floor/ceil rectangle pixels, no tolerance or expansion.", "pairs": results}
print(json.dumps(report, indent=2))
if args.record:
    (here / "pixel-results.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
if args.expect_submitted_defect:
    assert next(row for row in results if row["pairId"] == "treasure-en-light-desktop")["changedPixelsOutsideDisabledActionBoxes"] == 1734
    assert all(row["changedPixelsOutsideDisabledActionBoxes"] == 0 for row in results if row["pairId"] != "treasure-en-light-desktop")
else:
    assert all(row["changedPixelsOutsideDisabledActionBoxes"] == 0 for row in results), "Unclassified presentation pixels outside intentional action fences"
