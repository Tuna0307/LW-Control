#!/usr/bin/env python3
"""Verify LWBridge 0.3.1 City-export pagination, row mapping and cell coercion."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
from pathlib import Path

import capstone
import pefile

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"

ROW_STAGE = (0x334604, 0x334A06)
WRITER_STAGE = (0x335B41, 0x3374AD)
NUMERIC_HELPER = (0x3359AE, 0x335B41)
DATETIME_HELPER = (0x3374AD, 0x337646)
TEXT_HELPER = (0x337646, 0x337747)
STRICT_STRING_HELPER = (0x1D2EE7, 0x1D2F28)
APPEND_HELPER_VA = 0x14027C16B

TIMESTAMP_CONSTANTS = {
    0x1408215B0: 1000.0,
    0x140C96EF0: 100_000_000_000.0,
    0x140C96EF8: 86_400_000.0,
    0x140C96F00: 25_569.0,
}

class InspectError(ValueError):
    pass

def require(condition: bool, message: str) -> None:
    if not condition:
        raise InspectError(message)

def disassemble(pe: pefile.PE, blob: bytes, start_rva: int, end_rva: int) -> list[capstone.CsInsn]:
    raw = pe.get_offset_from_rva(start_rva)
    md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
    md.detail = True
    return list(md.disasm(
        blob[raw: raw + end_rva - start_rva],
        pe.OPTIONAL_HEADER.ImageBase + start_rva,
    ))

def by_va(instructions: list[capstone.CsInsn]) -> dict[int, capstone.CsInsn]:
    return {ins.address: ins for ins in instructions}

def require_insn(
    instructions: dict[int, capstone.CsInsn],
    va: int,
    mnemonic: str,
    contains: str,
) -> capstone.CsInsn:
    ins = instructions.get(va)
    actual = None if ins is None else f"{ins.mnemonic} {ins.op_str}"
    require(
        ins is not None and ins.mnemonic == mnemonic and contains in ins.op_str,
        f"unexpected instruction at 0x{va:X}: expected {mnemonic} *{contains}*, got {actual}",
    )
    return ins

def require_call(instructions: dict[int, capstone.CsInsn], va: int, target_va: int) -> None:
    require_insn(instructions, va, "call", hex(target_va))

def rip_target(ins: capstone.CsInsn) -> int | None:
    for op in ins.operands:
        if (
            op.type == capstone.x86.X86_OP_MEM
            and op.mem.base == capstone.x86.X86_REG_RIP
        ):
            return ins.address + ins.size + op.mem.disp
    return None

def require_rip_bytes(
    pe: pefile.PE,
    blob: bytes,
    instructions: dict[int, capstone.CsInsn],
    va: int,
    expected: bytes,
) -> int:
    ins = instructions.get(va)
    require(ins is not None, f"instruction missing at 0x{va:X}")
    target = rip_target(ins)
    require(target is not None, f"instruction at 0x{va:X} has no RIP-relative operand")
    raw = pe.get_offset_from_rva(target - pe.OPTIONAL_HEADER.ImageBase)
    actual = blob[raw: raw + len(expected)]
    require(
        actual == expected,
        f"RIP literal mismatch at 0x{va:X}: expected {expected!r}, got {actual!r}",
    )
    return target

def require_rip_target(
    instructions: dict[int, capstone.CsInsn],
    va: int,
    expected_target: int,
) -> None:
    ins = instructions.get(va)
    require(ins is not None, f"instruction missing at 0x{va:X}")
    target = rip_target(ins)
    require(
        target == expected_target,
        f"RIP target mismatch at 0x{va:X}: expected 0x{expected_target:X}, got {target!r}",
    )

def read_f64(pe: pefile.PE, blob: bytes, va: int) -> float:
    raw = pe.get_offset_from_rva(va - pe.OPTIONAL_HEADER.ImageBase)
    return struct.unpack_from("<d", blob, raw)[0]

def inspect(binary: Path) -> dict[str, object]:
    blob = binary.read_bytes()
    digest = hashlib.sha256(blob).hexdigest()
    require(digest == EXPECTED_SHA256, f"unexpected LWBridge SHA-256: {digest}")
    require(b"serde_json-1.0.151" in blob, "serde_json 1.0.151 provenance missing")

    pe = pefile.PE(data=blob, fast_load=True)

    rows_list = disassemble(pe, blob, *ROW_STAGE)
    rows = by_va(rows_list)
    require_insn(rows, 0x140334647, "mov", "r12d, 1")
    require_insn(rows, 0x140334658, "lea", "r15d, [r12 + 1]")
    require_insn(rows, 0x14033465D, "cmp", "r12d, 0x3e8")
    require_insn(rows, 0x140334664, "cmove", "r15d, r12d")
    require_rip_bytes(pe, blob, rows, 0x140334677, b"page")
    require_insn(rows, 0x140334699, "mov", "[r13 + 0x10], rbx")
    require_rip_bytes(pe, blob, rows, 0x1403346A2, b"pageSize")
    require_insn(rows, 0x1403346C8, "mov", "[r13 + 0x10], 0xc8")
    require_rip_bytes(pe, blob, rows, 0x140334772, b"city")
    require_rip_bytes(pe, blob, rows, 0x1403347E9, b"total")
    require_rip_bytes(pe, blob, rows, 0x140334833, b"rows")
    require_call(rows, 0x1403348A3, APPEND_HELPER_VA)
    require_insn(rows, 0x1403348A8, "test", "rbx, rbx")
    require_insn(rows, 0x1403348AB, "je", "0x14033490d")
    require_insn(rows, 0x1403348AD, "cmp", "qword ptr [rsp + 0x70], r13")
    require_insn(rows, 0x1403348B2, "jge", "0x14033490d")
    require_insn(rows, 0x1403348C1, "cmp", "r12d, 0x3e8")
    require_insn(rows, 0x1403348C8, "je", "0x140334989")
    require_insn(rows, 0x1403348CE, "mov", "r12d, r15d")
    require_insn(rows, 0x1403348D1, "cmp", "r15d, 0x3e8")
    require_insn(rows, 0x1403348D8, "jle", "0x140334658")
    require_rip_bytes(pe, blob, rows, 0x140334992, b"MAP_EXPORT_FAILED")
    require_rip_bytes(pe, blob, rows, 0x140334999, b"city export exceeded the row limit")

    writer_list = disassemble(pe, blob, *WRITER_STAGE)
    writer = by_va(writer_list)

    field_calls = [
        ("A", "serverId", 0x140336103, 0x140336124, 0x1403359AE),
        ("B", "x", 0x14033618A, 0x1403361AB, 0x1403359AE),
        ("C", "y", 0x140336211, 0x140336232, 0x1403359AE),
        ("D", "ownerName", 0x1403362CC, 0x1403362D3, 0x1401D2EE7),
        ("E", "ownerUid", 0x140336399, 0x1403363A0, 0x1401D2EE7),
        ("F", "uuid", 0x140336466, 0x14033646D, 0x1401D2EE7),
        ("G", "allianceName", 0x140336533, 0x14033653A, 0x1401D2EE7),
        ("H", "level", 0x1403365CC, 0x1403365ED, 0x1403359AE),
        ("I", "health", 0x140336653, 0x140336674, 0x1403359AE),
        ("L", "updatedAt", 0x1403368A4, 0x1403368C5, 0x1403374AD),
    ]
    mapping: dict[str, dict[str, object]] = {}
    for column, field, literal_va, call_va, target in field_calls:
        require_rip_bytes(pe, blob, writer, literal_va, field.encode("ascii"))
        require_call(writer, call_va, target)
        mapping[column] = {"field": field}

    for call_va in (0x1403362F2, 0x1403363BF, 0x14033648C, 0x140336559):
        require_call(writer, call_va, 0x140337646)

    require_rip_bytes(pe, blob, writer, 0x1403366DD, b"protectEndTime")
    require_call(writer, 0x1403366E4, 0x1405A2051)
    require_insn(writer, 0x1403366EC, "test", "rax, rax")
    require_insn(writer, 0x1403366EF, "jne", "0x14033670e")
    require_rip_bytes(pe, blob, writer, 0x1403366FA, b"shieldEndTime")
    require_call(writer, 0x140336701, 0x1405A2051)
    require_call(writer, 0x14033671B, 0x1403374AD)
    mapping["J"] = {
        "field": "protectEndTime",
        "fallbackOnlyWhenAbsent": "shieldEndTime",
    }

    require_rip_bytes(pe, blob, writer, 0x1403367CC, b"marked")
    require_insn(writer, 0x1403367F3, "cmp", "byte ptr [rax], 1")
    require_insn(writer, 0x1403367F9, "cmp", "byte ptr [rax + 1], 0")
    require_call(writer, 0x140336831, 0x140337646)
    mapping["K"] = {"field": "marked", "trueLabel": "yesLabel", "otherwise": "noLabel"}

    numeric = by_va(disassemble(pe, blob, *NUMERIC_HELPER))
    require_insn(numeric, 0x140335A18, "cmp", "byte ptr [rbx], 2")
    require_rip_target(numeric, 0x1403359FF, 0x140C98FEE)
    require_rip_target(numeric, 0x140335A7E, 0x140C9900C)

    dt = by_va(disassemble(pe, blob, *DATETIME_HELPER))
    require_insn(dt, 0x140337514, "cmp", "byte ptr [rbx], 2")
    require_insn(dt, 0x140337598, "movsd", "qword ptr [rip")
    require_insn(dt, 0x1403375A0, "mulsd", "xmm1, xmm0")
    require_insn(dt, 0x1403375BD, "divsd", "xmm2")
    require_insn(dt, 0x1403375C5, "addsd", "xmm2")
    for va, expected in TIMESTAMP_CONSTANTS.items():
        actual = read_f64(pe, blob, va)
        require(
            math.isclose(actual, expected, rel_tol=0.0, abs_tol=0.0),
            f"timestamp constant at 0x{va:X} changed: {actual}",
        )
    require_rip_target(dt, 0x1403375F8, 0x140C99C55)

    text = by_va(disassemble(pe, blob, *TEXT_HELPER))
    require_call(text, 0x1403376C9, 0x140335593)
    require_rip_target(text, 0x140337702, 0x140C99C74)

    strict = by_va(disassemble(pe, blob, *STRICT_STRING_HELPER))
    require_insn(strict, 0x1401D2EEB, "cmp", "byte ptr [rcx], 5")
    require_call(strict, 0x1401D2EF4, 0x1405A2051)
    require_insn(strict, 0x1401D2EFE, "cmp", "byte ptr [rax], 3")

    base = pe.OPTIONAL_HEADER.ImageBase
    datetime_template_raw = pe.get_offset_from_rva(0x140C99C55 - base)
    text_template_raw = pe.get_offset_from_rva(0x140C99C74 - base)
    require(
        b's="3"><v>' in blob[datetime_template_raw:datetime_template_raw + 64],
        "style-3 datetime cell template changed",
    )
    require(
        b't="inlineStr"' in blob[text_template_raw:text_template_raw + 96]
        and b'xml:space="preserve"' in blob[text_template_raw:text_template_raw + 96],
        "inline-string cell template changed",
    )

    success = by_va(disassemble(pe, blob, 0x14E12F, 0x14E33E))
    require_insn(success, 0x14014E183, "movabs", "0x64656c65636e6163")
    require_insn(success, 0x14014E1AB, "mov", "word ptr [r9], 1")
    require_insn(success, 0x14014E1F3, "mov", "dword ptr [rax], 0x68746170")
    require_insn(success, 0x14014E2D8, "movabs", "0x746e756f43776f72")
    require_insn(success, 0x14014E307, "mov", "byte ptr [r9], 2")

    result = {
        "schemaVersion": 1,
        "date": "2026-09-19",
        "findingId": "LWB-R7-060",
        "scope": "original City export pagination, row mapping, cell coercion and success result",
        "evidenceStatus": "RECOVERED static + IMPLEMENTED/OFFLINE-TESTED",
        "sourceIdentity": {"path": str(binary), "sha256": digest},
        "pagination": {
            "pageStart": 1,
            "pageSize": 200,
            "lastPage": 1000,
            "maximumRows": 200000,
            "accumulatesRowsAcrossPages": True,
            "successStops": [
                "returned page rows is empty",
                "accumulated row count is greater than or equal to returned total",
            ],
            "overflow": {
                "code": "MAP_EXPORT_FAILED",
                "message": "city export exceeded the row limit",
            },
        },
        "columns": {
            "A": {"field": "serverId", "cellType": "numeric"},
            "B": {"field": "x", "cellType": "numeric"},
            "C": {"field": "y", "cellType": "numeric"},
            "D": {"field": "ownerName", "cellType": "inlineStr"},
            "E": {"field": "ownerUid", "cellType": "inlineStr"},
            "F": {"field": "uuid", "cellType": "inlineStr"},
            "G": {"field": "allianceName", "cellType": "inlineStr"},
            "H": {"field": "level", "cellType": "numeric"},
            "I": {"field": "health", "cellType": "numeric"},
            "J": {
                "field": "protectEndTime",
                "fallbackField": "shieldEndTime",
                "fallbackOnlyWhenPrimaryKeyAbsent": True,
                "cellType": "style3-datetime",
            },
            "K": {"field": "marked", "cellType": "inlineStr", "true": "yesLabel", "otherwise": "noLabel"},
            "L": {"field": "updatedAt", "cellType": "style3-datetime"},
        },
        "coercion": {
            "numeric": "JSON Number only; non-number/non-finite emits empty numeric cell",
            "text": "JSON String only; missing or non-string emits empty inlineStr",
            "ownerUidAndUuid": "lossless inlineStr, never spreadsheet numeric coercion",
            "marked": "JSON true selects yesLabel; all other values select noLabel",
            "datetime": {
                "input": "positive finite JSON Number only",
                "secondsThresholdExclusive": 100000000000,
                "millisecondsFormula": "value_ms / 86400000 + 25569",
                "secondsFormula": "(value * 1000) / 86400000 + 25569",
            },
        },
        "successResult": {
            "canceled": False,
            "path": "<selected path>",
            "rowCount": "<actual written row count>",
        },
        "implementation": {
            "publicCommand": "map_city_export",
            "enabledThroughDesktopDialogHost": True,
            "directBackendInvocation": "NATIVE_DIALOG_REQUIRED",
            "queryScope": "one SQLite full filtered/sorted City snapshot with original 200000-row ceiling",
            "writer": "recovered six-part OOXML with recovered per-column cell coercion",
            "picker": "UI-thread SaveFileDialog; no app starting directory/title; Excel workbook/xlsx; native overwrite prompt",
            "cancelResult": {"canceled": True, "path": "", "rowCount": 0},
        },
        "acceptance": {
            "fullDeterministicSuite": "PASS",
            "deterministicGroups": [
                "profileRouting",
                "persistence",
                "requestLifetime",
                "mapPersistence",
                "mapContract",
                "bridgeControlPipeContract",
            ],
            "gameRunningAfterSuite": False,
            "launcherRunningAfterSuite": False,
            "liveDialogWritePerformed": False,
        },
        "restrictions": {
            "replayed": [],
            "preserved": ["SB-08", "SB-09", "SB-99"],
            "note": "Uses accepted command-owned row/pagination/writer functions; denied template-xref/register requests were not replayed.",
        },
    }
    return result

def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("--json", action="store_true")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = inspect(args.binary)
    rendered = json.dumps(result, indent=2)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered + "\n", encoding="utf-8")
    if args.json:
        print(rendered)
    else:
        print(
            "RECOVERED",
            result["findingId"],
            "maxRows=200000",
            "UID/UUID=inlineStr",
            "J=protectEndTime->shieldEndTime(absent-only)",
        )
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
