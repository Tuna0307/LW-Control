from pathlib import Path
import json
from PIL import Image, ImageChops
import numpy as np

root = Path(__file__).resolve().parent / "pixels"
packet = json.loads((root / "pairs.json").read_text(encoding="utf-8"))
fenced = {"stopped", "running-connected", "repair", "unavailable"}
rows = []
for pair in packet["pairs"]:
    a = Image.open(root / pair["original"]["screenshot"]).convert("RGB")
    b = Image.open(root / pair["current"]["screenshot"]).convert("RGB")
    size = (max(a.width, b.width), max(a.height, b.height))
    aa = Image.new("RGB", size, (255, 0, 255))
    bb = Image.new("RGB", size, (0, 255, 255))
    aa.paste(a, (0, 0))
    bb.paste(b, (0, 0))
    diff = ImageChops.difference(aa, bb)
    changed = int(np.any(np.asarray(diff), axis=2).sum())
    rows.append({
        "id": pair["id"],
        "scenario": pair["scenario"],
        "changedPixels": changed,
        "classification": "availability-fence" if pair["scenario"] in fenced else "exact-required",
        "originalRoot": pair["original"]["measurement"]["root"],
        "currentRoot": pair["current"]["measurement"]["root"],
        "originalControls": pair["original"]["measurement"]["controls"],
        "currentControls": pair["current"]["measurement"]["controls"],
    })

exact_required = [row for row in rows if row["classification"] == "exact-required"]
fence_rows = [row for row in rows if row["classification"] == "availability-fence"]
output = {
    "marker": "LWB317_REMAINING_M4_HOME_PIXEL_RESULTS",
    "pairs": len(rows),
    "exactRequired": len(exact_required),
    "exactRequiredPass": sum(row["changedPixels"] == 0 for row in exact_required),
    "availabilityFencePairs": len(fence_rows),
    "availabilityFenceChanged": sum(row["changedPixels"] > 0 for row in fence_rows),
    "rows": rows,
}
(root / "pixel-results.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({k: output[k] for k in ["pairs", "exactRequired", "exactRequiredPass", "availabilityFencePairs", "availabilityFenceChanged"]}))
