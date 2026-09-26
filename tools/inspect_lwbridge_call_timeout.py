#!/usr/bin/env python3
"""Hash-lock the original generic call_lua timeout future (R7-119)."""

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
BRIDGE_STORE_PATH_RVA = 0x825348
TIMEOUT_SOURCE_RECORD_RVA = 0x825418


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
            f"call at 0x{rva:X} targets 0x{observed:X}, "
            f"expected 0x{target_rva:X}"
        )
    return f"{ins.mnemonic} {ins.op_str}"


def expect_rip_target(
    pe: pefile.PE,
    data: bytes,
    rva: int,
    target_rva: int,
) -> str:
    ins = ins_at(pe, data, rva)
    observed = None
    for op in ins.operands:
        if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
            observed = (
                ins.address + ins.size + op.mem.disp
                - pe.OPTIONAL_HEADER.ImageBase
            )
            break
    if observed != target_rva:
        raise InspectError(
            f"RIP target at 0x{rva:X} is "
            f"{None if observed is None else hex(observed)}, "
            f"expected 0x{target_rva:X}"
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

    path_off = pe.get_offset_from_rva(BRIDGE_STORE_PATH_RVA)
    source_path = data[path_off:path_off + 28]
    if source_path != b"src\\services\\bridge_store.rs":
        raise InspectError(
            f"bridge_store.rs path changed: {source_path!r}"
        )

    record_off = pe.get_offset_from_rva(TIMEOUT_SOURCE_RECORD_RVA)
    file_pointer, file_length, line, column = struct.unpack_from(
        "<QQII",
        data,
        record_off,
    )
    if (
        file_pointer != IMAGE_BASE + BRIDGE_STORE_PATH_RVA
        or file_length != 28
        or line != 980
        or column != 28
    ):
        raise InspectError(
            "call timeout source record changed: "
            f"ptr=0x{file_pointer:X}, len={file_length}, "
            f"line={line}, column={column}"
        )

    timeout_future = {
        "function": "0x0E537E-0x0E5535",
        "sourceRecordLoad": expect_rip_target(
            pe, data, 0x0E53A5, TIMEOUT_SOURCE_RECORD_RVA
        ),
        "durationSeconds": expect_contains(
            pe, data, 0x0E53AC, "xor", "edx, edx"
        ),
        "durationNanoseconds": expect_contains(
            pe, data, 0x0E53AE, "mov", "r8d, 0xbebc200"
        ),
        "timerConstructor": expect_direct_call(
            pe, data, 0x0E53B4, 0x63B764
        ),
    }

    timer_builder = {
        "function": "0x63B474-0x63B537",
        "nanosecondsPerSecondInvariant": expect_contains(
            pe, data, 0x63B4B2, "cmp", "0x3b9aca00"
        ),
        "secondsStored": expect_contains(
            pe, data, 0x63B4E9, "mov", "qword ptr [rax + 0x60], r14"
        ),
        "nanosecondsStored": expect_contains(
            pe, data, 0x63B4ED, "mov", "dword ptr [rax + 0x68], ebx"
        ),
    }

    return {
        "ok": True,
        "schemaVersion": 1,
        "findingId": "LWB-R7-119",
        "source": {
            "path": "../LW/lwbridge-0.3.1.exe",
            "sha256": digest,
            "imageBase": f"0x{pe.OPTIONAL_HEADER.ImageBase:X}",
            "rustSource": "src\\services\\bridge_store.rs",
            "timeoutSourceLine": line,
            "timeoutSourceColumn": column,
        },
        "timeoutFuture": timeout_future,
        "timerBuilder": timer_builder,
        "recovered": {
            "durationSeconds": 0,
            "durationNanoseconds": 200_000_000,
            "timeoutMilliseconds": 200,
            "nanosecondsPerSecond": 1_000_000_000,
        },
        "boundary": {
            "firstCommandCounterSeedRecovered": False,
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
