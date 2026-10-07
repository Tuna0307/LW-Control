#!/usr/bin/env python3
"""Hash-gated checkpoint-D audit for original Lua controller boundaries.

Static only: fixed reference EXE, retained 0.3.17 native/frontend evidence,
carved encrypted resources, and current product source. No executable launch,
runtime key access, auth, transport, or decryption.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

import pefile

ROOT = Path(__file__).resolve().parents[2]
REFERENCE = Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
REFERENCE_SHA = "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
DISCOVERY = ROOT / "evidence/lwbridge-0.3.17/map/action-handler-discovery.json"
DISCOVERY_SHA = "179383662a142f71d8b57e2e8318886dddfabdf2bd75c39eb6b9c088b9934dcc"
STATUS_AUDIT = ROOT / "evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-MAP-BLOCKER-BOUNDARIES-004/a-status-audit.json"
STATUS_AUDIT_SHA = "5f42f690e784adfe67dc64a26189e1f5a260e659aada6caef9d380df1bb8eb6d"
EXTRACTED = ROOT / "evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-ORIGINAL-LUA-RECOVERY-006/c-extracted"
CURRENT_PROVIDER = ROOT / "src/LWBridge.Desktop/CurrentClientMap317ActionProvider.cs"
CURRENT_PROVIDER_SHA = "f90a62baa9a189344fea0bfce769d4ab45215f29aea1e2be379605d81c1cd3b8"
CONTROL = ROOT / "src/LWBridge.Map-0.3.17/MapActionControlPlane.cs"
CONTROL_SHA = "e30cc96d5bf1f124cf93590a46e6a7a01ea1b49b9e1efd58899a5888d21146e7"

FUNCTION_HASHES = {
    "statusPublic": (0x115C5D, 0x1166BE, "76a871144538664d684ef2cbf9a693aa03a81864885d0479a49e87e5e537eb1c"),
    "treasureService": (0xDD807, 0xDE902, "c013598b3325323b00e294d23eb76c9d37ce159b979a5786b3911500db337f95"),
    "claimPublic": (0x18EE07, 0x190734, "ed532af562b54969df287264ad2417e978b60e2845e3fb278649bdbeb00987b6"),
    "claimCandidates": (0x3D68CE, 0x3D70AB, "caa98c4d7a2cc6038f02a48783230d1cd64be696c4704bbbefb15c5df37e0f13"),
    "ghostSchedule": (0x1336A9, 0x1350DF, "e203da25841cddd71eb5a0548c9167a0223f720a76916c81fe1a99ac4ccba1a4"),
    "providerBridge": (0xE5095, 0xE5466, "472954c3d3852a6174c9be3f39ed494aa268d834138579bca4fd1e3e501d8e04"),
}

CONTROLLERS = (
    "getTreasureClaimStatus",
    "claimTreasures",
    "prepareGhostPlunderTasks",
)


class AuditError(RuntimeError):
    pass


def require(cond: bool, message: str) -> None:
    if not cond:
        raise AuditError(message)


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def file_sha(path: Path) -> str:
    return sha(path.read_bytes())


def handler(discovery: dict[str, Any], marker: str, fn_range: str) -> dict[str, Any]:
    for item in discovery["markers"][marker]["handlerCandidates"]:
        if item["functionRva"] == fn_range:
            return item
    raise AuditError(f"handler missing: {marker} {fn_range}")


def instruction(item: dict[str, Any], rva: str) -> dict[str, Any]:
    for ins in item["instructions"]:
        if ins["rva"].lower() == rva.lower():
            return ins
    raise AuditError(f"instruction missing: {rva}")


def ref_text(item: dict[str, Any], rva: str) -> list[str]:
    return [x["text"] for x in instruction(item, rva).get("stringRefs", [])]


def inspect() -> dict[str, Any]:
    host = REFERENCE.read_bytes()
    require(sha(host) == REFERENCE_SHA, "reference EXE hash changed")
    require(file_sha(DISCOVERY) == DISCOVERY_SHA, "action discovery hash changed")
    require(file_sha(STATUS_AUDIT) == STATUS_AUDIT_SHA, "status audit hash changed")
    require(file_sha(CURRENT_PROVIDER) == CURRENT_PROVIDER_SHA, "current provider source changed")
    require(file_sha(CONTROL) == CONTROL_SHA, "control-plane source changed")

    pe = pefile.PE(data=host, fast_load=False)
    body_hashes: dict[str, str] = {}
    for name, (start, end, expected) in FUNCTION_HASHES.items():
        off = pe.get_offset_from_rva(start)
        actual = sha(host[off : off + end - start])
        require(actual == expected, f"{name} native body changed")
        body_hashes[name] = actual

    discovery = json.loads(DISCOVERY.read_text(encoding="utf-8"))
    status = handler(discovery, "map_treasure_claim_status", "0x115C5D-0x1166BE")
    claim = handler(discovery, "map_treasure_claim", "0x18EE07-0x190734")
    ghost = handler(discovery, "map_dispatch_plunder_schedule", "0x1336A9-0x1350DF")

    require(ref_text(status, "0x1160CD") == ["getTreasureClaimStatus"], "status provider ref changed")
    require("0x1388" in instruction(status, "0x1160E6")["opStr"].lower(), "status timeout changed")
    require(ref_text(claim, "0x18FCDA") == ["claimTreasures"], "claim provider ref changed")
    require("0x1388" in instruction(claim, "0x18FCF3")["opStr"].lower(), "claim timeout changed")
    ghost_ref = ref_text(ghost, "0x133CA8")
    require(ghost_ref and ghost_ref[0].startswith("prepareGhostPlunderTasks"), "ghost preparer ref changed")
    require("0x1388" in instruction(ghost, "0x133CC1")["opStr"].lower(), "ghost prepare timeout changed")
    require(ref_text(ghost, "0x133F57") == ["rows"], "ghost prepared rows extraction changed")
    require(any("ownerServer" in x for x in ref_text(ghost, "0x1342E2")), "ghost ownerServer check changed")

    # Exact claim payload keys are constructed immediately before provider invocation.
    # Four are compiler-emitted immediates; prioritizeLuckySlots is a literal reference.
    claim_key_ops = {
        "serverId": ("0x18F9BE", "0x6449726576726573"),
        "recordsLo": ("0x18FA49", "0x6f636572"),
        "recordsHi": ("0x18FA42", "0x7364726f"),
        "claimScope": ("0x18FAE5", "0x6f63536d69616c63"),
        "targetUuid": ("0x18FB86", "0x7555746567726174"),
    }
    for label, (rva, operand) in claim_key_ops.items():
        require(operand in instruction(claim, rva)["opStr"].lower(),
                f"claim provider key encoding changed: {label}")
    require(any("prioritizeLuckySlots" in text for text in ref_text(claim, "0x18FC2B")),
            "claim provider key missing: prioritizeLuckySlots")
    claim_string_blob = "\n".join(
        ref["text"] for ins in claim["instructions"] for ref in ins.get("stringRefs", [])
    )
    for key in (
        "eligible", "queued", "skipped", "directQueued", "scoutQueued",
        "scoutDispatched", "claimed", "noScoutSkipped", "otherAllianceSkipped", "failed",
    ):
        require(key in claim_string_blob, f"claim result counter missing: {key}")

    # Host candidate selection is owned outside the encrypted controller.
    sql = (
        "SELECT data_json FROM map_records\n"
        "                 WHERE kind='treasure' AND server_id=?1"
    ).encode()
    sql_at = host.find(sql)
    require(sql_at >= 0, "treasure candidate SQL missing")
    sql_tail = host[sql_at : sql_at + 1500]
    for clause in (
        b"suppliesType') AS INTEGER),0) IN (1,3,4)",
        b"suppliesType') AS INTEGER),0)=0",
        b"complete') AS INTEGER),0)=1",
        b"uuid IS NOT NULL AND TRIM(uuid)<>'' AND uuid<>'0'",
        b"expireTime') AS INTEGER)>?2",
        b"ORDER BY point_index ASC",
    ):
        require(clause in sql_tail, f"claim candidate SQL clause missing: {clause!r}")

    status_meta = json.loads(STATUS_AUDIT.read_text(encoding="utf-8"))
    require(status_meta["status"]["requiredIdleFields"] == ["playerUid", "allianceId", "states"],
            "status idle fields changed")
    require(status_meta["status"]["optionalObservedField"] == "batch", "status batch observation changed")
    require(status_meta["ownership"]["pollProfilePinned"] is False, "poll profile pinning changed")
    require(status_meta["ownership"]["frontendInvocationProfile"].startswith("currently selected profileId"),
            "frontend profile injection classification changed")

    carved_presence: dict[str, dict[str, list[int]]] = {}
    for name in ("bridge-scripts.dat", "xlua-proxy-secure.dll", "xlua-proxy-plain.dll"):
        data = (EXTRACTED / name).read_bytes()
        carved_presence[name] = {}
        for controller in CONTROLLERS:
            hits: list[int] = []
            pos = 0
            needle = controller.encode()
            while True:
                found = data.find(needle, pos)
                if found < 0:
                    break
                hits.append(found)
                pos = found + 1
            carved_presence[name][controller] = hits
            require(not hits, f"unexpected plaintext controller name in {name}: {controller}")

    secure = (EXTRACTED / "xlua-proxy-secure.dll").read_bytes()
    for marker in (
        b"XluaBridgeHandlePipeMessage",
        b"__XluaBridgeLoad",
        b"__XLUA_BRIDGE_PROFILE_ID",
        b"@bridge-scripts.dat",
    ):
        require(secure.find(marker) >= 0, f"secure proxy loader marker missing: {marker!r}")

    provider_src = CURRENT_PROVIDER.read_text(encoding="utf-8")
    control_src = CONTROL.read_text(encoding="utf-8")
    for phrase in (
        'ProviderUnavailable("current-client Treasure claim status provider is unavailable")',
        'ProviderUnavailable("current-client Treasure claim provider is unavailable")',
        "prepareGhostPlunderTasks row transformation/rejection semantics are incomplete",
        "downstream Ghost execution terminal-result correlation is separately unresolved",
    ):
        require(phrase in provider_src, f"provider fence changed: {phrase}")
    require("taskExpireTime > 0 && plunderAt >= taskExpireTime" in provider_src,
            "conditional Ghost expiry guard changed")
    # Retained 005 discrepancy: TryGetInt64 executes before the later string branch.
    read_fn = provider_src.index("private static long? ReadNullableInteger")
    read_tail = provider_src[read_fn : read_fn + 1000]
    require(read_tail.index("value.TryGetInt64") < read_tail.index("value.ValueKind == JsonValueKind.String"),
            "numeric-string reader ordering changed")
    require('provider.PrepareGhostPlunderTasksAsync(ghostInput' in control_src,
            "control-plane Ghost preparation boundary changed")
    require('new TreasureClaimProviderRequest(serverId, records, claimScope, targetUuid, prioritizeLuckySlots)' in control_src,
            "control-plane claim request shape changed")

    return {
        "ok": True,
        "findingId": "LWB317-FUNCTION-ORIGINAL-LUA-RECOVERY-006-D",
        "referenceSha256": REFERENCE_SHA,
        "nativeBodyHashes": body_hashes,
        "plaintextControllerNameSearch": carved_presence,
        "bodyRecovery": {
            "getTreasureClaimStatus": "NOT_RECOVERED_FROM_SUPPLIED_INPUTS",
            "claimTreasures": "NOT_RECOVERED_FROM_SUPPLIED_INPUTS",
            "prepareGhostPlunderTasks": "NOT_RECOVERED_FROM_SUPPLIED_INPUTS",
            "reason": (
                "the exact original module-table plaintext cannot be produced without a legitimate "
                "signed package-key.envelope and matching persisted CNG private key; no duplicate "
                "plaintext controller body exists in the carved package/proxies"
            ),
        },
        "hostRecovered": {
            "treasureStatus": {
                "handler": "0x115C5D-0x1166BE",
                "provider": "getTreasureClaimStatus",
                "timeoutMs": 5000,
                "featurePayload": "none at provider method boundary",
                "resultFieldsObserved": ["playerUid", "allianceId", "states", "batch?"],
                "profileScope": "frontend injects currently selected profileId on each call; poll is not pinned",
                "persistentState": (
                    "profile-owned map DB has treasure_claim_states keyed by "
                    "(server_id,player_uid,treasure_uuid); batch/controller lifecycle is not stored in native schema"
                ),
            },
            "treasureClaim": {
                "handler": "0x18EE07-0x190734",
                "provider": "claimTreasures",
                "timeoutMs": 5000,
                "payloadKeys": ["serverId", "records", "claimScope", "targetUuid", "prioritizeLuckySlots"],
                "scopes": ["single", "boxes", "season"],
                "luckyDefault": True,
                "candidateOrdering": "host persisted candidate query ORDER BY point_index ASC",
                "candidateFilter": (
                    "nonexpired valid UUID rows; suppliesType 1/3/4 or complete ordinary suppliesType 0"
                ),
                "resultCounters": [
                    "eligible", "queued", "skipped", "directQueued", "scoutQueued",
                    "scoutDispatched", "claimed", "noScoutSkipped",
                    "otherAllianceSkipped", "failed",
                ],
            },
            "ghostPreparation": {
                "handler": "0x1336A9-0x1350DF",
                "provider": "prepareGhostPlunderTasks",
                "timeoutMs": 5000,
                "payload": {"rows": "validated Ghost input rows"},
                "result": {"rows": "required array"},
                "postconditions": [
                    "prepared rows are identity-matched instead of blindly persisted by provider position",
                    "uuid and serverId identity are checked",
                    "taskKind must remain ghost",
                    "positive ownerServer required",
                    "completionTime positive and plunderAt >= completionTime",
                    "taskExpireTime may be nonpositive; positive value must be > plunderAt",
                    "remaining steal capacity is enforced when maxStealCount is positive",
                ],
            },
        },
        "controllerOwnedStillMissing": {
            "treasureStatus": [
                "where/how idle states and optional batch are stored or recomputed inside the Lua controller",
                "batch creation/default state/counter transitions",
                "terminal/error/timeout/reconnect/profile-switch retirement/reset semantics",
            ],
            "treasureClaim": [
                "controller record selection among already host-selected candidates",
                "prioritizeLuckySlots ordering algorithm",
                "direct claim vs scout decision/dispatch ordering",
                "batch ownership/counter transition meanings and duplicate/retry behavior",
            ],
            "ghostPreparation": [
                "whether rows are re-read/hydrated from game state",
                "exact transformation/additional fields",
                "exact per-row rejection policy and any output reordering",
            ],
        },
        "cloneDifferences": {
            "publicStatus": "current production returns GAME_PROVIDER_UNAVAILABLE; original calls controller",
            "publicClaim": "current production returns GAME_PROVIDER_UNAVAILABLE; original calls controller",
            "publicGhostPreparation": "current production returns GAME_PROVIDER_UNAVAILABLE; original calls controller",
            "ghostPreparationHelper": (
                "internal current-v22 adaptation exists but is not promoted as original; 005-retained "
                "numeric/malformed string reader discrepancy remains"
            ),
        },
        "dependency": {
            "missingArtifact": "package-key.envelope",
            "requiredOwnerState": "matching persisted CNG private key",
            "bypassAttempted": False,
            "futureStaticClosure": (
                "legitimate recovered module-table plaintext containing the bridge Lua dispatcher/provider "
                "definitions for the three methods"
            ),
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        result = inspect()
    except (OSError, AuditError, pefile.PEFormatError, json.JSONDecodeError) as exc:
        parser.error(str(exc))
    encoded = json.dumps(result, indent=2) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded, encoding="utf-8")
    else:
        print(encoded, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
