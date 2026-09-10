#!/usr/bin/env python3
"""Verify the recovered LWBridge 0.3.1 control-pipe frame and proxy hello.

This inspector is read-only and hash-gated. It extracts the embedded secure
xLua proxy from the verified LWBridge host, verifies the exact hello JSON
builder fragments, the four-byte little-endian length framing, the recovered
frame-size bound, and host/proxy endpoint imports plus handshake/error markers.
It deliberately does not infer hello.ack serialization, heartbeat freshness,
or the host-to-proxy command/result grammar.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path
from typing import Any

import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86_const import X86_OP_MEM, X86_REG_RIP


EXPECTED_HOST_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
SECURE_PROXY_OFFSET = 0x987374
SECURE_PROXY_SIZE = 612_352
EXPECTED_PROXY_SHA256 = "481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400"
EXPECTED_IMAGE_BASE = 0x180000000

HELLO_FUNCTION = (0x97B0, 0xA55F)
FRAME_FUNCTION = (0xAD60, 0xB293)
HOST_HELLO_ACK_RAW = 0x824590
HOST_HELLO_ACK_DESCRIPTOR_RAW = 0x8245A0

HELLO_FRAGMENTS = {
    "buildId": (0x6F308, b',"buildId":'),
    "pid": (0x6F318, b',"pid":'),
    "payloadToken": (0x6F320, b',"payload":{"token":'),
    "requestTimestamp": (0x6F338, b',"requestId":"","timestamp":'),
    "instanceId": (0x6F358, b',"instanceId":'),
    "prefix": (0x6F368, b'{"version":1,"type":"hello","profileId":'),
}

HELLO_XREF_RVAS = {
    "prefix": 0x9A2E,
    "instanceId": 0x9B2D,
    "requestTimestamp": 0x9BD8,
    "payloadToken": 0x9C85,
    "pid": 0x9D2E,
    "buildId": 0x9DDB,
}

HOST_MARKERS = (
    b"src\\services\\bridge_pipe.rs",
    b"/payload/token",
    b"/payload/pid",
    b"/payload/buildId",
    b"PIPE_HANDSHAKE_REJECTED",
    b"hello.ack",
    b"PIPE_DISCONNECTED",
    b"PIPE_HANDSHAKE_TIMEOUT",
    b"PIPE_CONNECT_FAILED",
    b"PIPE_WRITE_FAILED",
    b"src\\services\\bridge_store.rs",
    b"BRIDGE_STOPPED",
    b"bridge result channel closed",
    b"LUA_CALL_TIMEOUT",
)


class InspectError(ValueError):
    pass


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _imports(pe: pefile.PE) -> set[str]:
    result: set[str] = set()
    for entry in getattr(pe, "DIRECTORY_ENTRY_IMPORT", []):
        for imp in entry.imports:
            if imp.name:
                result.add(imp.name.decode(errors="replace"))
    return result


def _instructions(pe: pefile.PE, data: bytes, bounds: tuple[int, int]):
    begin, end = bounds
    offset = pe.get_offset_from_rva(begin)
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    return list(
        md.disasm(
            data[offset : offset + (end - begin)],
            pe.OPTIONAL_HEADER.ImageBase + begin,
        )
    )


def _rip_target(ins) -> int | None:
    for op in ins.operands:
        if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
            return ins.address + ins.size + op.mem.disp
    return None


def inspect(path: Path) -> dict[str, Any]:
    host = path.read_bytes()
    host_sha = _sha256(host)
    if host_sha != EXPECTED_HOST_SHA256:
        raise InspectError(f"unsupported host SHA-256 {host_sha}")

    proxy = host[SECURE_PROXY_OFFSET : SECURE_PROXY_OFFSET + SECURE_PROXY_SIZE]
    proxy_sha = _sha256(proxy)
    if proxy_sha != EXPECTED_PROXY_SHA256:
        raise InspectError(f"unexpected secure proxy SHA-256 {proxy_sha}")

    proxy_pe = pefile.PE(data=proxy, fast_load=False)
    host_pe = pefile.PE(data=host, fast_load=False)
    if proxy_pe.OPTIONAL_HEADER.ImageBase != EXPECTED_IMAGE_BASE:
        raise InspectError("unexpected secure proxy image base")

    for name, (raw, expected) in HELLO_FRAGMENTS.items():
        actual = proxy[raw : raw + len(expected)]
        if actual != expected:
            raise InspectError(f"hello fragment {name} missing at raw 0x{raw:X}")

    hello_ins = _instructions(proxy_pe, proxy, HELLO_FUNCTION)
    hello_by_rva = {ins.address - EXPECTED_IMAGE_BASE: ins for ins in hello_ins}
    xrefs: dict[str, str] = {}
    for name, rva in HELLO_XREF_RVAS.items():
        ins = hello_by_rva.get(rva)
        if ins is None:
            raise InspectError(f"hello xref instruction missing at RVA 0x{rva:X}")
        raw, _ = HELLO_FRAGMENTS[name]
        expected_va = EXPECTED_IMAGE_BASE + proxy_pe.get_rva_from_offset(raw)
        if _rip_target(ins) != expected_va:
            raise InspectError(f"hello xref at RVA 0x{rva:X} does not target {name}")
        xrefs[name] = f"0x{rva:X}"

    frame_ins = _instructions(proxy_pe, proxy, FRAME_FUNCTION)
    frame_by_rva = {ins.address - EXPECTED_IMAGE_BASE: ins for ins in frame_ins}
    expected_frame = {
        0xB000: ("call", "qword ptr"),
        0xB00A: ("cmp", "dword ptr [rsp + 0x34], 4"),
        0xB011: ("movzx", "byte ptr [rsp + 0x33]"),
        0xB016: ("shl", "rax, 8"),
        0xB01A: ("movzx", "byte ptr [rsp + 0x32]"),
        0xB022: ("shl", "rax, 8"),
        0xB026: ("movzx", "byte ptr [rsp + 0x31]"),
        0xB02E: ("shl", "rax, 8"),
        0xB032: ("movzx", "byte ptr [rsp + 0x30]"),
        0xB03A: ("lea", "rcx, [rax - 1]"),
        0xB03E: ("cmp", "rcx, 0x7fffff"),
        0xB047: ("lea", "rcx, [rax + 4]"),
        0xB054: ("mov", "ecx, 5"),
    }
    frame_rows: dict[str, str] = {}
    for rva, (mnemonic, operand_fragment) in expected_frame.items():
        ins = frame_by_rva.get(rva)
        if ins is None or ins.mnemonic != mnemonic or operand_fragment not in ins.op_str:
            observed = "missing" if ins is None else f"{ins.mnemonic} {ins.op_str}"
            raise InspectError(
                f"frame instruction mismatch at RVA 0x{rva:X}: {observed}"
            )
        frame_rows[f"0x{rva:X}"] = f"{ins.mnemonic} {ins.op_str}"

    proxy_imports = _imports(proxy_pe)
    host_imports = _imports(host_pe)
    for required in ("CreateFileW", "ReadFile", "WriteFile", "PeekNamedPipe"):
        if required not in proxy_imports:
            raise InspectError(f"secure proxy missing import {required}")
    for required in ("CreateNamedPipeW", "ConnectNamedPipe", "ReadFile", "WriteFile"):
        if required not in host_imports:
            raise InspectError(f"host missing import {required}")

    marker_offsets: dict[str, str] = {}
    for marker in HOST_MARKERS:
        offset = host.find(marker)
        if offset < 0:
            raise InspectError(f"host marker missing: {marker!r}")
        marker_offsets[marker.decode(errors="replace")] = f"0x{offset:X}"

    hello_ack_rva = host_pe.get_rva_from_offset(HOST_HELLO_ACK_RAW)
    hello_ack_va = host_pe.OPTIONAL_HEADER.ImageBase + hello_ack_rva
    descriptor_pointer, descriptor_length = struct.unpack_from(
        "<QQ", host, HOST_HELLO_ACK_DESCRIPTOR_RAW
    )
    if descriptor_pointer != hello_ack_va or descriptor_length != len(b"hello.ack"):
        raise InspectError("host hello.ack string descriptor does not match the recovered pointer/length")

    return {
        "schema": 1,
        "findingId": "LWB-R5-007",
        "date": "2026-09-10",
        "scope": "minimum original control-pipe frame, secure-proxy hello schema, endpoint roles, and host handshake/error boundary",
        "evidenceLabels": {
            "frameAndProxyHello": "RECOVERED",
            "rebuildCodecAndParser": "IMPLEMENTED/OFFLINE-TESTED",
            "helloAckHeartbeatAndRequestResult": "UNKNOWN/BLOCKED",
        },
        "source": {
            "path": "../LW/lwbridge-0.3.1.exe",
            "hostSha256": host_sha,
            "secureProxyRawOffset": f"0x{SECURE_PROXY_OFFSET:X}",
            "secureProxySize": len(proxy),
            "secureProxySha256": proxy_sha,
            "secureProxyImageBase": f"0x{proxy_pe.OPTIONAL_HEADER.ImageBase:X}",
        },
        "hello": {
            "functionRva": [f"0x{HELLO_FUNCTION[0]:X}", f"0x{HELLO_FUNCTION[1]:X}"],
            "fragments": {
                name: {"raw": f"0x{raw:X}", "text": text.decode()}
                for name, (raw, text) in HELLO_FRAGMENTS.items()
            },
            "xrefs": xrefs,
            "shape": {
                "version": 1,
                "type": "hello",
                "requestId": "",
                "topLevelFields": ["version", "type", "profileId", "instanceId", "requestId", "timestamp", "payload"],
                "payloadFields": ["token", "pid", "buildId"],
            },
        },
        "frame": {
            "functionRva": [f"0x{FRAME_FUNCTION[0]:X}", f"0x{FRAME_FUNCTION[1]:X}"],
            "lengthPrefixBytes": 4,
            "byteOrder": "little-endian",
            "payloadLengthMin": 1,
            "payloadLengthMax": 0x800000,
            "completeFrameBytes": "payloadLength + 4",
            "incompletePollMs": 5,
            "instructions": frame_rows,
        },
        "endpointRoles": {
            "proxyImports": [name for name in ("CreateFileW", "ReadFile", "WriteFile", "PeekNamedPipe") if name in proxy_imports],
            "hostImports": [name for name in ("CreateNamedPipeW", "ConnectNamedPipe", "ReadFile", "WriteFile", "GetNamedPipeClientProcessId") if name in host_imports],
            "interpretation": "original host is the named-pipe server; xLua proxy opens the pipe as the client",
        },
        "hostMarkers": marker_offsets,
        "hostMetadata": {
            "helloAckStringDescriptor": {
                "raw": f"0x{HOST_HELLO_ACK_DESCRIPTOR_RAW:X}",
                "pointer": f"0x{descriptor_pointer:X}",
                "length": descriptor_length,
                "stringRaw": f"0x{HOST_HELLO_ACK_RAW:X}",
            },
            "limit": "the string descriptor proves the exact type literal but not the surrounding hello.ack JSON/object serialization",
        },
        "reproduction": {
            "tool": "Python 3.12.10 + pefile 2024.8.26 + capstone 5.0.6",
            "command": "python tools\\inspect_lwbridge_control_pipe_protocol.py ..\\LW\\lwbridge-0.3.1.exe --output evidence\\lwbridge-implementation\\2026-09-10-r5-session-frame-hello-contract.json",
            "method": "hash-gate host, extract embedded secure proxy at raw 0x987374/612352 bytes, hash-gate proxy, verify exact hello literals/xrefs and fixed frame instructions, then verify host/proxy import roles and bridge-pipe marker cluster",
        },
        "validationAndLimits": {
            "staticOnly": True,
            "liveProven": False,
            "implemented": [
                "src/LWBridge.Desktop/LWBridgeControlPipeProtocol.cs",
                "tests/LWBridge.Desktop.Checks/Program.cs",
            ],
            "implementationPolicy": "MatchesExpectedIdentity compares the recovered profileId/instanceId/token/buildId identity tuple only; it does not claim the original hello.ack or readiness algorithm.",
        },
        "implementationImpact": {
            "enabled": "rebuild can encode/decode the recovered frame and parse/identity-check a proxy hello without claiming readiness",
            "notEnabled": "no production INativeAsyncCommandService, hello.ack writer, heartbeat-ready transition, command request/result correlation, or map_scan_start",
            "nextExactGap": "recover the host-to-proxy post-hello acknowledgement/readiness contract and command/result envelope sufficient for one correlated current-client request",
        },
        "limits": [
            "hello.ack output field/value serialization is not recovered",
            "heartbeat freshness/readiness timing is not recovered",
            "host-to-proxy command envelope and result correlation grammar are not recovered",
            "this is static recovery, not a live connected-session result",
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("path", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = inspect(args.path)
    text = json.dumps(result, indent=2) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(text, encoding="utf-8")
    print(text, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
