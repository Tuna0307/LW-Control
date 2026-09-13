"""Verify recovered map-scan event identity admission semantics."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from capstone import Cs, CS_ARCH_X86, CS_MODE_64
import pefile

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
IMAGE_BASE = 0x140000000
ADMISSION_START = 0x140344DCF
ADMISSION_END = 0x140344F1D
EVENT_START = 0x1403403B8
EVENT_END = 0x140340560


class InspectError(ValueError):
    pass


def raw_offset_for_va(pe: pefile.PE, va: int) -> int:
    return pe.get_offset_from_rva(va - pe.OPTIONAL_HEADER.ImageBase)


def disassemble(blob: bytes, pe: pefile.PE, start_va: int, end_va: int) -> list[dict[str, object]]:
    start = raw_offset_for_va(pe, start_va)
    cs = Cs(CS_ARCH_X86, CS_MODE_64)
    return [{"va": ins.address, "mnemonic": ins.mnemonic, "opStr": ins.op_str}
            for ins in cs.disasm(blob[start:start + end_va - start_va], start_va)]

def require(index: dict[int, dict[str, object]], va: int, mnemonic: str, operand: str) -> None:
    item = index.get(va)
    if item is None:
        raise InspectError(f"missing instruction at 0x{va:X}")
    if item["mnemonic"] != mnemonic or operand not in str(item["opStr"]):
        raise InspectError(f"unexpected instruction at 0x{va:X}: {item['mnemonic']} {item['opStr']}")


def inspect(binary: Path) -> dict[str, object]:
    blob = binary.read_bytes()
    digest = hashlib.sha256(blob).hexdigest()
    if digest != EXPECTED_SHA256:
        raise InspectError(f"unexpected LWBridge SHA-256: {digest}")
    pe = pefile.PE(data=blob, fast_load=False)
    if pe.OPTIONAL_HEADER.ImageBase != IMAGE_BASE:
        raise InspectError(f"unexpected image base: 0x{pe.OPTIONAL_HEADER.ImageBase:X}")

    helper = disassemble(blob, pe, ADMISSION_START, ADMISSION_END)
    h = {int(row["va"]): row for row in helper}
    event = disassemble(blob, pe, EVENT_START, EVENT_END)
    e = {int(row["va"]): row for row in event}

    require(e, 0x14034040C, "cmp", "rbx, 0x11")
    require(e, 0x140340420, "pcmpeqb", "xmmword ptr [rip + 0x4f3e68]")
    require(e, 0x140340451, "pcmpeqb", "xmmword ptr [rip + 0x956b27]")
    require(e, 0x140340475, "cmp", "rbx, 0xe")
    require(e, 0x14034047B, "movabs", "0x6e6163732e70616d")
    require(e, 0x140340488, "movabs", "0x726f7272652e6e61")
    require(e, 0x1403404DE, "call", "0x140344dcf")
    require(e, 0x140340520, "call", "0x140344dcf")
    require(h, 0x140344DF0, "lea", "[rip + 0x955d16]")
    require(h, 0x140344E19, "cmp", "byte ptr [rcx], 1")
    require(h, 0x140344E22, "cmp", "byte ptr [rcx + 1], 0")
    require(h, 0x140344E35, "lea", "[rip + 0x955afe]")
    require(h, 0x140344E61, "test", "rdi, rdi")
    require(h, 0x140344E80, "test", "r14, r14")
    require(h, 0x140344E68, "lea", "[rip + 0x954106]")
    require(h, 0x140344E78, "call", "0x140345030")
    require(h, 0x140344E8B, "test", "rbx, rbx")
    require(h, 0x140344EA5, "cmp", "rbx, rax")
    require(h, 0x140344EB7, "lea", "[rip + 0x955a7c]")
    require(h, 0x140344EE6, "cmp", "r14, rdx")
    require(h, 0x140344EFF, "call", "0x14079c7f0")
    require(h, 0x140344F04, "test", "eax, eax")
    require(h, 0x140344F0C, "mov", "al, 1")

    for marker in (b"map.scan.progres", b"map.scan.complete",
                   b"isReading", b"scanRunId", b"serverId"):
        if marker not in blob:
            raise InspectError(f"missing recovered marker: {marker!r}")

    return {
        "findingId": "LWB-R6-060",
        "date": "2026-09-14",
        "scope": "active map-scan event run/server identity admission",
        "evidenceStatus": "RECOVERED",
        "sourceIdentity": {"path": str(binary), "sha256": digest, "imageBase": hex(IMAGE_BASE)},
        "recoveredResult": {
            "activeGate": "events are admitted only while current scan state isReading=true",
            "scanRunId": "if event scanRunId is present and nonempty, it must byte-match the active scanRunId; missing/empty is tolerated",
            "serverId": "if event serverId is positive, it must equal the active scan-state serverId; zero/missing is tolerated",
            "eventPath": "map.scan.progress, map.scan.complete and map.scan.error pass this helper before scan-event processing",
        },
        "locators": {
            "admissionHelper": [hex(ADMISSION_START), hex(ADMISSION_END)],
            "eventAdmissionCalls": ["0x1403404DE", "0x140340520"],
        },
        "limits": [
            "this finding does not claim every event carries scanRunId or serverId",
            "missing identity fields are tolerated by the recovered helper rather than synthesized",
            "game-side event generation and delivery ordering remain unrecovered",
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
    rendered = json.dumps(result, indent=2)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered + "\n", encoding="utf-8")
    print(f"finding={result['findingId']} status={result['evidenceStatus']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
