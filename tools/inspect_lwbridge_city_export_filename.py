#!/usr/bin/env python3
"""Verify LWBridge 0.3.1 City-export default filename and UTC timestamp contract."""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path

import capstone
import pefile

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
RUSTC_COMMIT = "4a4ef493e3a1488c6e321570238084b38948f6db"
TIME_VERSION = "time-0.3.54"

CITY_OFFSET = 0x00821F12
CITY_FORMAT = bytes.fromhex(
    "0b6d61702d6369746965732dc0012d"
    "c3200000690400c3200000690200c3200000690200"
    "012dc3200000690200c3200000690200c3200000690200"
    "052e786c737800"
)
FEEDBACK_OFFSET = 0x00823008
FEEDBACK_FORMAT = bytes.fromhex(
    "126c776272696467652d666565646261636b2d"
    "c3200000690400c3200000690200c3200000690200"
    "012dc3200000690200c3200000690200c3200000690200"
    "042e7a697000"
)
SERVER_KEY_RVA_INSN = 0x14D83F
CLOCK_CALL_RVA = 0x14D876
DATE_EXTRACT_START_RVA = 0x14D87B
TIME_EXTRACT_START_RVA = 0x14D8E9
ARGUMENT_ARRAY_START_RVA = 0x14D928
CLOCK_HELPER_RVA = 0x10A30B
PRECISE_TIME_IMPORT_VA = 0x1407B94B0
FILETIME_UNIX_EPOCH_100NS = 116_444_736_000_000_000


class InspectError(ValueError):
    pass


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def require_bytes(blob: bytes, offset: int, expected: bytes, label: str) -> None:
    actual = blob[offset : offset + len(expected)]
    if actual != expected:
        raise InspectError(
            f"unexpected {label} bytes at raw 0x{offset:X}: "
            f"expected {expected.hex()} got {actual.hex()}"
        )


def decode_format_bytecode(data: bytes) -> list[dict[str, object]]:
    pieces: list[dict[str, object]] = []
    cursor = 0
    implicit_arg = 0
    while cursor < len(data):
        opcode = data[cursor]
        cursor += 1
        if opcode == 0:
            break
        if opcode < 0x80:
            literal = data[cursor : cursor + opcode]
            cursor += opcode
            pieces.append({"kind": "literal", "value": literal.decode("ascii")})
            continue
        if opcode != 0x80 and opcode < 0xC0:
            raise InspectError(f"unexpected format bytecode opcode 0x{opcode:02X}")
        if opcode == 0x80:
            length = struct.unpack_from("<H", data, cursor)[0]
            cursor += 2
            literal = data[cursor : cursor + length]
            cursor += length
            pieces.append({"kind": "literal", "value": literal.decode("utf-8")})
            continue

        has_flags = bool(opcode & 0x01)
        has_width = bool(opcode & 0x02)
        has_precision = bool(opcode & 0x04)
        explicit_position = bool(opcode & 0x08)
        width_indirect = bool(opcode & 0x10)
        precision_indirect = bool(opcode & 0x20)
        flags = 0x60000020
        width: int | None = None
        precision: int | None = None
        if has_flags:
            flags = struct.unpack_from("<I", data, cursor)[0]
            cursor += 4
            if has_width:
                width = struct.unpack_from("<H", data, cursor)[0]
                cursor += 2
            if has_precision:
                precision = struct.unpack_from("<H", data, cursor)[0]
                cursor += 2
        position = implicit_arg
        if explicit_position:
            position = struct.unpack_from("<H", data, cursor)[0]
            cursor += 2
        implicit_arg = position + 1
        pieces.append(
            {
                "kind": "placeholder",
                "argument": position,
                "flags": f"0x{flags:08X}",
                "zeroPad": bool(flags & (1 << 24)),
                "widthPresent": bool(flags & (1 << 27)),
                "width": width,
                "widthIndirect": width_indirect,
                "precision": precision,
                "precisionIndirect": precision_indirect,
            }
        )
    return pieces


def require_instruction(
    instructions: dict[int, capstone.CsInsn],
    va: int,
    mnemonic: str,
    op_contains: str,
) -> None:
    insn = instructions.get(va)
    if insn is None or insn.mnemonic != mnemonic or op_contains not in insn.op_str:
        actual = None if insn is None else f"{insn.mnemonic} {insn.op_str}"
        raise InspectError(
            f"unexpected instruction at 0x{va:X}: "
            f"expected {mnemonic} *{op_contains}* got {actual}"
        )


def disassemble_range(
    pe: pefile.PE, blob: bytes, start_rva: int, end_rva: int
) -> dict[int, capstone.CsInsn]:
    raw = pe.get_offset_from_rva(start_rva)
    md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
    md.detail = True
    return {
        insn.address: insn
        for insn in md.disasm(
            blob[raw : raw + end_rva - start_rva],
            pe.OPTIONAL_HEADER.ImageBase + start_rva,
        )
    }


