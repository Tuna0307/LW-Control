"""Read-only, fail-closed actual-game adapter/host witness audit for 003."""
from __future__ import annotations

import argparse
import importlib.util
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
SRC = HERE / "audit_background_witness_002.py"
spec = importlib.util.spec_from_file_location("witness002audit", SRC)
assert spec and spec.loader
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

STAGES = (
    "begin_connect_entry", "native_startup", "worker_created",
    "worker_start_called", "worker_entry", "wait_named_pipe_success",
    "native_pipe_opened", "hello_frame_written", "worker_connected",
)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence_root", type=Path)
    args = parser.parse_args()
    base = args.evidence_root.resolve()
    reports = [
        base / "diagnostic-attempt-001.json",
        base / "retest-attempt-002.json",
        base / "rpc-retest-attempt-003.json",
    ]
    audit = [module.inspect(path) for path in reports]
    details = []
    for idx, (report, archive) in enumerate(zip(reports, audit), start=1):
        raw = json.loads(report.read_text(encoding="utf-8-sig"))
        receipts = base / ("receipts-%03d" % idx)
        lua = json.loads(
            (receipts / "pipe-invocation-receipt.json").read_text(encoding="utf-8-sig")
        )
        expected_route = "reflection" if idx == 1 else "delegate_direct_once"
        assert lua["route"] == expected_route
        assert lua["delegateValueType"] == "userdata"
        assert lua["sessionId"] in {
            event["details"]["instance"] for event in raw["events"]
            if event["kind"] == "real-home-start-returned"
        }
        assert lua["gamePid"] in {
            event["details"]["ownedPid"] for event in raw["events"]
            if event["kind"] == "real-home-start-returned"
        }
        assert archive["restorationVerified"]
        pipe = json.loads((receipts / "pipe-transport.json").read_text())
        assert pipe["clientConnected"] is (idx != 1)
        assert pipe["state"] == ("hello_sent" if idx == 1 else "connected")
        assert archive["hostAccepted"] is (idx != 1)
        assert archive["freshNativeResourceStartEvidence"] is False
        assert archive["isolatedSqliteCounts"] == {
            "scanRuns": 0, "scanBlocks": 0,
            "resourceScanRecords": 0, "resourcePublishedRows": 0,
        }
        native_file = receipts / "pipe-native-receipts.txt"
        stages = []
        if native_file.exists():
            for line in native_file.read_text(encoding="utf-8-sig").splitlines():
                fields = dict(part.split("=", 1) for part in line.split("|")[1:] if "=" in part)
                assert fields["session"] == lua["sessionId"]
                assert int(fields["pid"]) == lua["gamePid"]
                assert "token" not in line.lower()
                stages.append(fields["stage"])
        if idx == 1:
            assert not stages
        if idx == 3:
            assert all(stage in stages for stage in STAGES)
            indexes = [stages.index(stage) for stage in STAGES]
            # Worker entry may race with the parent recording worker_start_called,
            # but the native opening/writing sequence must be ordered.
            assert indexes[0] < indexes[1] < indexes[2]
            assert indexes[4] < indexes[5] < indexes[6] < indexes[7] < indexes[8]
        rpc = [e for e in raw["events"]
               if e["kind"] == "production-authenticated-getStatus-rpc"]
        if idx == 3:
            assert len(rpc) == 1
            assert rpc[0]["details"]["resultWasNull"] is False
            assert rpc[0]["details"]["resultKind"] == "Object"
            assert not any(e["kind"] == "production-getStatus-rpc-failed"
                           for e in raw["events"])
        world = [
            e["details"]["context"] for e in raw["events"]
            if e["kind"] == "production-map-context-rpc-attempt"
        ]
        if idx >= 2:
            assert len(world) == 1
            assert world[0]["serverId"] > 0
            assert world[0]["isInWorld"] is False
        details.append({
            "attempt": idx,
            "route": lua["route"],
            "delegateValueType": lua["delegateValueType"],
            "nativeStages": stages,
            "authenticated": archive["hostAccepted"],
            "authenticatedSessions": archive["hostAuthenticatedCount"],
            "connectedRoutes": archive["hostConnectedRoutes"],
            "genuineGetStatusRpc": bool(rpc),
            "world": world[0] if world else None,
            "restorationVerified": archive["restorationVerified"],
            "sqliteRows": archive["isolatedSqliteCounts"],
            "resourceStarted": archive["freshNativeResourceStartEvidence"],
        })
    result = {
        "workItem": "LWB317-FUNCTION-HOME-MAP-BACKGROUND-PIPE-CONNECT-003",
        "allThreeRestored": all(x["restorationVerified"] for x in audit),
        "oldReflectionUnconnected": not audit[0]["hostAccepted"],
        "directDelegateHostAuthenticated": audit[1]["hostAccepted"] and audit[2]["hostAccepted"],
        "genuineFinalGetStatusRpc": details[2]["genuineGetStatusRpc"],
        "resourceBlockedByWorldReadiness": details[2]["world"]["isInWorld"] is False,
        "canonicalUiProof": False,
        "attempts": details,
    }
    dest = base / "read-only-audit.json"
    dest.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({k:v for k,v in result.items() if k != "attempts"}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
