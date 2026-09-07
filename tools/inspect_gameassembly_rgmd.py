#!/usr/bin/env python3
"""Read-only xref inspector for the current Last War GameAssembly RGMD runtime.

The current game ships a patched Mono/IL2CPP runtime that accepts the custom
``RGMD`` metadata root used by ``*.rdl`` assemblies. This tool hashes the native
runtime, locates the normal ``BSJB`` and custom ``RGMD`` signatures, and scans
executable sections for direct x86-64 references to those addresses or inline
four-byte signature constants. It never loads or executes GameAssembly.dll.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import struct
from typing import Any

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

EXPECTED_GAMEASSEMBLY_SHA256 = (
    "496fbb32195086deaf39221668957d130a96a94aa90f87dd5adaab23bb800279"
)
SIGNATURES = (b"RGMD", b"BSJB")
OBSERVED_HANDLER_TABLE_RVA = 0x332D00
EAX_DECODE_SEQUENCE = bytes.fromhex(
    "35 a5 a5 a5 a5 2d 16 cd 5b 07 35 b1 68 de 3a c1 c8 05"
)


class GameAssemblyInspectError(ValueError):
    pass


def _all_offsets(data: bytes, needle: bytes) -> list[int]:
    result: list[int] = []
    cursor = 0
    while True:
        position = data.find(needle, cursor)
        if position < 0:
            return result
        result.append(position)
        cursor = position + 1


def _offset_to_rva(pe: pefile.PE, offset: int) -> int:
    for section in pe.sections:
        start = int(section.PointerToRawData)
        stop = start + int(section.SizeOfRawData)
        if start <= offset < stop:
            return int(section.VirtualAddress) + (offset - start)
    if offset < int(pe.OPTIONAL_HEADER.SizeOfHeaders):
        return offset
    raise GameAssemblyInspectError(f"file offset 0x{offset:X} is outside PE sections")


def inspect(path: Path) -> dict[str, Any]:
    data = path.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if digest != EXPECTED_GAMEASSEMBLY_SHA256:
        raise GameAssemblyInspectError(
            f"unsupported GameAssembly.dll SHA-256 {digest}; expected {EXPECTED_GAMEASSEMBLY_SHA256}"
        )

    pe = pefile.PE(data=data, fast_load=False)
    image_base = int(pe.OPTIONAL_HEADER.ImageBase)
    signature_rows: dict[str, list[dict[str, int]]] = {}
    target_vas: dict[int, str] = {}
    for signature in SIGNATURES:
        rows: list[dict[str, int]] = []
        for file_offset in _all_offsets(data, signature):
            rva = _offset_to_rva(pe, file_offset)
            va = image_base + rva
            rows.append({"file_offset": file_offset, "rva": rva, "va": va})
            target_vas[va] = signature.decode("ascii")
        signature_rows[signature.decode("ascii")] = rows

    decode_sites: list[dict[str, Any]] = []
    for file_offset in _all_offsets(data, EAX_DECODE_SEQUENCE):
        rva = _offset_to_rva(pe, file_offset)
        decode_sites.append({"file_offset": file_offset, "rva": rva, "register": "eax"})

    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    xrefs: list[dict[str, Any]] = []
    inline_constants = {
        int.from_bytes(signature, "little"): signature.decode("ascii")
        for signature in SIGNATURES
    }

    for section in pe.sections:
        if not (int(section.Characteristics) & 0x20000000):
            continue
        raw_start = int(section.PointerToRawData)
        raw_size = int(section.SizeOfRawData)
        code = data[raw_start : raw_start + raw_size]
        section_va = image_base + int(section.VirtualAddress)
        for instruction in md.disasm(code, section_va):
            seen: set[tuple[str, str]] = set()
            for operand in instruction.operands:
                if operand.type == X86_OP_MEM and operand.mem.base == X86_REG_RIP:
                    target = instruction.address + instruction.size + operand.mem.disp
                    label = target_vas.get(target)
                    if label is not None:
                        seen.add(("address", label))
                elif operand.type == X86_OP_IMM:
                    target = int(operand.imm) & 0xFFFFFFFFFFFFFFFF
                    label = target_vas.get(target)
                    if label is not None:
                        seen.add(("address", label))
                    inline = inline_constants.get(target & 0xFFFFFFFF)
                    if inline is not None:
                        seen.add(("inline_constant", inline))
            for kind, label in sorted(seen):
                xrefs.append(
                    {
                        "signature": label,
                        "kind": kind,
                        "section": section.Name.rstrip(b"\0").decode("ascii", "replace"),
                        "va": instruction.address,
                        "rva": instruction.address - image_base,
                        "mnemonic": instruction.mnemonic,
                        "op_str": instruction.op_str,
                        "bytes": instruction.bytes.hex(),
                    }
                )

    exports: list[dict[str, Any]] = []
    if hasattr(pe, "DIRECTORY_ENTRY_EXPORT"):
        for symbol in pe.DIRECTORY_ENTRY_EXPORT.symbols:
            name = symbol.name.decode("ascii", "replace") if symbol.name else None
            interesting = (
                name == "il2cpp_init"
                or (name is not None and "InitMono" in name)
                or (name is not None and "LoadAssemblyWithImageBinary" in name)
                or (name is not None and "LoadAssemblyByName" in name)
                or (name is not None and "SetAssemblyOnlyReadFromPackage" in name)
            )
            if interesting:
                exports.append({"name": name, "rva": int(symbol.address)})

    handler_offset = pe.get_offset_from_rva(OBSERVED_HANDLER_TABLE_RVA)
    handler_values = struct.unpack_from("<6Q", data, handler_offset)
    handler_table = []
    image_end = image_base + int(pe.OPTIONAL_HEADER.SizeOfImage)
    for index, value in enumerate(handler_values):
        row: dict[str, Any] = {"index": index, "value": value}
        if image_base <= value < image_end:
            row["target_rva"] = value - image_base
        handler_table.append(row)

    return {
        "path": str(path),
        "size": len(data),
        "sha256": digest,
        "image_base": image_base,
        "signatures": signature_rows,
        "xrefs": xrefs,
        "decode_transform_sites": decode_sites,
        "exports": sorted(exports, key=lambda row: row["name"] or ""),
        "observed_handler_table": {
            "rva": OBSERVED_HANDLER_TABLE_RVA,
            "entries": handler_table,
        },
        "read_only": True,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path", type=Path)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        result = inspect(args.path)
    except (GameAssemblyInspectError, OSError, pefile.PEFormatError) as exc:
        print(json.dumps({"ok": False, "error": str(exc)}, indent=2))
        return 2
    output = {"ok": True, **result}
    print(json.dumps(output, indent=2) if args.json else output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
