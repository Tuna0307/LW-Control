"""Integrity-verified R5 source-review ZIP; NEVER label this a Windows delivery.

The last actual Windows executable was built for R4. The R5 Windows build
cannot be credited because an unrelated npm ci operation was denied by the
filesystem (EPERM unlink esbuild.exe). This tool only archives committed source
and the historical R4 Windows digest, without running npm, game or packaging
unverified Windows binaries. Never use its result as packaged-app proof.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parent.parent
BRANCH = "codex/feature-recovery-018"
R4_SHA = "7ef40983553155ba79907640201720f3fe69a89a6eab6dcd6f749a83615c992f"
R4_ZIP = (ROOT / "artifacts/feature018/delivery-r4-3c9049aa/"
          "LW-Control-FEATURE-RECOVERY-018-R4-PARTIAL-3c9049aa0662.zip")


def git(*args: str) -> str:
    return subprocess.check_output(["git", *args], cwd=ROOT, text=True).strip()


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def source_file(path: str) -> bool:
    return (path.startswith("tools/feature018_") and
            Path(path).suffix in (".lua", ".py")) or (
        path.startswith("src/LWBridge.Desktop/Feature018") and
        path.endswith(".cs")) or (
        path.startswith("tests/LWBridge.Desktop.Checks/Feature018") and
        path.endswith(".cs")) or path in {
        "src/LWBridge.UI-0.3.17/src/HotkeyPages.jsx",
        "src/LWBridge.UI-0.3.17/scripts/check-feature018-rapid-mounted.mjs",
        "src/LWBridge.UI-0.3.17/scripts/check-feature018-land-mounted.mjs",
        "docs/FEATURE_RECOVERY_018_WORKER_REPORT.md",
        "docs/FEATURE_RECOVERY_018_INVENTORY.md",
        "docs/OWNER_DIRECTION_SUB_ACCOUNT_ACTIONS_2026-10-17.md",
        "docs/work-items/LWB317-FEATURE-RECOVERY-018.md",
        "AGENTS.md",
    }


def main() -> None:
    if git("branch", "--show-current") != BRANCH or git("status", "--porcelain"):
        raise RuntimeError("R5 source review requires a clean feature branch")
    sha = git("rev-parse", "HEAD")
    remote = git("ls-remote", "origin", "refs/heads/" + BRANCH).split()[0]
    if sha != remote:
        raise RuntimeError("Remote commit differs from local; refusing review ZIP")
    if not R4_ZIP.is_file() or digest(R4_ZIP.read_bytes()) != R4_SHA:
        raise RuntimeError("Last actual Windows delivery integrity not verified")

    paths = [p for p in git("ls-files").splitlines() if source_file(p)]
    if len(paths) < 25 or not all((ROOT / p).is_file() for p in paths):
        raise RuntimeError("Missing source, tests or accepted original inventory")
    output = ROOT / "artifacts/feature018" / ("source-review-r5-" + sha[:8])
    output.mkdir(parents=True, exist_ok=True)
    target = output / ("LW-Control-FEATURE-018-R5-SOURCE-ONLY-" + sha[:12] + ".zip")
    if target.exists():
        raise FileExistsError("Never overwrite an existing source-review ZIP")
    files = {}
    for path in paths:
        files["Source/" + path] = (ROOT / path).read_bytes()
    readme = (f"FEATURE-RECOVERY-018 R5 SOURCE REVIEW ONLY — NOT A WINDOWS DELIVERY\n"
              f"Commit: {sha}\nBranch: {BRANCH}\n"
              "Status: PARTIAL / NOT READY_FOR_LEAD_AUDIT\n"
              "Do not install or infer packaged game proof from these sources.\n"
              "The R4 Windows executable remains the LAST independently published/build-verified app.\n"
              "R5 full build was prevented by the explicit OS EPERM denial during npm ci unlink.\n"
              "The R4 game helper's separate WinError5 recovery-marker rename denial was not retried.\n"
              f"R4 Windows delivery SHA256: {R4_SHA}\n"
              "No approved account gameplay was attempted or credited in R5.\n"
              "Read Source/docs/FEATURE_RECOVERY_018_WORKER_REPORT.md before interpreting source results.\n")
    files["README-NOT-WINDOWS-DELIVERY.txt"] = readme.encode("utf-8")
    manifest = {
        "task": "FEATURE-RECOVERY-018", "branch": BRANCH, "commit": sha,
        "status": "PARTIAL", "sourceOnly": True, "windowsDelivery": False,
        "lastValidatedWindowsCommit": "3c9049aa06624e4b02f80db1a0828d5ee545bfaa",
        "lastValidatedWindowsZipSha256": R4_SHA,
        "newNativeGameplayEvidence": False,
        "newWindowsBuildFailure": "EPERM denied npm ci unlink esbuild.exe",
        "newGameStartup": False, "files": {
            name: {"sha256": digest(data), "size": len(data)}
            for name, data in sorted(files.items())
        },
    }
    with zipfile.ZipFile(target, "x", compression=zipfile.ZIP_DEFLATED) as z:
        for name, data in sorted(files.items()):
            z.writestr(name, data)
        z.writestr("SOURCE-REVIEW-INTEGRITY.json",
                   json.dumps(manifest, indent=2).encode("utf-8"))
    with zipfile.ZipFile(target) as z:
        if z.testzip() is not None:
            raise RuntimeError("Review ZIP CRC failure")
        for name, record in manifest["files"].items():
            if digest(z.read(name)) != record["sha256"]:
                raise RuntimeError("Review source file digest mismatch: " + name)
    report = {
        "path": str(target), "sha256": digest(target.read_bytes()),
        "size": target.stat().st_size, "entries": len(files) + 1,
        "commit": sha, "windowsDelivery": False, "sourceOnly": True,
    }
    (output / "source-integrity.json").write_text(
        json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print("FEATURE018_R5_SOURCE_REVIEW_ONLY_OK", json.dumps(report))


if __name__ == "__main__":
    main()
