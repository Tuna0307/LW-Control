#!/usr/bin/env python3
"""Verify LWBridge 0.3.1 City-export save-dialog and cancel-result contract."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import capstone
import pefile

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"

RFD_VERSION = "0.16.0"
RFD_COMMIT = "5d32eec3a7930eb43b7e864eb773831bbd3d91b4"
RFD_FILE_DIALOG_BLOB = "d5c414a87a6f42416b2c63c7b53a5eeaa079ae7e"
RFD_DIALOG_FFI_BLOB = "29e9dd330259db60a88569c64528ef7c5a3b5485"
SERDE_JSON_VERSION = "1.0.151"

BUILDER_HELPER_VA = 0x14034EFD5
ADD_FILTER_VA = 0x14042270B
SAVE_FILE_VA = 0x140422B20
EMPTY_STRING_TO_JSON_VA = 0x1402ADBC0

CALL_ADD_FILTER_VA = 0x14014D9D2
CALL_SAVE_FILE_VA = 0x14014DA4C
NONE_SENTINEL_LOAD_VA = 0x14014DA51
NONE_COMPARE_VA = 0x14014DA5B
NONE_BRANCH_VA = 0x14014DA5E

CANCELED_KEY_RAW = 0x00821F90
PATH_KEY_RAW = 0x00821FB0
EMPTY_PATH_RAW = 0x00821FB8
ROW_COUNT_KEY_RAW = 0x00821FC8
FILTER_NAME_RAW = 0x00821F68
FILTER_EXT_RAW = 0x00821F54

class InspectError(ValueError):
    pass

def require(condition: bool, message: str) -> None:
    if not condition:
        raise InspectError(message)

def disassemble(pe: pefile.PE, blob: bytes, start_rva: int, end_rva: int) -> list[capstone.CsInsn]:
    raw = pe.get_offset_from_rva(start_rva)
    md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
    md.detail = True
    return list(
        md.disasm(
            blob[raw : raw + end_rva - start_rva],
            pe.OPTIONAL_HEADER.ImageBase + start_rva,
        )
    )

def by_va(instructions: list[capstone.CsInsn]) -> dict[int, capstone.CsInsn]:
    return {ins.address: ins for ins in instructions}

def require_insn(
    instructions: dict[int, capstone.CsInsn],
    va: int,
    mnemonic: str,
    contains: str,
) -> None:
    ins = instructions.get(va)
    actual = None if ins is None else f"{ins.mnemonic} {ins.op_str}"
    require(
        ins is not None and ins.mnemonic == mnemonic and contains in ins.op_str,
        f"unexpected instruction at 0x{va:X}: expected {mnemonic} *{contains}*, got {actual}",
    )

def inspect(binary: Path) -> dict[str, object]:
    blob = binary.read_bytes()
    digest = hashlib.sha256(blob).hexdigest()
    require(digest == EXPECTED_SHA256, f"unexpected LWBridge SHA-256: {digest}")

    require(
        b"rfd-0.16.0\\src\\backend\\win_cid\\file_dialog\\dialog_ffi.rs" in blob,
        "rfd 0.16.0 Windows CID provenance string missing",
    )
    require(b"serde_json-1.0.151" in blob, "serde_json 1.0.151 provenance string missing")
    require(blob[FILTER_NAME_RAW : FILTER_NAME_RAW + 14] == b"Excel workbook",
            "City export filter name changed")
    require(blob[FILTER_EXT_RAW : FILTER_EXT_RAW + 4] == b"xlsx",
            "City export extension changed")
    require(blob[CANCELED_KEY_RAW : CANCELED_KEY_RAW + 8] == b"canceled",
            "canceled result key changed")
    require(blob[PATH_KEY_RAW : PATH_KEY_RAW + 4] == b"path",
            "path result key changed")
    require(blob[ROW_COUNT_KEY_RAW : ROW_COUNT_KEY_RAW + 8] == b"rowCount",
            "rowCount result key changed")
    require(
        blob[EMPTY_PATH_RAW : EMPTY_PATH_RAW + 16] ==
        b"\x01\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00",
        "empty path string descriptor changed",
    )

    pe = pefile.PE(data=blob, fast_load=True)
    base = pe.OPTIONAL_HEADER.ImageBase

    builder = by_va(disassemble(pe, blob, 0x34EFD5, 0x34F03F))
    require_insn(builder, 0x14034EFFD, "movabs", "0x8000000000000000")
    require_insn(builder, 0x14034F007, "mov", "[rsi + 0xb0], rax")
    require_insn(builder, 0x14034F00E, "mov", "[rsi + 0xd0], rax")
    require_insn(builder, 0x14034F015, "mov", "[rsi + 0xe8], rax")

    picker_list = disassemble(pe, blob, 0x14D970, 0x14DA64)
    picker = by_va(picker_list)
    require_insn(picker, CALL_ADD_FILTER_VA, "call", hex(ADD_FILTER_VA))
    require_insn(picker, 0x14014D9BF, "lea", "r8")
    require_insn(picker, 0x14014D9C6, "mov", "r9d, 0xe")
    require_insn(picker, 0x14014D9F0, "movups", "[rdi + 0xd0], xmm0")
    require_insn(picker, 0x14014D9FB, "mov", "[rdi + 0xe0], rax")
    require_insn(picker, CALL_SAVE_FILE_VA, "call", hex(SAVE_FILE_VA))
    require_insn(picker, NONE_SENTINEL_LOAD_VA, "movabs", "0x8000000000000001")
    require_insn(picker, NONE_COMPARE_VA, "cmp", "qword ptr [rdi], rax")
    require_insn(picker, NONE_BRANCH_VA, "jne", "0x14014dc47")

    forbidden_writes = (
        "[rdi + 0xb0]",
        "[rdi + 0xb8]",
        "[rdi + 0xc0]",
        "[rdi + 0xe8]",
        "[rdi + 0xf0]",
        "[rdi + 0xf8]",
        "[rbp + 0xb0]",
        "[rbp + 0xe8]",
    )
    for ins in picker_list:
        if ins.mnemonic.startswith("mov") and any(token in ins.op_str for token in forbidden_writes):
            raise InspectError(
                "City picker modifies starting_directory/title after builder initialization: "
                f"0x{ins.address:X} {ins.mnemonic} {ins.op_str}"
            )

    cancel = by_va(disassemble(pe, blob, 0x14DA64, 0x14DBD7))
    require_insn(cancel, 0x14014DA75, "lea", "rdx")
    require_insn(cancel, 0x14014DA9A, "mov", "word ptr [r9], 0x101")
    require_insn(cancel, 0x14014DACA, "lea", "rdx")
    require_insn(cancel, 0x14014DAF6, "call", hex(EMPTY_STRING_TO_JSON_VA))
    require_insn(cancel, 0x14014DB4E, "lea", "rdx")
    require_insn(cancel, 0x14014DB73, "mov", "byte ptr [r9], 2")
    require_insn(cancel, 0x14014DB77, "xorps", "xmm0, xmm0")
    require_insn(cancel, 0x14014DB7A, "movups", "[r9 + 8], xmm0")

    string_converter = by_va(disassemble(pe, blob, 0x2ADBC0, 0x2ADC2F))
    require_insn(string_converter, 0x1402ADC0C, "mov", "byte ptr [rdi], 3")

    return {
        "findingId": "LWB-R7-059",
        "date": "2026-09-19",
        "scope": "original City export rfd save-dialog, default-location and cancellation contract",
        "evidenceStatus": "RECOVERED static + upstream-source corroboration",
        "sourceIdentity": {"path": str(binary), "sha256": digest},
        "upstreamSources": {
            "rfd": {
                "version": RFD_VERSION,
                "tagCommit": RFD_COMMIT,
                "fileDialogImplBlob": RFD_FILE_DIALOG_BLOB,
                "dialogFfiBlob": RFD_DIALOG_FFI_BLOB,
                "fileDialogUrl": (
                    "https://github.com/PolyMeilex/rfd/blob/0.16.0/"
                    "src/backend/win_cid/file_dialog.rs"
                ),
                "dialogFfiUrl": (
                    "https://github.com/PolyMeilex/rfd/blob/0.16.0/"
                    "src/backend/win_cid/file_dialog/dialog_ffi.rs"
                ),
            },
            "serdeJson": {
                "version": SERDE_JSON_VERSION,
                "valueEnumUrl": (
                    "https://github.com/serde-rs/json/blob/v1.0.151/src/value/mod.rs"
                ),
                "variantOrder": ["Null", "Bool", "Number", "String", "Array", "Object"],
            },
            "windows": {
                "commonDialogUrl": (
                    "https://learn.microsoft.com/windows/win32/shell/common-file-dialog"
                ),
                "defaultSaveOptions": [
                    "FOS_OVERWRITEPROMPT",
                    "FOS_NOREADONLYRETURN",
                    "FOS_PATHMUSTEXIST",
                    "FOS_NOCHANGEDIR",
                ],
            },
        },
        "builder": {
            "filter": {"name": "Excel workbook", "extensions": ["xlsx"]},
            "fileName": "formatted R7-058 City default filename",
            "startingDirectory": None,
            "title": None,
            "startingDirectoryMeaning": (
                "no application SetFolder call; Windows common dialog chooses its persisted/"
                "system default location"
            ),
            "filterMeaning": (
                "rfd sets first filter extension as IFileDialog default extension and SetFileTypes"
            ),
        },
        "saveDialog": {
            "api": "rfd::FileDialog::save_file",
            "windowsBackend": "IFileSaveDialog",
            "overwritePrompt": True,
            "overwriteReason": (
                "rfd build_save_file does not SetOptions; Windows Save dialog default options "
                "include FOS_OVERWRITEPROMPT"
            ),
            "rfdErrorBehavior": (
                "FileSaveDialogImpl::save_file calls run(self).ok(); user cancellation and any "
                "build/show/get_result COM error both collapse to Option::None"
            ),
        },
        "noneResult": {
            "canceled": True,
            "path": "",
            "rowCount": 0,
            "decode": (
                "serde_json 1.0.151 Value order: Bool tag=1 with payload=1; Number tag=2 "
                "with zero payload; String tag=3 with empty String descriptor"
            ),
            "dialogErrorDistinguishableFromUserCancel": False,
        },
        "refinement": {
            "previousDialogLabel": (
                "R6-018's 'Excel workbook' dialog-label wording is refined: the callsite "
                "proves it is the file-filter display name; custom dialog title remains unset"
            )
        },
        "restrictions": {
            "replayed": [],
            "preserved": ["SB-08", "SB-09", "SB-99"],
            "note": "R7-059 does not inspect the denied SB-99 formatter-argument range",
        },
    }

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
            "directory=None",
            "cancel={canceled:true,path:'',rowCount:0}",
            "overwritePrompt=true",
        )
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
