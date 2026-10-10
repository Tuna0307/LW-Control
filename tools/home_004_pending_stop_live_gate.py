"""Fresh isolated, exact-owned Home pending-recovery user Stop witness.

Usage: prepare|enable|check-owned|exit-owned|finish. The existing R1 backup
and R2 exact-handle checks are mandatory. No general process termination.
"""
from pathlib import Path
import json
import shutil
import subprocess
import sys

import home_004_r1_live_gate as gate
import home_004_r2_owned_exit as exact

followup = "--followup" in sys.argv[1:]
task = Path(__file__).resolve().parents[1] / "artifacts" / "home-004" / (
    "pending-stop-live-followup" if followup else "pending-stop-live")
gate.task = task
gate.profile_root = task / "isolated"
gate.receipt_file = task / "preflight.json"
gate.profile = "home-004-pending-stop-live-followup" if followup else "home-004-pending-stop-live"
exact.TASK = task
exact.RUNTIME = task / "isolated" / "overview-bridge"
exact.PROFILE = gate.profile


def restore_after_verified_exit():
    """Restore only the task's backed-up triplet after a failed cancel cleanup."""
    if gate.owners():
        raise RuntimeError("Live game/launcher/host owner; refusing installation restore")
    active = subprocess.check_output(["tasklist", "/fo", "csv", "/nh"],
                                     text=True, errors="replace").lower()
    if any(name in active for name in ("lastwarupdater.exe", "lastwarsync.exe")):
        raise RuntimeError("Updater/sync process exists; refusing installation restore")
    preflight = gate.read()
    failed = json.loads((task / "finish.json").read_text(encoding="utf-8"))
    if failed["processesRemaining"] or failed["exactScriptsRestored"] or failed["recoveryJournalExists"]:
        raise RuntimeError("Not the failed, zero-owner, journal-free task receipt")
    if gate.prior.sha(gate.prior.TARGET) != gate.prior.REFERENCE:
        raise RuntimeError("Original reference executable changed")
    if preflight["originalScriptHashes"] != gate.prior.EXPECTED:
        raise RuntimeError("Expected original script baseline differs from saved preflight")
    current = gate.hashes()
    if current != failed["installedScriptHashes"]:
        raise RuntimeError("Installed script bytes changed since failure receipt")
    for name in gate.prior.FILES:
        if gate.prior.sha(task / "backup-original" / name) != gate.prior.EXPECTED[name]:
            raise RuntimeError("Task's original backup checksum differs for " + name)
    archive = task / "post-stop-unrestored-scripts"
    archive.mkdir(exist_ok=False)
    shutil.copy2(task / "finish.json", task / "finish-before-manual-restoration.json")
    for name in gate.prior.FILES:
        shutil.copy2(gate.prior.SCRIPT / name, archive / name)
        if gate.prior.sha(archive / name) != current[name]:
            raise RuntimeError("Could not preserve failed installed bytes for " + name)
    for name in gate.prior.FILES:
        shutil.copy2(task / "backup-original" / name, gate.prior.SCRIPT / name)
        if gate.prior.sha(gate.prior.SCRIPT / name) != gate.prior.EXPECTED[name]:
            raise RuntimeError("Exact original restoration failed at " + name)
    receipt = {"task": str(task), "originalBackupsRestored": True,
               "beforeSha256": current, "afterSha256": gate.hashes(),
               "ownerProcesses": gate.owners(), "failedReceiptPreserved": True}
    (task / "verified-manual-restoration.json").write_text(
        json.dumps(receipt, indent=2), encoding="utf-8")
    print(json.dumps(receipt, indent=2))

if __name__ == "__main__":
    actions = [a for a in sys.argv[1:] if a != "--followup"]
    if len(actions) != 1 or sys.argv[1:].count("--followup") > 1:
        raise SystemExit(__doc__)
    commands = {
        "prepare": gate.prepare,
        "enable": gate.enable,
        "check-owned": lambda: exact.trigger(check_only=True),
        "exit-owned": lambda: exact.trigger(check_only=False),
        "finish": gate.finish,
        "restore-owned": restore_after_verified_exit,
    }
    if actions[0] not in commands:
        raise SystemExit(__doc__)
    commands[actions[0]]()