def inspect(binary: Path) -> dict[str, object]:
    blob = binary.read_bytes()
    digest = hashlib.sha256(blob).hexdigest()
    if digest != EXPECTED_SHA256:
        raise InspectError(f"unexpected LWBridge SHA-256: {digest}")

    require_bytes(blob, CITY_OFFSET, CITY_FORMAT, "City filename format")
    require_bytes(blob, FEEDBACK_OFFSET, FEEDBACK_FORMAT, "Feedback filename format")
    if TIME_VERSION.encode("ascii") not in blob:
        raise InspectError(f"{TIME_VERSION} provenance string not found")
    if RUSTC_COMMIT.encode("ascii") not in blob:
        raise InspectError(f"rustc commit {RUSTC_COMMIT} not found")

    city_pieces = decode_format_bytecode(CITY_FORMAT)
    feedback_pieces = decode_format_bytecode(FEEDBACK_FORMAT)
    expected_timestamp_widths = [4, 2, 2, 2, 2, 2]
    city_placeholders = [p for p in city_pieces if p["kind"] == "placeholder"]
    feedback_placeholders = [p for p in feedback_pieces if p["kind"] == "placeholder"]
    if len(city_placeholders) != 7 or len(feedback_placeholders) != 6:
        raise InspectError("unexpected City/Feedback placeholder count")
    if city_placeholders[0]["width"] is not None or city_placeholders[0]["zeroPad"]:
        raise InspectError("serverId placeholder is not the recovered default formatter")
    for group in (city_placeholders[1:], feedback_placeholders):
        widths = [p["width"] for p in group]
        if widths != expected_timestamp_widths:
            raise InspectError(f"unexpected timestamp widths: {widths!r}")
        if not all(p["zeroPad"] and p["widthPresent"] for p in group):
            raise InspectError("timestamp placeholders are not all zero-padded fixed widths")

    pe = pefile.PE(data=blob)
    pe.parse_data_directories(
        directories=[
            pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_IMPORT"],
            pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"],
        ]
    )
    imports = {
        imp.address: (
            dll.dll.decode("ascii", "replace"),
            None if imp.name is None else imp.name.decode("ascii", "replace"),
        )
        for dll in pe.DIRECTORY_ENTRY_IMPORT
        for imp in dll.imports
    }
    if imports.get(PRECISE_TIME_IMPORT_VA) != (
        "kernel32.dll",
        "GetSystemTimePreciseAsFileTime",
    ):
        raise InspectError("precise system-time import changed")

    picker = disassemble_range(pe, blob, 0x14D83F, 0x14D9D8)
    base = pe.OPTIONAL_HEADER.ImageBase
    require_instruction(picker, base + SERVER_KEY_RVA_INSN, "lea", "rdx")
    server_ref = picker[base + SERVER_KEY_RVA_INSN]
    server_mem = next(
        (
            op.mem
            for op in server_ref.operands
            if op.type == capstone.x86.X86_OP_MEM
            and op.mem.base == capstone.x86.X86_REG_RIP
        ),
        None,
    )
    if server_mem is None:
        raise InspectError("server key reference is no longer RIP-relative")
    server_key_va = server_ref.address + server_ref.size + server_mem.disp
    server_key_raw = pe.get_offset_from_rva(server_key_va - base)
    if blob[server_key_raw : server_key_raw + 8] != b"serverId":
        raise InspectError("server filename argument no longer comes from serverId")
    require_instruction(picker, base + 0x14D84F, "call", "0x14023195e")
    require_instruction(picker, base + 0x14D854, "mov", "[rbx + 0x13a8], rax")
    require_instruction(picker, base + 0x14D85B, "test", "rax, rax")
    require_instruction(picker, base + CLOCK_CALL_RVA, "call", "0x14010a30b")
    require_instruction(picker, base + DATE_EXTRACT_START_RVA, "mov", "ecx, dword ptr [rbp + 8]")
    require_instruction(picker, base + 0x14D880, "sar", "eax, 0xa")
    require_instruction(picker, base + 0x14D88B, "mov", "dword ptr [rdi], eax")
    require_instruction(picker, base + TIME_EXTRACT_START_RVA, "mov", "rax, qword ptr [rbp]")
    require_instruction(picker, base + 0x14D8F0, "shr", "r8, 0x30")
    require_instruction(picker, base + 0x14D902, "shr", "r8, 0x28")
    require_instruction(picker, base + 0x14D911, "shr", "rax, 0x20")
    require_instruction(picker, base + ARGUMENT_ARRAY_START_RVA, "mov", "qword ptr [r8], r15")
    require_instruction(picker, base + 0x14D936, "mov", "qword ptr [r8 + 0x10], rsi")
    require_instruction(picker, base + 0x14D945, "mov", "qword ptr [r8 + 0x20], rcx")
    require_instruction(picker, base + 0x14D954, "mov", "qword ptr [r8 + 0x30], rdx")
    require_instruction(picker, base + 0x14D95C, "mov", "qword ptr [r8 + 0x40], r9")
    require_instruction(picker, base + 0x14D964, "mov", "qword ptr [r8 + 0x50], r10")
    require_instruction(picker, base + 0x14D96C, "mov", "qword ptr [r8 + 0x60], r11")
    clock = disassemble_range(pe, blob, CLOCK_HELPER_RVA, 0x10A6ED)
    require_instruction(clock, base + 0x10A327, "call", "rip + 0x6af183")
    require_instruction(clock, base + 0x10A330, "movabs", hex(FILETIME_UNIX_EPOCH_100NS))
    require_instruction(clock, base + 0x10A6D5, "mov", "byte ptr [rsi + 0xe], 0")
    require_instruction(clock, base + 0x10A6D9, "mov", "word ptr [rsi + 0xc], 0")

    return {
        "findingId": "LWB-R7-058",
        "date": "2026-09-19",
        "scope": "original City export default filename format and timestamp clock",
        "evidenceStatus": "RECOVERED static with one explicitly derived argument binding",
        "sourceIdentity": {"path": str(binary), "sha256": digest},
        "sourceReferences": {
            "rustcFormatLowering": (
                "https://github.com/rust-lang/rust/blob/"
                + RUSTC_COMMIT
                + "/compiler/rustc_ast_lowering/src/format.rs"
            ),
            "timeDate": "https://github.com/time-rs/time/blob/v0.3.54/time/src/date.rs",
            "timeTime": "https://github.com/time-rs/time/blob/v0.3.54/time/src/time.rs",
            "timeOffsetDateTime": (
                "https://github.com/time-rs/time/blob/v0.3.54/time/src/offset_date_time.rs"
            ),
        },
        "formatBytecode": {
            "rustcCommit": RUSTC_COMMIT,
            "compilerRule": (
                "0xC0 placeholder; bit0 flags, bit1 width; flags bit24 zero-pad, "
                "bit27 width-present; literal widths are little-endian u16"
            ),
            "cityRawOffset": f"0x{CITY_OFFSET:08X}",
            "feedbackRawOffset": f"0x{FEEDBACK_OFFSET:08X}",
            "cityPieces": city_pieces,
            "feedbackPieces": feedback_pieces,
        },
        "filename": {
            "pattern": "map-cities-{serverId}-{YYYY}{MM}{DD}-{HH}{mm}{ss}.xlsx",
            "serverId": "positive parsed serverId; default decimal formatting",
            "timestampZone": "UTC",
            "timestampWidths": expected_timestamp_widths,
            "timestampZeroPadded": True,
        },
        "timestampSource": {
            "import": "kernel32.dll!GetSystemTimePreciseAsFileTime",
            "iatVa": f"0x{PRECISE_TIME_IMPORT_VA:X}",
            "filetimeUnixEpoch100ns": FILETIME_UNIX_EPOCH_100NS,
            "timeCrate": TIME_VERSION,
            "dateLayout": (
                "time 0.3.54 Date.value packs year<<10, leap bit 9, ordinal low 9; "
                "caller extracts year/month/day from that representation"
            ),
            "timeLayout": (
                "time 0.3.54 little-endian Time packs second/minute/hour at "
                "bits 32/40/48; caller extracts those exact bytes"
            ),
            "utcOffsetStores": ["+0xC word 0", "+0xE byte 0"],
        },
        "argumentBinding": {
            "arg0": "serverId (direct)",
            "arg1": (
                "year (derived by exhaustive structure: year is computed immediately before "
                "the 7-argument formatter; args2-6 are directly month/day/hour/minute/second; "
                "the timestamp bytecode has exactly six date/time placeholders)"
            ),
            "arg2": "month (direct)",
            "arg3": "day (direct)",
            "arg4": "hour (direct)",
            "arg5": "minute (direct)",
            "arg6": "second (direct)",
        },
        "corroboration": (
            "lwbridge-feedback- uses the identical six zero-padded UTC timestamp "
            "placeholder sequence, without City's leading serverId placeholder"
        ),
        "restriction": (
            "SB-99: a later request to inspect the earlier RSI assignment was rejected; "
            "it was not replayed or rerouted and is not used as evidence"
        ),
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
        print("RECOVERED", result["findingId"], result["filename"]["pattern"])
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
