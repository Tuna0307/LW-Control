#!/usr/bin/env python3
"""Verify original LWBridge Map diagnostic/error and failed-run persistence boundaries."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86_const import X86_OP_MEM, X86_REG_RIP
import pefile

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
IMAGE_BASE = 0x140000000
COMMON_EVENT = (0x14033FFA1, 0x140341913)
FAIL_HELPER = (0x140277E21, 0x1402785BA)
PRESERVE_FAIL_HELPER = (0x14026A12E, 0x14026AAC0)
CLEANUP_HELPER = 0x14033FBE8
ERROR_FALLBACK_HELPER = 0x1403810D5


class InspectError(ValueError):
    pass


def raw_offset(pe: pefile.PE, va: int) -> int:
    return pe.get_offset_from_rva(va - pe.OPTIONAL_HEADER.ImageBase)


def disassemble(blob: bytes, pe: pefile.PE, start: int, end: int):
    cs = Cs(CS_ARCH_X86, CS_MODE_64)
    cs.detail = True
    off = raw_offset(pe, start)
    return list(cs.disasm(blob[off:off + (end - start)], start))


def rip_ascii(blob: bytes, pe: pefile.PE, insn) -> str | None:
    for op in insn.operands:
        if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
            va = insn.address + insn.size + op.mem.disp
            try:
                off = raw_offset(pe, va)
            except Exception:
                continue
            end = off
            while end < min(len(blob), off + 420) and 0x20 <= blob[end] <= 0x7E:
                end += 1
            if end - off >= 3:
                return blob[off:end].decode("ascii", "replace")
    return None


def require(index, va: int, mnemonic: str, operand: str = ""):
    insn = index.get(va)
    if insn is None:
        raise InspectError(f"missing instruction at 0x{va:X}")
    if insn.mnemonic != mnemonic or operand not in insn.op_str:
        raise InspectError(f"unexpected instruction at 0x{va:X}: {insn.mnemonic} {insn.op_str}")
    return insn


def require_rip_prefix(blob: bytes, pe: pefile.PE, index, va: int, mnemonic: str, prefix: str):
    insn = require(index, va, mnemonic)
    text = rip_ascii(blob, pe, insn) or ""
    if not text.startswith(prefix):
        raise InspectError(f"RIP string mismatch at 0x{va:X}: {text!r}")
    return text


def inspect(binary: Path) -> dict[str, object]:
    blob = binary.read_bytes()
    digest = hashlib.sha256(blob).hexdigest()
    if digest != EXPECTED_SHA256:
        raise InspectError(f"unexpected LWBridge SHA-256: {digest}")
    pe = pefile.PE(data=blob, fast_load=False)
    if pe.OPTIONAL_HEADER.ImageBase != IMAGE_BASE:
        raise InspectError(f"unexpected image base: 0x{pe.OPTIONAL_HEADER.ImageBase:X}")

    event_rows = disassemble(blob, pe, *COMMON_EVENT)
    event = {i.address: i for i in event_rows}

    # map.scan.diagnostic: exact event branch -> Rust fmt helper -> bridge log helper -> return.
    require(event, 0x1403400D6, "pcmpeqb")
    require(event, 0x1403400DE, "pcmpeqb")
    require(event, 0x1403400EE, "cmp", "eax, 0xffff")
    require(event, 0x14034012E, "call", "0x1400276b0")
    require(event, 0x14034013E, "call", "0x1403ce0d2")
    require(event, 0x140340168, "jmp", "0x14034073f")
    if b"map scan diagnostic " not in blob:
        raise InspectError("diagnostic log marker missing")

    # map.scan.error packed compare.
    require(event, 0x140340C5B, "cmp", "rbx, 0xe")
    require(event, 0x140340C64, "movabs", "0x6e6163732e70616d")
    require(event, 0x140340C71, "movabs", "0x726f7272652e6e61")
    require(event, 0x140340C82, "je", "0x140341182")

    # Event error -> lastError -> literal fallback.
    require_rip_prefix(blob, pe, event, 0x140341191, "lea", "error")
    require(event, 0x14034119E, "call", "0x1405a2051")
    require(event, 0x140341749, "call", hex(ERROR_FALLBACK_HELPER))
    fallback = require_rip_prefix(blob, pe, event, 0x140341751, "lea", "map scan failed")
    if fallback != "map scan failed":
        raise InspectError(f"unexpected map.scan.error fallback: {fallback!r}")

    fallback_rows = disassemble(blob, pe, 0x1403810D5, 0x14038112B)
    fallback_index = {i.address: i for i in fallback_rows}
    require(fallback_index, 0x1403810DC, "test", "rcx, rcx")
    require(fallback_index, 0x1403810DF, "jne", "0x140381126")
    require_rip_prefix(blob, pe, fallback_index, 0x1403810EE, "lea", "lastError")
    require(fallback_index, 0x1403810FE, "call", "0x1405a2051")
    require(fallback_index, 0x140381111, "cmovne", "rcx, rax")
    require(fallback_index, 0x140381115, "cmp", "byte ptr [rcx], 3")

    # map.scan.error cleanup flags: stop native=true, preserve failed rows=false.
    require(event, 0x140341786, "mov", "byte ptr [rsp + 0x30], 0")
    require(event, 0x14034178B, "mov", "byte ptr [rsp + 0x28], 1")
    require(event, 0x1403417A6, "call", hex(CLEANUP_HELPER))

    # map.scan.complete terminal-failure flags: stop native=false, preserve failed rows=true.
    require(event, 0x140341264, "mov", "byte ptr [rsp + 0x30], 1")
    require(event, 0x140341269, "mov", "byte ptr [rsp + 0x28], 0")
    require(event, 0x14034127F, "call", hex(CLEANUP_HELPER))

    cleanup_rows = disassemble(blob, pe, 0x14033FBE8, 0x14033FEEA)
    cleanup = {i.address: i for i in cleanup_rows}
    require(cleanup, 0x14033FC5E, "cmp", "byte ptr [rsp + 0x130], 0")
    require(cleanup, 0x14033FC82, "call", "0x14026a12e")
    require(cleanup, 0x14033FCA3, "call", "0x140277e21")
    require(cleanup, 0x14033FD8E, "mov", "bpl, byte ptr [rsp + 0x128]")
    require(cleanup, 0x14033FE08, "test", "bpl, bpl")
    require_rip_prefix(blob, pe, cleanup, 0x14033FE40, "lea", "stopMapScan")

    # Ordinary failure transaction.
    fail_rows = disassemble(blob, pe, *FAIL_HELPER)
    fail = {i.address: i for i in fail_rows}
    require_rip_prefix(
        blob, pe, fail, 0x140278000, "lea",
        "UPDATE scan_runs SET status='failed',error=?1,updated_at=?2 WHERE id=?3 AND status='running'"
    )
    require_rip_prefix(blob, pe, fail, 0x1402782B0, "lea", "DELETE FROM scan_records WHERE run_id=?1")
    require_rip_prefix(blob, pe, fail, 0x14027857E, "lea", "commit fail map scan")
    fail_rip_strings = [rip_ascii(blob, pe, insn) or "" for insn in fail_rows]
    if any(text.startswith("INSERT OR REPLACE INTO map_records(") for text in fail_rip_strings):
        raise InspectError("ordinary fail helper unexpectedly publishes staged rows")

    # Preserve-failed transaction.
    preserve_rows = disassemble(blob, pe, *PRESERVE_FAIL_HELPER)
    preserve = {i.address: i for i in preserve_rows}
    require_rip_prefix(
        blob, pe, preserve, 0x14026A304, "lea",
        "UPDATE scan_runs SET status='failed',error=?1,updated_at=?2 WHERE id=?3 AND status='running'"
    )
    require_rip_prefix(blob, pe, preserve, 0x14026A5C5, "lea", "INSERT OR REPLACE INTO map_records(")
    for marker in [
        b"SELECT kind,server_id,record_key,point_index,uuid,name,alliance_name,",
        b"FROM scan_records WHERE run_id=?1",
    ]:
        if marker not in blob:
            raise InspectError(f"missing preserve-failed SQL marker: {marker!r}")
    require_rip_prefix(blob, pe, preserve, 0x14026A7C1, "lea", "DELETE FROM scan_records WHERE run_id=?1")
    require_rip_prefix(blob, pe, preserve, 0x14026AA84, "lea", "commit preserved map scan")

    return {
        "findingId": "LWB-R8-078",
        "date": "2026-09-27",
        "scope": "Map diagnostic/error event handling and failed-run persistence split",
        "evidenceStatus": "RECOVERED CONTRACT",
        "sourceIdentity": {"path": str(binary), "sha256": digest, "imageBase": hex(IMAGE_BASE)},
        "recoveredResult": {
            "diagnostic": {
                "event": "map.scan.diagnostic",
                "behavior": "formats the event payload with prefix 'map scan diagnostic ' and sends it to the bridge log helper; the bounded branch then returns without scan-state mutation",
                "logHelper": "0x1403CE0D2",
            },
            "scanErrorMessagePrecedence": [
                "event error when it is a string",
                "current scan-state lastError when it is a string",
                "literal 'map scan failed'",
            ],
            "scanErrorFailure": {
                "runTransition": "status='failed' with error and updated_at only while status='running'",
                "staging": "DELETE FROM scan_records WHERE run_id=?1",
                "publishesStaging": False,
                "requestsStopMapScan": True,
            },
            "completeTerminalFailure": {
                "runTransition": "same failed transition",
                "stagingPublication": "INSERT OR REPLACE INTO map_records(...) SELECT ... FROM scan_records WHERE run_id=?1",
                "stagingCleanup": "DELETE FROM scan_records WHERE run_id=?1",
                "publishesStaging": True,
                "requestsStopMapScan": False,
            },
            "sharedCleanup": "both paths reset visible scan state through 0x14033FBE8; the two caller flags independently select preserve-vs-ordinary failure and stopMapScan dispatch",
        },
        "locators": {
            "commonEventHandler": [hex(COMMON_EVENT[0]), hex(COMMON_EVENT[1])],
            "diagnosticBranch": "0x1403400CB-0x140340168",
            "scanErrorBranch": "0x140340C5B -> 0x140341182-0x1403417D6",
            "errorFallbackHelper": hex(ERROR_FALLBACK_HELPER),
            "sharedCleanupHelper": hex(CLEANUP_HELPER),
            "ordinaryFailHelper": [hex(FAIL_HELPER[0]), hex(FAIL_HELPER[1])],
            "preserveFailHelper": [hex(PRESERVE_FAIL_HELPER[0]), hex(PRESERVE_FAIL_HELPER[1])],
        },
        "limits": [
            "this finding does not recover producer-side block traversal, acknowledgement item schema, queue drain timing or retry scheduling",
            "diagnostic payload contents are logged but this finding does not assign producer semantics to arbitrary diagnostic text",
            "this finding describes host handling after events have already been emitted",
            "no production scanner behavior is changed",
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        result = inspect(args.binary)
    except (InspectError, OSError, pefile.PEFormatError, ValueError) as exc:
        parser.error(str(exc))
    rendered = json.dumps(result, indent=2, sort_keys=True)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered + "\n", encoding="utf-8")
    print(rendered)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
