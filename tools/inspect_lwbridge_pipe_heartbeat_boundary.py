#!/usr/bin/env python3
"""Recover the safe host/proxy heartbeat boundary from LWBridge 0.3.1."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86_const import X86_OP_MEM, X86_REG_RIP

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
PROXIES = {
    "secure": (0x987F74, 0x95800, "481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400"),
    "plain": (0xA1D774, 0x96000, "c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794"),
}


class InspectError(ValueError):
    pass


def require(ok: bool, message: str) -> None:
    if not ok:
        raise InspectError(message)


def extract_proxy(outer: bytes, outer_pe: pefile.PE, name: str) -> bytes:
    rva, size, expected = PROXIES[name]
    raw = int(outer_pe.get_offset_from_rva(rva))
    data = outer[raw:raw + size]
    digest = hashlib.sha256(data).hexdigest()
    require(digest == expected, f"{name} proxy SHA-256 mismatch: {digest}")
    return data
def inspect_proxy(name: str, data: bytes) -> dict[str, object]:
    pe = pefile.PE(data=data, fast_load=False)
    base = int(pe.OPTIONAL_HEADER.ImageBase)
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True

    def instruction(rva: int):
        off = int(pe.get_offset_from_rva(rva))
        return next(md.disasm(data[off:off + 16], base + rva))

    def rip_target(ins) -> int | None:
        for operand in ins.operands:
            if operand.type == X86_OP_MEM and operand.mem.base == X86_REG_RIP:
                return int(ins.address + ins.size + operand.mem.disp - base)
        return None

    def text_at(rva: int) -> str:
        off = int(pe.get_offset_from_rva(rva))
        out = bytearray()
        for value in data[off:off + 96]:
            if 0x20 <= value <= 0x7E:
                out.append(value)
            else:
                break
        return out.decode("ascii", "replace")

    def expect(rva: int, mnemonic: str, op_text: str | None = None):
        ins = instruction(rva)
        require(ins.mnemonic == mnemonic, f"{name}: 0x{rva:X} mnemonic {ins.mnemonic}")
        if op_text is not None:
            require(ins.op_str == op_text, f"{name}: 0x{rva:X} operand {ins.op_str}")
        return ins

    expect(0x1A526, "cmp", "rsi, 9")
    expect(0x1A52A, "jne", "0x18001a546")
    heartbeat_load = expect(0x1A52F, "lea")
    heartbeat_rva = rip_target(heartbeat_load)
    require(
        heartbeat_rva is not None and text_at(heartbeat_rva) == "heartbeat",
        f"{name}: heartbeat literal mismatch",
    )
    expect(0x1A536, "call", "0x18006b560")
    expect(0x1A53B, "test", "eax, eax")
    expect(0x1A53D, "jne", "0x18001a546")
    expect(0x1A53F, "mov", "esi, 2")

    expect(0x1A552, "cmp", "rsi, 6")
    result_load = expect(0x1A55B, "lea")
    result_rva = rip_target(result_load)
    require(
        result_rva is not None and text_at(result_rva) == "result",
        f"{name}: result literal mismatch",
    )
    expect(0x1A562, "call", "0x18006b560")
    expect(0x1A567, "test", "eax, eax")
    expect(0x1A569, "mov", "esi, r12d")
    expect(0x1A56C, "mov", "eax, 1")
    expect(0x1A571, "cmove", "esi, eax")

    expect(0x1A698, "mov", "edx, esi")
    expect(0x1A69A, "lea", "rcx, [rbp - 0x30]")
    expect(0x1A69E, "call", "0x180008c80")
    expect(0x1A6A3, "test", "al, al")

    return {
        "proxy": name,
        "sha256": hashlib.sha256(data).hexdigest(),
        "classifierRva": "0x1A3C0-0x1A74A",
        "heartbeatLiteralRva": f"0x{heartbeat_rva:X}",
        "heartbeatCategory": 2,
        "resultLiteralRva": f"0x{result_rva:X}",
        "resultCategory": 1,
        "ownership": (
            "native code classifies an already serialized message string by type; "
            "this function is not the heartbeat JSON serializer"
        ),
    }


def inspect(binary: Path) -> dict[str, object]:
    outer = binary.read_bytes()
    digest = hashlib.sha256(outer).hexdigest()
    require(digest == EXPECTED_SHA256, f"unsupported reference SHA-256: {digest}")
    outer_pe = pefile.PE(data=outer, fast_load=False)

    def expect_raw(raw: int, literal: bytes) -> None:
        actual = outer[raw:raw + len(literal)]
        require(actual == literal, f"host raw 0x{raw:X} metadata mismatch: {actual!r}")

    expect_raw(0xD091A4, b"PIPE_FRAME_INVALIDsrc\\services\\bridge_pipe.rs")
    expect_raw(0xD0923D, b"pipe_connected=")
    expect_raw(0xD0924E, b" pipe_generation=")
    expect_raw(0xD09261, b" activity_generation=")
    expect_raw(0xD09278, b" pipe_activity_at=")
    expect_raw(0xD0928C, b" pipe_activity_age_ms=")
    expect_raw(0xD092A4, b" pipe_heartbeat=")
    expect_raw(0xD092B6, b" inbound_frames=")
    expect_raw(0xD092C8, b" writer_ready=")
    expect_raw(0xD092F0, b"inbound frame is available")
    expect_raw(0xD09358, b"/payload/time")

    timeout_window = outer[0xD09398:0xD094B1]
    require(b"PIPE_IDLE_TIMEOUT" in timeout_window, "host idle-timeout metadata missing")
    require(
        b"named pipe received no frames before the activity timeout" in timeout_window,
        "host no-frame activity-timeout text missing",
    )

    generic_window = outer[0xD094E8:0xD09677]
    for literal in (b"version", b"requestId", b"timestamp", b"named pipe envelope is invalid"):
        require(literal in generic_window, f"host generic-envelope metadata missing {literal!r}")

    proxies = [
        inspect_proxy(name, extract_proxy(outer, outer_pe, name))
        for name in ("secure", "plain")
    ]
    require(
        proxies[0]["heartbeatCategory"] == proxies[1]["heartbeatCategory"] == 2,
        "secure/plain heartbeat classification differs",
    )
    require(
        proxies[0]["resultCategory"] == proxies[1]["resultCategory"] == 1,
        "secure/plain result classification differs",
    )

    return {
        "findingId": "LWB-R8-089",
        "date": "2026-09-27",
        "evidenceStatus": "RECOVERED CONTRACT",
        "sourceIdentity": {"path": str(binary), "sha256": digest},
        "recoveredResult": {
            "hostParserOwner": r"src\services\bridge_pipe.rs",
            "heartbeatPayloadJsonPointer": "/payload/time",
            "hostActivityMetadata": [
                "pipe_connected",
                "pipe_generation",
                "activity_generation",
                "pipe_activity_at",
                "pipe_activity_age_ms",
                "pipe_heartbeat",
                "inbound_frames",
                "writer_ready",
            ],
            "idleTimeoutSemantics": "PIPE_IDLE_TIMEOUT is a no-inbound-frame activity timeout",
            "idleTimeoutMs": 30000,
            "genericEnvelopeContext": [
                "version",
                "type",
                "profileId",
                "instanceId",
                "requestId",
                "timestamp",
                "payload",
            ],
            "proxyClassifier": (
                "safe native proxy code recognizes an already serialized type heartbeat "
                "as category 2 and result as category 1 before forwarding; heartbeat JSON "
                "serialization remains owned below this safe native classifier"
            ),
        },
        "proxies": proxies,
        "limits": [
            "metadata proves the host JSON pointer /payload/time but not its exact JSON number width/range or semantic clock source",
            "the exact heartbeat requestId value is not recovered",
            "whether top-level timestamp equals payload.time is not recovered",
            "the protected Lua/package heartbeat serializer remains unrecovered",
            "the exact heartbeat emission cadence remains unrecovered",
            "the exact assignment from payload.time or arrival time into public lastHeartbeatAt remains unrecovered",
            "the current five-second Lua heartbeat remains EQUIVALENT_REIMPLEMENTATION, not exact original parity",
            "no production behavior is changed by this checkpoint",
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
