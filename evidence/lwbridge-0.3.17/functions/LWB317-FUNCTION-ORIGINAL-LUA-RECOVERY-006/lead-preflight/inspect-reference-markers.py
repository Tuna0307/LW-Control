"""Static original-0.3.17 literal inventory; no execution/key access/decryption."""
from pathlib import Path
import hashlib
import json

reference = Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
blob = reference.read_bytes()
identity = hashlib.sha256(blob).hexdigest()
expected = "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
if identity != expected:
    raise SystemExit("Original reference identity changed")
markers = {}
for text in (
    "bridge-scripts.dat", "package-key.envelope", "LWBP2|", "LWKE1",
    "getTreasureClaimStatus", "claimTreasures", "prepareGhostPlunderTasks",
):
    markers[text] = {}
    for encoding in ("ascii", "utf-16-le"):
        needle = text.encode(encoding)
        positions = []
        cursor = 0
        while True:
            cursor = blob.find(needle, cursor)
            if cursor < 0:
                break
            positions.append(hex(cursor))
            cursor += len(needle)
        markers[text][encoding] = positions
report = {
    "referencePath": str(reference),
    "referenceSha256": identity,
    "scope": "Static literal file offsets only; offsets are not RVAs",
    "markers": markers,
    "limits": "References do not prove a package is embedded, that usable key material exists, or that its format matches 0.3.1. No extraction or decryption performed.",
}
destination = Path(__file__).with_name("original-marker-results.json")
destination.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print("LWB317_ORIGINAL_LUA_PREFLIGHT_OK markers=7 scope=literal-locations-only")
