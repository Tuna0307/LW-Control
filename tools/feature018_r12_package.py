"""Package an already tested, source-pinned R12 Windows app; no live actions or push."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parent.parent


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("publish", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    if subprocess.check_output(["git", "status", "--porcelain"], cwd=ROOT, text=True).strip():
        raise RuntimeError("Commit the tested source before packaging")
    publish = args.publish.resolve(strict=True)
    for name in ("LWBridge.Desktop.exe", "LWBridge.Desktop.dll", "ProductionUi/index.html"):
        if not (publish / name).is_file():
            raise RuntimeError("Incomplete Windows publish: " + name)
    version = subprocess.check_output(["powershell", "-NoProfile", "-Command",
        "[Diagnostics.FileVersionInfo]::GetVersionInfo($env:LWBRIDGE_R12_PACKAGE_EXE).ProductVersion"],
        env=dict(os.environ, LWBRIDGE_R12_PACKAGE_EXE=str(publish / "LWBridge.Desktop.exe")),
        text=True).strip()
    if not version.endswith("+" + commit):
        raise RuntimeError("Executable does not identify the committed source: " + version)
    subprocess.run(["node", str(ROOT / "src/LWBridge.UI-0.3.17/scripts/check-production-build.mjs"),
                    str(publish / "ProductionUi")], cwd=ROOT, check=True)
    for packed in (publish / "OverviewBridge").glob("*.lua"):
        if sha(packed) != sha(ROOT / "tools" / packed.name):
            raise RuntimeError("Stale game adapter: " + packed.name)
    files = sorted(p for p in publish.rglob("*") if p.is_file() and p.suffix != ".pdb")
    manifest = {"sourceCommit": commit, "productVersion": version,
        "scope": "FEATURE-RECOVERY-018 R12 G key normal/Sunday Parkour stages",
        "approvedUid": "2553945035002288", "approvedServerId": 2288,
        "files": {p.relative_to(publish).as_posix(): sha(p) for p in files}}
    args.output.mkdir(parents=True, exist_ok=True)
    target = args.output / ("LW-Control-FEATURE-018-R12-G-" + commit[:12] + ".zip")
    with zipfile.ZipFile(target, "x", compression=zipfile.ZIP_DEFLATED) as archive:
        for file in files:
            archive.write(file, "LW-Control-UI/" + file.relative_to(publish).as_posix())
        archive.writestr("LW-Control-UI/SOURCE-COMMIT.txt", commit + "\n")
        archive.writestr("LW-Control-UI/R12-MANIFEST.json", json.dumps(manifest, indent=2))
        archive.writestr("LW-Control-UI/README-R12.txt",
            "R12 owner-test checkpoint. Extract the entire folder and run LWBridge.Desktop.exe.\n"
            "Connect the approved sub-account: UID2553945035002288 / server2288.\n"
            "Enable G on Mini Games, then press G in the game window during a normal numbered\n"
            "stage or Sunday Parkour challenge. Each press adds five native soldiers.\n"
            "No per-press confirmation. Outside an active run G does nothing.\n"
            "At formation capacity, additions enter the game's native reserve troop count.\n"
            "The game's native stage capacity and end/pause rules still apply.\n"
            "R12 is for owner testing; the remaining FEATURE-018 work is not accepted.\n")
    result = {"zip": str(target.resolve()), "sha256": sha(target),
              "sourceCommit": commit, "fileCount": len(files)}
    (args.output / "r12-package-receipt.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result))


if __name__ == "__main__":
    main()
