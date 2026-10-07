"""Read-only provenance check; never invokes a live probe entry point."""
from __future__ import annotations

import hashlib
import importlib.util
import json
from pathlib import Path


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


root = Path(__file__).resolve().parents[5]
packet = Path(__file__).resolve().parent.parent
identity = json.loads((packet / "a-source-hashes.json").read_text(encoding="utf-8-sig"))
strings = json.loads((packet / "d-v22-selected-strings.json").read_text(encoding="utf-8-sig"))
artifacts = {}
for name, item in identity["artifacts"].items():
    actual = digest(Path(item["path"]))
    if actual != item["sha256"].lower():
        raise SystemExit(f"artifact changed: {name}")
    artifacts[name] = actual
for name, item in identity["sources"].items():
    if digest(root / name) != item["sha256"].lower():
        raise SystemExit(f"source changed: {name}")

spec = importlib.util.spec_from_file_location("read_only_lwlf", root / "tools/run_live_resource_probe.py")
probe = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(probe)
file_version, content_version, entries = probe.read_lwlf(Path(identity["artifacts"]["currentPackage"]["path"]))
if content_version != 22:
    raise SystemExit("current package is not v22")
selected = {
    "Net/Msgs/RadarCenter/DetectEventClaimTreasureMessage.luac",
    "Net/Msgs/RadarCenter/DetectEventGetTreasureClaimInfoMessage.luac",
    "DataCenter/ActivityListData/ActGhostrecon/ActGhostreconManager.luac",
}
modules = {}
for name, raw in entries:
    if name not in selected:
        continue
    decoded = probe.decode_lenc(raw)
    actual = hashlib.sha256(decoded).hexdigest()
    if actual != strings["modules"][name]["decodedSha256"]:
        raise SystemExit(f"decoded module changed: {name}")
    if decoded[:4] != b"\x1bLua" or decoded[4] != 0x53:
        raise SystemExit(f"unexpected bytecode header: {name}")
    modules[name] = {
        "decodedSha256": actual,
        "decodedBytes": len(decoded),
        "first12BytesHex": decoded[:12].hex(),
        "versionByte": "0x53",
        "semanticFunctionExecutionProven": False,
    }
if set(modules) != selected:
    raise SystemExit("selected modules missing")
report = {
    "scope": "lead read-only provenance and bytecode availability check",
    "artifacts": artifacts,
    "productionSourcesMatched": len(identity["sources"]),
    "package": {"fileVersion": file_version, "contentVersion": content_version, "entries": len(entries)},
    "modules": modules,
    "liveExecution": False,
    "limits": "Does not validate function semantics, providers, worker suites, or live game behavior.",
}
destination = Path(__file__).resolve().with_name("static-boundary-results.json")
destination.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(f"LWB317_DEEP002_LEAD_STATIC_BOUNDARY_OK artifacts={len(artifacts)} sources={len(identity['sources'])} modules={len(modules)}")
