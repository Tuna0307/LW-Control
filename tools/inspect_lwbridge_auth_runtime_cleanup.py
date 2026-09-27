#!/usr/bin/env python3
"""Verify LWBridge 0.3.1 authorization ticket/envelope cleanup ownership."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"


class InspectError(ValueError):
    pass


def require(ok: bool, message: str) -> None:
    if not ok:
        raise InspectError(message)


def inspect(binary: Path) -> dict[str, object]:
    data = binary.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    require(digest == EXPECTED_SHA256, f"unsupported reference SHA-256: {digest}")

    pe = pefile.PE(data=data, fast_load=False)
    base = int(pe.OPTIONAL_HEADER.ImageBase)
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True

    def ins_at(rva: int):
        off = int(pe.get_offset_from_rva(rva))
        return next(md.disasm(data[off:off + 16], base + rva))

    def expect(rva: int, mnemonic: str, contains: str | None = None):
        ins = ins_at(rva)
        require(ins.mnemonic == mnemonic, f"0x{rva:X}: expected {mnemonic}, got {ins.mnemonic}")
        if contains is not None:
            require(contains in ins.op_str, f"0x{rva:X}: expected operand containing {contains!r}, got {ins.op_str!r}")
        return ins

    def direct_call_target(rva: int) -> int:
        ins = expect(rva, "call")
        require(ins.operands and ins.operands[0].type == X86_OP_IMM, f"0x{rva:X}: expected direct call")
        return int(ins.operands[0].imm - base)

    def rip_target(ins) -> int | None:
        for op in ins.operands:
            if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                return int(ins.address + ins.size + op.mem.disp - base)
        return None

    def read_ascii(rva: int, length: int) -> str:
        raw = int(pe.get_offset_from_rva(rva))
        return data[raw:raw + length].decode("ascii")

    def assert_rip_literal(rva: int, mnemonic: str, literal: str) -> None:
        ins = expect(rva, mnemonic)
        target = rip_target(ins)
        require(target is not None, f"0x{rva:X}: missing RIP target")
        require(read_ascii(target, len(literal)) == literal, f"0x{rva:X}: expected {literal!r}")

    # The service constructor owns both paths.
    assert_rip_literal(0x23D111, "lea", "authorization.ticket")
    assert_rip_literal(0x23D130, "lea", "package-key.envelope")
    expect(0x23D118, "lea", "rsp + 0x1f8")
    expect(0x23D137, "lea", "rsp + 0x218")

    # Constructor copy-out places the ticket PathBuf at service +0x178
    # and the envelope PathBuf at service +0x198.
    expect(0x23D1DA, "lea", "rsp + 0x528")
    expect(0x23D2EA, "lea", "rsp + 0x1f8")
    expect(0x23D2F9, "lea", "rsp + 0x218")
    expect(0x23D335, "movups", "[rdi - 0xc8]")
    expect(0x23D33C, "movups", "[rdi - 0xd8]")
    expect(0x23D343, "movups", "[rdi - 0xa8]")
    expect(0x23D34A, "movups", "[rdi - 0xb8]")
    expect(0x23D3E8, "lea", "rsp + 0x2d8")
    expect(0x23D3DF, "mov", "r8d, 0x398")

    # 0x23CBB8 is a two-path best-effort cleanup helper.
    expect(0x23CBC0, "mov", "qword ptr [rcx + 0x180]")
    expect(0x23CBC7, "mov", "qword ptr [rsi + 0x188]")
    require(direct_call_target(0x23CBCE) == 0x5B6540, "ticket delete-wrapper call mismatch")
    expect(0x23CBD8, "test", "rax, rax")
    require(direct_call_target(0x23CBE2) == 0x58588, "ticket cleanup error-drop mismatch")

    expect(0x23CBE7, "mov", "qword ptr [rsi + 0x1a0]")
    expect(0x23CBEE, "mov", "qword ptr [rsi + 0x1a8]")
    require(direct_call_target(0x23CBF5) == 0x5B6540, "envelope delete-wrapper call mismatch")
    expect(0x23CBFF, "test", "rax, rax")
    require(direct_call_target(0x23CC09) == 0x58588, "envelope cleanup error-drop mismatch")

    # R6-040 already source-locked 0x5B6540 as the host DeleteFileW wrapper.
    require(direct_call_target(0x5B65BD) == 0x5B8C00, "delete wrapper primitive mismatch")

    # Recovered direct callers show this helper is shared lifecycle cleanup,
    # not a grace-clock mutator.
    callers = {
        "heartbeat_error": 0x0F6DEB,
        "supervisor_session_error": 0x0F7352,
        "supervisor_no_session": 0x0F7C82,
        "login_projection": 0x2399F7,
        "auth_state_related": 0x23B2EE,
        "service_cleanup_wrapper": 0x23C6BA,
        "auth_artifact_ingest": 0x23CAB9,
    }
    for label, site in callers.items():
        require(direct_call_target(site) == 0x23CBB8, f"{label}: cleanup target mismatch")

    # The no-session branch cleans runtime artifacts, then publishes normal signedOut.
    require(direct_call_target(0x0F7C82) == 0x23CBB8, "no-session cleanup mismatch")
    require(direct_call_target(0x0F7C99) == 0x23855D, "no-session signedOut mismatch")

    # Invalid envelope ingestion also reaches the same delete wrapper directly,
    # consistent with the earlier R8-004/R6-040 artifact-lifecycle evidence.
    require(direct_call_target(0x23CA57) == 0x5B6540, "invalid-envelope delete mismatch")
    require(direct_call_target(0x23CAB9) == 0x23CBB8, "post-ingest shared cleanup mismatch")

    return {
        "findingId": "LWB-R8-094",
        "date": "2026-09-27",
        "status": "RECOVERED CONTRACT / CORRECTION",
        "sourceIdentity": {"path": str(binary), "sha256": digest},
        "cleanupHelper": {
            "rva": "0x23CBB8-0x23CC15",
            "ticketPathBufStart": "service+0x178",
            "ticketDataLengthMembers": ["service+0x180", "service+0x188"],
            "envelopePathBufStart": "service+0x198",
            "envelopeDataLengthMembers": ["service+0x1A0", "service+0x1A8"],
            "deleteWrapper": "0x5B6540",
            "errorPolicy": "best-effort: delete errors are consumed and cleanup continues",
        },
        "ownedFiles": [
            "authorization.ticket",
            "package-key.envelope",
        ],
        "directCallers": {label: hex(site) for label, site in callers.items()},
        "noUsableSession": {
            "cleanup": "0x23CBB8",
            "then": "normal signedOut emitter 0x23855D",
        },
        "correction": {
            "withdrawn": "0x23CBB8 resets the grace clock",
            "replacement": "0x23CBB8 best-effort deletes authorization.ticket and package-key.envelope",
        },
        "limits": [
            "R6-040 remains authority for the downstream DeleteFileW wrapper classification",
            "this checkpoint does not read stored ticket/envelope contents",
            "this checkpoint does not reconstruct protected package decryption or private key material",
            "no production behavior is changed",
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        result = inspect(args.binary)
    except (InspectError, OSError, pefile.PEFormatError) as exc:
        parser.error(str(exc))
    rendered = json.dumps(result, indent=2, sort_keys=True)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered + "\n", encoding="utf-8")
    print(rendered)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
