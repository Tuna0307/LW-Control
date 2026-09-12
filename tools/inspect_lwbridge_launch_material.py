#!/usr/bin/env python3
"""Verify recovered outer-host launch-proof and launch-ticket source selection.

This inspector is intentionally build-specific and read-only. It validates the
immutable lwbridge-0.3.1.exe hash, then checks exact preferred-VA instructions
in the outer profile-launch state machine that consume ``launchProof`` from a
``LeaseActivationResponse`` value and select among the recovered launch-ticket
sources. It never executes LWBridge or Last War.
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


EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
EXPECTED_IMAGE_BASE = 0x140000000
PROFILE_LAUNCH_FUNCTION = (0x1401D3F1B, 0x1401DCAD1)

PROOF = {
    "responseFieldNameVa": 0x1401DA425,
    "responseTypeVa": 0x1401DA43A,
    "responseTypeLengthVa": 0x1401DA441,
    "responseTypeLength": 0x17,
    "responseDecodeCallVa": 0x1401DA44D,
    "responseDecodeTargetVa": 0x1402AAEBD,
    "proofStateCopyVa": 0x1401DA857,
    "proofStateOffset": 0x690,
    "envelopeFieldNameVa": 0x1401DA91D,
    "envelopeFieldLengthVa": 0x1401DA92C,
    "envelopeFieldLength": 0x0B,
    "envelopeValueSourceVa": 0x1401DA937,
}

TICKET_SELECTION = {
    "candidatePrepareCallVa": 0x1401D67D2,
    "candidatePrepareTargetVa": 0x1401E4910,
    "missingTicketLogVa": 0x1401D67F0,
    "missingTicketLogLengthVa": 0x1401D67F7,
    "missingTicketLogLength": 0x4B,
    "ticketStateVa": 0x1401D6824,
    "ticketStateOffset": 0x4C0,
    "candidateCommitCallVa": 0x1401D6833,
    "candidateCommitTargetVa": 0x1401E470F,
    "primarySourceVa": 0x1401D6866,
    "independentSourceVa": 0x1401D687D,
    "cachedSourceVa": 0x1401D6884,
    "cachedIndependentSelectVa": 0x1401D688B,
    "selectedSourceLogVa": 0x1401D68CC,
}

CACHED_REPORT = {
    "pidFieldVa": 0x1401DB605,
    "pidFieldLengthVa": 0x1401DB60C,
    "pidFieldLength": 3,
    "ticketFieldVa": 0x1401DB65E,
    "ticketFieldLengthVa": 0x1401DB665,
    "ticketFieldLength": 0x10,
    "expiresFieldVa": 0x1401DB81E,
    "expiresFieldLengthVa": 0x1401DB825,
    "expiresFieldLength": 0x19,
    "fallbackVa": 0x1401DB993,
    "fallbackCallVa": 0x1401DB9B3,
    "fallbackCallTargetVa": 0x14033C51B,
    "fallbackBranches": (
        0x1401DB658,
        0x1401DB818,
        0x1401DB836,
        0x1401DB83F,
        0x1401DB848,
        0x1401DB851,
        0x1401DB85C,
    ),
}


class InspectError(ValueError):
    pass


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


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


def _expect_immediate(instruction: Any, expected: int) -> None:
    actual = _immediate(instruction)
    if actual != expected:
        raise InspectError(
            f"unexpected immediate at 0x{instruction.address:X}: {actual!r}; expected 0x{expected:X}"
        )


def _expect_displacement(instruction: Any, expected: int) -> None:
    for operand in instruction.operands:
        if operand.type == X86_OP_MEM and operand.mem.disp == expected:
            return
    raise InspectError(
        f"instruction 0x{instruction.address:X} does not reference displacement 0x{expected:X}"
    )


def _text_at_va(pe: pefile.PE, data: bytes, image_base: int, va: int, size: int) -> bytes:
    offset = int(pe.get_offset_from_rva(va - image_base))
    return data[offset : offset + size]


def _expect_rip_text(
    pe: pefile.PE,
    data: bytes,
    image_base: int,
    instruction: Any,
    expected_prefix: str,
) -> int:
    target = _rip_target(instruction)
    if target is None:
        raise InspectError(f"instruction 0x{instruction.address:X} has no RIP-relative operand")
    raw = expected_prefix.encode("ascii")
    if _text_at_va(pe, data, image_base, target, len(raw)) != raw:
        raise InspectError(
            f"RIP target 0x{target:X} from 0x{instruction.address:X} does not begin with {expected_prefix!r}"
        )
    return target


def inspect(path: Path) -> dict[str, Any]:
    data = path.read_bytes()
    digest = _sha256(data)
    if digest != EXPECTED_SHA256:
        raise InspectError(f"unsupported lwbridge SHA-256 {digest}; expected {EXPECTED_SHA256}")

    pe = pefile.PE(data=data, fast_load=False)
    image_base = int(pe.OPTIONAL_HEADER.ImageBase)
    if image_base != EXPECTED_IMAGE_BASE:
        raise InspectError(
            f"unexpected preferred image base 0x{image_base:X}; expected 0x{EXPECTED_IMAGE_BASE:X}"
        )
    if PROFILE_LAUNCH_FUNCTION not in _runtime_functions(pe, image_base):
        raise InspectError(
            "profile-launch runtime-function range is absent: "
            f"0x{PROFILE_LAUNCH_FUNCTION[0]:X}-0x{PROFILE_LAUNCH_FUNCTION[1]:X}"
        )

    instructions = _instruction_map(pe, data, image_base, *PROFILE_LAUNCH_FUNCTION)

    proof_name = _instruction(instructions, PROOF["responseFieldNameVa"], "lea")
    _expect_rip_text(pe, data, image_base, proof_name, "launchProof")
    response_type = _instruction(instructions, PROOF["responseTypeVa"], "lea")
    response_type_target = _expect_rip_text(
        pe, data, image_base, response_type, "LeaseActivationResponse"
    )
    _expect_immediate(
        _instruction(instructions, PROOF["responseTypeLengthVa"], "mov"),
        PROOF["responseTypeLength"],
    )
    response_decode = _instruction(instructions, PROOF["responseDecodeCallVa"], "call")
    if _call_target(response_decode) != PROOF["responseDecodeTargetVa"]:
        raise InspectError("LeaseActivationResponse decode/extract call target changed")
    proof_copy = _instruction(instructions, PROOF["proofStateCopyVa"], "movdqu")
    _expect_displacement(proof_copy, PROOF["proofStateOffset"])
    envelope_name = _instruction(instructions, PROOF["envelopeFieldNameVa"], "lea")
    _expect_rip_text(pe, data, image_base, envelope_name, "launchProof")
    _expect_immediate(
        _instruction(instructions, PROOF["envelopeFieldLengthVa"], "mov"),
        PROOF["envelopeFieldLength"],
    )
    envelope_value = _instruction(instructions, PROOF["envelopeValueSourceVa"], "lea")
    _expect_displacement(envelope_value, PROOF["proofStateOffset"])

    candidate_prepare = _instruction(
        instructions, TICKET_SELECTION["candidatePrepareCallVa"], "call"
    )
    if _call_target(candidate_prepare) != TICKET_SELECTION["candidatePrepareTargetVa"]:
        raise InspectError("recovered ticket-candidate preparation target changed")
    missing_log = _instruction(instructions, TICKET_SELECTION["missingTicketLogVa"], "lea")
    _expect_rip_text(
        pe,
        data,
        image_base,
        missing_log,
        "profile launch requesting independent official ticket reason=ticket_missing",
    )
    _expect_immediate(
        _instruction(instructions, TICKET_SELECTION["missingTicketLogLengthVa"], "mov"),
        TICKET_SELECTION["missingTicketLogLength"],
    )
    ticket_state = _instruction(instructions, TICKET_SELECTION["ticketStateVa"], "lea")
    _expect_displacement(ticket_state, TICKET_SELECTION["ticketStateOffset"])
    candidate_commit = _instruction(
        instructions, TICKET_SELECTION["candidateCommitCallVa"], "call"
    )
    if _call_target(candidate_commit) != TICKET_SELECTION["candidateCommitTargetVa"]:
        raise InspectError("recovered ticket-candidate commit/copy target changed")

    source_rows: list[dict[str, Any]] = []
    for label, va in (
        ("primary_official", TICKET_SELECTION["primarySourceVa"]),
        ("independent_official", TICKET_SELECTION["independentSourceVa"]),
        ("cached_reusable", TICKET_SELECTION["cachedSourceVa"]),
    ):
        instruction = _instruction(instructions, va, "lea")
        target = _expect_rip_text(pe, data, image_base, instruction, label)
        source_rows.append(
            {
                "label": label,
                "labelLength": len(label),
                "xrefPreferredVa": f"0x{va:X}",
                "stringPreferredVa": f"0x{target:X}",
            }
        )
    _instruction(
        instructions, TICKET_SELECTION["cachedIndependentSelectVa"], "cmovo"
    )
    selected_source_log = _instruction(
        instructions, TICKET_SELECTION["selectedSourceLogVa"], "lea"
    )
    _expect_rip_text(
        pe, data, image_base, selected_source_log, "&profile launch ticket selected source="
    )

    cached_fields: list[dict[str, Any]] = []
    for name, key_va, length_va, length in (
        (
            "pid",
            CACHED_REPORT["pidFieldVa"],
            CACHED_REPORT["pidFieldLengthVa"],
            CACHED_REPORT["pidFieldLength"],
        ),
        (
            "gameLaunchTicket",
            CACHED_REPORT["ticketFieldVa"],
            CACHED_REPORT["ticketFieldLengthVa"],
            CACHED_REPORT["ticketFieldLength"],
        ),
        (
            "gameLaunchTicketExpiresAt",
            CACHED_REPORT["expiresFieldVa"],
            CACHED_REPORT["expiresFieldLengthVa"],
            CACHED_REPORT["expiresFieldLength"],
        ),
    ):
        key_instruction = _instruction(instructions, key_va, "lea")
        key_target = _expect_rip_text(pe, data, image_base, key_instruction, name)
        _expect_immediate(_instruction(instructions, length_va, "mov"), length)
        cached_fields.append(
            {
                "name": name,
                "nameLength": length,
                "nameXrefPreferredVa": f"0x{key_va:X}",
                "stringPreferredVa": f"0x{key_target:X}",
            }
        )

    fallback_branches: list[str] = []
    for branch_va in CACHED_REPORT["fallbackBranches"]:
        branch = _instruction(instructions, branch_va)
        if branch.mnemonic not in {"je", "jne"} or _immediate(branch) != CACHED_REPORT["fallbackVa"]:
            raise InspectError(
                f"cached-ticket fallback branch changed at preferred VA 0x{branch_va:X}"
            )
        fallback_branches.append(f"0x{branch_va:X}")
    fallback_call = _instruction(instructions, CACHED_REPORT["fallbackCallVa"], "call")
    if _call_target(fallback_call) != CACHED_REPORT["fallbackCallTargetVa"]:
        raise InspectError("cached-ticket fallback call target changed")

    return {
        "findingId": "LWB-R5-004",
        "date": "2026-09-08",
        "scope": "outer launch-proof response propagation and launch-ticket source selection/cache reuse",
        "evidenceStatus": "RECOVERED",
        "sourceIdentity": {
            "artifact": "../LW/lwbridge-0.3.1.exe",
            "sha256": digest,
            "preferredImageBase": f"0x{image_base:X}",
            "coordinateSystem": "preferred virtual addresses (VA)",
        },
        "profileLaunchRuntimeFunctionPreferredVa": (
            f"0x{PROFILE_LAUNCH_FUNCTION[0]:X}-0x{PROFILE_LAUNCH_FUNCTION[1]:X}"
        ),
        "launchProof": {
            "responseType": "LeaseActivationResponse",
            "responseTypeStringPreferredVa": f"0x{response_type_target:X}",
            "responseTypeXrefPreferredVa": f"0x{PROOF['responseTypeVa']:X}",
            "responseDecodeCallPreferredVa": f"0x{PROOF['responseDecodeCallVa']:X}",
            "responseDecodeTargetPreferredVa": f"0x{PROOF['responseDecodeTargetVa']:X}",
            "responseField": "launchProof",
            "proofStateOffset": f"0x{PROOF['proofStateOffset']:X}",
            "proofStateCopyPreferredVa": f"0x{PROOF['proofStateCopyVa']:X}",
            "envelopeFieldXrefPreferredVa": f"0x{PROOF['envelopeFieldNameVa']:X}",
            "envelopeValueSourcePreferredVa": f"0x{PROOF['envelopeValueSourceVa']:X}",
        },
        "ticketSelection": {
            "missingTicketReason": "ticket_missing",
            "missingTicketLogPreferredVa": f"0x{TICKET_SELECTION['missingTicketLogVa']:X}",
            "ticketStateOffset": f"0x{TICKET_SELECTION['ticketStateOffset']:X}",
            "ticketStateReferencePreferredVa": f"0x{TICKET_SELECTION['ticketStateVa']:X}",
            "candidatePrepareCallPreferredVa": f"0x{TICKET_SELECTION['candidatePrepareCallVa']:X}",
            "candidatePrepareTargetPreferredVa": f"0x{TICKET_SELECTION['candidatePrepareTargetVa']:X}",
            "candidateCommitCallPreferredVa": f"0x{TICKET_SELECTION['candidateCommitCallVa']:X}",
            "candidateCommitTargetPreferredVa": f"0x{TICKET_SELECTION['candidateCommitTargetVa']:X}",
            "sourceLabels": source_rows,
            "cachedIndependentSelectPreferredVa": f"0x{TICKET_SELECTION['cachedIndependentSelectVa']:X}",
            "selectedSourceLogPreferredVa": f"0x{TICKET_SELECTION['selectedSourceLogVa']:X}",
        },
        "cachedLauncherReport": {
            "fields": cached_fields,
            "fallbackPreferredVa": f"0x{CACHED_REPORT['fallbackVa']:X}",
            "fallbackBranchPreferredVas": fallback_branches,
            "fallbackProcessingCallPreferredVa": f"0x{CACHED_REPORT['fallbackCallVa']:X}",
            "fallbackProcessingTargetPreferredVa": f"0x{CACHED_REPORT['fallbackCallTargetVa']:X}",
        },
        "result": [
            "The inspected outer profile-launch state consumes a field named launchProof from a value named LeaseActivationResponse and later serializes that propagated value as LaunchEnvelope.launchProof.",
            "The outer host has three explicit launch-ticket source labels: primary_official, cached_reusable, and independent_official.",
            "When the reusable candidate path is missing, the state machine logs reason=ticket_missing before the independent-official candidate path and later logs the selected ticket source.",
            "Cached launcher state is queried for pid, gameLaunchTicket, and gameLaunchTicketExpiresAt; missing/type/shape failures converge on the recovered fallback-processing path.",
        ],
        "reproduction": [
            "python tools\\inspect_lwbridge_launch_material.py ..\\LW\\lwbridge-0.3.1.exe",
            "python tools\\inspect_lwbridge_launch_material.py ..\\LW\\lwbridge-0.3.1.exe --output evidence\\lwbridge-implementation\\2026-09-08-r5-launch-material.json",
        ],
        "validationAndLimits": {
            "validation": "static-only against the immutable verified LWBridge reference",
            "liveProven": False,
            "unknownBlocked": [
                "the producer/service implementation that creates LeaseActivationResponse and launchProof",
                "launchProof signing inputs and complete claim-name/order mapping",
                "gameLaunchTicket signing inputs and exact primary/independent producer implementation",
                "semantic meaning of the LWLT2 extra decimal field",
                "ticket ownership and consumption protocol/state transitions",
                "semantic contract of fallback helper 0x14033C51B beyond its recovered callsite and inputs",
                "exact outer-host child argument construction/quoting/input channel",
                "current-client live launch/bootstrap acceptance",
            ],
        },
        "implementationImpact": {
            "r5LaunchProofSource": "response propagation recovered; producer/signing remain blocked",
            "r5TicketSelection": "source taxonomy and cached-report reuse gate recovered; producer/signing/ownership remain blocked",
            "profileInstanceStart": "remains fail-closed with OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED",
            "nextTrace": "recover LeaseActivationResponse producer/signing inputs, ticket ownership/consumption transitions, and exact child argument construction without guessing helper semantics",
        },
    }


def _text_report(result: dict[str, Any]) -> str:
    lines = [
        f"finding={result['findingId']} status={result['evidenceStatus']}",
        f"lwbridge_sha256={result['sourceIdentity']['sha256']}",
        f"profile_launch_function={result['profileLaunchRuntimeFunctionPreferredVa']}",
        f"launch_proof_response_type={result['launchProof']['responseType']}",
        "ticket_sources="
        + ",".join(row["label"] for row in result["ticketSelection"]["sourceLabels"]),
        "cached_report_fields="
        + ",".join(row["name"] for row in result["cachedLauncherReport"]["fields"]),
        "limits:",
    ]
    for item in result["validationAndLimits"]["unknownBlocked"]:
        lines.append(f"  UNKNOWN/BLOCKED: {item}")
    return "\n".join(lines)


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

    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    if args.json:
        print(json.dumps(result, indent=2))
    else:
        print(_text_report(result))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
