"""Offline MAP-016 remote world.get.block feasibility audit (no game access).

Inspect retained MAP-013 metadata snapshots and the independently decoded managed
IL. This script deliberately never reads game process memory, network packets,
credentials or native installed files. It reports counts and contract failures,
not the original packet contents or player identities.
"""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
IL = ROOT / "artifacts/map014-il-decode/il_WorldPointManager_reply.txt"
SAMPLER = ROOT / "tools/map_009_offline/managed_sampler/Sampler.cs"
ARCHIVE = ROOT / "artifacts/map-013-live"


def inspect_contract(il_text: str, sampler_text: str) -> dict:
    """Tie the foreign-server early return to the actual protobuf wire API."""
    marker = re.search(r"^##### .*WorldPointManager::ParseWorldGetBlock\(Sfs2X\.Entities\.Data\.ISFSObject\).*$",
                       il_text, flags=re.MULTILINE)
    if marker is None:
        raise ValueError("decoded ParseWorldGetBlock method is unavailable")
    method = il_text[marker.end():].split("#####", 1)[0]
    # Pin actual IL offsets to avoid accidentally matching another parser or
    # its final return after a future source change.
    expected = {
        0x0022: "ldstr 'serverId'",
        0x005e: "ret",
        0x0060: "ldstr 'worldId'",
        0x006c: "ldstr 'points'",
        0x0087: "callvirt [MemberRef] Sfs2X.Entities.Data.ISFSArray::GetByteArray",
        0x0091: "callvirt [MemberRef] Google.Protobuf.MessageParser`1<Protobuf.WorldPointInfo>::ParseFrom",
    }
    for offset, op in expected.items():
        if f"IL_{offset:04x}: {op}" not in method:
            raise ValueError(f"unexpected native IL at 0x{offset:04x}: {op}")
    get_server, reject_foreign, get_world, get_points, get_bytes, parse_point = expected
    if not (get_server < reject_foreign < get_world < get_points < get_bytes < parse_point):
        raise ValueError("foreign-server guard is not before native protobuf parsing")
    for token in ("WorldScene::IsSingleServerMode", "DCPlayer::GetCurServerId",
                  "Protobuf.WorldPointInfo::get_Parser", "ISFSArray::GetByteArray"):
        if token not in method:
            raise ValueError("incomplete native IL guard/decoder contract: " + token)
    if ("GetSFSObject\", new[] { typeof(int) }" not in sampler_text or
            'At(points, p)' not in sampler_text or
            'Math.Min(pn, 3)' not in sampler_text or
            'GetByteArray' in sampler_text):
        raise ValueError("sampler no longer matches its metadata-only SFS-object point reader")
    if "_receiveData" not in sampler_text or "packet.GetHashCode()" not in sampler_text:
        raise ValueError("the original managed packet observation contract changed")
    return {
        "singleServerGuardBeforeProtobuf": True,
        "nativePointElementType": "SFS byte array containing Protobuf.WorldPointInfo",
        "samplerPointElementType": "SFS object (incorrect for native point bytes)",
        "samplerMaximumPointShapesPerSegment": 3,
        "samplerEmitsOriginalProtobufBytes": False,
        "packetHashIsRequestCorrelation": False,
    }


