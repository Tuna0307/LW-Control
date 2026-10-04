import json
from pathlib import Path
from PIL import Image, ImageChops

HERE = Path(__file__).resolve().parent
report = json.loads((HERE / "enabled-class-current.json").read_text(encoding="utf-8"))
results = []

for case in report["cases"]:
    # Compare RGB explicitly.  ImageChops.getbbox() on RGBA can report an empty
    # box when RGB differs but the alpha-difference band is uniformly zero.
    fixed = Image.open(Path(case["screenshot"]["path"])).convert("RGB")
    mutation = Image.open(Path(case["mutationScreenshot"]["path"])).convert("RGB")
    if fixed.size != mutation.size:
        raise AssertionError(f"{case['id']}: screenshot dimensions differ {fixed.size} != {mutation.size}")
    diff = ImageChops.difference(fixed, mutation)
    bbox = diff.getbbox()
    changed = 0
    if bbox:
        changed = sum(1 for px in diff.getdata() if px != (0, 0, 0))
    if changed <= 0:
        raise AssertionError(f"{case['id']}: removing source-proven is-enabled class produced zero pixel changes")
    results.append({"id": case["id"], "size": list(fixed.size), "changedPixels": changed, "diffBox": list(bbox) if bbox else None})

out = {
    "result": "LWB317_VISUAL_FINAL_UNIT_C_ENABLED_CLASS_PIXELS_OK",
    "cases": results,
    "caseCount": len(results),
    "allMutationsDetected": all(item["changedPixels"] > 0 for item in results),
}
(HERE / "enabled-class-pixels.json").write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
print(json.dumps(out, indent=2))
