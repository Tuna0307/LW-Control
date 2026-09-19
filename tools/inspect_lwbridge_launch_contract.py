#!/usr/bin/env python3
"""Verify the recovered LWBridge host-side LaunchEnvelope producer.

This inspector is intentionally build-specific. It validates the immutable
lwbridge-0.3.1.exe SHA-256, then checks the exact preferred-VA callsites and
field references recovered from the outer host's profile-launch state machine.
It never executes the reference binary or starts the game.
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
PRODUCER_FUNCTION = (0x1401D3F1B, 0x1401DCAD1)

FIELDS = (
    {
        "name": "secureExportFingerprint",
        "fileOffset": 0x8341DB,
        "preferredVa": 0x140834DDB,
        "nameXrefVa": 0x1401D8D2C,
        "nameLength": 0x17,
        "valueSource": "profile launch context +0x60",
        "valueLoadVa": 0x1401D8D46,
        "valueAdjustVa": 0x1401D8D4D,
        "valueAdjust": 0x60,
    },
    {
        "name": "plainExportFingerprint",
        "fileOffset": 0x8341F2,
        "preferredVa": 0x140834DF2,
        "nameXrefVa": 0x1401D8DC0,
        "nameLength": 0x16,
        "valueSource": "profile launch context +0x78",
        "valueLoadVa": 0x1401D8DDA,
        "valueAdjustVa": 0x1401D8DE1,
        "valueAdjust": 0x78,
    },
    {
        "name": "descriptorJson",
        "fileOffset": 0x8342AF,
        "preferredVa": 0x140834EAF,
        "nameXrefVa": 0x1401DA895,
        "nameLength": 0x0E,
        "valueSource": "profile launch state +0x678",
        "valueLoadVa": 0x1401DA8AF,
        "valueDisplacement": 0x678,
    },
    {
        "name": "launchProof",
        "fileOffset": 0x8342D8,
        "preferredVa": 0x140834ED8,
        "nameXrefVa": 0x1401DA91D,
        "nameLength": 0x0B,
        "valueSource": "profile launch state +0x690",
        "valueLoadVa": 0x1401DA937,
        "valueDisplacement": 0x690,
    },
    {
        "name": "gameLaunchTicket",
        "fileOffset": 0x8342E3,
        "preferredVa": 0x140834EE3,
        "nameXrefVa": 0x1401DA9A5,
        "nameLength": 0x10,
        "valueSource": "profile launch state +0x4c0",
        "valueLoadVa": 0x1401DA9BF,
        "valueDisplacement": 0x4C0,
    },
)

ENVELOPE_SERIALIZATION = {
    "jsonObjectTagVa": 0x1401DAA4B,
    "jsonSerializerCallVa": 0x1401DAA5B,
    "jsonSerializerTargetVa": 0x1401D296D,
    "serializedStateCopyStartVa": 0x1401DACC4,
    "serializedStateCopyEndVa": 0x1401DACD3,
    "serializedStateOffset": 0x730,
    "handoffCloneSourceVa": 0x1401DB119,
    "handoffCloneCallVa": 0x1401DB12B,
    "handoffCloneTargetVa": 0x14002A2C0,
}


class InspectError(ValueError):
    pass


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _runtime_functions(pe: pefile.PE, image_base: int) -> list[tuple[int, int]]:
    pe.parse_data_directories(
        directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]]
    )
    rows: list[tuple[int, int]] = []
    for entry in getattr(pe, "DIRECTORY_ENTRY_EXCEPTION", []):
        begin = image_base + int(entry.struct.BeginAddress)
        end = image_base + int(entry.struct.EndAddress)
        if begin < end:
            rows.append((begin, end))
    return rows


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


def _instruction(instructions: dict[int, Any], va: int, mnemonic: str) -> Any:
    instruction = instructions.get(va)
    if instruction is None:
        raise InspectError(f"missing instruction at preferred VA 0x{va:X}")
    if instruction.mnemonic != mnemonic:
        raise InspectError(
            f"unexpected instruction at preferred VA 0x{va:X}: "
            f"{instruction.mnemonic} {instruction.op_str}; expected {mnemonic}"
        )
    return instruction


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


def _expect_text(data: bytes, offset: int, value: str) -> None:
    raw = value.encode("ascii")
    if data[offset : offset + len(raw)] != raw:
        raise InspectError(f"expected {value!r} at raw file offset 0x{offset:X}")


def _expect_displacement(instruction: Any, displacement: int) -> None:
    for operand in instruction.operands:
        if operand.type == X86_OP_MEM and operand.mem.disp == displacement:
            return
    raise InspectError(
        f"instruction 0x{instruction.address:X} does not reference displacement 0x{displacement:X}"
    )


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

    functions = _runtime_functions(pe, image_base)
    if PRODUCER_FUNCTION not in functions:
        raise InspectError(
            "profile-launch producer runtime-function range is absent: "
            f"0x{PRODUCER_FUNCTION[0]:X}-0x{PRODUCER_FUNCTION[1]:X}"
        )
    instructions = _instruction_map(pe, data, image_base, *PRODUCER_FUNCTION)

    field_rows: list[dict[str, Any]] = []
    for field in FIELDS:
        _expect_text(data, field["fileOffset"], field["name"])
        xref = _instruction(instructions, field["nameXrefVa"], "lea")
        if _rip_target(xref) != field["preferredVa"]:
            raise InspectError(
                f"{field['name']} xref 0x{field['nameXrefVa']:X} no longer targets "
                f"0x{field['preferredVa']:X}"
            )

        load = instructions.get(field["valueLoadVa"])
        if load is None:
            raise InspectError(f"missing {field['name']} value source at 0x{field['valueLoadVa']:X}")
        if "valueDisplacement" in field:
            _expect_displacement(load, field["valueDisplacement"])
        else:
            _expect_displacement(load, 0x150)
            adjust = _instruction(instructions, field["valueAdjustVa"], "add")
            if not adjust.operands or adjust.operands[-1].type != X86_OP_IMM:
                raise InspectError(f"missing immediate adjustment for {field['name']}")
            if int(adjust.operands[-1].imm) != field["valueAdjust"]:
                raise InspectError(f"unexpected value-context adjustment for {field['name']}")

        field_rows.append(
            {
                "name": field["name"],
                "rawFileOffset": f"0x{field['fileOffset']:X}",
                "preferredVa": f"0x{field['preferredVa']:X}",
                "nameXrefPreferredVa": f"0x{field['nameXrefVa']:X}",
                "serializedNameLength": field["nameLength"],
                "valueSource": field["valueSource"],
                "valueLoadPreferredVa": f"0x{field['valueLoadVa']:X}",
            }
        )

    tag = _instruction(instructions, ENVELOPE_SERIALIZATION["jsonObjectTagVa"], "mov")
    if not tag.operands or tag.operands[-1].type != X86_OP_IMM or int(tag.operands[-1].imm) != 5:
        raise InspectError("recovered JSON object-tag write changed")

    serializer = _instruction(instructions, ENVELOPE_SERIALIZATION["jsonSerializerCallVa"], "call")
    if _call_target(serializer) != ENVELOPE_SERIALIZATION["jsonSerializerTargetVa"]:
        raise InspectError("recovered LaunchEnvelope JSON serializer target changed")

    state_copy = _instruction(instructions, ENVELOPE_SERIALIZATION["serializedStateCopyStartVa"], "mov")
    state_copy_end = _instruction(instructions, ENVELOPE_SERIALIZATION["serializedStateCopyEndVa"], "movdqu")
    if state_copy is None or state_copy_end is None:
        raise InspectError("serialized LaunchEnvelope state-copy sequence changed")

    handoff_source = _instruction(instructions, ENVELOPE_SERIALIZATION["handoffCloneSourceVa"], "lea")
    _expect_displacement(handoff_source, ENVELOPE_SERIALIZATION["serializedStateOffset"])
    handoff_clone = _instruction(instructions, ENVELOPE_SERIALIZATION["handoffCloneCallVa"], "call")
    if _call_target(handoff_clone) != ENVELOPE_SERIALIZATION["handoffCloneTargetVa"]:
        raise InspectError("serialized LaunchEnvelope handoff clone target changed")

    return {
        "findingId": "LWB-R5-001",
        "date": "2026-09-08",
        "scope": "host-side LaunchEnvelope producer and JSON handoff",
        "evidenceStatus": "RECOVERED",
        "sourceIdentity": {
            "artifact": "../LW/lwbridge-0.3.1.exe",
            "sha256": digest,
            "preferredImageBase": f"0x{image_base:X}",
            "coordinateSystem": "preferred virtual addresses (VA) plus raw PE file offsets",
        },
        "producer": {
            "runtimeFunctionPreferredVa": (
                f"0x{PRODUCER_FUNCTION[0]:X}-0x{PRODUCER_FUNCTION[1]:X}"
            ),
            "fields": field_rows,
            "serialization": {
                "jsonObjectTagWritePreferredVa": f"0x{ENVELOPE_SERIALIZATION['jsonObjectTagVa']:X}",
                "jsonSerializerCallPreferredVa": f"0x{ENVELOPE_SERIALIZATION['jsonSerializerCallVa']:X}",
                "jsonSerializerTargetPreferredVa": f"0x{ENVELOPE_SERIALIZATION['jsonSerializerTargetVa']:X}",
                "serializedStateCopyPreferredVa": (
                    f"0x{ENVELOPE_SERIALIZATION['serializedStateCopyStartVa']:X}-"
                    f"0x{ENVELOPE_SERIALIZATION['serializedStateCopyEndVa']:X}"
                ),
                "serializedStateOffset": f"0x{ENVELOPE_SERIALIZATION['serializedStateOffset']:X}",
                "handoffCloneSourcePreferredVa": f"0x{ENVELOPE_SERIALIZATION['handoffCloneSourceVa']:X}",
                "handoffCloneCallPreferredVa": f"0x{ENVELOPE_SERIALIZATION['handoffCloneCallVa']:X}",
                "handoffCloneTargetPreferredVa": f"0x{ENVELOPE_SERIALIZATION['handoffCloneTargetVa']:X}",
            },
        },
        "result": [
            "The outer LWBridge host, not only the embedded profile launcher, constructs the LaunchEnvelope object.",
            "The object contains descriptorJson, launchProof, and gameLaunchTicket and is serialized to owned JSON text before the profile-launch task handoff.",
            "The launch descriptor receives secureExportFingerprint and plainExportFingerprint from two distinct host launch-context values (+0x60 and +0x78 respectively).",
        ],
        "reproduction": [
            "python tools\\inspect_lwbridge_launch_contract.py ..\\LW\\lwbridge-0.3.1.exe",
            "python tools\\inspect_lwbridge_launch_contract.py ..\\LW\\lwbridge-0.3.1.exe --json",
        ],
        "validationAndLimits": {
            "validation": "static-only against the immutable verified LWBridge reference",
            "liveProven": False,
            "unknownBlocked": [
                "exact launchProof generation and validation semantics",
                "exact gameLaunchTicket generation, representation, reuse and validation semantics",
                "exact child profile-launcher input channel/decoding contract",
                "exact xLua export-fingerprint algorithm and secure/plain classification predicate",
                "current-client live launch/bootstrap acceptance",
            ],
        },
        "implementationImpact": {
            "r5HostProducer": "recovered",
            "profileInstanceStart": "remains fail-closed with OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED",
            "nextTrace": "trace launchProof/gameLaunchTicket producers and shared xLua ABI fingerprint routine in the same verified outer-host state machine",
        },
    }


def _text_report(result: dict[str, Any]) -> str:
    lines = [
        f"finding={result['findingId']} status={result['evidenceStatus']}",
        f"lwbridge_sha256={result['sourceIdentity']['sha256']}",
        f"producer_function={result['producer']['runtimeFunctionPreferredVa']}",
        "fields:",
    ]
    for field in result["producer"]["fields"]:
        lines.append(
            f"  {field['name']}: name_xref={field['nameXrefPreferredVa']} "
            f"value_source={field['valueSource']}"
        )
    serialization = result["producer"]["serialization"]
    lines.extend(
        [
            f"json_serializer_call={serialization['jsonSerializerCallPreferredVa']} "
            f"target={serialization['jsonSerializerTargetPreferredVa']}",
            f"handoff_clone={serialization['handoffCloneCallPreferredVa']} "
            f"target={serialization['handoffCloneTargetPreferredVa']}",
            "limits:",
        ]
    )
    for item in result["validationAndLimits"]["unknownBlocked"]:
        lines.append(f"  UNKNOWN/BLOCKED: {item}")
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=Path)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        result = inspect(args.binary)
    except (InspectError, OSError, pefile.PEFormatError) as exc:
        parser.error(str(exc))
    if args.json:
        print(json.dumps(result, indent=2))
    else:
        print(_text_report(result))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
