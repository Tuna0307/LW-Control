"""MAP016 real packaged first-use DENIAL witness; never permits an updater/game.

The inherited work item forbids updater actions. This checks a real extracted
Windows WebView app with a genuinely absent settlement marker and native Auto
Launch disabled; the production lifecycle must reject Start BEFORE the official
launcher is created. It is deliberately a negative witness, not proof that an
unmarked first-use game can start once.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools/map_014_lead_native"))
from run_ui import LUA, admit, current_processes  # noqa: E402


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check_processes():
    rows = current_processes()
    # The normal runner is not allowed to interact with any owner process.
    relevant = [r for r in rows if r.get("Name") in
                ("LastWar.exe", "LastWarLauncher.exe", "LWBridge.Desktop.exe")]
    if relevant:
        raise RuntimeError(f"An existing game/launcher/app blocks this negative proof: {relevant}")


def run(exe: Path, destination: Path, commit: str):
    if os.name != "nt" or not destination.resolve().is_relative_to((ROOT / "artifacts").resolve()):
        raise RuntimeError("The first-use packaged proof requires Windows and a task-owned artifacts directory")
    if destination.exists():
        raise FileExistsError("Refusing to overwrite a prior first-use proof")
    if not exe.is_file() or exe.name != "LWBridge.Desktop.exe":
        raise RuntimeError("An extracted packaged executable is required")
    check_processes()
    originals = admit()
    version = subprocess.run(
        ["powershell", "-NoProfile", "-Command",
         "(Get-Item -LiteralPath '" + str(exe).replace("'", "''") + "').VersionInfo.ProductVersion"],
        capture_output=True, text=True, check=True).stdout.strip()
    if not version.endswith("+" + commit):
        raise RuntimeError("The extracted executable's source commit differs")
    destination.mkdir(parents=True)
    (destination / "config.json").write_text(json.dumps({
        "schemaVersion": 1, "owner": "LWBridgeRebuild", "profileId": "map014-native-ui",
        "gameRoot": str(Path.home() / "AppData/Local/FunFly/Last War-Survival Game"),
        "autoLaunchGame": False, "autoReconnect": False,
        "gameDesiredRunning": False, "serverJumpHistory": [],
    }), encoding="utf-8")
    proof = destination / "native-ui-map.json"
    environment = dict(os.environ,
                       LWBRIDGE_MAP014_NATIVE_UI="1",
                       LWBRIDGE_MAP014_SELECTION_UI="all-nine",
                       LWBRIDGE_MAP016_NO_OFFICIAL_UPDATER_PROOF="1")
    command = [str(exe), "--isolated-root", str(destination),
               "--normal-ui-live-map-proof", str(proof),
               "--view", "map-data", "--language", "zh-CN", "--theme", "light"]
    # Explicit hidden-window background UI. The process must close itself.
    startup = subprocess.STARTUPINFO()
    startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    startup.wShowWindow = 0
    completed = subprocess.run(command, env=environment, startupinfo=startup,
                               capture_output=True, text=True, timeout=100, check=False)
    errors = Path(str(proof) + ".error.txt")
    admission = destination / "overview-evidence/map016-official-settle-admission.json"
    details = json.loads(admission.read_text(encoding="utf-8")) if admission.is_file() else None
    after = {name: sha(LUA / name) for name in originals}
    check_processes()
    # BridgeCommandException.ToString() writes its user-facing message, not
    # the internal code. Require that exact admission and the structured
    # denial receipt together; neither alone establishes updater avoidance.
    passed = (completed.returncode != 0 and not proof.exists() and errors.is_file() and
              "First-use official settlement requires the unmodified launcher; no updater was started." in
              errors.read_text(encoding="utf-8") and
              details is not None and details.get("state") == "first_use_official_launcher_required" and
              details.get("officialLauncherStartedByThisAdmission") is False and
              details.get("updaterActionAuthorized") is False and
              details.get("forceOfficialSettle") is False and
              details.get("packageSha256") == originals["LWScripts.data"] and
              after == originals)
    receipt = dict(state="proven" if passed else "failed",
                   proof="actual_packaged_first_use_no_official_updater_denial",
                   sourceCommit=commit, executableSha256=sha(exe),
                   profileId="map014-native-ui", nativeAutoLaunch=False,
                   firstUseMarkerAbsent=not (destination / "overview-evidence/official-settled-client.json").exists(),
                   processExitCode=completed.returncode,
                   deniedBeforeOfficialUpdater=bool(passed),
                   admission=details, originalScriptsRestored=after == originals,
                   noGameOrLauncherProcesses=True)
    (destination / "first-use-denial-receipt.json").write_text(
        json.dumps(receipt, indent=2), encoding="utf-8")
    if not passed:
        raise RuntimeError("Real packaged no-updater first-use denial failed; inspect task-owned receipts")
    return receipt


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--exe", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--commit", required=True)
    args = parser.parse_args()
    try:
        print(json.dumps(run(args.exe.resolve(strict=True), args.output.resolve(), args.commit), indent=2))
    except Exception as error:
        print("MAP016_FIRST_USE_NO_UPDATER_ERROR:", error, file=sys.stderr)
        sys.exit(2)
