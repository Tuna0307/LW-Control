"""Read-only structural compatibility report for the current Last War client.

This tool never approves a binary identity and never edits the compatibility
allowlist. It supplements current_client_compat.py with the recovered runtime
contracts LWBridge actually depends on, so a changed build can be assessed
quickly and reproducibly.
"""
from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import subprocess
import sys

HERE = Path(__file__).resolve().parent
BASE = HERE / "run_live_resource_probe.py"
COMPAT = HERE / "current_client_compat.py"
RDL_INSPECT = HERE / "inspect_lastwar_rdl_metadata.py"


def load(path: Path, name: str):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"could not load {path.name}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def inspect_type(assembly: Path, type_name: str) -> str:
    completed = subprocess.run(
        [sys.executable, str(RDL_INSPECT), str(assembly), "--type", type_name],
        capture_output=True,
        text=True,
        check=False,
    )
    if completed.returncode != 0:
        raise RuntimeError(
            f"RDL inspection failed for {type_name}: "
            f"{completed.stderr.strip() or completed.stdout.strip()}"
        )
    return completed.stdout


def missing_contracts(text: str, required: list[str]) -> list[str]:
    return [value for value in required if value not in text]


def main() -> int:
    lr = load(BASE, "lwbridge_contract_base")
    compat = load(COMPAT, "lwbridge_contract_compat")
    p = lr.paths()
    identity = compat.inspect_current(lr, p)
    observed = dict(identity.get("observed", {}))

    manager = inspect_type(p["assembly"], "WorldPointManager")
    block = inspect_type(p["assembly"], "WorldGetBlockMessage")

    manager_required = [
        "int32 once_max_request_count = 160",
        "bool _splitLastAOIRequest",
        "int32 _lwAoiBlockSize",
        "int32 _lwAoiBlockCount",
        "System.Collections.Generic.HashSet`1<int32> _msgViewIndex",
        "System.Collections.Generic.List`1<int32> _addViewIndex",
        "System.Collections.Generic.HashSet`1<int32> _curViewIndex",
        "void UpdateLWAoi_Normal(bool isForce)",
        "void SendAoiRequest(int32 bigMap, int32 serverLod, int32 x, int32 y, System.Collections.Generic.List`1<int32> addIndex, int32 blockSize, int32 lbTileIndex, int32 rtTileIndex)",
        "void SendViewRequest(UnityEngine.Vector2Int tilePos, int32 viewLevel, int32 serverId)",
        "HeroDispatchMissionPointInfo GetHeroDispatchTaskPointInfoByIndex(int32 pointIndex)",
        "System.Collections.Generic.List`1<BuildPointInfo> GetAllMainBaseListByType(PlayerType t0)",
    ]
    block_required = [
        "int32 x",
        "int32 y",
        "int32 serverId",
        "int32 worldId",
        "int32 type",
        "int32 lod",
        "int32[] index",
        "int32 blockSize",
        "int32 leftBottom",
        "int32 rightTop",
    ]

    missing_manager = missing_contracts(manager, manager_required)
    missing_block = missing_contracts(block, block_required)

    critical_observed = observed.get("criticalEntries", {})
    critical_lua_ok = isinstance(critical_observed, dict) and all(
        critical_observed.get(name) == expected
        for name, expected in compat.CRITICAL_ENTRIES.items()
    )
    internal_package_ok = (
        observed.get("fileVersion") == compat.EXPECTED_FILE_VERSION
        and observed.get("contentVersion", 0) > 0
        and observed.get("versionMarkerMatchesContentVersion") is True
    )

    report = {
        "ok": not missing_manager and not missing_block
              and critical_lua_ok and internal_package_ok,
        "purpose": "read-only structural evidence; not binary approval",
        "compatibilityPolicy": compat.POLICY,
        "binaryIdentityApproved": identity.get("ok") is True,
        "binaryIdentityProblems": identity.get("problems", []),
        "observed": observed,
        "criticalLuaAnchorsOk": critical_lua_ok,
        "internalLuaPackageOk": internal_package_ok,
        "worldPointManagerContractOk": not missing_manager,
        "worldGetBlockContractOk": not missing_block,
        "missingWorldPointManagerContracts": missing_manager,
        "missingWorldGetBlockContracts": missing_block,
    }
    print(json.dumps(report, indent=2, sort_keys=True))
    return 0 if report["ok"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
