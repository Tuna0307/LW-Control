#!/usr/bin/env python3
"""Verify the original raw pipeToken -> startup-registry alias.

Read-only/hash-gated R7-108 inspector. It proves that the token object generated
at launch-state +0x3A0 supplies the exact pointer/length slice forwarded into
the recovered startup-registry registration path. No process is launched and
no environment or registry state is modified.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64

EXPECTED_HOST_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
EXPECTED_IMAGE_BASE = 0x140000000


class InspectError(RuntimeError):
    pass


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _instructions(pe: pefile.PE, data: bytes) -> dict[int, object]:
    section = next(
        (item for item in pe.sections if item.Name.rstrip(b"\0") == b".text"),
        None,
    )
    if section is None:
        raise InspectError("host .text section missing")
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    code = data[
        section.PointerToRawData:
        section.PointerToRawData + section.SizeOfRawData
    ]
    return {
        ins.address - pe.OPTIONAL_HEADER.ImageBase: ins
        for ins in md.disasm(code, pe.OPTIONAL_HEADER.ImageBase + section.VirtualAddress)
    }


def _require(insns: dict[int, object], rva: int, mnemonic: str, operand: str) -> str:
    ins = insns.get(rva)
    if ins is None or ins.mnemonic != mnemonic or operand not in ins.op_str:
        observed = "missing" if ins is None else f"{ins.mnemonic} {ins.op_str}"
        raise InspectError(f"instruction mismatch at RVA 0x{rva:X}: {observed}")
    return f"{ins.mnemonic} {ins.op_str}"


def inspect(path: Path) -> dict:
    data = path.read_bytes()
    digest = _sha256(data)
    if digest != EXPECTED_HOST_SHA256:
        raise InspectError(f"unsupported host SHA-256 {digest}")
    pe = pefile.PE(data=data, fast_load=False)
    if pe.OPTIONAL_HEADER.ImageBase != EXPECTED_IMAGE_BASE:
        raise InspectError("unexpected host image base")
    ins = _instructions(pe, data)

    producer = {
        "destination": _require(ins, 0x1D5116, "lea", "rcx, [r14 + 0x3a0]"),
        "call": _require(ins, 0x1D511D, "call", "0x1403a2e51"),
        "storeOwner": _require(ins, 0x401F64, "mov", "qword ptr [rsi], rbx"),
        "storePointer": _require(ins, 0x401F67, "mov", "qword ptr [rsi + 8], r12"),
        "storeLength": _require(ins, 0x401F6B, "mov", "qword ptr [rsi + 0x10], r15"),
    }

    direct_registration = {
        "profilePointer": _require(ins, 0x1D7C5B, "mov", "r8, qword ptr [r14 + 0x390]"),
        "profileLength": _require(ins, 0x1D7C6A, "mov", "r9, qword ptr [r14 + 0x398]"),
        "tokenSlice": _require(ins, 0x1D7C71, "movups", "xmm0, xmmword ptr [r14 + 0x3a8]"),
        "tokenStackForward": _require(ins, 0x1D7C8C, "movups", "xmmword ptr [rsp + 0x20], xmm0"),
        "wrapperCall": _require(ins, 0x1D7C94, "call", "0x1403cca38"),
    }

    wrapper = {
        "receiveTokenSlice": _require(ins, 0x3CCA52, "movaps", "xmm0, xmmword ptr [rsp + 0x120]"),
        "forwardTokenSlice": _require(ins, 0x3CCA80, "movups", "xmmword ptr [rsp + 0x30], xmm0"),
        "registryCall": _require(ins, 0x3CCA97, "call", "0x1403c2a69"),
    }

    registry = {
        "tokenLengthLoad": _require(ins, 0x3C2B1C, "mov", "r15, qword ptr [rsp + 0x168]"),
        "minimumLengthCheck": _require(ins, 0x3C2B24, "cmp", "r15, 7"),
        "tokenPointerLoad": _require(ins, 0x3C2C74, "mov", "r13, qword ptr [rsp + 0x160]"),
        "hashPointerArg": _require(ins, 0x3C2C92, "mov", "rdx, r13"),
        "hashLengthArg": _require(ins, 0x3C2C95, "mov", "r8, r15"),
        "hashCall": _require(ins, 0x3C2C98, "call", "0x1403eb155"),
    }

    return {
        "ok": True,
        "schemaVersion": 1,
        "findingId": "LWB-R7-108",
        "source": {
            "path": "../LW/lwbridge-0.3.1.exe",
            "sha256": digest,
            "imageBase": f"0x{pe.OPTIONAL_HEADER.ImageBase:X}",
        },
        "pipeTokenObject": {
            "launchStateOffset": "0x3A0",
            "objectBytes": 24,
            "payloadPointerOffset": "0x3A8",
            "payloadLengthOffset": "0x3B0",
            "producer": producer,
        },
        "directStartupRegistration": direct_registration,
        "registryWrapper": wrapper,
        "registryTokenUse": registry,
        "conclusion": (
            "The raw pipeToken generated into launch-state +0x3A0 supplies the "
            "exact +0x3A8/+0x3B0 pointer/length slice forwarded unchanged into "
            "startup registration; no challenge alias or token transform occurs."
        ),
        "boundary": {
            "staticOnly": True,
            "productionListenerImplemented": False,
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
