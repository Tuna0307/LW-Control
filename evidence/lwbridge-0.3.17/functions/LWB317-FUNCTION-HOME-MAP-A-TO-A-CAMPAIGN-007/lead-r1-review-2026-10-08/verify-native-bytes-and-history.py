"""Independently verify R1 native slice identities and immutable packet contents."""
import hashlib
import json
import subprocess
from pathlib import Path
import pefile

LEAD = Path(__file__).resolve().parent
REPO = next(p for p in LEAD.parents if (p / "AGENTS.md").is_file())
PACKET = LEAD.parent
R1 = PACKET / "r1-2026-10-08"
CHECKPOINT = "57fc69b0219e5c2457ba13d0a24bb7acc3a58de3"
REFERENCE = Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
EXPECTED = "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
data = REFERENCE.read_bytes()
assert hashlib.sha256(data).hexdigest() == EXPECTED
pe = pefile.PE(data=data)
trace = json.loads((R1 / "home-native-static-trace.json").read_text(encoding="utf-8-sig"))
assert trace["referenceSha256"] == EXPECTED
rows = list(trace["primary"].values()) + trace["boundedDirectCallees"] + [trace["sharedAvailableLargeCallee"]]
for row in rows:
    start, end = [int(x, 16) for x in row["range"]]
    assert hashlib.sha256(pe.get_data(start, end-start)).hexdigest() == row["sha256"], row["range"]
tracked = subprocess.check_output(["git", "ls-tree", "-r", "--name-only", CHECKPOINT, "--",
    str(PACKET.relative_to(REPO)).replace("\\", "/")], cwd=REPO, text=True).splitlines()
for path in tracked:
    actual = subprocess.check_output(["git", "hash-object", "--path="+path, path], cwd=REPO, text=True).strip()
    expected = subprocess.check_output(["git", "rev-parse", CHECKPOINT+":"+path], cwd=REPO, text=True).strip()
    assert actual == expected, path
result = {
    "workerCheckpoint": CHECKPOINT, "originalExeSha256": EXPECTED,
    "verifiedNativeSlices": len(rows), "handlerSlices": len(trace["primary"]),
    "directCalleeSlices": len(trace["boundedDirectCallees"]),
    "historicalFilesUnchanged": len(tracked),
    "proof": "EXACT_BYTES identities; no semantic retry/timing equivalence inferred.",
    "remainingHomeStaticWork": True,
    "leadDesktopActions": 0, "leadGameLaunches": 0
}
(LEAD / "native-and-history-integrity.json").write_text(json.dumps(result, indent=2)+"\n", encoding="utf-8")
print("LEAD_R1_NATIVE_HISTORY_OK slices", len(rows), "historical", len(tracked))
