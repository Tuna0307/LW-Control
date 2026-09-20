#!/usr/bin/env python3
"""Hash-lock original bridge-host production transport inputs (R7-122)."""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path

import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
IMAGE_BASE = 0x140000000


class InspectError(RuntimeError):
    pass


def decoder() -> Cs:
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    return md


def ins_at(pe: pefile.PE, data: bytes, rva: int):
    for entry in pe.DIRECTORY_ENTRY_EXCEPTION:
        if entry.struct.BeginAddress <= rva < entry.struct.EndAddress:
            start, end = entry.struct.BeginAddress, entry.struct.EndAddress
            off = pe.get_offset_from_rva(start)
            for ins in decoder().disasm(
                data[off:off + (end - start)],
                pe.OPTIONAL_HEADER.ImageBase + start,
            ):
                if ins.address - pe.OPTIONAL_HEADER.ImageBase == rva:
                    return ins
            break
    raise InspectError(f"instruction not found at RVA 0x{rva:X}")


def rip_target(ins) -> int | None:
    for op in ins.operands:
        if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
            return ins.address + ins.size + op.mem.disp
    return None


def expect_direct_call(pe, data, rva: int, target_rva: int) -> str:
    ins = ins_at(pe, data, rva)
    if (
        ins.mnemonic != "call"
        or not ins.operands
        or ins.operands[0].type != X86_OP_IMM
    ):
        raise InspectError(f"expected direct call at 0x{rva:X}")
    observed = ins.operands[0].imm - pe.OPTIONAL_HEADER.ImageBase
    if observed != target_rva:
        raise InspectError(
            f"call 0x{rva:X} -> 0x{observed:X}, expected 0x{target_rva:X}"
        )
    return f"{ins.mnemonic} {ins.op_str}"


def expect_contains(pe, data, rva: int, mnemonic: str, text: str) -> str:
    ins = ins_at(pe, data, rva)
    if ins.mnemonic != mnemonic or text not in ins.op_str:
        raise InspectError(
            f"expected {mnemonic} containing {text!r} at 0x{rva:X}, "
            f"got {ins.mnemonic} {ins.op_str}"
        )
    return f"{ins.mnemonic} {ins.op_str}"


def expect_literal(pe, data, rva: int, expected: bytes) -> str:
    ins = ins_at(pe, data, rva)
    target = rip_target(ins)
    if target is None:
        raise InspectError(f"missing RIP literal at 0x{rva:X}")
    off = pe.get_offset_from_rva(target - pe.OPTIONAL_HEADER.ImageBase)
    observed = data[off:off + len(expected)]
    if observed != expected:
        raise InspectError(
            f"literal mismatch at 0x{rva:X}: {observed!r} != {expected!r}"
        )
    return expected.decode("ascii", errors="replace")


