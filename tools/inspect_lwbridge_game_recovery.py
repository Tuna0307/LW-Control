#!/usr/bin/env python3
"""Verify build-specific LWBridge startup/recovery contracts.

Static only: validates the immutable 0.3.1 reference, then checks exact
preferred-VA strings, xrefs, thresholds, and retry tables. It never executes
the reference binary or starts Last War.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path
from typing import Any

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
EXPECTED_IMAGE_BASE = 0x140000000
STARTUP_RECONCILE = (0x1402022FA, 0x140204AB5)
RECOVERY_CLASSIFIER = (0x14033883A, 0x140338CB0)
RECOVERY_GATE = (0x140339789, 0x140339839)
DESIRED_RUNNING = (0x140339DD6, 0x14033A045)
RECOVERY_EVENT = (0x14033A045, 0x14033A19B)
RECOVERY_WORKER = (0x1400E579C, 0x1400E6B0F)
PROCESS_WATCHER = (0x14033BF89, 0x14033C37A)
STRINGS = {
    "autoLaunchAll": (0x1408352E8, b"autoLaunchAll"),
    "auto_force_update_reload": (0x140C9A132, b"auto_force_update_reload"),
    "game_desired_running": (0x140C9A3A7, b"game_desired_running"),
    "processExit": (0x140C9A064, b"processExit"),
    "hang": (0x140C9A06F, b"hang"),
    "disconnect": (0x140C99D65, b"disconnect"),
    "forceUpdate": (0x140C99D6F, b"forceUpdate"),
    "gameRecoveryEvent": (0x140C9A189, b"bridge://game-recovery"),
}
XREFS = {
    "autoLaunchAll": 0x140202802,
    "auto_force_update_reload": 0x1403397EA,
    "game_desired_running": 0x140339ED4,
    "processExit": 0x140338AA1,
    "hang": 0x140338BAE,
    "disconnect": 0x140338BFF,
    "gameRecoveryEvent": 0x14033A171,
}
THRESHOLDS = {
    0x140338B92: 30_000,
    0x140338BA5: 29_999,
    0x140338BCE: 59_999,
    0x140338BED: 180_000,
    0x1400E5FAE: 899_999,
    0x1400E619D: 15_000,
    0x1400E6225: 179_999,
    0x1400E6467: 60_000,
}
RETRY_TABLES = {
    0x140C9A3C0: [120_000, 300_000, 600_000],
    0x140C99D10: [15_000, 30_000, 60_000, 120_000, 300_000],
}
TABLE_XREFS = {
    0x140C9A3C0: [0x1400E5E12, 0x1400E6275, 0x1400E6545, 0x1400E66B0, 0x1400E671D],
    0x140C99D10: [0x1400E5B42, 0x1400E5F41, 0x1400E5FEC, 0x1400E675E, 0x1400E6835, 0x1400E69F8],
}
PROCESS_NAMES = (b"LastWarLauncher.exe", b"LastWarUpdater.exe", b"LastWarSync.exe")


class InspectError(ValueError):
    pass


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def instruction_map(pe: pefile.PE, data: bytes, begin: int, end: int) -> dict[int, Any]:
    off = int(pe.get_offset_from_rva(begin - EXPECTED_IMAGE_BASE))
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    return {ins.address: ins for ins in md.disasm(data[off:off + end - begin], begin)}


def rip_target(ins: Any) -> int | None:
    for op in ins.operands:
        if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
            return ins.address + ins.size + op.mem.disp
    return None


def immediate(ins: Any) -> int | None:
    for op in reversed(ins.operands):
        if op.type == X86_OP_IMM:
            return int(op.imm)
    return None


def require_instruction(m: dict[int, Any], va: int, mnemonic: str | None = None) -> Any:
    ins = m.get(va)
    if ins is None:
        raise InspectError(f"missing instruction at preferred VA 0x{va:X}")
    if mnemonic and ins.mnemonic != mnemonic:
        raise InspectError(f"0x{va:X}: expected {mnemonic}, found {ins.mnemonic} {ins.op_str}")
    return ins
def read_at_va(pe: pefile.PE, data: bytes, va: int, size: int) -> bytes:
    off = int(pe.get_offset_from_rva(va - EXPECTED_IMAGE_BASE))
    return data[off:off + size]


def verify_string(pe: pefile.PE, data: bytes, va: int, raw: bytes) -> None:
    if read_at_va(pe, data, va, len(raw)) != raw:
        raise InspectError(f"expected {raw!r} at preferred VA 0x{va:X}")


def verify_xref(maps: list[dict[int, Any]], va: int, target: int) -> None:
    ins = next((m[va] for m in maps if va in m), None)
    if ins is None:
        raise InspectError(f"missing xref instruction 0x{va:X}")
    if rip_target(ins) != target:
        raise InspectError(
            f"xref 0x{va:X} targets {rip_target(ins)!r}; expected preferred VA 0x{target:X}"
        )


def runtime_functions(pe: pefile.PE) -> set[tuple[int, int]]:
    pe.parse_data_directories(
        directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]]
    )
    return {
        (EXPECTED_IMAGE_BASE + int(e.struct.BeginAddress), EXPECTED_IMAGE_BASE + int(e.struct.EndAddress))
        for e in getattr(pe, "DIRECTORY_ENTRY_EXCEPTION", [])
        if int(e.struct.BeginAddress) < int(e.struct.EndAddress)
    }


def locate_ascii_all(pe: pefile.PE, data: bytes, raw: bytes) -> list[int]:
    result: list[int] = []
    start = 0
    while True:
        off = data.find(raw, start)
        if off < 0:
            break
        result.append(EXPECTED_IMAGE_BASE + int(pe.get_rva_from_offset(off)))
        start = off + 1
    if not result:
        raise InspectError(f"missing ASCII string {raw!r}")
    return result


def inspect(path: Path) -> dict[str, Any]:
    data = path.read_bytes()
    digest = sha256(data)
    if digest != EXPECTED_SHA256:
        raise InspectError(f"unsupported lwbridge SHA-256 {digest}; expected {EXPECTED_SHA256}")
    pe = pefile.PE(data=data, fast_load=False)
    if int(pe.OPTIONAL_HEADER.ImageBase) != EXPECTED_IMAGE_BASE:
        raise InspectError("unexpected preferred image base")

    funcs = runtime_functions(pe)
    required_funcs = [STARTUP_RECONCILE, RECOVERY_CLASSIFIER, RECOVERY_GATE,
                      DESIRED_RUNNING, RECOVERY_EVENT, RECOVERY_WORKER, PROCESS_WATCHER]
    for fn in required_funcs:
        if fn not in funcs:
            raise InspectError(f"missing runtime function 0x{fn[0]:X}-0x{fn[1]:X}")

    maps = [instruction_map(pe, data, *fn) for fn in required_funcs]
    startup, classifier, gate, desired, event_emitter, worker, watcher = maps
    for key, (va, raw) in STRINGS.items():
        verify_string(pe, data, va, raw)
        if key in XREFS:
            verify_xref(maps, XREFS[key], va)

    # Startup parser: absent/non-bool autoLaunchAll falls through to mov al,1.
    default_true = require_instruction(startup, 0x140202823, "mov")
    if immediate(default_true) != 1:
        raise InspectError("autoLaunchAll fallback is no longer true")

    # Native reconnect getter defaults false when the config field is absent/invalid.
    fallback_false = require_instruction(gate, 0x14033982C, "xor")
    if "esi" not in fallback_false.op_str:
        raise InspectError("auto_force_update_reload fallback is no longer false")

    # Desired-running property is a separate persisted boolean gate.
    desired_read = require_instruction(desired, 0x140339EF0, "mov")
    if "byte ptr [rax + 1]" not in desired_read.op_str:
        raise InspectError("game_desired_running boolean read changed")
    # Process-exit recovery begins only after the absent counter exceeds one.
    process_counter = require_instruction(classifier, 0x140338A98, "cmp")
    if immediate(process_counter) != 1:
        raise InspectError("process-exit consecutive-observation threshold changed")

    threshold_rows = []
    for va, expected in THRESHOLDS.items():
        ins = next((m.get(va) for m in maps if va in m), None)
        if ins is None or immediate(ins) != expected:
            raise InspectError(f"threshold at 0x{va:X} changed from {expected} ms")
        threshold_rows.append({"preferredVa": f"0x{va:X}", "immediateMs": expected})

    retry_rows = []
    for table_va, expected in RETRY_TABLES.items():
        raw = read_at_va(pe, data, table_va, 8 * len(expected))
        actual = list(struct.unpack("<" + "Q" * len(expected), raw))
        if actual != expected:
            raise InspectError(f"retry table 0x{table_va:X} changed: {actual}")
        for xref in TABLE_XREFS[table_va]:
            verify_xref([worker], xref, table_va)
        retry_rows.append({
            "preferredVa": f"0x{table_va:X}",
            "milliseconds": expected,
            "xrefPreferredVas": [f"0x{x:X}" for x in TABLE_XREFS[table_va]],
        })

    process_rows = []
    watcher_targets = {rip_target(ins) for ins in watcher.values()}
    for raw in PROCESS_NAMES:
        candidates = locate_ascii_all(pe, data, raw)
        va = next((candidate for candidate in candidates if candidate in watcher_targets), None)
        if va is None:
            raise InspectError(f"process watcher no longer references {raw.decode()}")
        process_rows.append({"name": raw.decode(), "preferredVa": f"0x{va:X}"})


    return {
        "ok": True,
        "scope": "static original LWBridge startup/game-recovery contract; no process execution",
        "reference": {"path": str(path), "sha256": digest, "imageBase": f"0x{EXPECTED_IMAGE_BASE:X}"},
        "defaults": {"autoLaunchAll": True, "autoForceUpdateReload": False},
        "gates": {
            "autoReconnectSetting": "auto_force_update_reload",
            "desiredRunningSetting": "game_desired_running",
            "processExitConsecutiveMissingObservations": 2,
        },
        "thresholds": threshold_rows,
        "retryTables": retry_rows,
        "processWatcher": process_rows,
        "states": ["waiting", "updating", "repairing", "launching", "verifying", "maintenance"],
        "reasons": ["processExit", "hang", "disconnect", "forceUpdate", "crossDisconnect", "exitPrompt"],
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=Path, help="path to immutable lwbridge-0.3.1.exe")
    parser.add_argument("--pretty", action="store_true")
    args = parser.parse_args()
    try:
        result = inspect(args.binary)
    except (InspectError, OSError, pefile.PEFormatError) as exc:
        print(json.dumps({"ok": False, "error": str(exc)}, sort_keys=True))
        return 1
    print(json.dumps(result, indent=2 if args.pretty else None, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
