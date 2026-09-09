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
from capstone.x86_const import X86_OP_MEM, X86_REG_RIP, X86_REG_RSP


EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
PREFIX_OFFSETS = {
    "treasure": 0x00C83B21,
    "supplies": 0x00C83B59,
}
PREFIX_DESCRIPTOR_OFFSETS = {
    "treasure": 0x00C83B20,
    "supplies": 0x00C83B58,
}
CLUSTER_MARKERS = {
    "key": (0x00C83AE5, b"key"),
    "iconPath": (0x00C83B00, b"iconPath"),
    "treasurePrefix": (0x00C83B21, b"treasure:"),
    "treasureNameKey": (0x00C83B2C, b"treasureNameKey"),
    "count": (0x00C83B3B, b"count"),
    "suppliesPrefix": (0x00C83B59, b"supplies:"),
}
OPTION_ASSEMBLY_FUNCTION_VA_RANGE = (0x140257AA4, 0x14025B436)
SUPPLIES_BRANCH_VA_RANGE = (0x140259A00, 0x140259E02)
TREASURE_BRANCH_VA_RANGE = (0x140259D70, 0x14025A078)
TREASURE_ROW_DECODE_VA_RANGE = (0x140259700, 0x140259BA0)
ANALYSIS_TARGETS = {
    "call_0259C1B": 0x1400276B0,
    "call_0259E55": 0x14006D05A,
    "formatter_ptr_0397B0": 0x1400397B0,
    "formatter_numeric_0394C0": 0x1400394C0,
    "formatter_emit_03BE70": 0x14003BE70,
}
STACK_SLOT_OFFSETS = (0x50, 0x58, 0x60, 0x200, 0x250, 0x270)
TREASURE_TYPE_FIELD_VA = 0x140838A85


class InspectError(ValueError):
    pass


def require(condition: bool, message: str) -> None:
    if not condition:
        raise InspectError(message)


def va_for_offset(pe: pefile.PE, offset: int) -> int:
    return pe.OPTIONAL_HEADER.ImageBase + pe.get_rva_from_offset(offset)


def offset_for_va(pe: pefile.PE, va: int) -> int:
    return pe.get_offset_from_rva(va - pe.OPTIONAL_HEADER.ImageBase)


def function_bounds(pe: pefile.PE, va: int) -> tuple[int, int] | None:
    rva = va - pe.OPTIONAL_HEADER.ImageBase
    for entry in pe.DIRECTORY_ENTRY_EXCEPTION:
        if entry.struct.BeginAddress <= rva < entry.struct.EndAddress:
            return (
                pe.OPTIONAL_HEADER.ImageBase + entry.struct.BeginAddress,
                pe.OPTIONAL_HEADER.ImageBase + entry.struct.EndAddress,
            )
    return None


def bytes_for_va_range(pe: pefile.PE, blob: bytes, start_va: int, end_va: int) -> bytes:
    require(end_va > start_va, "invalid VA range")
    image_base = pe.OPTIONAL_HEADER.ImageBase
    start_offset = pe.get_offset_from_rva(start_va - image_base)
    end_offset = pe.get_offset_from_rva(end_va - image_base)
    require(end_offset > start_offset, "invalid file range for VA range")
    return blob[start_offset:end_offset]


