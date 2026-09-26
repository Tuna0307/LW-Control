#!/usr/bin/env python3
"""Verify recovered LWBridge child launch-proof/ticket validation contracts.

This inspector is intentionally build-specific and read-only. It validates the
immutable lwbridge-0.3.1 reference, extracts the embedded profile launcher by
RVA, verifies its hash, and checks the exact preferred-VA instructions used by
the recovered descriptor/proof/ticket validators. It never executes LWBridge,
the embedded launcher, or Last War.
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


EXPECTED_LWBRIDGE_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
PROFILE_LAUNCHER_RVA = 0xB72B52
PROFILE_LAUNCHER_SIZE = 0xA7200
EXPECTED_PROFILE_LAUNCHER_SHA256 = "8f42adb9ed678445425e529cdee8f12a097c63053f9d4de314a758a8dfe362de"
EXPECTED_LAUNCHER_IMAGE_BASE = 0x140000000

FUNCTIONS = {
    "launchEnvelopeParser": (0x1400184B0, 0x140019154),
    "descriptorValidator": (0x14002DB30, 0x14002E072),
    "ticketParser": (0x14002E080, 0x14002EB4C),
    "proofValidator": (0x14002EF70, 0x14002F823),
}

ERROR_MARKERS = (
    "DESCRIPTOR_REQUIRED",
    "DESCRIPTOR_INVALID",
    "LAUNCH_EXPIRED",
    "LAUNCH_PROOF_INVALID",
    "LAUNCH_PROOF_MISMATCH",
    "LAUNCH_PROOF_EXPIRED",
    "LAUNCH_TICKET_INVALID",
    "LAUNCH_TICKET_OWNERSHIP_CHANGED",
    "LAUNCH_TICKET_CONSUMPTION_FAILED",
    "LAUNCH_TICKET_CONSUMPTION_TIMEOUT",
)


class InspectError(ValueError):
    pass


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _extract_profile_launcher(outer: bytes) -> bytes:
    pe = pefile.PE(data=outer, fast_load=False)
    try:
        offset = int(pe.get_offset_from_rva(PROFILE_LAUNCHER_RVA))
    except pefile.PEFormatError as exc:
        raise InspectError(
            f"profile launcher RVA 0x{PROFILE_LAUNCHER_RVA:X} is not file-backed"
        ) from exc
    payload = outer[offset : offset + PROFILE_LAUNCHER_SIZE]
    if len(payload) != PROFILE_LAUNCHER_SIZE:
        raise InspectError("embedded profile launcher is truncated")
    return payload


def _runtime_functions(pe: pefile.PE, image_base: int) -> set[tuple[int, int]]:
    pe.parse_data_directories(
        directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]]
    )
    return {
        (image_base + int(entry.struct.BeginAddress), image_base + int(entry.struct.EndAddress))
        for entry in getattr(pe, "DIRECTORY_ENTRY_EXCEPTION", [])
        if int(entry.struct.BeginAddress) < int(entry.struct.EndAddress)
    }


def _instruction_map(
    pe: pefile.PE, data: bytes, image_base: int, begin_va: int, end_va: int
) -> dict[int, Any]:
    offset = int(pe.get_offset_from_rva(begin_va - image_base))
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    return {
        instruction.address: instruction
        for instruction in md.disasm(data[offset : offset + (end_va - begin_va)], begin_va)
    }


def _instruction(instructions: dict[int, Any], va: int, mnemonic: str | None = None) -> Any:
    instruction = instructions.get(va)
    if instruction is None:
        raise InspectError(f"missing instruction at preferred VA 0x{va:X}")
    if mnemonic is not None and instruction.mnemonic != mnemonic:
        raise InspectError(
            f"unexpected instruction at preferred VA 0x{va:X}: "
            f"{instruction.mnemonic} {instruction.op_str}; expected {mnemonic}"
        )
    return instruction


def _immediate(instruction: Any) -> int | None:
    for operand in instruction.operands:
        if operand.type == X86_OP_IMM:
            return int(operand.imm)
    return None


def _rip_target(instruction: Any) -> int | None:
    for operand in instruction.operands:
        if operand.type == X86_OP_MEM and operand.mem.base == X86_REG_RIP:
            return instruction.address + instruction.size + operand.mem.disp
    return None


def _call_target(instruction: Any) -> int | None:
    if instruction.mnemonic != "call" or not instruction.operands:
        return None
    operand = instruction.operands[0]
    return int(operand.imm) if operand.type == X86_OP_IMM else None


def _expect_imm(instructions: dict[int, Any], va: int, mnemonic: str, expected: int) -> None:
    instruction = _instruction(instructions, va, mnemonic)
    if _immediate(instruction) != expected:
        raise InspectError(
            f"unexpected immediate at preferred VA 0x{va:X}: "
            f"{instruction.op_str}; expected 0x{expected:X}"
        )


def _expect_call(instructions: dict[int, Any], va: int, target: int) -> None:
    instruction = _instruction(instructions, va, "call")
    if _call_target(instruction) != target:
        raise InspectError(
            f"unexpected call target at preferred VA 0x{va:X}: "
            f"{instruction.op_str}; expected 0x{target:X}"
        )


def _read_ascii_at_va(pe: pefile.PE, data: bytes, image_base: int, va: int, length: int) -> str:
    offset = int(pe.get_offset_from_rva(va - image_base))
    return data[offset : offset + length].decode("ascii", "strict")


def _expect_rip_ascii(
    pe: pefile.PE,
    data: bytes,
    image_base: int,
    instructions: dict[int, Any],
    va: int,
    mnemonic: str,
    expected: str,
) -> None:
    instruction = _instruction(instructions, va, mnemonic)
    target = _rip_target(instruction)
    if target is None:
        raise InspectError(f"instruction 0x{va:X} has no RIP-relative target")
    actual = _read_ascii_at_va(pe, data, image_base, target, len(expected))
    if actual != expected:
        raise InspectError(
            f"unexpected text at target of 0x{va:X}: {actual!r}; expected {expected!r}"
        )


def inspect(path: Path) -> dict[str, Any]:
    outer = path.read_bytes()
    outer_digest = _sha256(outer)
    if outer_digest != EXPECTED_LWBRIDGE_SHA256:
        raise InspectError(
            f"unsupported lwbridge SHA-256 {outer_digest}; expected {EXPECTED_LWBRIDGE_SHA256}"
        )

    launcher = _extract_profile_launcher(outer)
    launcher_digest = _sha256(launcher)
    if launcher_digest != EXPECTED_PROFILE_LAUNCHER_SHA256:
        raise InspectError(
            "embedded profile launcher SHA-256 changed: "
            f"{launcher_digest}; expected {EXPECTED_PROFILE_LAUNCHER_SHA256}"
        )

    pe = pefile.PE(data=launcher, fast_load=False)
    image_base = int(pe.OPTIONAL_HEADER.ImageBase)
    if image_base != EXPECTED_LAUNCHER_IMAGE_BASE:
        raise InspectError(
            f"unexpected launcher image base 0x{image_base:X}; "
            f"expected 0x{EXPECTED_LAUNCHER_IMAGE_BASE:X}"
        )

    runtime_functions = _runtime_functions(pe, image_base)
    for name, function_range in FUNCTIONS.items():
        if function_range not in runtime_functions:
            raise InspectError(
                f"{name} runtime-function range absent: "
                f"0x{function_range[0]:X}-0x{function_range[1]:X}"
            )

    parsed = {
        name: _instruction_map(pe, launcher, image_base, *function_range)
        for name, function_range in FUNCTIONS.items()
    }
    envelope = parsed["launchEnvelopeParser"]
    descriptor = parsed["descriptorValidator"]
    ticket = parsed["ticketParser"]
    proof = parsed["proofValidator"]

    # LaunchEnvelope child parser: recovered Serde object fields.
    _expect_rip_ascii(pe, launcher, image_base, envelope, 0x140018625, "xor", "descriptorJson")
    _expect_rip_ascii(pe, launcher, image_base, envelope, 0x1400186E3, "xor", "launchProof")
    _expect_rip_ascii(pe, launcher, image_base, envelope, 0x140018655, "movdqu", "gameLaunchTicket")

    # Descriptor validator: expiry gate, 1..96 instance-id length, >=32 launch-token length.
    _expect_imm(descriptor, 0x14002DB68, "cmp", 0x5F)
    _expect_imm(descriptor, 0x14002DC0E, "cmp", 0x20)

    # Proof framing: exactly two dot-separated encoded segments.
    _expect_imm(proof, 0x14002EFCE, "movabs", 0x2E0000002E)
    _expect_call(proof, 0x14002EFF9, 0x140030440)
    _expect_call(proof, 0x14002F017, 0x140030440)
    _expect_call(proof, 0x14002F05D, 0x140030440)
    _expect_call(proof, 0x14002F07F, 0x140032FA0)
    _expect_call(proof, 0x14002F0C7, 0x140032FA0)
    _expect_call(proof, 0x14002F306, 0x14004FF20)

    # Decoded proof payload grammar and timing/mismatch gates.
    _expect_imm(proof, 0x14002F3EA, "movabs", 0x7C0000007C)
    _expect_imm(proof, 0x14002F44D, "mov", 0x504D574C)  # "LWPM"
    _expect_imm(proof, 0x14002F458, "xor", 0x31)  # "1"
    _expect_imm(proof, 0x14002F472, "mov", 0x6E75616C)  # "laun"
    _expect_imm(proof, 0x14002F47D, "xor", 0x6863)  # "ch"
    for va in (0x14002F499, 0x14002F4CF, 0x14002F502):
        _expect_call(proof, va, 0x14002D2A0)
    _expect_imm(proof, 0x14002F529, "add", 0x7530)  # 30,000 ms
    _expect_imm(proof, 0x14002F58C, "cmp", 0x493E0)  # 300,000 ms
    _expect_rip_ascii(pe, launcher, image_base, proof, 0x14002F53E, "lea", "LAUNCH_PROOF_EXPIRED")
    _expect_rip_ascii(pe, launcher, image_base, proof, 0x14002F783, "lea", "LAUNCH_PROOF_MISMATCH")

    # Ticket parser: six-field LWLT1 / seven-field LWLT2, fixed product string,
    # 1..300-second timestamp span, 32 hex chars plus 128 hex signature chars.
    _expect_imm(ticket, 0x14002E278, "cmp", 7)
    _expect_imm(ticket, 0x14002E282, "cmp", 6)
    _expect_imm(ticket, 0x14002E293, "cmp", 5)
    _expect_imm(ticket, 0x14002E2A5, "mov", 0x544C574C)  # "LWLT"
    _expect_imm(ticket, 0x14002E2B0, "xor", 0x31)
    _expect_imm(ticket, 0x14002E3A0, "mov", 0x544C574C)
    _expect_imm(ticket, 0x14002E3AB, "xor", 0x32)
    _expect_imm(ticket, 0x14002E2BF, "cmp", 0xF)
    _expect_imm(ticket, 0x14002E3B6, "cmp", 0xF)
    _expect_imm(ticket, 0x14002E4E0, "cmp", 0x12D)  # reject >=301-second span
    _expect_imm(ticket, 0x14002E528, "cmp", 0x20)
    _expect_imm(ticket, 0x14002E987, "cmp", 0x80)
    _expect_rip_ascii(pe, launcher, image_base, ticket, 0x14002E357, "lea", "LAUNCH_TICKET_INVALID")

    for marker in ERROR_MARKERS:
        if marker.encode("ascii") not in launcher:
            raise InspectError(f"expected launcher error marker is absent: {marker}")

    return {
        "findingId": "LWB-R5-003",
        "date": "2026-09-08",
        "scope": "child LaunchEnvelope parsing plus descriptor/proof/ticket validation",
        "evidenceStatus": "RECOVERED",
        "sourceIdentity": {
            "outerArtifact": "../LW/lwbridge-0.3.1.exe",
            "outerSha256": outer_digest,
            "embeddedProfileLauncherSha256": launcher_digest,
            "embeddedProfileLauncherRva": f"0x{PROFILE_LAUNCHER_RVA:X}",
            "embeddedProfileLauncherSize": PROFILE_LAUNCHER_SIZE,
            "preferredImageBase": f"0x{image_base:X}",
            "coordinateSystem": "embedded launcher preferred virtual addresses (VA)",
        },
        "locators": {
            name: f"0x{begin:X}-0x{end:X}" for name, (begin, end) in FUNCTIONS.items()
        },
        "result": {
            "launchEnvelope": {
                "jsonFields": ["descriptorJson", "launchProof", "gameLaunchTicket"],
                "childParserRecovered": True,
            },
            "descriptorValidation": {
                "expiryFailure": "LAUNCH_EXPIRED",
                "instanceIdLength": {"min": 1, "max": 96},
                "launchTokenMinLength": 32,
                "note": "other path/isolation/mode/proof constraints remain only partially mapped",
            },
            "launchProof": {
                "segments": 2,
                "separator": ".",
                "decodedPayloadPrefixFields": ["LWPM1", "launch"],
                "numericFieldsParsed": 3,
                "freshnessAllowanceMs": 30000,
                "maxTimestampSpanMs": 300000,
                "signatureVerificationCallPreferredVa": "0x14002F306 -> 0x14004FF20",
                "failures": ["LAUNCH_PROOF_INVALID", "LAUNCH_PROOF_EXPIRED", "LAUNCH_PROOF_MISMATCH"],
            },
            "gameLaunchTicket": {
                "delimiter": "|",
                "versions": {
                    "LWLT1": {"fieldCount": 6},
                    "LWLT2": {"fieldCount": 7, "extraField": "positive decimal value; exact semantic meaning unresolved"},
                },
                "product": "lastwar.windows",
                "timestampSpanSeconds": {"min": 1, "max": 300},
                "hexFieldLength": 32,
                "signatureHexLength": 128,
                "failures": [
                    "LAUNCH_TICKET_INVALID",
                    "LAUNCH_TICKET_OWNERSHIP_CHANGED",
                    "LAUNCH_TICKET_CONSUMPTION_FAILED",
                    "LAUNCH_TICKET_CONSUMPTION_TIMEOUT",
                ],
            },
        },
        "validationAndLimits": {
            "validation": "static-only against the immutable verified LWBridge reference",
            "liveProven": False,
            "unknownBlocked": [
                "complete LaunchDescriptor field type/semantic mapping beyond the recovered validation gates",
                "exact launchProof producer and decoded claim names/order beyond the recovered prefix/numeric/timing/match checks",
                "exact gameLaunchTicket producer/signing inputs and the semantic meaning of the LWLT2 extra positive decimal field",
                "ticket ownership and post-launch consumption protocol/state transitions",
                "outer-host child process argument construction/quoting and current-client live acceptance",
            ],
        },
        "implementationImpact": {
            "proofTicketValidator": "substantially recovered but producer/consumption prerequisites remain open",
            "profileInstanceStart": "remains fail-closed with OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED",
            "nextTrace": "trace outer-host proof/ticket producers and ticket ownership/consumption state machine before lifecycle implementation",
        },
        "reproduction": [
            "python tools\\inspect_lwbridge_launch_validation.py ..\\LW\\lwbridge-0.3.1.exe",
            "python tools\\inspect_lwbridge_launch_validation.py ..\\LW\\lwbridge-0.3.1.exe --json",
        ],
    }


def _text_report(result: dict[str, Any]) -> str:
    proof = result["result"]["launchProof"]
    ticket = result["result"]["gameLaunchTicket"]
    return "\n".join(
        [
            f"finding={result['findingId']} status={result['evidenceStatus']}",
            f"lwbridge_sha256={result['sourceIdentity']['outerSha256']}",
            f"launcher_sha256={result['sourceIdentity']['embeddedProfileLauncherSha256']}",
            f"proof=segments:{proof['segments']} separator:{proof['separator']} "
            f"prefix:{'|'.join(proof['decodedPayloadPrefixFields'])} "
            f"freshness_ms:{proof['freshnessAllowanceMs']} max_span_ms:{proof['maxTimestampSpanMs']}",
            f"ticket=versions:{','.join(ticket['versions'])} product:{ticket['product']} "
            f"span_s:{ticket['timestampSpanSeconds']['min']}..{ticket['timestampSpanSeconds']['max']} "
            f"hex:{ticket['hexFieldLength']} signature_hex:{ticket['signatureHexLength']}",
            "live_proven=false",
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

    rendered = json.dumps(result, indent=2) if args.json or args.output else _text_report(result)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(rendered)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
