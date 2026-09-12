#!/usr/bin/env python3
"""Recover and verify LWBridge 0.3.1 Map Data keyword construction."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import capstone
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP


EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
SQL_OFFSET = 0x00C89A56
SQL = (
    "(name LIKE ? ESCAPE '\\' COLLATE NOCASE OR alliance_name LIKE ? ESCAPE '\\' "
    "COLLATE NOCASE OR uuid LIKE ? ESCAPE '\\' COLLATE NOCASE OR data_json LIKE ? "
    "ESCAPE '\\' COLLATE NOCASE)"
)
REPLACEMENTS = [
    ("backslash", 0x5C, "\\\\", 0x00C89B06, 0x140272B30, 0x140272B4A),
    ("percent", 0x25, "\\%", 0x00C89B08, 0x140272B5D, 0x140272B74),
    ("underscore", 0x5F, "\\_", 0x00C89B0A, 0x140272B87, 0x140272B9E),
]
FORMAT_DESCRIPTOR_OFFSET = 0x00C83ECF
FORMAT_DESCRIPTOR = b"\x01%\xC0\x01%\x00"
STRING_REPLACE = 0x1402948C1
FORMAT_STRING = 0x1400276B0
STRING_CLONE = 0x14002A2C0


class InspectError(ValueError):
    pass


def require(condition: bool, message: str) -> None:
    if not condition:
        raise InspectError(message)


def va_for_offset(pe: pefile.PE, offset: int) -> int:
    return pe.OPTIONAL_HEADER.ImageBase + pe.get_rva_from_offset(offset)


def instruction_at(pe: pefile.PE, blob: bytes, decoder: Cs, va: int):
    offset = pe.get_offset_from_rva(va - pe.OPTIONAL_HEADER.ImageBase)
    return next(decoder.disasm(blob[offset:offset + 15], va))


def rip_target(instruction) -> int:
    operand = next(
        operand for operand in instruction.operands
        if operand.type == X86_OP_MEM and operand.mem.base == X86_REG_RIP
    )
    return instruction.address + instruction.size + operand.mem.disp


def immediate(instruction) -> int:
    return next(operand.imm for operand in instruction.operands if operand.type == X86_OP_IMM)


def inspect(binary: Path) -> dict[str, object]:
    blob = binary.read_bytes()
    digest = hashlib.sha256(blob).hexdigest()
    require(digest == EXPECTED_SHA256, f"unexpected LWBridge SHA-256: {digest}")
    require(blob[SQL_OFFSET:SQL_OFFSET + len(SQL)] == SQL.encode("ascii"), "keyword SQL mismatch")
    require(SQL.count("?") == 4, "keyword SQL placeholder count mismatch")
    require(
        blob[FORMAT_DESCRIPTOR_OFFSET:FORMAT_DESCRIPTOR_OFFSET + len(FORMAT_DESCRIPTOR)] == FORMAT_DESCRIPTOR,
        "percent-wrapper format descriptor mismatch",
    )

    pe = pefile.PE(data=blob, fast_load=False)
    pe.parse_data_directories(
        directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]]
    )
    function = next(
        entry for entry in pe.DIRECTORY_ENTRY_EXCEPTION
        if entry.struct.BeginAddress <= 0x00272AD8 < entry.struct.EndAddress
    )
    require(
        (function.struct.BeginAddress, function.struct.EndAddress) == (0x00271864, 0x00276D7B),
        "map-search runtime-function bounds mismatch",
    )
    decoder = Cs(CS_ARCH_X86, CS_MODE_64)
    decoder.detail = True

    sql_xref = instruction_at(pe, blob, decoder, 0x140272AD8)
    require(sql_xref.mnemonic == "lea" and rip_target(sql_xref) == va_for_offset(pe, SQL_OFFSET), "keyword SQL xref mismatch")
    require(immediate(instruction_at(pe, blob, decoder, 0x140272ADF)) == len(SQL), "keyword SQL length mismatch")
    require(immediate(instruction_at(pe, blob, decoder, 0x140272AE5)) == len(SQL), "keyword SQL copy length mismatch")

    replacement_report: list[dict[str, object]] = []
    for name, character, replacement, offset, xref_va, character_va in REPLACEMENTS:
        encoded = replacement.encode("ascii")
        require(blob[offset:offset + len(encoded)] == encoded, f"{name} replacement mismatch")
        xref = instruction_at(pe, blob, decoder, xref_va)
        require(rip_target(xref) == va_for_offset(pe, offset), f"{name} replacement xref mismatch")
        require(immediate(instruction_at(pe, blob, decoder, character_va)) == character, f"{name} character mismatch")
        call = instruction_at(pe, blob, decoder, character_va + 6)
        require(call.mnemonic == "call" and immediate(call) == STRING_REPLACE, f"{name} replacement call mismatch")
        replacement_report.append({
            "name": name,
            "inputCharacter": chr(character),
            "replacement": replacement,
            "fileOffset": f"0x{offset:08X}",
            "replacementXrefVa": f"0x{xref_va:016X}",
            "characterImmediateVa": f"0x{character_va:016X}",
        })

    format_xref = instruction_at(pe, blob, decoder, 0x140272BDD)
    require(rip_target(format_xref) == va_for_offset(pe, FORMAT_DESCRIPTOR_OFFSET), "percent wrapper xref mismatch")
    format_argument = instruction_at(pe, blob, decoder, 0x140272BCF)
    require(format_argument.mnemonic == "mov" and format_argument.op_str == "qword ptr [r14], rbx", "escaped keyword format argument mismatch")
    format_call = instruction_at(pe, blob, decoder, 0x140272BF4)
    require(format_call.mnemonic == "call" and immediate(format_call) == FORMAT_STRING, "percent wrapper format call mismatch")
    require(immediate(instruction_at(pe, blob, decoder, 0x140272BF9)) == 4, "keyword parameter clone count mismatch")
    clone_call = instruction_at(pe, blob, decoder, 0x140272C0A)
    require(clone_call.mnemonic == "call" and immediate(clone_call) == STRING_CLONE, "keyword clone call mismatch")
    require(instruction_at(pe, blob, decoder, 0x140272C5B).mnemonic == "dec", "keyword clone loop decrement mismatch")
    require(immediate(instruction_at(pe, blob, decoder, 0x140272C5E)) == 0x140272C04, "keyword clone loop branch mismatch")

    replace_load = instruction_at(pe, blob, decoder, 0x1402949DD)
    replace_store = instruction_at(pe, blob, decoder, 0x1402949E5)
    require("word ptr" in replace_load.op_str and "word ptr" in replace_store.op_str, "two-byte replacement copy mismatch")

    return {
        "schemaVersion": 1,
        "date": "2026-09-08",
        "findingId": "LWB-R6-007",
        "scope": "Map Data literal-substring keyword SQL, escaping and parameter construction",
        "evidenceStatus": "RECOVERED static",
        "reference": {
            "path": str(binary),
            "sha256": digest,
            "coordinateSystem": "raw PE file offsets and preferred-image virtual addresses",
        },
        "locators": {
            "mapSearchFunctionVaRange": ["0x0000000140271864", "0x0000000140276D7B"],
            "sqlFileOffset": f"0x{SQL_OFFSET:08X}",
            "sqlXrefVa": "0x0000000140272AD8",
            "replaceFunctionVa": f"0x{STRING_REPLACE:016X}",
            "formatDescriptorFileOffset": f"0x{FORMAT_DESCRIPTOR_OFFSET:08X}",
            "formatDescriptorXrefVa": "0x0000000140272BDD",
            "formatStringFunctionVa": f"0x{FORMAT_STRING:016X}",
            "cloneLoopVaRange": ["0x0000000140272BF9", "0x0000000140272C60"],
        },
        "result": {
            "predicate": SQL,
            "columns": ["name", "alliance_name", "uuid", "data_json"],
            "collation": "NOCASE",
            "escapeCharacter": "\\",
            "replacementOrder": replacement_report,
            "parameterConstruction": "wrap the fully escaped keyword with one literal % on each side",
            "parameterCopies": 4,
            "semanticMatch": "case-insensitive literal substring across the four columns",
        },
        "analysisTools": {
            "capstone": {
                "version": capstone.__version__,
                "package": "PyPI capstone==5.0.6",
                "location": str(Path(capstone.__file__).resolve()),
            },
            "pefile": {
                "version": pefile.__version__,
                "package": f"PyPI pefile=={pefile.__version__}",
                "location": str(Path(pefile.__file__).resolve()),
            },
        },
        "reproduction": [
            "python -m pip install --user capstone==5.0.6 pefile==2024.8.26",
            "python tools\\inspect_lwbridge_map_keyword.py ..\\LW\\lwbridge-0.3.1.exe --json",
        ],
        "validationAndLimits": {
            "validation": "verified binary hash, exact SQL/static bytes and bounded instruction/data-flow sequence",
            "liveProven": False,
            "limits": "no live scan or native ingestion; this does not resolve other DB-05 filters or sorts",
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=Path)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        result = inspect(args.binary)
    except (InspectError, OSError, pefile.PEFormatError, StopIteration) as exc:
        parser.error(str(exc))
    print(json.dumps(result, indent=2, ensure_ascii=False) if args.json else f"finding={result['findingId']} status={result['evidenceStatus']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
