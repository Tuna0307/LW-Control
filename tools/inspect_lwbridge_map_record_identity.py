#!/usr/bin/env python3
"""Verify LWBridge's original map-record identity normalization branch.

This is a read-only, hash-locked verifier for the immutable LWBridge 0.3.1
reference.  It checks only the fixed native locators recovered for LWB-R6-049;
it does not load or execute the reference binary.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import struct

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86_const import X86_OP_IMM


EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
EXPECTED_IMAGE_BASE = 0x140000000

BUILDER = (0x2785BA, 0x2793BE)
UPSERT = (0x27A1E3, 0x27AA73)
SIGNED_DECIMAL_FORMATTER = (0x285410, 0x285556)
DECIMAL_DIGIT_CORE = (0x394C0, 0x3968E)
POINT_FIELD_TABLE_RVA = 0xC8BC70


class InspectError(ValueError):
    pass


def _runtime_functions(pe: pefile.PE) -> list[tuple[int, int]]:
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


def _containing_function(
    functions: list[tuple[int, int]], rva: int
) -> tuple[int, int] | None:
    for begin, end in functions:
        if begin <= rva < end:
            return begin, end
        if begin > rva:
            break
    return None


def _instruction_map(
    pe: pefile.PE, data: bytes, begin: int, end: int
) -> dict[int, tuple[str, str]]:
    try:
        offset = int(pe.get_offset_from_rva(begin))
    except pefile.PEFormatError as exc:
        raise InspectError(f"RVA 0x{begin:X} is not file-backed") from exc
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    return {
        int(ins.address - int(pe.OPTIONAL_HEADER.ImageBase)): (ins.mnemonic, ins.op_str)
        for ins in md.disasm(data[offset : offset + (end - begin)], int(pe.OPTIONAL_HEADER.ImageBase) + begin)
    }


def _expect(
    instructions: dict[int, tuple[str, str]], rva: int, mnemonic: str, fragment: str
) -> None:
    actual = instructions.get(rva)
    if actual is None:
        raise InspectError(f"missing instruction at RVA 0x{rva:X}")
    if actual[0] != mnemonic or fragment not in actual[1]:
        raise InspectError(
            f"unexpected instruction at RVA 0x{rva:X}: {actual[0]} {actual[1]}"
        )


def _decode_rust_str_table(
    pe: pefile.PE, data: bytes, rva: int, count: int
) -> list[str]:
    image_base = int(pe.OPTIONAL_HEADER.ImageBase)
    offset = int(pe.get_offset_from_rva(rva))
    rows: list[str] = []
    for index in range(count):
        ptr, length = struct.unpack_from("<QQ", data, offset + index * 16)
        string_rva = ptr - image_base
        string_offset = int(pe.get_offset_from_rva(string_rva))
        raw = data[string_offset : string_offset + length]
        rows.append(raw.decode("ascii"))
    return rows


def _direct_callers(
    pe: pefile.PE, data: bytes, target_rva: int
) -> list[dict[str, str]]:
    image_base = int(pe.OPTIONAL_HEADER.ImageBase)
    target_va = image_base + target_rva
    functions = _runtime_functions(pe)
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    rows: list[dict[str, str]] = []
    for section in pe.sections:
        if not (int(section.Characteristics) & 0x20000000):
            continue
        section_rva = int(section.VirtualAddress)
        for ins in md.disasm(section.get_data(), image_base + section_rva):
            if ins.mnemonic != "call" or not ins.operands:
                continue
            operand = ins.operands[0]
            if operand.type != X86_OP_IMM or int(operand.imm) != target_va:
                continue
            call_rva = int(ins.address - image_base)
            function_range = _containing_function(functions, call_rva)
            if function_range is None:
                raise InspectError(f"direct caller RVA 0x{call_rva:X} has no runtime function")
            rows.append(
                {
                    "callRva": f"0x{call_rva:X}",
                    "functionRva": f"0x{function_range[0]:X}-0x{function_range[1]:X}",
                }
            )
    return rows


def inspect(path: Path) -> dict[str, object]:
    data = path.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if digest != EXPECTED_SHA256:
        raise InspectError(
            f"unsupported lwbridge SHA-256 {digest}; expected {EXPECTED_SHA256}"
        )

    pe = pefile.PE(data=data, fast_load=False)
    image_base = int(pe.OPTIONAL_HEADER.ImageBase)
    if image_base != EXPECTED_IMAGE_BASE:
        raise InspectError(f"unexpected image base 0x{image_base:X}")

    functions = _runtime_functions(pe)
    for expected in (BUILDER, UPSERT, SIGNED_DECIMAL_FORMATTER, DECIMAL_DIGIT_CORE):
        if expected not in functions:
            raise InspectError(
                f"expected runtime function 0x{expected[0]:X}-0x{expected[1]:X} is absent"
            )

    builder = _instruction_map(pe, data, *BUILDER)
    formatter = _instruction_map(pe, data, *SIGNED_DECIMAL_FORMATTER)
    decimal_core = _instruction_map(pe, data, *DECIMAL_DIGIT_CORE)

    # Builder argument use and first-available point-index field table.
    _expect(builder, 0x2785F3, "mov", "r14, r9")
    _expect(builder, 0x2785F6, "mov", "rsi, r8")
    _expect(builder, 0x2785F9, "mov", "r12, rdx")
    _expect(builder, 0x278638, "lea", "rbp")
    _expect(builder, 0x27865D, "add", "r13, 0x10")
    _expect(builder, 0x278661, "cmp", "r13, 0x40")
    point_fields = _decode_rust_str_table(pe, data, POINT_FIELD_TABLE_RVA, 4)
    if point_fields != ["pointIndex", "mainIndex", "pointId", "index"]:
        raise InspectError(f"unexpected point-index alias table: {point_fields!r}")

    # The point value used for identity is the extracted value in RDI.  The
    # coordinate fallback separately decrements a copy before tile division.
    _expect(builder, 0x278654, "mov", "rdi, rdx")
    _expect(builder, 0x278698, "mov", "rax, rdi")
    _expect(builder, 0x27869B, "dec", "rax")
    _expect(builder, 0x278C94, "mov", "qword ptr [rdx], rdi")
    _expect(builder, 0x278C9F, "call", "0x140285410")

    # Exact kind overrides and UUID/march fallback decision.
    _expect(builder, 0x278C47, "cmp", "rsi, 7")
    _expect(builder, 0x278C4D, "cmp", "rsi, 5")
    _expect(builder, 0x278C53, "mov", "eax, 0x63757274")  # "truc"
    _expect(builder, 0x278C62, "xor", "ecx, 0x6b")  # "k"
    _expect(builder, 0x278C67, "mov", "eax, 0x6c696172")  # "rail"
    _expect(builder, 0x278C70, "mov", "ecx, 0x7961776c")  # "lway"
    _expect(builder, 0x278C7E, "cmp", "qword ptr [rsp + 0x118], 0")
    _expect(builder, 0x278C89, "test", "r15b, 1")
    _expect(builder, 0x278CA6, "cmp", "qword ptr [rsp + 0xe8], 0")
    _expect(builder, 0x278CB9, "lea", "rdx, [rsp + 0xd8]")
    _expect(builder, 0x278CCB, "mov", "rax, qword ptr [rsp + 0x118]")
    _expect(builder, 0x278CDB, "movupd", "xmm0, xmmword ptr [rsp + 0x108]")

    # The chosen String is copied to normalized record offset +0x78.
    _expect(builder, 0x279214, "mov", "rax, qword ptr [rsp + 0xd0]")
    _expect(builder, 0x27921C, "mov", "qword ptr [rbx + 0x88], rax")
    _expect(builder, 0x279223, "movaps", "xmm0, xmmword ptr [rsp + 0xc0]")
    _expect(builder, 0x27922B, "movups", "xmmword ptr [rbx + 0x78], xmm0")

    # 0x285410 is a signed decimal formatter: it adds '-' for negatives and
    # its digit core uses the canonical 00..99 table plus /10000 and /100 steps.
    _expect(formatter, 0x285423, "test", "r14, r14")
    _expect(formatter, 0x28548F, "mov", "byte ptr [rax], 0x2d")
    _expect(formatter, 0x2854BB, "call", "0x1400394c0")
    _expect(decimal_core, 0x394DC, "cmp", "rcx, 0x3e8")
    _expect(decimal_core, 0x3952B, "imul", "eax, edx, 0x2710")
    _expect(decimal_core, 0x3957C, "imul", "eax, r15d, 0x64")

    callers = _direct_callers(pe, data, UPSERT[0])
    expected_callers = [
        {"callRva": "0x25767E", "functionRva": "0x2574CD-0x2577E2"},
        {"callRva": "0x25F368", "functionRva": "0x25F01D-0x25F5C8"},
        {"callRva": "0x270863", "functionRva": "0x26EC06-0x271056"},
    ]
    if callers != expected_callers:
        raise InspectError(f"unexpected direct upsert caller set: {callers!r}")

    return {
        "findingId": "LWB-R6-049",
        "classification": "RECOVERED static",
        "source": {
            "path": str(path),
            "sha256": digest,
            "imageBase": f"0x{image_base:X}",
        },
        "locators": {
            "normalizedRecordBuilderRva": f"0x{BUILDER[0]:X}-0x{BUILDER[1]:X}",
            "sharedUpsertRva": f"0x{UPSERT[0]:X}-0x{UPSERT[1]:X}",
            "signedDecimalFormatterRva": f"0x{SIGNED_DECIMAL_FORMATTER[0]:X}-0x{SIGNED_DECIMAL_FORMATTER[1]:X}",
            "decimalDigitCoreRva": f"0x{DECIMAL_DIGIT_CORE[0]:X}-0x{DECIMAL_DIGIT_CORE[1]:X}",
            "pointFieldTableRva": f"0x{POINT_FIELD_TABLE_RVA:X}",
            "recordKeyFinalStoreRva": "0x279214-0x27922B",
            "recordKeyDecisionRva": "0x278C47-0x278CEF",
        },
        "directUpsertCallers": callers,
        "pointIndexCandidatesInOrder": point_fields,
        "recordKeyRule": {
            "truckOrRailway": "uuid when nonempty, otherwise marchUuid",
            "otherKindWithNonemptyMarchUuid": "uuid when nonempty, otherwise marchUuid",
            "otherKindWithEmptyMarchUuidAndPointIndex": "signed decimal text of the original extracted point index",
            "otherKindWithoutPointIndex": "uuid when nonempty, otherwise marchUuid",
            "pointCoordinateSeparation": "coordinate fallback decrements a copy of pointIndex; record key formatting receives the original extracted value",
            "destination": "normalized record String at +0x78 with length at +0x88",
        },
        "validationAndLimits": {
            "liveProven": False,
            "publicCapabilityEnabled": False,
            "unknownBlocked": [
                "capture payload producer to this normalized-record builder",
                "typed current-client field normalization and removal behavior",
                "scan scheduling/completion and live bridge readiness",
            ],
            "restriction": "a separate raw string-cluster follow-up was rejected by automatic review and was not replayed or rerouted; it contributed no finding",
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        result = inspect(args.binary)
    except (InspectError, OSError, pefile.PEFormatError, UnicodeDecodeError) as exc:
        parser.error(str(exc))
        return 2
    if args.json:
        print(json.dumps(result, indent=2))
    else:
        print(f"finding={result['findingId']}")
        print(f"classification={result['classification']}")
        source = result["source"]
        print(f"sha256={source['sha256']}")
        print("point_index_candidates=" + ",".join(result["pointIndexCandidatesInOrder"]))
        print("record_key_rule:")
        for key, value in result["recordKeyRule"].items():
            print(f"  {key}: {value}")
        print("direct_upsert_callers:")
        for row in result["directUpsertCallers"]:
            print(f"  {row['callRva']} in {row['functionRva']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
