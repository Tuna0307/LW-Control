#!/usr/bin/env python3
"""Recover and verify LWBridge 0.3.1 Map Data ordinary-quality mapping."""

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
QUALITY_OFFSET = 0x00C89E59
SPECIAL_UR_GUARD_OFFSET = 0x00C89E60
FORMAT_DESCRIPTOR_OFFSET = 0x00C8B3F7
MAP_SEARCH_RANGE = (0x140271864, 0x140276D7B)
QUALITY_XREF = 0x140273E02
QUALITY_HELPER = 0x14027A129
QUALITY_HELPER_RANGE = (0x14027A129, 0x14027A1E3)
FORMAT_STRING = 0x1400276B0
SPECIAL_UR_GUARD = "COALESCE(CAST(json_extract(data_json,'$.isSpecialURQuality') AS INTEGER),0) = 0"


class InspectError(ValueError):
    pass


def require(condition: bool, message: str) -> None:
    if not condition:
        raise InspectError(message)


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


def va_for_offset(pe: pefile.PE, offset: int) -> int:
    return pe.OPTIONAL_HEADER.ImageBase + pe.get_rva_from_offset(offset)


def require_instruction(pe: pefile.PE, blob: bytes, decoder: Cs, va: int, mnemonic: str, op_str: str) -> None:
    instruction = instruction_at(pe, blob, decoder, va)
    require(
        instruction.mnemonic == mnemonic and instruction.op_str == op_str,
        f"instruction mismatch at 0x{va:016X}: {instruction.mnemonic} {instruction.op_str}",
    )


