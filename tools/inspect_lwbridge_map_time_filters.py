#!/usr/bin/env python3
"""Recover and verify LWBridge 0.3.1 Map Data wall-clock predicates."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import capstone
import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP


EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
MAP_SEARCH_RANGE = (0x140271864, 0x140276D7B)
CLOCK_HELPER_RANGE = (0x14023F1C0, 0x14023F244)
CLOCK_HELPER = CLOCK_HELPER_RANGE[0]
CLOCK_IAT = 0x1407B94B0
FILETIME_UNIX_EPOCH = 0x019DB1DED53E8000

PREDICATES = {
    "activeArrival": (0x00C894A6, "(json_extract(data_json,'$.arriveTs') IS NULL OR CAST(json_extract(data_json,'$.arriveTs') AS INTEGER) > ?)"),
    "arrivalPresent": (0x00C8971A, "json_extract(data_json,'$.arriveTs') IS NOT NULL"),
    "remainingLoot": (0x00C8974A, "COALESCE(CAST(json_extract(data_json,'$.remainingLootCount') AS INTEGER),MAX(COALESCE(CAST(json_extract(data_json,'$.maxLootCount') AS INTEGER),0)-COALESCE(CAST(json_extract(data_json,'$.robTimes') AS INTEGER),0),0)) > 0"),
    "completionPositive": (0x00C89826, "COALESCE(CAST(json_extract(data_json,'$.completionTime') AS INTEGER),0) > 0"),
    "plunderAtPositive": (0x00C89871, "COALESCE(CAST(json_extract(data_json,'$.plunderAt') AS INTEGER),CAST(json_extract(data_json,'$.completionTime') AS INTEGER),0) > 0"),
    "taskNotExpired": (0x00C898F3, "(COALESCE(CAST(json_extract(data_json,'$.taskExpireTime') AS INTEGER),0) <= 0 OR CAST(json_extract(data_json,'$.taskExpireTime') AS INTEGER) > ?)"),
    "stealsRemaining": (0x00C89984, "(COALESCE(CAST(json_extract(data_json,'$.maxStealCount') AS INTEGER),0) <= 0 OR COALESCE(CAST(json_extract(data_json,'$.stolenCount') AS INTEGER),0) < CAST(json_extract(data_json,'$.maxStealCount') AS INTEGER))"),
    "completionPending": (0x00C89F8D, "(CAST(json_extract(data_json,'$.completionTime') AS INTEGER) IS NULL OR CAST(json_extract(data_json,'$.completionTime') AS INTEGER) <= 0 OR CAST(json_extract(data_json,'$.completionTime') AS INTEGER) > ?)"),
    "completionCompleted": (0x00C8A059, "CAST(json_extract(data_json,'$.completionTime') AS INTEGER) > 0 AND CAST(json_extract(data_json,'$.completionTime') AS INTEGER) <= ?"),
}


class InspectError(ValueError):
    pass


def require(condition: bool, message: str) -> None:
    if not condition:
        raise InspectError(message)


def instruction_at(pe: pefile.PE, blob: bytes, decoder: Cs, va: int):
    offset = pe.get_offset_from_rva(va - pe.OPTIONAL_HEADER.ImageBase)
    return next(decoder.disasm(blob[offset:offset + 15], va))


def immediate(instruction) -> int:
    return next(op.imm for op in instruction.operands if op.type == X86_OP_IMM)


def rip_target(instruction) -> int:
    op = next(
        op for op in instruction.operands
        if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP
    )
    return instruction.address + instruction.size + op.mem.disp


def va_for_offset(pe: pefile.PE, offset: int) -> int:
    return pe.OPTIONAL_HEADER.ImageBase + pe.get_rva_from_offset(offset)


def require_instruction(pe: pefile.PE, blob: bytes, decoder: Cs, va: int, mnemonic: str, op_str: str) -> None:
    ins = instruction_at(pe, blob, decoder, va)
    require(ins.mnemonic == mnemonic and ins.op_str == op_str,
            f"instruction mismatch at 0x{va:016X}: {ins.mnemonic} {ins.op_str}")


def inspect(binary: Path) -> dict[str, object]:
    blob = binary.read_bytes()
    digest = hashlib.sha256(blob).hexdigest()
    require(digest == EXPECTED_SHA256, f"unexpected LWBridge SHA-256: {digest}")

    pe = pefile.PE(data=blob, fast_load=False)
    pe.parse_data_directories()
    decoder = Cs(CS_ARCH_X86, CS_MODE_64)
    decoder.detail = True

    for name, (offset, text) in PREDICATES.items():
        actual = blob[offset:offset + len(text)].decode("ascii")
        require(actual == text, f"{name} predicate mismatch")

    map_rva = 0x140272353 - pe.OPTIONAL_HEADER.ImageBase
    map_function = next(
        entry for entry in pe.DIRECTORY_ENTRY_EXCEPTION
        if entry.struct.BeginAddress <= map_rva < entry.struct.EndAddress
    )
    require(
        (pe.OPTIONAL_HEADER.ImageBase + map_function.struct.BeginAddress,
         pe.OPTIONAL_HEADER.ImageBase + map_function.struct.EndAddress) == MAP_SEARCH_RANGE,
        "map-search runtime-function bounds mismatch",
    )

    helper_rva = CLOCK_HELPER - pe.OPTIONAL_HEADER.ImageBase
    helper_function = next(
        entry for entry in pe.DIRECTORY_ENTRY_EXCEPTION
        if entry.struct.BeginAddress <= helper_rva < entry.struct.EndAddress
    )
    require(
        (pe.OPTIONAL_HEADER.ImageBase + helper_function.struct.BeginAddress,
         pe.OPTIONAL_HEADER.ImageBase + helper_function.struct.EndAddress) == CLOCK_HELPER_RANGE,
        "clock helper bounds mismatch",
    )

    imported = None
    for descriptor in pe.DIRECTORY_ENTRY_IMPORT:
        for item in descriptor.imports:
            if item.address == CLOCK_IAT:
                imported = (descriptor.dll.decode("ascii"), item.name.decode("ascii") if item.name else str(item.ordinal))
                break
    require(imported == ("kernel32.dll", "GetSystemTimePreciseAsFileTime"),
            f"clock import mismatch: {imported!r}")

    helper_call = instruction_at(pe, blob, decoder, 0x14023F1D3)
    require(helper_call.mnemonic == "call" and rip_target(helper_call) == CLOCK_IAT,
            "clock helper IAT call mismatch")
    require_instruction(pe, blob, decoder, 0x14023F1DC, "movabs", f"r9, {FILETIME_UNIX_EPOCH:#x}")
    require_instruction(pe, blob, decoder, 0x14023F217, "imul", "rax, rdx, 0x989680")
    require_instruction(pe, blob, decoder, 0x14023F221, "imul", "rdx, rdx, 0x3e8")

    # Reward-option aggregation clones scope parameters, samples the clock once, then
    # appends the returned integer as the next SQLite parameter.
    options_clock = instruction_at(pe, blob, decoder, 0x14025A1B5)
    require(options_clock.mnemonic == "call" and immediate(options_clock) == CLOCK_HELPER,
            "map_data_options clock helper call mismatch")
    require_instruction(pe, blob, decoder, 0x14025A1BA, "mov", "rsi, rax")
    require_instruction(pe, blob, decoder, 0x14025A1E2, "mov", "qword ptr [rax + rcx], 1")
    require_instruction(pe, blob, decoder, 0x14025A1EA, "mov", "qword ptr [rax + rcx + 8], rsi")

    # map_search samples the same clock once and stores it for all time predicates.
    search_clock = instruction_at(pe, blob, decoder, 0x140272353)
    require(search_clock.mnemonic == "call" and immediate(search_clock) == CLOCK_HELPER,
            "map_search clock helper call mismatch")
    require_instruction(pe, blob, decoder, 0x140272358, "mov", "qword ptr [rsp + 0x248], rax")
    require_instruction(pe, blob, decoder, 0x140272360, "mov", "qword ptr [rsp + 0x410], rax")

    xrefs = {
        "activeArrival": 0x1402725CD,
        "arrivalPresent": 0x140272733,
        "remainingLoot": 0x140272792,
        "completionPositive": 0x14027284D,
        "plunderAtPositive": 0x1402728B6,
        "taskNotExpired": 0x14027291F,
        "stealsRemaining": 0x1402729C7,
        "completionPending": 0x1402743EC,
        "completionCompleted": 0x1402743A8,
    }
    for name, va in xrefs.items():
        ins = instruction_at(pe, blob, decoder, va)
        require(ins.mnemonic in {"lea", "movupd", "movups"}, f"unexpected {name} xref instruction")
        require(rip_target(ins) == va_for_offset(pe, PREDICATES[name][0]), f"{name} xref mismatch")

    require_instruction(pe, blob, decoder, 0x140272647, "mov", "rdx, qword ptr [rsp + 0x248]")
    require_instruction(pe, blob, decoder, 0x14027264F, "mov", "qword ptr [rax + rcx + 8], rdx")
    require_instruction(pe, blob, decoder, 0x140272999, "mov", "rdx, qword ptr [rsp + 0x248]")
    require_instruction(pe, blob, decoder, 0x1402729A1, "mov", "qword ptr [rax + rcx + 8], rdx")
    require_instruction(pe, blob, decoder, 0x140274465, "mov", "rdx, qword ptr [rsp + 0x248]")
    require_instruction(pe, blob, decoder, 0x14027446D, "mov", "qword ptr [rax + rcx + 8], rdx")

    return {
        "schemaVersion": 1,
        "date": "2026-09-09",
        "findingId": "LWB-R6-014",
        "scope": "Map Data current-time source, completion status and plunderability predicates",
        "evidenceStatus": "RECOVERED static",
        "reference": {
            "path": str(binary),
            "sha256": digest,
            "coordinateSystem": "raw PE file offsets and preferred-image virtual addresses",
        },
        "clock": {
            "helperVaRange": [f"0x{CLOCK_HELPER_RANGE[0]:016X}", f"0x{CLOCK_HELPER_RANGE[1]:016X}"],
            "importIatVa": f"0x{CLOCK_IAT:016X}",
            "source": "kernel32.dll!GetSystemTimePreciseAsFileTime",
            "filetimeUnixEpoch100ns": FILETIME_UNIX_EPOCH,
            "resultUnit": "Unix milliseconds",
            "derivedFormula": "max(FILETIME_100ns - 116444736000000000, 0) // 10000",
            "mapSearchCallVa": "0x0000000140272353",
            "mapDataOptionsRewardCallVa": "0x000000014025A1B5",
        },
        "predicates": {
            name: {"fileOffset": f"0x{offset:08X}", "sql": text}
            for name, (offset, text) in PREDICATES.items()
        },
        "result": {
            "truckRailwayDefault": "truck and railway append activeArrival with the sampled Unix-ms clock even when plunderableOnly is absent",
            "truckRailwayPlunderableOnly": ["arrivalPresent", "remainingLoot"],
            "dispatchGhostTreasureNativePlunderableBranch": [
                "completionPositive", "plunderAtPositive", "taskNotExpired(now)", "stealsRemaining"
            ],
            "frontendPublicPlunderableKinds": ["truck", "railway", "dispatch"],
            "completionStatus": {
                "pending": "completionTime IS NULL OR completionTime <= 0 OR completionTime > now",
                "completed": "completionTime > 0 AND completionTime <= now",
                "frontendKinds": ["dispatch", "ghost"],
            },
            "rewardOptionCutoff": "reward-item arriveTs filtering binds the same sampled Unix-ms wall clock",
        },
        "analysisTools": {
            "capstone": {"version": capstone.__version__, "package": f"PyPI capstone=={capstone.__version__}"},
            "pefile": {"version": pefile.__version__, "package": f"PyPI pefile=={pefile.__version__}"},
        },
        "reproduction": [
            "python tools\\inspect_lwbridge_map_time_filters.py ..\\LW\\lwbridge-0.3.1.exe --json",
            "python tools\\inspect_lwbridge_map_frontend_consumers.py evidence\\lwbridge-0.3.1\\frontend --json",
        ],
        "validationAndLimits": {
            "validation": "verified reference hash, import identity, clock helper constants/calls, exact SQL bytes/xrefs and clock-parameter append sites",
            "liveProven": False,
            "limits": "static original evidence plus offline rebuild tests only; native ingestion, treasure visibility/lucky semantics and alternate sorts remain unproven",
        },
        "implementationImpact": {
            "mapSearch": "unblocks frontend-emitted completionStatus and plunderableOnly forms plus the original truck/railway active-arrival default",
            "mapDataOptions": "closes the reward arriveTs cutoff source/unit gap, but public options still require complete source/run-context response semantics before enabling",
            "escalation": "ESC-001 clock/source/unit and completion-status question is answered by regular-AI static analysis",
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=Path)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        result = inspect(args.binary)
    except (InspectError, OSError, pefile.PEFormatError, StopIteration) as exc:
        parser.error(str(exc))
    print(json.dumps(result, indent=2, ensure_ascii=False) if args.json else f"finding={result['findingId']} status={result['evidenceStatus']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
