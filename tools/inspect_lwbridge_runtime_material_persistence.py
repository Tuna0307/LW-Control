#!/usr/bin/env python3
"""Verify LWBridge package-key envelope persistence and cleanup.

This inspector is build-specific, hash-locked, and read-only. It verifies the
recovered host path from the parsed packageKeyEnvelope response field to the
runtime package-key.envelope file, the write/finalize primitives, and the
invalid-or-expired cleanup branch. It does not decrypt bridge-scripts.dat or
read user authorization material.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP


EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
EXPECTED_IMAGE_BASE = 0x140000000

AUTH_RESPONSE_FUNCTION = (0x14023C7EC, 0x14023CB51)
PERSIST_FUNCTION = (0x14023D8FC, 0x14023DB02)
WRITE_DISPATCH_FUNCTION = (0x140037A58, 0x140037B78)
NTWRITE_FUNCTION = (0x1405BB570, 0x1405BB65F)
FINALIZE_FUNCTION = (0x14027D760, 0x14027D79C)
MOVE_PRIMITIVE_FUNCTION = (0x1405B6BC0, 0x1405B6EFD)
DELETE_WRAPPER_FUNCTION = (0x1405B6540, 0x1405B65E6)
DELETE_PRIMITIVE_FUNCTION = (0x1405B8C00, 0x1405B8D25)

FORMATTER = 0x1400276B0
PERSIST_HELPER = 0x14023D8FC
WRITE_DISPATCH = 0x140037A58
NTWRITE_WRAPPER = 0x1405BB570
FINALIZE_WRAPPER = 0x14027D760
MOVE_PRIMITIVE = 0x1405B6BC0
DELETE_WRAPPER = 0x1405B6540
DELETE_PRIMITIVE = 0x1405B8C00
FORMAT_DESCRIPTOR_VA = 0x140C80070
FORMAT_DESCRIPTOR = bytes.fromhex("c0010a00")


class InspectError(ValueError):
    pass


def require(condition: bool, message: str) -> None:
    if not condition:
        raise InspectError(message)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def instruction_map(
    pe: pefile.PE, data: bytes, image_base: int, begin_va: int, end_va: int
) -> dict[int, Any]:
    offset = int(pe.get_offset_from_rva(begin_va - image_base))
    decoder = Cs(CS_ARCH_X86, CS_MODE_64)
    decoder.detail = True
    return {
        instruction.address: instruction
        for instruction in decoder.disasm(data[offset : offset + end_va - begin_va], begin_va)
    }


def instruction(instructions: dict[int, Any], va: int, mnemonic: str | None = None) -> Any:
    result = instructions.get(va)
    if result is None:
        raise InspectError(f"missing instruction at preferred VA 0x{va:X}")
    if mnemonic is not None and result.mnemonic != mnemonic:
        raise InspectError(
            f"unexpected instruction at 0x{va:X}: {result.mnemonic} {result.op_str}; expected {mnemonic}"
        )
    return result


def immediate(ins: Any) -> int | None:
    for operand in ins.operands:
        if operand.type == X86_OP_IMM:
            return int(operand.imm)
    return None


def rip_target(ins: Any) -> int | None:
    for operand in ins.operands:
        if operand.type == X86_OP_MEM and operand.mem.base == X86_REG_RIP:
            return ins.address + ins.size + operand.mem.disp
    return None


def expect_instruction(
    instructions: dict[int, Any], va: int, mnemonic: str, op_str: str
) -> None:
    ins = instruction(instructions, va, mnemonic)
    require(ins.op_str == op_str, f"instruction mismatch at 0x{va:X}: {ins.mnemonic} {ins.op_str}")


def expect_call(instructions: dict[int, Any], va: int, target: int) -> None:
    ins = instruction(instructions, va, "call")
    require(
        immediate(ins) == target,
        f"call target mismatch at 0x{va:X}: {immediate(ins)!r}; expected 0x{target:X}",
    )


def imports_by_iat(pe: pefile.PE) -> dict[int, tuple[str, str]]:
    result: dict[int, tuple[str, str]] = {}
    for descriptor in pe.DIRECTORY_ENTRY_IMPORT:
        dll = descriptor.dll.decode(errors="replace")
        for imported in descriptor.imports:
            if not imported.address:
                continue
            name = imported.name.decode(errors="replace") if imported.name else f"ord{imported.ordinal}"
            result[int(imported.address)] = (dll, name)
    return result


def expect_import_call(
    instructions: dict[int, Any], va: int, imports: dict[int, tuple[str, str]], name: str
) -> tuple[int, str]:
    ins = instruction(instructions, va, "call")
    target = rip_target(ins)
    require(target is not None, f"import call at 0x{va:X} is not RIP-relative")
    imported = imports.get(target)
    require(imported is not None, f"IAT target 0x{target:X} at 0x{va:X} is not imported")
    dll, actual = imported
    require(actual == name, f"import mismatch at 0x{va:X}: {dll}!{actual}; expected {name}")
    return target, dll


def runtime_functions(pe: pefile.PE, image_base: int) -> set[tuple[int, int]]:
    pe.parse_data_directories(
        directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]]
    )
    return {
        (image_base + int(entry.struct.BeginAddress), image_base + int(entry.struct.EndAddress))
        for entry in getattr(pe, "DIRECTORY_ENTRY_EXCEPTION", [])
        if int(entry.struct.BeginAddress) < int(entry.struct.EndAddress)
    }


def inspect(path: Path) -> dict[str, Any]:
    data = path.read_bytes()
    digest = sha256(data)
    require(digest == EXPECTED_SHA256, f"unsupported lwbridge SHA-256 {digest}")

    pe = pefile.PE(data=data, fast_load=False)
    image_base = int(pe.OPTIONAL_HEADER.ImageBase)
    require(image_base == EXPECTED_IMAGE_BASE, f"unexpected image base 0x{image_base:X}")
    functions = runtime_functions(pe, image_base)
    for bounds in (
        AUTH_RESPONSE_FUNCTION,
        PERSIST_FUNCTION,
        WRITE_DISPATCH_FUNCTION,
        NTWRITE_FUNCTION,
        FINALIZE_FUNCTION,
        MOVE_PRIMITIVE_FUNCTION,
        DELETE_WRAPPER_FUNCTION,
        DELETE_PRIMITIVE_FUNCTION,
    ):
        require(bounds in functions, f"runtime-function bounds missing: 0x{bounds[0]:X}-0x{bounds[1]:X}")

    imports = imports_by_iat(pe)
    auth = instruction_map(pe, data, image_base, *AUTH_RESPONSE_FUNCTION)
    expect_instruction(auth, 0x14023C9CE, "mov", "rbx, qword ptr [rdi + 0x1a0]")
    expect_instruction(auth, 0x14023C9D5, "mov", "r15, qword ptr [rdi + 0x1a8]")
    descriptor_ref = instruction(auth, 0x14023C9EF, "lea")
    require(rip_target(descriptor_ref) == FORMAT_DESCRIPTOR_VA, "envelope formatter descriptor xref mismatch")
    descriptor_offset = int(pe.get_offset_from_rva(FORMAT_DESCRIPTOR_VA - image_base))
    require(
        data[descriptor_offset : descriptor_offset + len(FORMAT_DESCRIPTOR)] == FORMAT_DESCRIPTOR,
        "envelope formatter descriptor bytes mismatch",
    )
    expect_call(auth, 0x14023C9FE, FORMATTER)
    expect_instruction(auth, 0x14023CA0B, "mov", "rcx, rbx")
    expect_instruction(auth, 0x14023CA0E, "mov", "rdx, r15")
    expect_call(auth, 0x14023CA11, PERSIST_HELPER)

    expect_instruction(auth, 0x14023CA49, "mov", "rcx, qword ptr [rdi + 0x1a0]")
    expect_instruction(auth, 0x14023CA50, "mov", "rdx, qword ptr [rdi + 0x1a8]")
    expect_call(auth, 0x14023CA57, DELETE_WRAPPER)

    persist = instruction_map(pe, data, image_base, *PERSIST_FUNCTION)
    expect_call(persist, 0x14023DA6B, WRITE_DISPATCH)
    expect_call(persist, 0x14023DAF8, FINALIZE_WRAPPER)

    write_dispatch = instruction_map(pe, data, image_base, *WRITE_DISPATCH_FUNCTION)
    expect_call(write_dispatch, 0x140037A94, NTWRITE_WRAPPER)

    ntwrite = instruction_map(pe, data, image_base, *NTWRITE_FUNCTION)
    ntwrite_iat, ntwrite_dll = expect_import_call(ntwrite, 0x1405BB5DA, imports, "NtWriteFile")

    finalize = instruction_map(pe, data, image_base, *FINALIZE_FUNCTION)
    expect_call(finalize, 0x14027D777, MOVE_PRIMITIVE)

    move_primitive = instruction_map(pe, data, image_base, *MOVE_PRIMITIVE_FUNCTION)
    move_iat, move_dll = expect_import_call(move_primitive, 0x1405B6CDE, imports, "MoveFileExW")
    move_flag = next(
        (
            ins.address
            for ins in move_primitive.values()
            if 0x1405B6CBE <= ins.address < 0x1405B6CDE
            and ins.mnemonic == "mov"
            and ins.op_str == "r8d, 1"
        ),
        None,
    )
    require(move_flag is not None, "MoveFileExW replace-existing flag setup was not found")

    delete_wrapper = instruction_map(pe, data, image_base, *DELETE_WRAPPER_FUNCTION)
    expect_call(delete_wrapper, 0x1405B65BD, DELETE_PRIMITIVE)
    delete_primitive = instruction_map(pe, data, image_base, *DELETE_PRIMITIVE_FUNCTION)
    delete_iat, delete_dll = expect_import_call(delete_primitive, 0x1405B8C17, imports, "DeleteFileW")

    return {
        "findingId": "LWB-R6-040",
        "date": "2026-09-09",
        "scope": "PM7-B parsed package-key envelope persistence and invalid cleanup",
        "evidenceStatus": "RECOVERED",
        "sourceIdentity": {
            "artifact": "../LW/lwbridge-0.3.1.exe",
            "sha256": digest,
            "preferredImageBase": f"0x{image_base:X}",
            "coordinateSystem": "preferred virtual addresses (VA)",
        },
        "parsedEnvelopePersistence": {
            "authResponseFunctionPreferredVa": f"0x{AUTH_RESPONSE_FUNCTION[0]:X}-0x{AUTH_RESPONSE_FUNCTION[1]:X}",
            "runtimePathDataPreferredVa": "0x14023C9CE",
            "runtimePathLengthPreferredVa": "0x14023C9D5",
            "runtimePathServiceOffsets": ["0x1A0", "0x1A8"],
            "formatterDescriptorPreferredVa": f"0x{FORMAT_DESCRIPTOR_VA:X}",
            "formatterDescriptorHex": FORMAT_DESCRIPTOR.hex(),
            "formatterCallPreferredVa": "0x14023C9FE",
            "formatterPreferredVa": f"0x{FORMATTER:X}",
            "persistCallPreferredVa": "0x14023CA11",
            "persistHelperPreferredVa": f"0x{PERSIST_HELPER:X}",
            "serializedContent": "packageKeyEnvelope response string followed by one LF byte",
        },
        "writePath": {
            "persistFunctionPreferredVa": f"0x{PERSIST_FUNCTION[0]:X}-0x{PERSIST_FUNCTION[1]:X}",
            "contentDispatchCallPreferredVa": "0x14023DA6B",
            "contentDispatchPreferredVa": f"0x{WRITE_DISPATCH:X}",
            "ntWriteWrapperCallPreferredVa": "0x140037A94",
            "ntWriteWrapperPreferredVa": f"0x{NTWRITE_WRAPPER:X}",
            "ntWriteFileCallPreferredVa": "0x1405BB5DA",
            "ntWriteFileImportIatPreferredVa": f"0x{ntwrite_iat:X}",
            "ntWriteFileImport": f"{ntwrite_dll}!NtWriteFile",
            "finalizeCallPreferredVa": "0x14023DAF8",
            "finalizeWrapperPreferredVa": f"0x{FINALIZE_WRAPPER:X}",
            "movePrimitiveCallPreferredVa": "0x14027D777",
            "movePrimitivePreferredVa": f"0x{MOVE_PRIMITIVE:X}",
            "moveFileExCallPreferredVa": "0x1405B6CDE",
            "moveFileExImportIatPreferredVa": f"0x{move_iat:X}",
            "moveFileExImport": f"{move_dll}!MoveFileExW",
            "moveFileExFlagPreferredVa": f"0x{move_flag:X}",
            "moveFileExFlags": 1,
            "moveFileExFlagMeaning": "MOVEFILE_REPLACE_EXISTING",
        },
        "invalidOrExpiredCleanup": {
            "runtimePathDataPreferredVa": "0x14023CA49",
            "runtimePathLengthPreferredVa": "0x14023CA50",
            "deleteWrapperCallPreferredVa": "0x14023CA57",
            "deleteWrapperPreferredVa": f"0x{DELETE_WRAPPER:X}",
            "deletePrimitiveCallPreferredVa": "0x1405B65BD",
            "deletePrimitivePreferredVa": f"0x{DELETE_PRIMITIVE:X}",
            "deleteFileCallPreferredVa": "0x1405B8C17",
            "deleteFileImportIatPreferredVa": f"0x{delete_iat:X}",
            "deleteFileImport": f"{delete_dll}!DeleteFileW",
            "resultCode": "KEY_ENVELOPE_INVALID",
        },
        "result": [
            "The packageKeyEnvelope success branch loads the package-key.envelope runtime path from the auth-service state, formats the parsed response field with the verified one-argument-plus-LF descriptor, and passes that path and serialized content to the shared persistence helper.",
            "The persistence helper writes through a content-dispatch chain that reaches ntdll!NtWriteFile, then finalizes through MoveFileExW with MOVEFILE_REPLACE_EXISTING.",
            "The invalid/expired branch loads the same runtime path, reaches kernel32!DeleteFileW, and returns KEY_ENVELOPE_INVALID.",
        ],
        "reproduction": [
            "python tools\\inspect_lwbridge_runtime_material_persistence.py ..\\LW\\lwbridge-0.3.1.exe",
            "python tools\\inspect_lwbridge_runtime_material_persistence.py ..\\LW\\lwbridge-0.3.1.exe --output evidence\\lwbridge-implementation\\2026-09-09-r6-runtime-material-persistence.json",
        ],
        "validationAndLimits": {
            "validation": "static-only against immutable verified LWBridge 0.3.1 host code",
            "liveProven": False,
            "unknownBlocked": [
                "the exact embedded proxy function/dataflow that opens and parses package-key.envelope",
                "key-envelope agreement/decrypt inputs and protected bridge-scripts.dat plaintext",
                "plaintext getWorldMapState/getCurrentServerId handler implementation",
                "propagated bridge readiness/transport errors and exact map scan state is unavailable trigger",
                "live authoritative correlation against the current Last War client",
            ],
        },
        "implementationImpact": {
            "closedGap": "R6-039 direct parsed packageKeyEnvelope -> package-key.envelope persistence/cleanup gap",
            "pm7B": "continue at the proxy read/decrypt and protected handler/readiness boundary",
            "mapSummary": "remains fail-closed; this persistence recovery alone does not prove an authoritative shared-state provider",
        },
    }


def text_report(result: dict[str, Any]) -> str:
    return "\n".join(
        [
            f"finding={result['findingId']} status={result['evidenceStatus']}",
            f"lwbridge_sha256={result['sourceIdentity']['sha256']}",
            f"serialized_content={result['parsedEnvelopePersistence']['serializedContent']}",
            f"write_primitive={result['writePath']['ntWriteFileImport']}",
            f"finalize={result['writePath']['moveFileExImport']} flags={result['writePath']['moveFileExFlags']}",
            f"cleanup={result['invalidOrExpiredCleanup']['deleteFileImport']}",
            "limits:",
            *[
                f"  UNKNOWN/BLOCKED: {item}"
                for item in result["validationAndLimits"]["unknownBlocked"]
            ],
        ]
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=Path)
    parser.add_argument("--json", action="store_true")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        result = inspect(args.binary)
    except (InspectError, OSError, pefile.PEFormatError) as exc:
        parser.error(str(exc))
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    if args.json:
        print(json.dumps(result, indent=2))
    else:
        print(text_report(result))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
