#!/usr/bin/env python3
"""Read-only native xLua export and LENC reference inspector."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import capstone
import pefile


def _export_rva(pe: pefile.PE, name: str) -> int:
    for symbol in pe.DIRECTORY_ENTRY_EXPORT.symbols:
        if symbol.name and symbol.name.decode(errors="replace") == name:
            return int(symbol.address)
    raise ValueError(f"export {name!r} not found")


def _instruction_rows(pe: pefile.PE, start_rva: int, size: int) -> list[dict]:
    file_offset = pe.get_offset_from_rva(start_rva)
    image_base = int(pe.OPTIONAL_HEADER.ImageBase)
    code = pe.__data__[file_offset : file_offset + size]
    engine = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
    engine.detail = True
    rows = []
    for instruction in engine.disasm(code, image_base + start_rva):
        row = {
            "rva": instruction.address - image_base,
            "file_offset": pe.get_offset_from_rva(instruction.address - image_base),
            "size": instruction.size,
            "mnemonic": instruction.mnemonic,
            "op_str": instruction.op_str,
        }
        if instruction.mnemonic == "call" and instruction.operands:
            operand = instruction.operands[0]
            if operand.type == capstone.x86.X86_OP_IMM:
                row["call_target_rva"] = int(operand.imm - image_base)
        rip_targets = []
        for operand in instruction.operands:
            if operand.type != capstone.x86.X86_OP_MEM:
                continue
            if operand.mem.base != capstone.x86.X86_REG_RIP:
                continue
            target_va = instruction.address + instruction.size + operand.mem.disp
            rip_targets.append(int(target_va - image_base))
        if rip_targets:
            row["rip_target_rvas"] = rip_targets
        rows.append(row)
    return rows


def _lenc_code_references(pe: pefile.PE, lenc_rvas: list[int]) -> tuple[list[dict], list[dict]]:
    image_base = int(pe.OPTIONAL_HEADER.ImageBase)
    engine = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
    engine.detail = True
    rip_refs: list[dict] = []
    immediate_refs: list[dict] = []
    magic_values = {0x434E454C, 0x4C454E43}
    for section in pe.sections:
        if not (int(section.Characteristics) & 0x20000000):
            continue
        rva = int(section.VirtualAddress)
        code = section.get_data()
        for instruction in engine.disasm(code, image_base + rva):
            for operand in instruction.operands:
                if operand.type == capstone.x86.X86_OP_MEM and operand.mem.base == capstone.x86.X86_REG_RIP:
                    target_rva = instruction.address + instruction.size + operand.mem.disp - image_base
                    if target_rva in lenc_rvas:
                        rip_refs.append(
                            {
                                "rva": instruction.address - image_base,
                                "mnemonic": instruction.mnemonic,
                                "op_str": instruction.op_str,
                                "target_rva": target_rva,
                            }
                        )
                elif operand.type == capstone.x86.X86_OP_IMM and (int(operand.imm) & 0xFFFFFFFF) in magic_values:
                    immediate_refs.append(
                        {
                            "rva": instruction.address - image_base,
                            "mnemonic": instruction.mnemonic,
                            "op_str": instruction.op_str,
                            "immediate": int(operand.imm) & 0xFFFFFFFF,
                        }
                    )
    return rip_refs, immediate_refs


def inspect_native(
    path: Path,
    export_name: str | None,
    start_rva: int | None,
    size: int,
    scan_lenc_xrefs: bool,
) -> dict:
    data = path.read_bytes()
    pe = pefile.PE(data=data, fast_load=False)
    pe.parse_data_directories(
        directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXPORT"]]
    )
    if (export_name is None) == (start_rva is None):
        raise ValueError("select exactly one of export_name or start_rva")
    selected_rva = _export_rva(pe, export_name) if export_name is not None else int(start_rva)
    lenc_offsets = []
    search_at = 0
    while True:
        found = data.find(b"LENC", search_at)
        if found < 0:
            break
        lenc_offsets.append(found)
        search_at = found + 1
    lenc_rvas = []
    for offset in lenc_offsets:
        try:
            lenc_rvas.append(pe.get_rva_from_offset(offset))
        except pefile.PEFormatError:
            pass

    rows = _instruction_rows(pe, selected_rva, size)
    direct_calls = sorted(
        {row["call_target_rva"] for row in rows if "call_target_rva" in row}
    )
    local_rip_refs = [
        row
        for row in rows
        if any(target in lenc_rvas for target in row.get("rip_target_rvas", []))
    ]
    all_rip_refs: list[dict] = []
    all_immediate_refs: list[dict] = []
    if scan_lenc_xrefs:
        all_rip_refs, all_immediate_refs = _lenc_code_references(pe, lenc_rvas)
    return {
        "path": str(path),
        "sha256": hashlib.sha256(data).hexdigest(),
        "image_base": int(pe.OPTIONAL_HEADER.ImageBase),
        "export": export_name,
        "selected_rva": selected_rva,
        "selected_file_offset": pe.get_offset_from_rva(selected_rva),
        "scan_size": size,
        "lenc_file_offsets": lenc_offsets,
        "lenc_rvas": lenc_rvas,
        "direct_call_rvas": direct_calls,
        "lenc_rip_references": local_rip_refs,
        "all_lenc_rip_references": all_rip_refs,
        "all_lenc_immediate_references": all_immediate_refs,
        "instructions": rows,
        "read_only": True,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path", type=Path)
    selector = parser.add_mutually_exclusive_group(required=True)
    selector.add_argument("--export")
    selector.add_argument("--rva", type=lambda value: int(value, 0))
    parser.add_argument("--size", type=lambda value: int(value, 0), default=0x180)
    parser.add_argument("--scan-lenc-xrefs", action="store_true")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    result = inspect_native(args.path, args.export, args.rva, args.size, args.scan_lenc_xrefs)
    if args.json:
        print(json.dumps(result, indent=2))
    else:
        label = result["export"] or "selected RVA"
        print(f"{label} RVA 0x{result['selected_rva']:X}")
        print("LENC RVAs:", ", ".join(f"0x{x:X}" for x in result["lenc_rvas"]))
        for row in result["instructions"]:
            print(f"0x{row['rva']:X}: {row['mnemonic']:<8} {row['op_str']}")
        print("Read-only: no file changes performed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