def inspect(path: Path) -> dict:
    data = path.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if digest != EXPECTED_SHA256:
        raise InspectError(f"unsupported host SHA-256 {digest}")

    pe = pefile.PE(data=data, fast_load=False)
    if pe.OPTIONAL_HEADER.ImageBase != IMAGE_BASE:
        raise InspectError(
            f"unexpected image base 0x{pe.OPTIONAL_HEADER.ImageBase:X}"
        )

    expected_client_path = {
        "startupBuilderCall": expect_direct_call(
            pe, data, 0x41001F, 0x43365C
        ),
        "gameLiteral": expect_literal(pe, data, 0x433687, b"Game"),
        "gameLength": expect_contains(
            pe, data, 0x43368E, "mov", "r8d, 4"
        ),
        "gameAppendCall": expect_direct_call(
            pe, data, 0x433697, 0x5C71B0
        ),
        "executableLiteral": expect_literal(
            pe, data, 0x4336C4, b"LastWar.exe"
        ),
        "executableLength": expect_contains(
            pe, data, 0x4336CB, "mov", "r8d, 0xb"
        ),
        "executableAppendCall": expect_direct_call(
            pe, data, 0x4336D4, 0x5C71B0
        ),
        "constructorArg2": expect_contains(
            pe, data, 0x4100A7, "lea", "[rsp + 0xf0]"
        ),
        "hostConstructorCall": expect_direct_call(
            pe, data, 0x4100B5, 0x3C30BE
        ),
        "constructorCapturesArg2": expect_contains(
            pe, data, 0x3C30F0, "mov", "r13, rdx"
        ),
        "constructorRestoresArg2": expect_contains(
            pe, data, 0x3C3568, "mov", "rdx, qword ptr [rsp + 0x38]"
        ),
        "canonicalizationCall": expect_direct_call(
            pe, data, 0x3C3654, 0x5B6630
        ),
    }

    seed_load = ins_at(pe, data, 0x3C3926)
    seed_target = rip_target(seed_load)
    if seed_load.mnemonic != "movups" or seed_target is None:
        raise InspectError("expected store seed constant load at 0x3C3926")
    seed_off = pe.get_offset_from_rva(
        seed_target - pe.OPTIONAL_HEADER.ImageBase
    )
    seed_raw = data[seed_off:seed_off + 16]
    if len(seed_raw) != 16:
        raise InspectError("store seed constant is truncated")
    seed_qwords = struct.unpack("<QQ", seed_raw)
    if seed_qwords[1] != 0:
        raise InspectError(
            f"command counter seed is {seed_qwords[1]}, expected 0"
        )

    command_counter = {
        "storeSeedLoad": f"{seed_load.mnemonic} {seed_load.op_str}",
        "storeSeedConstantRva": (
            f"0x{seed_target - pe.OPTIONAL_HEADER.ImageBase:X}"
        ),
        "storeSeedQwords": [seed_qwords[0], seed_qwords[1]],
        "storeSeedWrite": expect_contains(
            pe, data, 0x3C3AC3, "movups", "[r14 + 0x178], xmm6"
        ),
        "mutexAddress": expect_contains(
            pe, data, 0x3C4B4E, "lea", "[rsi + 0x100]"
        ),
        "mutexLockCall": expect_direct_call(
            pe, data, 0x3C4B68, 0x046750
        ),
        "lockHelperCapturesMutex": expect_contains(
            pe, data, 0x046756, "mov", "rdi, rdx"
        ),
        "lockHelperReturnsMutex": expect_contains(
            pe, data, 0x046781, "mov", "qword ptr [rsi + 8], rdi"
        ),
        "guardMutexPointer": expect_contains(
            pe, data, 0x3C4B6D, "mov", "r14, qword ptr [r15 + 8]"
        ),
        "counterAddress": expect_contains(
            pe, data, 0x3C4BDE, "lea", "[r14 + 0x80]"
        ),
        "preFormatIncrement": expect_contains(
            pe, data, 0x3C4BE5, "inc", "qword ptr [r14 + 0x80]"
        ),
        "commandIdLiteral": expect_literal(
            pe, data, 0x3C4C16, b"\x04cmd_"
        ),
    }

    return {
        "ok": True,
        "schemaVersion": 1,
        "findingId": "LWB-R7-122",
        "source": {
            "path": "../LW/lwbridge-0.3.1.exe",
            "sha256": digest,
            "imageBase": f"0x{pe.OPTIONAL_HEADER.ImageBase:X}",
        },
        "expectedClientImage": {
            **expected_client_path,
            "recoveredPath": r"<selectedGameRoot>\Game\LastWar.exe",
            "comparisonInput": (
                "canonicalized constructor argument 2"
            ),
        },
        "commandCounter": {
            **command_counter,
            "storeMutexOffset": "0x100",
            "mutexToCounterOffset": "0x80",
            "storeCounterOffset": "0x180",
            "initialValue": 0,
            "incrementTiming": "before cmd_ formatting",
            "firstCommandId": "cmd_1",
        },
        "boundary": {
            "normalWindowStartsTransport": False,
            "productionPendingExposed": False,
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
    encoded = json.dumps(
        report,
        indent=2 if args.pretty else None,
        sort_keys=True,
    )
    if args.output is not None:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded + "\n", encoding="utf-8")
    else:
        print(encoded)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