def inspect(binary: Path) -> dict[str, object]:
    blob = binary.read_bytes()
    digest = hashlib.sha256(blob).hexdigest()
    require(digest == EXPECTED_SHA256, f"unexpected LWBridge SHA-256: {digest}")

    pe = pefile.PE(data=blob, fast_load=False)
    pe.parse_data_directories()
    decoder = Cs(CS_ARCH_X86, CS_MODE_64)
    decoder.detail = True

    targets = {name: va_for_offset(pe, offset) for name, offset in PREFIX_OFFSETS.items()}
    descriptor_targets = {
        name: va_for_offset(pe, offset)
        for name, offset in PREFIX_DESCRIPTOR_OFFSETS.items()
    }
    cluster_targets = {
        name: va_for_offset(pe, offset)
        for name, (offset, _) in CLUSTER_MARKERS.items()
    }
    for name, offset in PREFIX_OFFSETS.items():
        expected = (name + ":").encode("ascii")
        require(blob[offset:offset + len(expected)] == expected, f"{name} prefix bytes changed")
        descriptor_offset = PREFIX_DESCRIPTOR_OFFSETS[name]
        require(blob[descriptor_offset] == len(expected), f"{name} descriptor length changed")
        require(descriptor_offset + 1 == offset, f"{name} descriptor is not adjacent to prefix")
    for name, (offset, expected) in CLUSTER_MARKERS.items():
        require(blob[offset:offset + len(expected)] == expected, f"{name} cluster marker changed")
    treasure_type_field_offset = offset_for_va(pe, TREASURE_TYPE_FIELD_VA)
    require(
        blob[treasure_type_field_offset:treasure_type_field_offset + 12] == b"treasureType",
        "treasureType field literal changed",
    )

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
    descriptor_xrefs: dict[str, list[dict[str, object]]] = {name: [] for name in descriptor_targets}
    cluster_xrefs: dict[str, list[dict[str, object]]] = {name: [] for name in cluster_targets}
    target_by_va = {va: (name, "string") for name, va in targets.items()}
    target_by_va.update({va: (name, "metadata") for va, (name, _) in metadata_targets.items()})
    descriptor_target_by_va = {va: name for name, va in descriptor_targets.items()}
    cluster_target_by_va = {va: name for name, va in cluster_targets.items()}

    # Preserve the R6-022 historical check: raw prefix strings and any discovered
    # absolute-pointer metadata slots are checked across executable sections.
    # Descriptor targets are intentionally excluded from this pass because the
    # later whole-image descriptor-xref attempt was denied (SB-13).
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

    function_start, function_end = OPTION_ASSEMBLY_FUNCTION_VA_RANGE
    function_data = bytes_for_va_range(pe, blob, function_start, function_end)
    function_instructions = list(decoder.disasm(function_data, function_start))
    stack_slot_references: dict[str, list[dict[str, str]]] = {
        f"0x{offset:X}": [] for offset in STACK_SLOT_OFFSETS
    }
    for ins in function_instructions:
        for op in ins.operands:
            if op.type == X86_OP_MEM and op.mem.base == X86_REG_RSP and op.mem.disp in STACK_SLOT_OFFSETS:
                stack_slot_references[f"0x{op.mem.disp:X}"].append({
                    "va": f"0x{ins.address:016X}",
                    "mnemonic": ins.mnemonic,
                    "opStr": ins.op_str,
                })
        for op in ins.operands:
            if op.type != X86_OP_MEM or op.mem.base != X86_REG_RIP:
                continue
            target = ins.address + ins.size + op.mem.disp
            bounds = function_bounds(pe, ins.address)
            cluster_name = cluster_target_by_va.get(target)
            if cluster_name is not None:
                cluster_xrefs[cluster_name].append({
                    "instructionVa": f"0x{ins.address:016X}",
                    "mnemonic": ins.mnemonic,
                    "opStr": ins.op_str,
                    "targetVa": f"0x{target:016X}",
                    "functionVaRange": None if bounds is None else [
                        f"0x{bounds[0]:016X}", f"0x{bounds[1]:016X}"
                    ],
                })
            descriptor_name = descriptor_target_by_va.get(target)
            if descriptor_name is not None:
                descriptor_xrefs[descriptor_name].append({
                    "instructionVa": f"0x{ins.address:016X}",
                    "mnemonic": ins.mnemonic,
                    "opStr": ins.op_str,
                    "targetVa": f"0x{target:016X}",
                    "functionVaRange": None if bounds is None else [
                        f"0x{bounds[0]:016X}", f"0x{bounds[1]:016X}"
                    ],
                })

    def serialize_instructions(start_va: int, end_va: int) -> list[dict[str, object]]:
        rows: list[dict[str, object]] = []
        for ins in function_instructions:
            if not (start_va <= ins.address < end_va):
                continue
            rip_targets: list[str] = []
            for op in ins.operands:
                if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                    rip_targets.append(f"0x{ins.address + ins.size + op.mem.disp:016X}")
            rows.append({
                "va": f"0x{ins.address:016X}",
                "mnemonic": ins.mnemonic,
                "opStr": ins.op_str,
                "ripTargets": rip_targets,
            })
        return rows

    supplies_start, supplies_end = SUPPLIES_BRANCH_VA_RANGE
    branch_start, branch_end = TREASURE_BRANCH_VA_RANGE
    row_decode_start, row_decode_end = TREASURE_ROW_DECODE_VA_RANGE
    branch_instructions = serialize_instructions(branch_start, branch_end)
    supplies_branch_instructions = serialize_instructions(supplies_start, supplies_end)
    row_decode_instructions = serialize_instructions(row_decode_start, row_decode_end)

    analysis_targets: dict[str, object] = {}
    for label, target_va in ANALYSIS_TARGETS.items():
        bounds = function_bounds(pe, target_va)
        require(bounds is not None, f"no function bounds for {label}")
        target_start, target_end = bounds
        target_data = bytes_for_va_range(pe, blob, target_start, target_end)
        target_instructions = list(decoder.disasm(target_data, target_start))
        analysis_targets[label] = {
            "targetVa": f"0x{target_va:016X}",
            "functionVaRange": [f"0x{target_start:016X}", f"0x{target_end:016X}"],
            "instructions": [
                {
                    "va": f"0x{ins.address:016X}",
                    "mnemonic": ins.mnemonic,
                    "opStr": ins.op_str,
                }
                for ins in target_instructions
            ],
        }

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
                "descriptorFileOffset": f"0x{PREFIX_DESCRIPTOR_OFFSETS[name]:08X}",
                "descriptorPreferredImageVa": f"0x{descriptor_targets[name]:016X}",
                "descriptorLengthByte": len(name + ":"),
                "descriptorXrefs": descriptor_xrefs[name],
                "allFileOccurrences": occurrences[name],
                "metadataSlots": metadata[name],
                "xrefs": xrefs[name],
            }
            for name, offset in PREFIX_OFFSETS.items()
        },
        "optionResultCluster": {
            name: {
                "fileOffset": f"0x{offset:08X}",
                "preferredImageVa": f"0x{cluster_targets[name]:016X}",
                "text": expected.decode("ascii"),
                "directXrefs": cluster_xrefs[name],
            }
            for name, (offset, expected) in CLUSTER_MARKERS.items()
        },
        "boundedCode": {
            "optionAssemblyFunctionVaRange": [
                f"0x{function_start:016X}", f"0x{function_end:016X}"
            ],
            "suppliesBranchVaRange": [
                f"0x{supplies_start:016X}", f"0x{supplies_end:016X}"
            ],
            "suppliesBranchInstructions": supplies_branch_instructions,
            "treasureRowDecodeVaRange": [
                f"0x{row_decode_start:016X}", f"0x{row_decode_end:016X}"
            ],
            "treasureRowDecodeInstructions": row_decode_instructions,
            "treasureBranchVaRange": [
                f"0x{branch_start:016X}", f"0x{branch_end:016X}"
            ],
            "treasureBranchInstructions": branch_instructions,
            "analysisTargets": analysis_targets,
            "stackSlotReferences": stack_slot_references,
            "treasureTypeField": {
                "text": "treasureType",
                "preferredImageVa": f"0x{TREASURE_TYPE_FIELD_VA:016X}",
                "fileOffset": f"0x{treasure_type_field_offset:08X}",
            },
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
