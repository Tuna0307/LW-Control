from __future__ import annotations

import hashlib
import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
HELPER = ROOT / "tools" / "run_live_resource_probe.py"
spec = importlib.util.spec_from_file_location("lwbridge_live_resource_probe", HELPER)
if spec is None or spec.loader is None:
    raise RuntimeError("could not load package helper")
lr = importlib.util.module_from_spec(spec)
spec.loader.exec_module(lr)

EXPECTED = {
    "packageSha256": "943873f26af843c6cb03b9bb0a449c06fb90ae9c26ec4de23d3f6aab1375d0b4",
    "packageSize": 41278785,
    "packageCrc32": 3454076078,
    "fileVersion": 3,
    "contentVersion": 16,
    "entryCount": 18734,
    "xluaSha256": "21eb704afdb7e528f4b90fa1b90bf414c221b06ba990d625aaaaed31b292740f",
    "assemblyCSharpSha256": "871efe06819fbac438413eb96b7df8193d0be56094f3a44d5ff141e6219adcbd",
}
CRITICAL_ENTRIES = {
    "DataCenter/Global/LuaEntry.luac": "50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137",
    "Global/ConstDefine.luac": "95e6c733b98dc641c330f36efdef044845033b37ea6703068f7c7ed5fb0048fd",
    "Util/CSharpCallLuaInterface.luac": "af1559723afba0fa5773bb815c486f3233fecb3928768abd082a96b51960229e",
    "UI/LWMainUI/Component/UIMainBottom/UIMainChangeScene.luac": "3843dc02869330f060a199b946ef3cd30aa335504914c2f4c60f8a824e871c5b",
}


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def relevant_launcher_lines(log_path: Path) -> list[str]:
    if not log_path.is_file():
        return []
    lines = log_path.read_text(encoding="utf-8", errors="replace").splitlines()
    needles = (
        "LWScripts_16_14_u440.patch",
        "Applied LWLuaFile update: Some(AppliedLwLuaUpdate { version: 16",
        "Starting game at:",
    )
    return [line for line in lines if any(needle in line for needle in needles)][-12:]
def main() -> int:
    p = lr.paths()
    file_version, content_version, entries = lr.read_lwlf(p["data"])
    mapped = dict(entries)
    observed = {
        "packageSha256": lr.sha256_file(p["data"]),
        "packageSize": p["data"].stat().st_size,
        "packageCrc32": lr.crc32_file(p["data"]),
        "fileVersion": file_version,
        "contentVersion": content_version,
        "entryCount": len(entries),
        "xluaSha256": lr.sha256_file(p["xlua"]),
        "assemblyCSharpSha256": lr.sha256_file(p["assembly"]),
        "metadata": p["metadata"].read_text(encoding="utf-8-sig").strip(),
        "version": p["version"].read_text(encoding="utf-8-sig").strip(),
    }
    entry_hashes = {
        name: sha256_bytes(mapped[name]) if name in mapped else None
        for name in CRITICAL_ENTRIES
    }
    checks = {key: observed.get(key) == value for key, value in EXPECTED.items()}
    checks["metadata"] = observed["metadata"] == "41278785|3454076078"
    checks["version"] = observed["version"] == "16"
    checks["criticalEntries"] = entry_hashes == CRITICAL_ENTRIES
    launcher_log = p["launcher"].with_name("Launcher.log")
    result = {
        "ok": all(checks.values()),
        "checks": checks,
        "observed": observed,
        "criticalEntries": entry_hashes,
        "launcherEvidence": relevant_launcher_lines(launcher_log),
    }
    print(json.dumps(result, indent=2, sort_keys=True))
    return 0 if result["ok"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
