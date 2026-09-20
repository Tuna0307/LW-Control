#!/usr/bin/env python3
"""Hash-lock original bridge client executable-path normalization (R7-114)."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
IMAGE_BASE = 0x140000000
FIXED_OPEN_OPTIONS_RVA = 0xDCAFC0
FIXED_OPEN_OPTIONS_PREFIX = bytes.fromhex("01000000000000000000000200000000")


class InspectError(RuntimeError):
    pass


def decoder() -> Cs:
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    return md


def bounds(pe: pefile.PE, rva: int) -> tuple[int, int]:
    for entry in pe.DIRECTORY_ENTRY_EXCEPTION:
        if entry.struct.BeginAddress <= rva < entry.struct.EndAddress:
            return entry.struct.BeginAddress, entry.struct.EndAddress
    raise InspectError(f"no unwind function for RVA 0x{rva:X}")


def ins_at(pe: pefile.PE, data: bytes, rva: int):
    start, end = bounds(pe, rva)
    off = pe.get_offset_from_rva(start)
    for ins in decoder().disasm(
        data[off:off + (end - start)],
        pe.OPTIONAL_HEADER.ImageBase + start,
    ):
        if ins.address - pe.OPTIONAL_HEADER.ImageBase == rva:
            return ins
    raise InspectError(f"instruction not found at RVA 0x{rva:X}")


def rip_target(ins) -> int | None:
    for op in ins.operands:
        if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
            return ins.address + ins.size + op.mem.disp
    return None


def expect_direct_call(pe, data, rva: int, target_rva: int) -> str:
    ins = ins_at(pe, data, rva)
    if ins.mnemonic != "call" or not ins.operands or ins.operands[0].type != X86_OP_IMM:
        raise InspectError(f"expected direct call at 0x{rva:X}")
    observed = ins.operands[0].imm - pe.OPTIONAL_HEADER.ImageBase
    if observed != target_rva:
        raise InspectError(
            f"call 0x{rva:X} -> 0x{observed:X}, expected 0x{target_rva:X}"
        )
    return f"{ins.mnemonic} {ins.op_str}"


def expect_iat_call(pe, data, imports: dict[str, int], rva: int, name: str) -> str:
    ins = ins_at(pe, data, rva)
    target = rip_target(ins)
    if ins.mnemonic != "call" or target != imports.get(name):
        raise InspectError(
            f"expected import call {name} at 0x{rva:X}, got {ins.mnemonic} {ins.op_str}"
        )
    return f"{ins.mnemonic} {ins.op_str} -> {name}"


def expect_import_pointer_load(
    pe,
    data,
    imports: dict[str, int],
    rva: int,
    register_name: str,
    import_name: str,
) -> str:
    ins = ins_at(pe, data, rva)
    target = rip_target(ins)
    if (
        ins.mnemonic != "mov"
        or not ins.op_str.startswith(register_name + ",")
        or target != imports.get(import_name)
    ):
        raise InspectError(
            f"expected {import_name} pointer load into {register_name} at 0x{rva:X}, "
            f"got {ins.mnemonic} {ins.op_str}"
        )
    return f"{ins.mnemonic} {ins.op_str} -> {import_name}"


def expect_register_call(pe, data, rva: int, register_name: str) -> str:
    ins = ins_at(pe, data, rva)
    if ins.mnemonic != "call" or ins.op_str != register_name:
        raise InspectError(
            f"expected call {register_name} at 0x{rva:X}, got {ins.mnemonic} {ins.op_str}"
        )
    return f"{ins.mnemonic} {ins.op_str}"


def expect_contains(pe, data, rva: int, mnemonic: str, operand_text: str) -> str:
    ins = ins_at(pe, data, rva)
    if ins.mnemonic != mnemonic or operand_text not in ins.op_str:
        raise InspectError(
            f"instruction mismatch at 0x{rva:X}: expected {mnemonic} containing "
            f"{operand_text!r}, got {ins.mnemonic} {ins.op_str}"
        )
    return f"{ins.mnemonic} {ins.op_str}"


def expect_rip_utf16(pe, data, rva: int, expected: str) -> str:
    ins = ins_at(pe, data, rva)
    target = rip_target(ins)
    if target is None:
        raise InspectError(f"missing RIP-relative literal at 0x{rva:X}")
    raw = expected.encode("utf-16le")
    off = pe.get_offset_from_rva(target - pe.OPTIONAL_HEADER.ImageBase)
    observed = data[off:off + len(raw)]
    if observed != raw:
        raise InspectError(
            f"UTF-16 literal mismatch at 0x{rva:X}: {observed!r} != {raw!r}"
        )
    return expected


def inspect(path: Path) -> dict:
    data = path.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if digest != EXPECTED_SHA256:
        raise InspectError(f"unsupported host SHA-256 {digest}")

    pe = pefile.PE(data=data, fast_load=False)
    if pe.OPTIONAL_HEADER.ImageBase != IMAGE_BASE:
        raise InspectError(f"unexpected image base 0x{pe.OPTIONAL_HEADER.ImageBase:X}")

    imports = {
        (item.name or b"").decode(errors="ignore"): item.address
        for entry in pe.DIRECTORY_ENTRY_IMPORT
        for item in entry.imports
    }

    fixed_off = pe.get_offset_from_rva(FIXED_OPEN_OPTIONS_RVA)
    fixed_observed = data[
        fixed_off:fixed_off + len(FIXED_OPEN_OPTIONS_PREFIX)
    ]
    if fixed_observed != FIXED_OPEN_OPTIONS_PREFIX:
        raise InspectError(
            "fixed OpenOptions bytes changed: "
            f"{fixed_observed.hex()} != {FIXED_OPEN_OPTIONS_PREFIX.hex()}"
        )

    conversion = {
        "utf16WordLoad": expect_contains(
            pe, data, 0x029899, "movzx", "word ptr [rdi]"
        ),
        "surrogateRangeMask": expect_contains(
            pe, data, 0x0298A4, "and", "0xf800"
        ),
        "highSurrogateCheck": expect_contains(
            pe, data, 0x0298AE, "cmp", "0xd800"
        ),
        "replacementLeadByte": expect_contains(
            pe, data, 0x029A25, "mov", "0xed"
        ),
        "lossyUtf8Scanner": {
            "function": "0x0291F0-0x029591",
            "invalidSurrogateLead": expect_contains(
                pe, data, 0x029264, "cmp", "0xed"
            ),
            "replacementSequence": expect_contains(
                pe, data, 0x029352, "mov", "0xbd"
            ),
        },
    }

    wrapper = {
        "utf8ToUtf16": expect_direct_call(pe, data, 0x5B6650, 0x5BB940),
        "fullPath": expect_direct_call(pe, data, 0x5B6680, 0x5BC4C0),
        "finalPath": expect_direct_call(pe, data, 0x5B66BE, 0x5B70D0),
    }

    utf8_to_utf16 = {
        "embeddedNulScan": expect_contains(
            pe, data, 0x5BBA04, "cmp", "word ptr [rcx], 0"
        ),
        "terminatingNul": expect_contains(
            pe, data, 0x5BBA83, "mov", "word ptr [rdi + rbx*2], 0"
        ),
    }

    full_path = {
        "getFullPathName": expect_iat_call(
            pe, data, imports, 0x5BC6A6, "GetFullPathNameW"
        ),
        "errorInsufficientBuffer": expect_contains(
            pe, data, 0x5BC6D3, "cmp", "0x7a"
        ),
        "verbatimDosPrefix": expect_rip_utf16(pe, data, 0x5BC7CA, "\\\\?\\"),
        "verbatimUncPrefix": expect_rip_utf16(
            pe, data, 0x5BC832, "\\\\?\\UNC\\"
        ),
        "existingVerbatimPrefixCheck": expect_contains(
            pe, data, 0x5BC51D, "movabs", "0x5c003f005c005c"
        ),
        "ntPrefixCheck": expect_contains(
            pe, data, 0x5BC530, "movabs", "0x5c003f003f005c"
        ),
    }

    final_path = {
        "fixedOpenOptionsLoad": expect_contains(
            pe, data, 0x5B7119, "movaps", "xmmword ptr [rip"
        ),
        "fixedOpenOptionsBytes": fixed_observed.hex(),
        "getFinalPathPointer": expect_import_pointer_load(
            pe, data, imports, 0x5B719F, "r14", "GetFinalPathNameByHandleW"
        ),
        "zeroFlags": expect_contains(pe, data, 0x5B7255, "xor", "r9d, r9d"),
        "getFinalPathName": expect_register_call(pe, data, 0x5B7258, "r14"),
        "errorInsufficientBuffer": expect_contains(
            pe, data, 0x5B729E, "cmp", "0x7a"
        ),
        "utf16ToUtf8": expect_direct_call(pe, data, 0x5B7334, 0x029810),
        "closeHandle": expect_iat_call(
            pe, data, imports, 0x5B7384, "CloseHandle"
        ),
    }

    create_file = {
        "createFile": expect_iat_call(pe, data, imports, 0x5B868A, "CreateFileW"),
        "fixedDesiredAccessFromOptions": 0,
        "fixedShareModeFromOptions": 7,
        "fixedCreationDispositionFromOptions": 3,
        "fixedFlagsAndAttributes": "0x02000000",
    }

    final_compare = {
        "lengthCompare": expect_contains(
            pe, data, 0x3C4538, "cmp", "qword ptr [rdi + 0x10]"
        ),
        "leftAsciiFoldRange": expect_contains(
            pe, data, 0x3C4559, "cmp", "0x1a"
        ),
        "leftAsciiFoldBit": expect_contains(
            pe, data, 0x3C4561, "shl", "5"
        ),
        "rightAsciiFoldRange": expect_contains(
            pe, data, 0x3C456F, "cmp", "0x1a"
        ),
        "byteCompare": expect_contains(pe, data, 0x3C4581, "cmp", "r8b, r9b"),
    }

    return {
        "ok": True,
        "schemaVersion": 1,
        "findingId": "LWB-R7-114",
        "source": {
            "path": "../LW/lwbridge-0.3.1.exe",
            "sha256": digest,
            "imageBase": f"0x{pe.OPTIONAL_HEADER.ImageBase:X}",
        },
        "utfConversion": conversion,
        "canonicalizationWrapper": wrapper,
        "utf8ToUtf16Preparation": utf8_to_utf16,
        "lexicalFullPath": full_path,
        "filesystemFinalPath": final_path,
        "canonicalOpenPolicy": create_file,
        "finalEquality": final_compare,
        "recovered": {
            "queryProcessImageOutput": "UTF-16 -> lossy UTF-8",
            "embeddedNulRejected": True,
            "lexicalNormalization": "GetFullPathNameW with explicit Win32 verbatim DOS/UNC handling",
            "canonicalOpen": {
                "desiredAccess": 0,
                "shareMode": 7,
                "creationDisposition": 3,
                "flagsAndAttributes": "0x02000000",
            },
            "filesystemNormalization": "GetFinalPathNameByHandleW flags=0",
            "finalComparison": "equal UTF-8 byte length + ASCII A-Z case-folded byte equality",
        },
        "boundary": {
            "nativeHandshakeImplemented": False,
            "productionHostRunsClientPathGate": False,
            "productionCallLuaEnabled": False,
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("host", type=Path)
    parser.add_argument("--pretty", action="store_true")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    report = inspect(args.host.resolve())
    encoded = json.dumps(report, indent=2 if args.pretty else None, sort_keys=True)
    if args.output is not None:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded + "\n", encoding="utf-8")
    else:
        print(encoded)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
