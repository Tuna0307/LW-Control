#!/usr/bin/env python3
"""Verify original bridge-listener ownership and profile-launch registration cleanup.

R7-109 is static-only and hash-gated. It never executes the reference host,
opens a pipe, registers a live instance, launches a child, or mutates state.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
EXPECTED_IMAGE_BASE = 0x140000000


class InspectError(RuntimeError):
    pass


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _decoder() -> Cs:
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    return md


def _instruction(pe: pefile.PE, data: bytes, rva: int):
    md = _decoder()
    off = pe.get_offset_from_rva(rva)
    rows = list(md.disasm(data[off:off + 24], pe.OPTIONAL_HEADER.ImageBase + rva, count=1))
    if not rows:
        raise InspectError(f"no instruction at RVA 0x{rva:X}")
    ins = rows[0]
    if ins.address != pe.OPTIONAL_HEADER.ImageBase + rva:
        raise InspectError(f"decode drift at RVA 0x{rva:X}")
    return ins


def _expect_call(pe: pefile.PE, data: bytes, rva: int, target_rva: int) -> str:
    ins = _instruction(pe, data, rva)
    if ins.mnemonic != "call" or not ins.operands or ins.operands[0].type != X86_OP_IMM:
        raise InspectError(f"expected direct call at RVA 0x{rva:X}, got {ins.mnemonic} {ins.op_str}")
    observed = ins.operands[0].imm - pe.OPTIONAL_HEADER.ImageBase
    if observed != target_rva:
        raise InspectError(
            f"call target mismatch at RVA 0x{rva:X}: expected 0x{target_rva:X}, got 0x{observed:X}"
        )
    return f"{ins.mnemonic} {ins.op_str}"


def _expect_contains(pe: pefile.PE, data: bytes, rva: int, mnemonic: str, operand_text: str) -> str:
    ins = _instruction(pe, data, rva)
    if ins.mnemonic != mnemonic or operand_text not in ins.op_str:
        raise InspectError(
            f"instruction mismatch at RVA 0x{rva:X}: expected {mnemonic} containing "
            f"{operand_text!r}, got {ins.mnemonic} {ins.op_str}"
        )
    return f"{ins.mnemonic} {ins.op_str}"


def _rip_target(ins) -> int | None:
    for op in ins.operands:
        if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
            return ins.address + ins.size + op.mem.disp
    return None


def _expect_rip_ascii(
    pe: pefile.PE, data: bytes, rva: int, mnemonic: str, expected: bytes
) -> str:
    ins = _instruction(pe, data, rva)
    if ins.mnemonic != mnemonic:
        raise InspectError(f"expected {mnemonic} at RVA 0x{rva:X}")
    target = _rip_target(ins)
    if target is None:
        raise InspectError(f"missing RIP-relative operand at RVA 0x{rva:X}")
    target_rva = target - pe.OPTIONAL_HEADER.ImageBase
    off = pe.get_offset_from_rva(target_rva)
    observed = data[off:off + len(expected)]
    if observed != expected:
        raise InspectError(
            f"ASCII target mismatch at RVA 0x{rva:X}: expected {expected!r}, got {observed!r}"
        )
    return f"{ins.mnemonic} {ins.op_str} -> {expected.decode('ascii')}"


def inspect(path: Path) -> dict:
    data = path.read_bytes()
    digest = _sha256(data)
    if digest != EXPECTED_SHA256:
        raise InspectError(f"unsupported host SHA-256 {digest}")

    pe = pefile.PE(data=data, fast_load=False)
    if pe.OPTIONAL_HEADER.ImageBase != EXPECTED_IMAGE_BASE:
        raise InspectError(
            f"unexpected image base 0x{pe.OPTIONAL_HEADER.ImageBase:X}"
        )

    listener = {
        "appStartupToHostConstructor": _expect_call(pe, data, 0x4100B5, 0x3C30BE),
    }

    ordinary_start = {
        "commandLiteral": _expect_rip_ascii(
            pe, data, 0x1FF23B, "lea", b"profile_instance_start"
        ),
        "launchPoll": _expect_call(pe, data, 0x1FF83B, 0x1D3F1B),
    }

    registration = {
        "registerWrapper": _expect_call(pe, data, 0x1D7C94, 0x3CCA38),
        "launchEnvelopeSource": _expect_contains(
            pe, data, 0x1DB119, "lea", "[r14 + 0x730]"
        ),
        "launchEnvelopeClone": _expect_call(pe, data, 0x1DB12B, 0x02A2C0),
    }

    refresh = {
        "clock": _expect_call(pe, data, 0x1DC033, 0x23F1C0),
        "deadline": _expect_contains(pe, data, 0x1DC038, "add", "0x15f90"),
        "instancePointer": _expect_contains(
            pe, data, 0x1DC04C, "mov", "[r14 + 0x390]"
        ),
        "instanceLength": _expect_contains(
            pe, data, 0x1DC057, "mov", "[r14 + 0x398]"
        ),
        "refreshPending": _expect_call(pe, data, 0x1DC071, 0x3C2F0C),
    }

    cleanup = {
        "outerCleanupAwait": _expect_call(pe, data, 0x1D7D99, 0x1D311F),
        "instancePointer": _expect_contains(
            pe, data, 0x1D31FB, "mov", "[rbx + 0x48]"
        ),
        "instanceLength": _expect_contains(
            pe, data, 0x1D31FF, "mov", "[rbx + 0x50]"
        ),
        "unregisterWrapper": _expect_call(pe, data, 0x1D3203, 0x3CC8B3),
        "laterUnregisterWrapper": _expect_call(
            pe, data, 0x1D393C, 0x3CC8B3
        ),
        "unregisterCore": _expect_call(pe, data, 0x3CC8D2, 0x3C2436),
    }

    return {
        "ok": True,
        "schemaVersion": 1,
        "findingId": "LWB-R7-109",
        "source": {
            "path": "../LW/lwbridge-0.3.1.exe",
            "sha256": digest,
            "imageBase": f"0x{pe.OPTIONAL_HEADER.ImageBase:X}",
        },
        "listenerOwnership": {
            "scope": "host-global shared listener",
            "evidence": listener,
            "meaning": (
                "application startup constructs the bridge host; the recovered "
                "host constructor owns the per-user named-pipe accept loop"
            ),
        },
        "ordinaryProfileStart": ordinary_start,
        "ordering": {
            "registration": registration,
            "meaning": (
                "the instance registration is established before the later "
                "LaunchEnvelope is cloned into the profile-launch task"
            ),
        },
        "pendingRefresh": {
            "deadlineMilliseconds": 90000,
            "evidence": refresh,
            "meaning": (
                "while startup progresses, the same pending instance "
                "registration is refreshed to current clock + 90,000 ms"
            ),
        },
        "launchFailureCleanup": {
            "evidence": cleanup,
            "meaning": (
                "post-registration cleanup futures explicitly unregister the "
                "instance through the recovered wrapper/core path; deadline "
                "expiry is not treated as automatic registry deletion"
            ),
        },
        "boundary": {
            "staticOnly": True,
            "productionNamedPipeListenerImplemented": False,
            "productionCallLuaEnabled": False,
            "pendingCallCollectionImplemented": False,
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
