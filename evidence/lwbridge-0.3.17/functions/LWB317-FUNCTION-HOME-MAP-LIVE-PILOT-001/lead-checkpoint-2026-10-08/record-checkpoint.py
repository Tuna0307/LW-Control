"""Read-only post-restoration checkpoint; no desktop input or game launch."""
from pathlib import Path
import datetime
import hashlib
import json
import subprocess
import sys

HERE = Path(__file__).resolve().parent
REPO = next(p for p in HERE.parents if (p / "AGENTS.md").is_file())
ROOT = Path(r"C:\Users\chimw\AppData\Local\Temp\LWB317-LIVE-PILOT-001-root")
SCRIPTS = Path(r"C:\Users\chimw\AppData\LocalLow\FunFly\Last War-Survival Game\lwScripts")
BACKUP = ROOT / "overview-bridge-backups/20261007-155600-22c4860c742040e39664c2dc1486155d"
EXPECTED = {
    "LWScripts.data": "248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22",
    "LWScripts.txt": "d2f9bcc165f41427d4b3b7b9243e4a483bf316519a16b4ce9be658ba9afa783a",
    "version.txt": "785f3ec7eb32f30b90cd0fcf3657d388b5ff4297f2f9716ff66e9b69c05ddd09",
}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(args):
    result = subprocess.run(args, cwd=REPO, capture_output=True, text=True)
    return {"command": args, "exitCode": result.returncode,
            "stdout": result.stdout, "stderr": result.stderr}


report = {"capturedAtUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
          "sourceCheckpoint": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=REPO, text=True).strip(),
          "purpose": "restoration and focused headless regression checkpoint; not full live-pilot acceptance",
          "desktopCaptureOrInput": False, "gameLaunchedByThisCheckpoint": False}
report["files"] = {name: {"expected": expected, "installed": digest(SCRIPTS / name),
                          "backup": digest(BACKUP / name)} for name, expected in EXPECTED.items()}
manifest = json.loads((BACKUP / "manifest.json").read_text(encoding="utf-8"))
report["backupManifest"] = {"path": str(BACKUP / "manifest.json"), "stage": manifest["stage"],
                            "updatedAtUtc": manifest["updatedAtUtc"], "sha256": digest(BACKUP / "manifest.json")}
report["recoveryJournalExists"] = (ROOT / "overview-bridge/recovery.json").exists()
report["processInventory"] = run(["pwsh", "-NoProfile", "-Command",
    "$items=@(Get-CimInstance Win32_Process | Where-Object { $_.Name -match 'LastWar|LWBridge' } | Select-Object ProcessId,Name,ExecutablePath,CreationDate); ConvertTo-Json -InputObject $items -Depth 4 -Compress"])
report["checks"] = [run([sys.executable, script]) for script in (
    "tests/home_runtime_file_ownership_checks.py",
    "tests/home_runtime_lease_lua_checks.py")]
# Existing system Python has dncil; the isolated Lua environment has lupa.
# Preserve the first missing-dncil attempt separately instead of changing a gate.
report["checks"].append(run([r"C:\Users\chimw\AppData\Local\Programs\Python\Python312\python.exe",
                            "tools/check_current_client_runtime_contract.py"]))
report["attempt5Files"] = {p.name: digest(p) for p in sorted((HERE.parent / "attempt-5-fresh-ready").iterdir()) if p.is_file()}
report["ok"] = (
    all(v["expected"] == v["installed"] == v["backup"] for v in report["files"].values())
    and report["backupManifest"]["stage"] == "restored"
    and not report["recoveryJournalExists"]
    and report["processInventory"]["exitCode"] == 0
    and json.loads(report["processInventory"]["stdout"]) == []
    and all(v["exitCode"] == 0 for v in report["checks"]))
(HERE / "checkpoint-results.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"ok": report["ok"], "files": len(report["files"]),
                  "attempt5Files": len(report["attempt5Files"]),
                  "checkExitCodes": [v["exitCode"] for v in report["checks"]]}))
sys.exit(0 if report["ok"] else 1)
