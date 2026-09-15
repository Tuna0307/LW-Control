#!/usr/bin/env python3
"""Verify recovered world-map readiness and live-server Start gates."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from capstone import Cs, CS_ARCH_X86, CS_MODE_64
import pefile

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
IMAGE_BASE = 0x140000000
START = 0x1400F95AF
END = 0x1400F9D66


class InspectError(ValueError):
    pass


def raw_offset_for_va(pe: pefile.PE, va: int) -> int:
    return pe.get_offset_from_rva(va - pe.OPTIONAL_HEADER.ImageBase)


def disassemble(blob: bytes, pe: pefile.PE) -> dict[int, tuple[str, str]]:
    start = raw_offset_for_va(pe, START)
    cs = Cs(CS_ARCH_X86, CS_MODE_64)
    return {insn.address: (insn.mnemonic, insn.op_str)
            for insn in cs.disasm(blob[start:start + (END - START)], START)}


def require(index: dict[int, tuple[str, str]], va: int, mnemonic: str, operand: str) -> None:
    item = index.get(va)
    if item is None:
        raise InspectError(f"missing instruction at VA 0x{va:X}")
    if item[0] != mnemonic or operand not in item[1]:
        raise InspectError(f"unexpected instruction at VA 0x{va:X}: {item[0]} {item[1]}")


def require_bytes(blob: bytes, marker: bytes, label: str) -> None:
    if marker not in blob:
        raise InspectError(f"missing recovered marker: {label}")


def inspect(binary: Path) -> dict[str, object]:
    blob = binary.read_bytes()
    digest = hashlib.sha256(blob).hexdigest()
    if digest != EXPECTED_SHA256:
        raise InspectError(f"unexpected LWBridge SHA-256: {digest}")
    pe = pefile.PE(data=blob, fast_load=False)
    if pe.OPTIONAL_HEADER.ImageBase != IMAGE_BASE:
        raise InspectError(f"unexpected image base: 0x{pe.OPTIONAL_HEADER.ImageBase:X}")
    index = disassemble(blob, pe)

    require(index, 0x1400F95D9, "lea", "[rip + 0x72d9e8]")
    require(index, 0x1400F95F2, "mov", "0x1388")
    require(index, 0x1400F961D, "call", "0x1400e4fad")
    require(index, 0x1400F96D0, "call", "0x14023f1c0")
    require(index, 0x1400F96D5, "add", "0x2710")
    require(index, 0x1400F96DB, "mov", "[r14 + 0x70]")
    require(index, 0x1400F9730, "lea", "[rip + 0x72d8a1]")
    require(index, 0x1400F9744, "mov", "r8d, 0x1dcd6500")
    require(index, 0x1400F974A, "call", "0x1406399cd")
    require(index, 0x1400F99FA, "call", "0x14023f1c0")
    require(index, 0x1400F99FF, "cmp", "[r14 + 0x70]")
    require(index, 0x1400F9A03, "jge", "0x1400f9a13")
    require(index, 0x1400F9A1C, "lea", "[rip + 0x72d5e5]")
    require(index, 0x1400F9A23, "lea", "[rip + 0x72d5ee]")

    require(index, 0x1400F9B53, "mov", "[r14 + 0xa0]")
    require(index, 0x1400F9B5A, "test", "rax, rax")
    require(index, 0x1400F9B5D, "jle", "0x1400f9d39")
    require(index, 0x1400F9B68, "lea", "[r14 + 0x58]")
    require(index, 0x1400F9B6C, "lea", "[rip + 0x72d335]")
    require(index, 0x1400F9B97, "call", "0x1405a1bb4")
    require(index, 0x1400F9B9C, "test", "al, al")
    require(index, 0x1400F9B9E, "je", "0x1400f9d39")
    require(index, 0x1400F9D42, "lea", "[rip + 0x72902f]")
    require(index, 0x1400F9D49, "lea", "[rip + 0x72903a]")

    require_bytes(blob, b"enterWorldMap", "enterWorldMap command")
    require_bytes(blob, b"WORLD_MAP_FAILED", "world-map failure code")
    require_bytes(blob, b"failed to enter world map", "world-map failure message")
    require_bytes(blob, b"SERVER_UNAVAILABLE", "server-unavailable code")
    require_bytes(blob, b"current server id unavailable", "server-unavailable message")
