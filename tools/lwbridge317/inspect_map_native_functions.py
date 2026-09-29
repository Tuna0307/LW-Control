#!/usr/bin/env python3
"""Annotate 0.3.17 Map native handler functions discovered by surface scan.

Read-only helper.  It consumes ``surface-discovery.json`` rather than hardcoding
0.3.1 RVAs, verifies the exact 0.3.17 binary hash, and emits bounded handler
disassembly plus printable/Rust-string references and direct call targets.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
from typing import Any

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP


REFERENCE_SHA256 = "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"

DEFAULT_MARKERS = (
    "map_scan_start",
    "map_scan_status",
    "map_scan_stop",
    "map_scan_clear",
    "map_summary",
    "map_search",
    "map_data_options",
    "map_city_export",
    "map_coordinate_jump",
    "map_march_follow",
    "map_player_mark_set",
    "server_jump",
    "server_jump_history_set",
    "server_jump_history_import",
)


class InspectError(ValueError):
    pass


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def parse_range(value: str) -> tuple[int, int]:
    match = re.fullmatch(r"0x([0-9A-Fa-f]+)-0x([0-9A-Fa-f]+)", value)
    if not match:
        raise InspectError(f"invalid runtime-function range: {value!r}")
    return int(match.group(1), 16), int(match.group(2), 16)


def rva_to_offset(pe: pefile.PE, rva: int) -> int | None:
    try:
        return int(pe.get_offset_from_rva(rva))
    except pefile.PEFormatError:
        return None


def printable_ascii(pe: pefile.PE, data: bytes, image_base: int, va: int) -> str | None:
    offset = rva_to_offset(pe, va - image_base)
    if offset is None or offset < 0 or offset >= len(data):
        return None
    end = offset
    while end < min(len(data), offset + 360) and data[end] in (9, 10, 13) or (
        end < min(len(data), offset + 360) and 0x20 <= data[end] <= 0x7E
    ):
        end += 1
    raw = data[offset:end]
    if len(raw) < 3:
        return None
    try:
        return raw.decode("ascii")
    except UnicodeDecodeError:
        return None


def rust_str(pe: pefile.PE, data: bytes, image_base: int, va: int) -> str | None:
    offset = rva_to_offset(pe, va - image_base)
    if offset is None or offset + 16 > len(data):
        return None
    ptr, length = struct.unpack_from("<QQ", data, offset)
    if not 3 <= length <= 360:
        return None
    string_offset = rva_to_offset(pe, ptr - image_base)
    if string_offset is None or string_offset + length > len(data):
        return None
    raw = data[string_offset : string_offset + length]
    if not all(byte in (9, 10, 13) or 0x20 <= byte <= 0x7E for byte in raw):
        return None
    return raw.decode("ascii", "replace")


def annotate_function(
    pe: pefile.PE,
    data: bytes,
    image_base: int,
    begin: int,
    end: int,
) -> dict[str, Any]:
    offset = rva_to_offset(pe, begin)
    if offset is None:
        raise InspectError(f"function RVA 0x{begin:X} is not file-backed")

    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    instructions: list[dict[str, Any]] = []
    strings: dict[str, dict[str, Any]] = {}
    calls: dict[str, int] = {}

    for ins in md.disasm(data[offset : offset + (end - begin)], image_base + begin):
        refs: list[dict[str, Any]] = []
        for operand in ins.operands:
            target: int | None = None
            if operand.type == X86_OP_MEM and operand.mem.base == X86_REG_RIP:
                target = int(ins.address + ins.size + operand.mem.disp)
            if target is not None:
                text = rust_str(pe, data, image_base, target) or printable_ascii(
                    pe, data, image_base, target
                )
                if text:
                    key = f"0x{target:X}"
                    strings.setdefault(
                        key,
                        {"targetVa": key, "text": text, "instructionRvas": []},
                    )["instructionRvas"].append(f"0x{ins.address - image_base:X}")
                    refs.append({"targetVa": key, "text": text})

        if ins.mnemonic == "call" and ins.operands and ins.operands[0].type == X86_OP_IMM:
            target = int(ins.operands[0].imm)
            key = f"0x{target - image_base:X}" if target >= image_base else f"0x{target:X}"
            calls[key] = calls.get(key, 0) + 1

        instructions.append(
            {
                "rva": f"0x{ins.address - image_base:X}",
                "mnemonic": ins.mnemonic,
                "opStr": ins.op_str,
                "bytes": ins.bytes.hex(),
                "stringRefs": refs,
            }
        )

    return {
        "functionRva": f"0x{begin:X}-0x{end:X}",
        "size": end - begin,
        "strings": sorted(strings.values(), key=lambda item: item["targetVa"]),
        "directCalls": [
            {"targetRva": target, "count": count}
            for target, count in sorted(calls.items())
        ],
        "instructions": instructions,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("surface_manifest", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--markers", nargs="*", default=list(DEFAULT_MARKERS))
    args = parser.parse_args()

    data = args.binary.read_bytes()
    digest = sha256(data)
    if digest != REFERENCE_SHA256:
        raise InspectError(f"unexpected LWBridge 0.3.17 SHA-256: {digest}")
    pe = pefile.PE(data=data, fast_load=False)
    image_base = int(pe.OPTIONAL_HEADER.ImageBase)

    surface_bytes = args.surface_manifest.read_bytes()
    surface_digest = sha256(surface_bytes)
    surface = json.loads(surface_bytes.decode("utf-8"))
    surface_reference = surface.get("native", {}).get("sha256")
    if surface_reference != digest:
        raise InspectError(
            "surface manifest reference SHA-256 does not match inspected binary: "
            f"{surface_reference!r} != {digest}"
        )
    marker_table = surface["native"]["markers"]
    result: dict[str, Any] = {
        "schema": 1,
        "findingId": "LWB317-RE-MAP-001-NATIVE-HANDLERS",
        "evidenceState": "EXACT_BYTES_DISCOVERY",
        "source": {
            "path": str(args.binary),
            "sha256": digest,
            "imageBase": f"0x{image_base:X}",
            "surfaceManifest": str(args.surface_manifest),
            "surfaceManifestSha256": surface_digest,
        },
        "markers": {},
    }

    cache: dict[str, dict[str, Any]] = {}
    for marker in args.markers:
        entry = marker_table.get(marker)
        if not entry:
            result["markers"][marker] = {"status": "marker-not-found"}
            continue
        ranges = []
        for xref in entry.get("xrefs", []):
            value = xref.get("functionRva")
            if not value or value == "0x218023-0x21C5B4":
                # The very large shared Tauri registration function is useful
                # for confirming registration but not a command semantic trace.
                continue
            if value not in ranges:
                ranges.append(value)
        annotated = []
        for value in ranges:
            if value not in cache:
                begin, end = parse_range(value)
                cache[value] = annotate_function(pe, data, image_base, begin, end)
            annotated.append(cache[value])
        result["markers"][marker] = {"handlerCandidates": annotated}

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
