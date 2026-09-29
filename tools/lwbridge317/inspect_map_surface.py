#!/usr/bin/env python3
"""Inspect LWBridge 0.3.17 Map frontend commands and native string xrefs.

This helper is intentionally read-only.  It never loads or executes the target
binary.  It hash-locks the exact 0.3.17 reference and recovered frontend assets,
extracts the Map-related Tauri wrapper surface from the recovered JavaScript,
then locates matching ASCII strings / Rust ``&str`` descriptors and code xrefs
inside the PE.

The output is a discovery manifest, not a claim that every string xref is a
complete handler trace.  Follow-up inspectors should verify each native branch
before it is promoted to an EXACT_CONTRACT finding.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
from typing import Any, Iterable

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP


REFERENCE_SHA256 = "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
INDEX_SHA256 = "44c4e4043825b7db296b64171951b27176f8850ff8d7d337cc991df4765524c6"
MAP_PANEL_SHA256 = "ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089"

MAP_COMMAND_PREFIXES = (
    "map_",
    "server_jump",
)

MAP_EVENT_MARKERS = (
    "bridge://map-scan-status",
    "bridge://player-mark-changed",
    "bridge://dispatch-plunder-changed",
    "bridge://truck-plunder-changed",
)

NATIVE_DISCOVERY_MARKERS = (
    "src\\commands\\map.rs",
    "src\\services\\map_store.rs",
    "src\\services\\map_scan.rs",
    "src\\services\\map_scan",
    "map.scan",
    "map.records",
    "map.native.capture",
    "map_records",
    "scan_records",
    "player_marks",
    "server_jump_history",
    "map_player_mark",
    "SCAN_RUNNING",
    "map scan already running",
    "GAME_CONNECTION_UNAVAILABLE",
    "game connection unavailable",
    "SERVER_UNAVAILABLE",
    "stop the map scan first",
    "current server id unavailable",
    "resumeAvailable",
    "selectedTypes",
    "nativeCaptureReady",
    "nativePendingRecords",
    "nativeDroppedRecords",
    "alliance_name = ?",
    "(alliance_name IS NULL OR alliance_name = '')",
    "CAST(json_extract(data_json,'$.resourceNameKey') AS TEXT) = ?",
    "CAST(json_extract(data_json,'$.monsterNameKey') AS TEXT) = ?",
    "itemKey EXISTS (SELECT 1 FROM json_each(",
    "CAST(json_extract(good.value,'$.key') AS TEXT) = ?",
    "enterWorldMap",
    "startMapScan",
    "stopMapScan",
    "XluaBridgeNativeStart",
    "__XLUA_BRIDGE_NATIVE_WORLD_CAPTURE_RUN",
    "__XluaBridgeNativeWorldCapture",
    "XluaBridgeMapScanTick",
    "XluaBridgeNativeUpdate",
    "XluaBridgePoll",
    "native world capture session stopped",
    "direct map scan completed",
)

FRONTEND_LOCATOR_MARKERS = (
    "lwbridge.mapScanMode",
    "lwbridge.mapIncludeForeignRadarTreasures",
    "lwbridge.mapLuckyTreasurePriority",
    "lwbridge.mapAutoScan.",
    "function nr(",
    "async function $n()",
    "async function er()",
    "async function tr()",
    "bridge://player-mark-changed",
    "bridge://dispatch-plunder-changed",
    "bridge://truck-plunder-changed",
)


class InspectError(ValueError):
    pass


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def all_offsets(data: bytes, needle: bytes) -> list[int]:
    result: list[int] = []
    start = 0
    while True:
        hit = data.find(needle, start)
        if hit < 0:
            return result
        result.append(hit)
        start = hit + 1


def raw_to_rva(pe: pefile.PE, raw: int) -> int | None:
    for section in pe.sections:
        start = int(section.PointerToRawData)
        end = start + int(section.SizeOfRawData)
        if start <= raw < end:
            return int(section.VirtualAddress) + raw - start
    if raw < int(pe.OPTIONAL_HEADER.SizeOfHeaders):
        return raw
    return None


def runtime_functions(pe: pefile.PE) -> list[tuple[int, int]]:
    pe.parse_data_directories(
        directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]]
    )
    rows = [
        (int(entry.struct.BeginAddress), int(entry.struct.EndAddress))
        for entry in getattr(pe, "DIRECTORY_ENTRY_EXCEPTION", [])
        if int(entry.struct.BeginAddress) < int(entry.struct.EndAddress)
    ]
    rows.sort()
    return rows


def containing_function(
    functions: list[tuple[int, int]], rva: int
) -> tuple[int, int] | None:
    lo, hi = 0, len(functions)
    while lo < hi:
        mid = (lo + hi) // 2
        begin, end = functions[mid]
        if rva < begin:
            hi = mid
        elif rva >= end:
            lo = mid + 1
        else:
            return begin, end
    return None


def executable_sections(pe: pefile.PE) -> Iterable[pefile.SectionStructure]:
    for section in pe.sections:
        if int(section.Characteristics) & 0x20000000:
            yield section


def frontend_contract(index_path: Path, panel_path: Path) -> dict[str, Any]:
    index_bytes = index_path.read_bytes()
    panel_bytes = panel_path.read_bytes()
    if sha256(index_bytes) != INDEX_SHA256:
        raise InspectError("unexpected recovered index asset SHA-256")
    if sha256(panel_bytes) != MAP_PANEL_SHA256:
        raise InspectError("unexpected recovered MapDataPanel asset SHA-256")

    index = index_bytes.decode("utf-8")
    panel = panel_bytes.decode("utf-8")

    # The minified shared module uses one small Tauri wrapper function per
    # command.  Capture wrappers whose literal command belongs to Map/server
    # navigation.  The body text is retained so transformed argument shapes are
    # inspectable without pretending this regex is a schema parser.
    wrapper_pattern = re.compile(
        r"function\s+(?P<fn>[A-Za-z_$][\w$]*)\((?P<args>[^)]*)\)"
        r"\{(?P<body>[^{}]{0,900}?N\(`(?P<command>[^`]+)`[^{}]{0,1400}?)\}"
    )
    wrappers: list[dict[str, Any]] = []
    for match in wrapper_pattern.finditer(index):
        command = match.group("command")
        if not command.startswith(MAP_COMMAND_PREFIXES):
            continue
        wrappers.append(
            {
                "function": match.group("fn"),
                "arguments": match.group("args"),
                "command": command,
                "sourceOffset": f"0x{match.start():X}",
                "body": match.group("body"),
            }
        )

    # A few wrappers contain nested mapping functions and therefore exceed the
    # deliberately conservative regex above.  Literal command discovery keeps
    # the manifest complete even when the body parser declines a wrapper.
    command_literals = sorted(
        {
            match.group(1)
            for match in re.finditer(r"N\(`([^`]+)`", index)
            if match.group(1).startswith(MAP_COMMAND_PREFIXES)
        }
    )

    events: list[dict[str, Any]] = []
    for event in MAP_EVENT_MARKERS:
        for source_name, source in (("index", index), ("MapDataPanel", panel)):
            start = 0
            while True:
                hit = source.find(event, start)
                if hit < 0:
                    break
                events.append(
                    {"event": event, "source": source_name, "sourceOffset": f"0x{hit:X}"}
                )
                start = hit + 1

    locators: dict[str, list[str]] = {}
    for marker in FRONTEND_LOCATOR_MARKERS:
        hits: list[str] = []
        for source_name, source in (("index", index), ("MapDataPanel", panel)):
            start = 0
            while True:
                hit = source.find(marker, start)
                if hit < 0:
                    break
                hits.append(f"{source_name}:0x{hit:X}")
                start = hit + 1
        if hits:
            locators[marker] = hits

    return {
        "index": {"path": str(index_path), "sha256": sha256(index_bytes), "size": len(index_bytes)},
        "mapPanel": {"path": str(panel_path), "sha256": sha256(panel_bytes), "size": len(panel_bytes)},
        "commandWrappers": wrappers,
        "commandLiterals": command_literals,
        "events": events,
        "locators": locators,
    }


def native_surface(binary: Path, markers: list[str]) -> dict[str, Any]:
    data = binary.read_bytes()
    digest = sha256(data)
    if digest != REFERENCE_SHA256:
        raise InspectError(f"unexpected LWBridge 0.3.17 SHA-256: {digest}")

    pe = pefile.PE(data=data, fast_load=False)
    image_base = int(pe.OPTIONAL_HEADER.ImageBase)
    functions = runtime_functions(pe)

    target_vas: dict[int, list[tuple[str, str]]] = {}
    marker_rows: dict[str, Any] = {}
    for marker in markers:
        raw_hits = all_offsets(data, marker.encode("utf-8"))
        occurrences: list[dict[str, Any]] = []
        for raw in raw_hits:
            rva = raw_to_rva(pe, raw)
            if rva is None:
                continue
            va = image_base + rva
            occurrences.append({"raw": f"0x{raw:X}", "rva": f"0x{rva:X}", "va": f"0x{va:X}"})
            target_vas.setdefault(va, []).append((marker, "string"))

            descriptor = struct.pack("<QQ", va, len(marker.encode("utf-8")))
            for desc_raw in all_offsets(data, descriptor):
                desc_rva = raw_to_rva(pe, desc_raw)
                if desc_rva is None:
                    continue
                desc_va = image_base + desc_rva
                occurrences[-1].setdefault("descriptors", []).append(
                    {"raw": f"0x{desc_raw:X}", "rva": f"0x{desc_rva:X}", "va": f"0x{desc_va:X}"}
                )
                target_vas.setdefault(desc_va, []).append((marker, "rustStrDescriptor"))
        marker_rows[marker] = {"occurrences": occurrences, "xrefs": []}

    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    for section in executable_sections(pe):
        section_rva = int(section.VirtualAddress)
        for ins in md.disasm(section.get_data(), image_base + section_rva):
            refs: set[int] = set()
            for op in ins.operands:
                if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                    refs.add(int(ins.address + ins.size + op.mem.disp))
                elif op.type == X86_OP_IMM:
                    refs.add(int(op.imm))
            for target in refs.intersection(target_vas):
                call_rva = int(ins.address - image_base)
                fn = containing_function(functions, call_rva)
                for marker, reference_kind in target_vas[target]:
                    marker_rows[marker]["xrefs"].append(
                        {
                            "instructionRva": f"0x{call_rva:X}",
                            "instructionVa": f"0x{ins.address:X}",
                            "mnemonic": ins.mnemonic,
                            "opStr": ins.op_str,
                            "referenceKind": reference_kind,
                            "targetVa": f"0x{target:X}",
                            "functionRva": None if fn is None else f"0x{fn[0]:X}-0x{fn[1]:X}",
                        }
                    )

    return {
        "path": str(binary),
        "sha256": digest,
        "size": len(data),
        "imageBase": f"0x{image_base:X}",
        "sections": [
            {
                "name": section.Name.rstrip(b"\0").decode("ascii", "replace"),
                "rva": f"0x{int(section.VirtualAddress):X}",
                "raw": f"0x{int(section.PointerToRawData):X}",
                "rawSize": int(section.SizeOfRawData),
                "virtualSize": int(section.Misc_VirtualSize),
                "characteristics": f"0x{int(section.Characteristics):X}",
            }
            for section in pe.sections
        ],
        "markers": marker_rows,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("index", type=Path)
    parser.add_argument("map_panel", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    frontend = frontend_contract(args.index, args.map_panel)
    native_markers = sorted(
        set(frontend["commandLiterals"])
        | set(MAP_EVENT_MARKERS)
        | set(NATIVE_DISCOVERY_MARKERS)
    )
    result = {
        "schema": 1,
        "findingId": "LWB317-RE-MAP-001",
        "evidenceState": "EXACT_BYTES_DISCOVERY",
        "frontend": frontend,
        "native": native_surface(args.binary, native_markers),
        "limits": [
            "String presence/xrefs identify native discovery entry points; they do not by themselves prove handler semantics.",
            "Nested JavaScript wrapper bodies are retained only where the conservative wrapper regex can capture them.",
            "Provider/game-side behavior requires dedicated exact-binary tracing and later bounded live proof.",
        ],
    }
    rendered = json.dumps(result, indent=2, ensure_ascii=False) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered, encoding="utf-8")
    else:
        print(rendered, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
