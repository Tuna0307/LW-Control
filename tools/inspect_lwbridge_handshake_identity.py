#!/usr/bin/env python3
"""Hash-lock the original control-pipe hello authentication gates (R7-113)."""

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
            for ins in decoder().disasm(data[off:off + (end - start)], pe.OPTIONAL_HEADER.ImageBase + start):
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
    if ins.mnemonic != "call" or not ins.operands or ins.operands[0].type != X86_OP_IMM:
        raise InspectError(f"expected direct call at 0x{rva:X}")
    observed = ins.operands[0].imm - pe.OPTIONAL_HEADER.ImageBase
    if observed != target_rva:
        raise InspectError(f"call 0x{rva:X} -> 0x{observed:X}, expected 0x{target_rva:X}")
    return f"{ins.mnemonic} {ins.op_str}"


def expect_iat_call(pe, data, imports: dict[str, int], rva: int, name: str) -> str:
    ins = ins_at(pe, data, rva)
    target = rip_target(ins)
    if ins.mnemonic != "call" or target != imports.get(name):
        raise InspectError(f"expected {name} import call at 0x{rva:X}, got {ins.mnemonic} {ins.op_str}")
    return f"{ins.mnemonic} {ins.op_str} -> {name}"


def expect_contains(pe, data, rva: int, mnemonic: str, text: str) -> str:
    ins = ins_at(pe, data, rva)
    if ins.mnemonic != mnemonic or text not in ins.op_str:
        raise InspectError(
            f"expected {mnemonic} containing {text!r} at 0x{rva:X}, got {ins.mnemonic} {ins.op_str}"
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
        raise InspectError(f"literal mismatch at 0x{rva:X}: {observed!r} != {expected!r}")
    return expected.decode("ascii")


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

    envelope_fields = {
        "version": expect_literal(pe, data, 0x3C40D1, b"version"),
        "type": expect_literal(pe, data, 0x3C412E, b"type"),
        "profileId": expect_literal(pe, data, 0x3C4155, b"profileId"),
        "instanceId": expect_literal(pe, data, 0x3C417C, b"instanceId"),
        "requestId": expect_literal(pe, data, 0x3C419F, b"requestId"),
        "timestamp": expect_literal(pe, data, 0x3C41BE, b"timestamp"),
        "payload": expect_literal(pe, data, 0x3C41F3, b"payload"),
    }

    hello_fields = {
        "profileId": expect_literal(pe, data, 0x0E3B2F, b"profileId"),
        "token": expect_literal(pe, data, 0x0E3B77, b"/payload/token"),
        "pid": expect_literal(pe, data, 0x0E3BC0, b"/payload/pid"),
        "buildId": expect_literal(pe, data, 0x0E3BFE, b"/payload/buildId"),
        "type": expect_literal(pe, data, 0x0E3C74, b"type"),
    }

    hello_type = {
        "lengthCheck": expect_contains(pe, data, 0x0E3C9B, "cmp", "5"),
        "helloPrefix": expect_contains(pe, data, 0x0E3CB3, "mov", "0x6c6c6568"),
        "helloSuffix": expect_contains(pe, data, 0x0E3CBE, "xor", "0x6f"),
    }

    pid_gate = {
        "rangeBase": expect_contains(pe, data, 0x0E3CED, "movabs", "0xffffffff00000000"),
        "rangeFloor": expect_contains(pe, data, 0x0E3CFF, "movabs", "0xffffffff00000001"),
        "processVerifier": expect_direct_call(pe, data, 0x0E3FD7, 0x3C424B),
        "getClientPid": expect_iat_call(
            pe, data, imports, 0x3C4283, "GetNamedPipeClientProcessId"
        ),
        "pidEquality": expect_contains(pe, data, 0x3C4291, "cmp", "dword ptr [rsp + 0x38], ebx"),
        "openProcessAccess": expect_contains(pe, data, 0x3C42E1, "mov", "0x1000"),
        "openProcess": expect_iat_call(pe, data, imports, 0x3C42EB, "OpenProcess"),
        "queryImage": expect_iat_call(
            pe, data, imports, 0x3C432D, "QueryFullProcessImageNameW"
        ),
        "pathLengthCompare": expect_contains(pe, data, 0x3C4538, "cmp", "qword ptr [rdi + 0x10]"),
        "asciiCaseFoldCompare": expect_contains(pe, data, 0x3C4581, "cmp", "r8b, r9b"),
    }

    build_gate = {
        "lengthCompare": expect_contains(pe, data, 0x0E3F87, "cmp", "qword ptr [r13 + 0x50]"),
        "bytesCompare": expect_direct_call(pe, data, 0x0E3F98, 0x79C7F0),
    }

    registry_gate = {
        "admissionCall": expect_direct_call(pe, data, 0x0E41D0, 0x3C47AB),
        "profileLength": expect_contains(pe, data, 0x3C4812, "cmp", "qword ptr [r12 + 0x10], r13"),
        "profileBytes": expect_direct_call(pe, data, 0x3C4824, 0x79C7F0),
        "claimedCheck": expect_contains(pe, data, 0x3C482D, "cmp", "byte ptr [r12 + 0x40], 0"),
        "expiryCheck": expect_contains(pe, data, 0x3C483D, "cmp", "qword ptr [r12 + 0x38], rax"),
        "hashLoopBytes": expect_contains(pe, data, 0x3C4856, "cmp", "rcx, 0x20"),
        "claimedWrite": expect_contains(pe, data, 0x3C48A4, "mov", "byte ptr [r12 + 0x40], 1"),
        "generationRead": expect_contains(pe, data, 0x3C48AA, "mov", "qword ptr [rbx + 0x60]"),
        "generationWrite": expect_contains(pe, data, 0x3C48BC, "mov", "qword ptr [rbx + 0x60], r13"),
    }

    return {
        "ok": True,
        "schemaVersion": 1,
        "findingId": "LWB-R7-113",
        "source": {
            "path": "../LW/lwbridge-0.3.1.exe",
            "sha256": digest,
            "imageBase": f"0x{pe.OPTIONAL_HEADER.ImageBase:X}",
        },
        "envelopeValidator": envelope_fields,
        "helloFields": hello_fields,
        "helloType": hello_type,
        "pidAndClientImageGate": pid_gate,
        "buildIdGate": build_gate,
        "registryAdmission": registry_gate,
        "recovered": {
            "claimedPidRange": "1..UInt32.MaxValue",
            "actualClientPidMustEqualClaimedPid": True,
            "clientImagePathMustMatchExpectedPath": True,
            "clientImageComparison": "length equality plus ASCII A-Z case-folded byte equality after helper normalization",
            "buildIdComparison": "exact length + byte equality",
            "tokenComparison": "SHA-256(token), fixed 32-byte XOR accumulator",
            "firstClaim": "deadline enforced, then claimed=true",
            "generation": "host-global increment with saturation semantics from R7-098",
        },
        "boundary": {
            "exactClientImageNormalizationHelperRecovered": False,
            "nativeHandshakeImplemented": False,
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