def inspect(binary: Path) -> dict[str, object]:
    blob = binary.read_bytes()
    digest = hashlib.sha256(blob).hexdigest()
    require(digest == EXPECTED_SHA256, f"unexpected LWBridge SHA-256: {digest}")
    require(blob[QUALITY_OFFSET:QUALITY_OFFSET + 7] == b"quality", "quality field marker mismatch")
    require(
        blob[SPECIAL_UR_GUARD_OFFSET:SPECIAL_UR_GUARD_OFFSET + len(SPECIAL_UR_GUARD)] == SPECIAL_UR_GUARD.encode("ascii"),
        "special-UR exclusion predicate mismatch",
    )
    require(
        blob[FORMAT_DESCRIPTOR_OFFSET:FORMAT_DESCRIPTOR_OFFSET + 7] == b"\xC0\x04 = ?\x00",
        "quality equality format descriptor mismatch",
    )

    pe = pefile.PE(data=blob, fast_load=False)
    pe.parse_data_directories(
        directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]]
    )
    decoder = Cs(CS_ARCH_X86, CS_MODE_64)
    decoder.detail = True

    map_rva = QUALITY_XREF - pe.OPTIONAL_HEADER.ImageBase
    map_function = next(
        entry for entry in pe.DIRECTORY_ENTRY_EXCEPTION
        if entry.struct.BeginAddress <= map_rva < entry.struct.EndAddress
    )
    require(
        (pe.OPTIONAL_HEADER.ImageBase + map_function.struct.BeginAddress,
         pe.OPTIONAL_HEADER.ImageBase + map_function.struct.EndAddress) == MAP_SEARCH_RANGE,
        "map-search runtime-function bounds mismatch",
    )

    quality_xref = instruction_at(pe, blob, decoder, QUALITY_XREF)
    require(
        quality_xref.mnemonic == "lea" and rip_target(quality_xref) == va_for_offset(pe, QUALITY_OFFSET),
        "quality field xref mismatch",
    )
    require_instruction(pe, blob, decoder, 0x140273E09, "mov", "r8d, 7")

    # String-length and selector comparisons.
    require_instruction(pe, blob, decoder, 0x140273E35, "cmp", "rcx, 1")
    require_instruction(pe, blob, decoder, 0x140273E3F, "cmp", "rcx, 3")
    require_instruction(pe, blob, decoder, 0x140273E49, "cmp", "rcx, 2")
    require_instruction(pe, blob, decoder, 0x140273E53, "cmp", "word ptr [rax], 0x7275")  # ur
    require_instruction(pe, blob, decoder, 0x140273E5E, "cmp", "word ptr [rax], 0x7273")  # sr
    require_instruction(pe, blob, decoder, 0x140273F61, "movzx", "ecx, word ptr [rax]")
    require_instruction(pe, blob, decoder, 0x140273F64, "xor", "ecx, 0x7373")             # ss
    require_instruction(pe, blob, decoder, 0x140273F6A, "movzx", "eax, byte ptr [rax + 2]")
    require_instruction(pe, blob, decoder, 0x140273F6E, "xor", "eax, 0x72")              # r
    require_instruction(pe, blob, decoder, 0x140273F91, "cmp", "eax, 0x6e")              # n
    require_instruction(pe, blob, decoder, 0x140273F9A, "cmp", "eax, 0x72")              # r

    selector_bindings = {
        "n": (0x140274027, 1),
        "r": (0x140273FAD, 2),
        "sr": (0x140273E73, 3),
        "ssr": (0x140273F84, 4),
    }
    for selector, (va, value) in selector_bindings.items():
        instruction = instruction_at(pe, blob, decoder, va)
        require(
            instruction.mnemonic == "mov" and instruction.op_str == f"ecx, {value}",
            f"{selector} numeric binding mismatch",
        )
    helper_call = instruction_at(pe, blob, decoder, 0x14027402C)
    require(
        helper_call.mnemonic == "call" and immediate(helper_call) == QUALITY_HELPER,
        "quality equality helper call mismatch",
    )

    # UR is a literal predicate, not an equality parameter.
    ur_prefix = instruction_at(pe, blob, decoder, 0x140273FCD)
    require(
        ur_prefix.mnemonic == "movabs" and immediate(ur_prefix) == 0x207974696C617571,
        "UR quality predicate prefix mismatch",
    )
    ur_suffix = instruction_at(pe, blob, decoder, 0x140273FDA)
    require(
        ur_suffix.mnemonic == "mov" and immediate(ur_suffix) == 0x35203D3E,
        "UR quality predicate suffix mismatch",
    )

    helper_rva = QUALITY_HELPER - pe.OPTIONAL_HEADER.ImageBase
    helper_function = next(
        entry for entry in pe.DIRECTORY_ENTRY_EXCEPTION
        if entry.struct.BeginAddress <= helper_rva < entry.struct.EndAddress
    )
    require(
        (pe.OPTIONAL_HEADER.ImageBase + helper_function.struct.BeginAddress,
         pe.OPTIONAL_HEADER.ImageBase + helper_function.struct.EndAddress) == QUALITY_HELPER_RANGE,
        "quality helper bounds mismatch",
    )
    helper_quality = instruction_at(pe, blob, decoder, 0x14027A13B)
    require(
        rip_target(helper_quality) == va_for_offset(pe, QUALITY_OFFSET),
        "quality helper field xref mismatch",
    )
    require_instruction(pe, blob, decoder, 0x14027A14A, "mov", "qword ptr [rcx + 8], 7")
    helper_format = instruction_at(pe, blob, decoder, 0x14027A165)
    require(
        rip_target(helper_format) == va_for_offset(pe, FORMAT_DESCRIPTOR_OFFSET),
        "quality helper format descriptor xref mismatch",
    )
    helper_format_call = instruction_at(pe, blob, decoder, 0x14027A171)
    require(
        helper_format_call.mnemonic == "call" and immediate(helper_format_call) == FORMAT_STRING,
        "quality helper format call mismatch",
    )
    require_instruction(pe, blob, decoder, 0x14027A1C5, "mov", "qword ptr [rax + rcx], 1")
    require_instruction(pe, blob, decoder, 0x14027A1CD, "mov", "qword ptr [rax + rcx + 8], rdi")

    # The special-UR exclusion is narrower than byte adjacency alone suggests:
    # it is gated by kind == "truck" and selector == "ur".
    require_instruction(pe, blob, decoder, 0x140274033, "cmp", "r15, 5")
    require_instruction(pe, blob, decoder, 0x14027403D, "mov", "r14d, 0x63757274")  # truc
    require_instruction(pe, blob, decoder, 0x140274050, "xor", "ecx, 0x6b")         # k
    require_instruction(pe, blob, decoder, 0x140274061, "cmp", "byte ptr [r13], 3")
    require_instruction(pe, blob, decoder, 0x140274068, "cmp", "qword ptr [r13 + 0x18], 2")
    require_instruction(pe, blob, decoder, 0x140274073, "cmp", "word ptr [rax], 0x7275")
    special_guard_xref = instruction_at(pe, blob, decoder, 0x140274093)
    require(
        rip_target(special_guard_xref) == va_for_offset(pe, SPECIAL_UR_GUARD_OFFSET),
        "truck UR special-quality exclusion xref mismatch",
    )
    require_instruction(pe, blob, decoder, 0x14027409A, "mov", "esi, 0x4f")

    return {
        "schemaVersion": 1,
        "date": "2026-09-08",
        "findingId": "LWB-R6-013",
        "scope": "Map Data ordinary quality selector mapping and truck UR exclusion",
        "evidenceStatus": "RECOVERED static",
        "reference": {
            "path": str(binary),
            "sha256": digest,
            "coordinateSystem": "raw PE file offsets and preferred-image virtual addresses",
        },
        "locators": {
            "mapSearchFunctionVaRange": [f"0x{MAP_SEARCH_RANGE[0]:016X}", f"0x{MAP_SEARCH_RANGE[1]:016X}"],
            "qualityFieldFileOffset": f"0x{QUALITY_OFFSET:08X}",
            "qualityFieldXrefVa": f"0x{QUALITY_XREF:016X}",
            "qualityHelperVaRange": [f"0x{QUALITY_HELPER_RANGE[0]:016X}", f"0x{QUALITY_HELPER_RANGE[1]:016X}"],
            "truckUrGuardXrefVa": "0x0000000140274093",
            "specialUrGuardFileOffset": f"0x{SPECIAL_UR_GUARD_OFFSET:08X}",
        },
        "result": {
            "frontendStringSelectors": {
                "n": {"predicate": "quality = ?", "parameter": 1},
                "r": {"predicate": "quality = ?", "parameter": 2},
                "sr": {"predicate": "quality = ?", "parameter": 3},
                "ssr": {"predicate": "quality = ?", "parameter": 4},
                "ur": {"predicate": "quality >= 5", "parameter": None},
            },
            "truckUrAdditionalPredicate": SPECIAL_UR_GUARD,
            "truckUrMeaning": "ordinary truck UR excludes rows with nonzero isSpecialURQuality",
            "numericBackendBranch": "the original backend also accepts a numeric quality value and binds equality, but the recovered frontend emits selector strings",
        },
        "analysisTools": {
            "capstone": {"version": capstone.__version__, "package": "PyPI capstone==5.0.6"},
            "pefile": {"version": pefile.__version__, "package": f"PyPI pefile=={pefile.__version__}"},
        },
        "reproduction": [
            "python tools\\inspect_lwbridge_map_quality.py ..\\LW\\lwbridge-0.3.1.exe --json",
        ],
        "validationAndLimits": {
            "validation": "verified binary hash, selector comparisons, helper parameter binding, literal UR SQL and truck+UR guard xref",
            "liveProven": False,
            "limits": "no live query or ingestion proof; production implementation should expose the recovered frontend string forms and keep unrelated DB-05 filters gated",
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
