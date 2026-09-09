#!/usr/bin/env python3
"""Inspect LWBridge 0.3.1 Map Data treasure-option key prefix xrefs."""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path

import capstone
import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86_const import X86_OP_MEM, X86_REG_RIP


EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
PREFIX_OFFSETS = {
    "treasure": 0x00C83B21,
    "supplies": 0x00C83B59,
}
CLUSTER_MARKERS = {
    "key": (0x00C83AE5, b"key"),
    "iconPath": (0x00C83B00, b"iconPath"),
    "treasurePrefix": (0x00C83B21, b"treasure:"),
    "treasureNameKey": (0x00C83B2C, b"treasureNameKey"),
    "count": (0x00C83B3B, b"count"),
    "suppliesPrefix": (0x00C83B59, b"supplies:"),
}


class InspectError(ValueError):
    pass


def require(condition: bool, message: str) -> None:
    if not condition:
        raise InspectError(message)


def va_for_offset(pe: pefile.PE, offset: int) -> int:
    return pe.OPTIONAL_HEADER.ImageBase + pe.get_rva_from_offset(offset)


def function_bounds(pe: pefile.PE, va: int) -> tuple[int, int] | None:
    rva = va - pe.OPTIONAL_HEADER.ImageBase
    for entry in pe.DIRECTORY_ENTRY_EXCEPTION:
        if entry.struct.BeginAddress <= rva < entry.struct.EndAddress:
            return (
                pe.OPTIONAL_HEADER.ImageBase + entry.struct.BeginAddress,
                pe.OPTIONAL_HEADER.ImageBase + entry.struct.EndAddress,
            )
    return None


def inspect(binary: Path) -> dict[str, object]:
    blob = binary.read_bytes()
    digest = hashlib.sha256(blob).hexdigest()
    require(digest == EXPECTED_SHA256, f"unexpected LWBridge SHA-256: {digest}")

    pe = pefile.PE(data=blob, fast_load=False)
    pe.parse_data_directories()
    decoder = Cs(CS_ARCH_X86, CS_MODE_64)
    decoder.detail = True

    targets = {name: va_for_offset(pe, offset) for name, offset in PREFIX_OFFSETS.items()}
    for name, offset in PREFIX_OFFSETS.items():
        expected = (name + ":").encode("ascii")
        require(blob[offset:offset + len(expected)] == expected, f"{name} prefix bytes changed")
    for name, (offset, expected) in CLUSTER_MARKERS.items():
        require(blob[offset:offset + len(expected)] == expected, f"{name} cluster marker changed")

    occurrences: dict[str, list[str]] = {}
    for name in PREFIX_OFFSETS:
        needle = (name + ":").encode("ascii")
        offsets: list[str] = []
        start = 0
        while True:
            offset = blob.find(needle, start)
            if offset < 0:
                break
            offsets.append(f"0x{offset:08X}")
            start = offset + 1
        occurrences[name] = offsets

    metadata: dict[str, list[dict[str, object]]] = {name: [] for name in targets}
    metadata_targets: dict[int, tuple[str, int]] = {}
    for name, target_va in targets.items():
        pointer = struct.pack("<Q", target_va)
        start = 0
        while True:
            offset = blob.find(pointer, start)
            if offset < 0:
                break
            slot_va = va_for_offset(pe, offset)
            length = struct.unpack_from("<Q", blob, offset + 8)[0] if offset + 16 <= len(blob) else None
            metadata[name].append({
                "fileOffset": f"0x{offset:08X}",
                "preferredImageVa": f"0x{slot_va:016X}",
                "followingU64": length,
            })
            metadata_targets[slot_va] = (name, offset)
            start = offset + 1

    xrefs: dict[str, list[dict[str, object]]] = {name: [] for name in targets}
    target_by_va = {va: (name, "string") for name, va in targets.items()}
    target_by_va.update({va: (name, "metadata") for va, (name, _) in metadata_targets.items()})
    for section in pe.sections:
        if not (section.Characteristics & 0x20000000):
            continue
        data = section.get_data()
        base = pe.OPTIONAL_HEADER.ImageBase + section.VirtualAddress
        for ins in decoder.disasm(data, base):
            for op in ins.operands:
                if op.type != X86_OP_MEM or op.mem.base != X86_REG_RIP:
                    continue
                target = ins.address + ins.size + op.mem.disp
                target_info = target_by_va.get(target)
                if target_info is None:
                    continue
                name, target_kind = target_info
                bounds = function_bounds(pe, ins.address)
                xrefs[name].append({
                    "instructionVa": f"0x{ins.address:016X}",
                    "mnemonic": ins.mnemonic,
                    "opStr": ins.op_str,
                    "targetKind": target_kind,
                    "targetVa": f"0x{target:016X}",
                    "functionVaRange": None if bounds is None else [
                        f"0x{bounds[0]:016X}", f"0x{bounds[1]:016X}"
                    ],
                })

    return {
        "reference": {
            "path": str(binary),
            "sha256": digest,
            "coordinateSystem": "raw PE file offsets and preferred-image virtual addresses",
        },
        "prefixes": {
            name: {
                "text": name + ":",
                "fileOffset": f"0x{offset:08X}",
                "preferredImageVa": f"0x{targets[name]:016X}",
                "allFileOccurrences": occurrences[name],
                "metadataSlots": metadata[name],
                "xrefs": xrefs[name],
            }
            for name, offset in PREFIX_OFFSETS.items()
        },
        "optionResultCluster": {
            name: {"fileOffset": f"0x{offset:08X}", "text": expected.decode("ascii")}
            for name, (offset, expected) in CLUSTER_MARKERS.items()
        },
        "analysisTools": {
            "capstone": capstone.__version__,
            "pefile": pefile.__version__,
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=Path)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        result = inspect(args.binary)
    except (InspectError, OSError, pefile.PEFormatError) as exc:
        parser.error(str(exc))
    print(json.dumps(result, indent=2) if args.json else json.dumps(result["prefixes"], indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
