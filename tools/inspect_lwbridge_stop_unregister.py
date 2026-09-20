#!/usr/bin/env python3
"""Hash-lock original profile_instance_stop successful unregister path (R7-125)."""

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
STOP_FUNCTION_START = 0x13FCFB
STOP_FUNCTION_END = 0x141336


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
    raise InspectError(f"no unwind function contains RVA 0x{rva:X}")


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


def expect_literal(
    pe: pefile.PE,
    data: bytes,
    rva: int,
    expected: bytes,
) -> str:
    ins = ins_at(pe, data, rva)
    target = rip_target(ins)
    if target is None:
        raise InspectError(f"missing RIP literal at RVA 0x{rva:X}")
    off = pe.get_offset_from_rva(target - pe.OPTIONAL_HEADER.ImageBase)
    observed = data[off:off + len(expected)]
    if observed != expected:
        raise InspectError(
            f"literal mismatch at 0x{rva:X}: {observed!r} != {expected!r}"
        )
    return expected.decode("ascii")


def expect_contains(
    pe: pefile.PE,
    data: bytes,
    rva: int,
    mnemonic: str,
    text: str,
) -> str:
    ins = ins_at(pe, data, rva)
    if ins.mnemonic != mnemonic or text not in ins.op_str:
        raise InspectError(
            f"expected {mnemonic} containing {text!r} at 0x{rva:X}, "
            f"got {ins.mnemonic} {ins.op_str}"
        )
    return f"{ins.mnemonic} {ins.op_str}"


def expect_direct_call(
    pe: pefile.PE,
    data: bytes,
    rva: int,
    target_rva: int,
) -> str:
    ins = ins_at(pe, data, rva)
    if (
        ins.mnemonic != "call"
        or not ins.operands
        or ins.operands[0].type != X86_OP_IMM
    ):
        raise InspectError(f"expected direct call at RVA 0x{rva:X}")
    observed = ins.operands[0].imm - pe.OPTIONAL_HEADER.ImageBase
    if observed != target_rva:
        raise InspectError(
            f"call 0x{rva:X} -> 0x{observed:X}, expected 0x{target_rva:X}"
        )
    return f"{ins.mnemonic} {ins.op_str}"


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

    observed_bounds = bounds(pe, 0x140882)
    if observed_bounds != (STOP_FUNCTION_START, STOP_FUNCTION_END):
        raise InspectError(
            "profile_instance_stop owner changed: "
            f"0x{observed_bounds[0]:X}-0x{observed_bounds[1]:X}"
        )

    stop_identity = {
        "function": (
            f"0x{STOP_FUNCTION_START:X}-0x{STOP_FUNCTION_END:X}"
        ),
        "commandLiteral": expect_literal(
            pe, data, 0x13FDA4, b"profile_instance_stop"
        ),
        "commandLength": expect_contains(
            pe, data, 0x13FDAE, "mov", "qword ptr [rdx + 8], 0x15"
        ),
        "profileIdLiteral": expect_literal(
            pe, data, 0x140158, b"profileId"
        ),
        "instanceIdLiteral": expect_literal(
            pe, data, 0x14019F, b"instanceId"
        ),
        "userStopLiteral": expect_literal(
            pe, data, 0x140225, b"user_stop"
        ),
    }

    unregister = {
        "instancePointerLoad": expect_contains(
            pe, data, 0x140874, "mov", "rdx, qword ptr [rbx + 0x23b0]"
        ),
        "instanceLengthLoad": expect_contains(
            pe, data, 0x14087B, "mov", "r8, qword ptr [rbx + 0x23b8]"
        ),
        "wrapperCall": expect_direct_call(
            pe, data, 0x140882, 0x3CC8B3
        ),
        "wrapperToCore": expect_direct_call(
            pe, data, 0x3CC8D2, 0x3C2436
        ),
    }

    stopped_state = {
        "upperBytes": expect_contains(
            pe, data, 0x140B2B, "mov", "dword ptr [rax + 3], 0x64657070"
        ),
        "lowerBytes": expect_contains(
            pe, data, 0x140B32, "mov", "dword ptr [rax], 0x706f7473"
        ),
        "decoded": "stopped",
    }

    return {
        "ok": True,
        "schemaVersion": 1,
        "findingId": "LWB-R7-125",
        "source": {
            "path": "../LW/lwbridge-0.3.1.exe",
            "sha256": digest,
            "imageBase": f"0x{pe.OPTIONAL_HEADER.ImageBase:X}",
        },
        "stopIdentity": stop_identity,
        "successfulStopUnregister": unregister,
        "postUnregisterState": stopped_state,
        "recovered": {
            "successfulProfileStopExplicitlyUnregistersInstance": True,
            "unregisterOccursBeforeStoppedStateConstruction": True,
            "wrapper": "0x3CC8B3",
            "core": "0x3C2436",
        },
        "boundary": {
            "failedStartUnregisterProof": "R7-109",
            "reconcileUnregisterProof": "R7-109",
            "realGameLaunched": False,
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
