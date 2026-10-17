"""Offline current-client LENC candidate proof for FEATURE-RECOVERY-018.

No game launches or mutations of an installed game, recovery journal or
original package. Supply an independently preserved, hash-verified original
package directory. The candidate is written only to a fresh temporary folder.
"""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
from pathlib import Path
import tempfile

HERE = Path(__file__).resolve().parent
ENTRY_NAMES = (
    "DataCenter/Global/LWBridgeLiveResourceProbe.luac",
    "DataCenter/Global/LWBridgeFeature018GameBridge.luac",
    "DataCenter/Global/LWBridgeFeature018MiniGame.luac",
    "DataCenter/Global/LWBridgeFeature018HotkeyGame.luac",
    "DataCenter/Global/LWBridgeFeature018HotkeyActions.luac",
    "DataCenter/Global/LWBridgeFeature018AttackActions.luac",
    "DataCenter/Global/LWBridgeFeature018RelocationActions.luac",
    "DataCenter/Global/LWBridgeFeature018EquipmentActions.luac",
    "DataCenter/Global/LWBridgeFeature018CityGame.luac",
    "DataCenter/Global/LWBridgeFeature018SheepSolver.luac",
)


def load_helper():
    spec = importlib.util.spec_from_file_location("feature018_package_helper",
                                                  HERE / "run_overview_bridge.py")
    if spec is None or spec.loader is None:
        raise RuntimeError("Offline native candidate helper cannot be loaded")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def sha(path: Path):
    digest = hashlib.sha256()
    with path.open("rb") as file:
        for chunk in iter(lambda: file.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--original-dir", type=Path, required=True)
    options = parser.parse_args()
    source = options.original_dir.resolve(strict=True)
    names = ("LWScripts.data", "LWScripts.txt", "version.txt")
    before = {name: sha(source / name) for name in names}
    helper = load_helper()
    with tempfile.TemporaryDirectory(prefix="feature018-candidate-") as folder:
        output = Path(folder)
        outcome = helper.make_candidate({"data": source / "LWScripts.data"}, output)
        _, _, entries = helper.lr.read_lwlf(output / "LWScripts.data")
        package = dict(entries)
        for name in ENTRY_NAMES:
            if name not in package:
                raise RuntimeError("Required candidate module missing: " + name)
            data = helper.lr.decode_lenc(package[name])
            if not data or len(package[name]) > 65535:
                raise RuntimeError("Invalid source-backed LENC module: " + name)
        if helper.lr.ORIGINAL_LUA_ENTRY not in package:
            raise RuntimeError("Official LuaEntry preservation marker absent")
        if outcome["entryCount"] != len(entries):
            raise RuntimeError("Candidate entry-count mismatch")
        print("FEATURE018_OFFLINE_PACKAGE_ROUNDTRIP_PASS",
              "entryCount=" + str(len(entries)),
              "candidateSha256=" + outcome["packageSha256"],
              "wrapperSize=" + str(outcome["wrapperLencSize"]))
    after = {name: sha(source / name) for name in names}
    if before != after:
        raise RuntimeError("Read-only source hash changed during offline candidate test")
    print("FEATURE018_ORIGINAL_BYTES_UNCHANGED_PASS", before["LWScripts.data"])


if __name__ == "__main__":
    main()