def summarize_snapshots(snapshots: list, current_server: int | None) -> dict:
    """Classify only *received* world.get.block segments and their metadata."""
    summary = dict(snapshots=0, replyRows=0, uniquePacketHashes=0, segments=0,
                   sameServerSegments=0, foreignServerSegments=0,
                   unknownServerSegments=0, pointCountReported=0,
                   observedProtobufByteArrays=0, truncatedObservations=0,
                   pointShapeSamples=0, nonemptyPointShapes=0)
    seen = set()
    for snapshot in snapshots:
        envelope = json.loads(snapshot["json"])
        summary["snapshots"] += 1
        if (envelope.get("receiveEnumerationTruncated") or
                envelope.get("packetRowsTruncated") or envelope.get("errorType")):
            summary["truncatedObservations"] += 1
        for packet in envelope.get("packets", []):
            if packet.get("direction") != "receive" or packet.get("command") != "world.get.block":
                continue
            summary["replyRows"] += 1
            if "packetHash" in packet:
                seen.add(packet["packetHash"])
            segment_set = packet.get("segments") or {}
            if segment_set.get("truncated"):
                summary["truncatedObservations"] += 1
            for segment in segment_set.get("entries", []):
                summary["segments"] += 1
                server = (segment.get("numbers") or {}).get("serverId")
                if type(server) is not int or type(current_server) is not int:
                    summary["unknownServerSegments"] += 1
                elif server != current_server:
                    summary["foreignServerSegments"] += 1
                else:
                    summary["sameServerSegments"] += 1
                point_count = (segment.get("arrayCounts") or {}).get("points")
                if type(point_count) is int and point_count >= 0:
                    summary["pointCountReported"] += point_count
                shapes = segment.get("pointShapes") or []
                summary["pointShapeSamples"] += len(shapes)
                summary["nonemptyPointShapes"] += sum(
                    bool(shape.get("keys")) or any(
                        value is not None for value in (shape.get("mapNumbers") or {}).values())
                    for shape in shapes)
                # No SFS wire buffers are serialized by Sampler.Sample. A
                # future exporter could add bytes: count them, but do not
                # confuse arbitrary new data with a validated decoder.
                if "protobufBytes" in segment or "pointByteArrays" in segment:
                    summary["observedProtobufByteArrays"] += 1
    summary["uniquePacketHashes"] = len(seen)
    return summary


def audit_archive(paths: list[Path]) -> dict:
    totals = dict(traces=0, tracesWithKnownCurrentServer=0)
    counters: dict[str, int] = {}
    for path in paths:
        data = json.loads(path.read_text(encoding="utf-8"))
        snapshots = data.get("managedPacketSnapshots") or []
        if not isinstance(snapshots, list):
            raise ValueError("malformed retained managed packet snapshots")
        world_path = path.parents[1] / "world-status.json"
        current = None
        if world_path.is_file():
            world = json.loads(world_path.read_text(encoding="utf-8"))
            if world.get("state") == "proven" and type(world.get("serverId")) is int:
                current = world["serverId"]
                totals["tracesWithKnownCurrentServer"] += 1
        summary = summarize_snapshots(snapshots, current)
        totals["traces"] += 1
        for name, value in summary.items():
            counters[name] = counters.get(name, 0) + value
    return {**totals, **counters}


def evaluate(contract: dict, observations: dict) -> dict:
    missing = []
    if observations["foreignServerSegments"] == 0:
        missing.append("no attributed off-server inbound segment in retained snapshots")
    if not observations["observedProtobufByteArrays"]:
        missing.append("no original SFS point-element byte arrays for typed protobuf decode")
    if not observations["tracesWithKnownCurrentServer"]:
        missing.append("no independently attributed live client-server context")
    # The old profiler does not retain an exact network request ID, separate
    # typed march pushes, or remote detail hydration. These are unconditional
    # outstanding capability gates even if a future sample captures raw bytes.
    missing.extend((
        "no exact per-request fresh reply correlation and requested AOI/bounds ledger",
        "no independently qualified 10,000-AOI target-server coverage or object fields",
        "no off-server typed march/Railway push and detail-source lifecycle proof",
    ))
    return {"productionRemoteProducerQualified": False, "missing": missing,
            "safeDecision": "retain guarded server-switch, fast-scan, return"}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", type=Path, default=ARCHIVE,
                        help="retained MAP-013 traces; entirely offline")
    args = parser.parse_args()
    if not args.archive.is_dir():
        parser.error("offline MAP-013 archive is unavailable")
    paths = sorted(args.archive.glob("native-causal-*/live-resource/map013-native-trace-result.json"))
    if not paths:
        parser.error("no genuine retained MAP-013 metadata traces available")
    contract = inspect_contract(IL.read_text(encoding="utf-8"),
                                SAMPLER.read_text(encoding="utf-8"))
    observation = audit_archive(paths)
    print(json.dumps({"source": "archived metadata and recovered IL only",
                      "contract": contract, "observations": observation,
                      "decision": evaluate(contract, observation)}, indent=2))


if __name__ == "__main__":
    main()
