#!/usr/bin/env python3
"""Recover the permitted metadata boundary for original pending-call teardown."""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path

import pefile

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
BRIDGE_STORE_PATH = rb"src\services\bridge_store.rs"


class InspectError(ValueError):
    pass


def require(ok: bool, message: str) -> None:
    if not ok:
        raise InspectError(message)


def source_location(data: bytes, raw: int, expected_file_va: int, expected_line: int, expected_col: int) -> dict[str, int]:
    file_va, file_len, line, col = struct.unpack_from("<QQII", data, raw)
    require(file_va == expected_file_va, f"0x{raw:X}: source file pointer mismatch")
    require(file_len == len(BRIDGE_STORE_PATH), f"0x{raw:X}: source file length mismatch")
    require(line == expected_line and col == expected_col, f"0x{raw:X}: source location {line}:{col}")
    return {"raw": raw, "line": line, "column": col}


def string_descriptor(data: bytes, raw: int, expected_va: int, expected: bytes) -> dict[str, object]:
    ptr, length = struct.unpack_from("<QQ", data, raw)
    require(ptr == expected_va, f"0x{raw:X}: string pointer mismatch")
    require(length == len(expected), f"0x{raw:X}: string length mismatch")
    return {"raw": raw, "value": expected.decode("ascii")}


def inspect(binary: Path) -> dict[str, object]:
    data = binary.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    require(digest == EXPECTED_SHA256, f"unsupported reference SHA-256: {digest}")
    pe = pefile.PE(data=data, fast_load=False)
    base = int(pe.OPTIONAL_HEADER.ImageBase)

    def va_for_raw(raw: int) -> int:
        return base + int(pe.get_rva_from_offset(raw))

    require(data[0x824748:0x824748 + len(BRIDGE_STORE_PATH)] == BRIDGE_STORE_PATH, "primary bridge_store path mismatch")
    primary_file_va = va_for_raw(0x824748)

    primary_locations = [
        source_location(data, 0x824798, primary_file_va, 498, 34),
        source_location(data, 0x824768, primary_file_va, 499, 34),
        source_location(data, 0x824780, primary_file_va, 501, 15),
        source_location(data, 0x824830, primary_file_va, 979, 33),
        source_location(data, 0x824818, primary_file_va, 980, 28),
        source_location(data, 0x824860, primary_file_va, 983, 13),
        source_location(data, 0x824848, primary_file_va, 991, 16),
    ]
    require(data[0x8247B0:0x8247B0 + 14] == b"BRIDGE_STOPPED", "BRIDGE_STOPPED literal mismatch")
    require(data[0x8247BE:0x8247BE + 28] == b"bridge result channel closed", "closed-channel literal mismatch")
    require(data[0x824804:0x824804 + 16] == b"LUA_CALL_TIMEOUT", "LUA_CALL_TIMEOUT literal mismatch")

    require(data[0xD09819:0xD09819 + len(BRIDGE_STORE_PATH)] == BRIDGE_STORE_PATH, "secondary bridge_store path mismatch")
    secondary_file_va = va_for_raw(0xD09819)
    secondary_locations = [
        source_location(data, 0xD09910, secondary_file_va, 168, 13),
        source_location(data, 0xD09838, secondary_file_va, 554, 13),
        source_location(data, 0xD099B0, secondary_file_va, 605, 24),
        source_location(data, 0xD09A58, secondary_file_va, 740, 25),
        source_location(data, 0xD09D50, secondary_file_va, 979, 5),
    ]
    call = string_descriptor(data, 0xD09858, va_for_raw(0xD09850), b"call")
    command = string_descriptor(data, 0xD099D0, va_for_raw(0xD099C8), b"command")
    timeout = string_descriptor(data, 0xD09A78, va_for_raw(0xD09A70), b"timeout")

    require(data[0x8245B9:0x8245B9 + 17] == b"PIPE_DISCONNECTED", "PIPE_DISCONNECTED literal mismatch")
    require(data[0x824718:0x824718 + 17] == b"PIPE_WRITE_FAILED", "PIPE_WRITE_FAILED literal mismatch")

    return {
        "findingId": "LWB-R8-090",
        "date": "2026-09-27",
        "status": "RECOVERED BOUNDARY / BOUNDED NEGATIVE",
        "sourceIdentity": {"path": str(binary), "sha256": digest},
        "bridgeStoreMetadata": {
            "primarySourceLocations": primary_locations,
            "closedResultChannel": {
                "code": "BRIDGE_STOPPED",
                "message": "bridge result channel closed",
                "nearSourceLines": [498, 499, 501],
            },
            "timeout": {
                "code": "LUA_CALL_TIMEOUT",
                "nearSourceLines": [979, 980, 983, 991],
            },
            "secondarySourceLocations": secondary_locations,
            "pairedDescriptors": {
                "line554": call,
                "line605": command,
                "line740": timeout,
            },
        },
        "connectionLayerVocabulary": {
            "disconnect": "PIPE_DISCONNECTED",
            "writeFailure": "PIPE_WRITE_FAILED",
        },
        "conclusion": {
            "proven": [
                "original bridge-store call waiting has a BRIDGE_STOPPED / bridge result channel closed branch distinct from LUA_CALL_TIMEOUT",
                "bridge-store metadata separately exposes call, command, and timeout source locations",
                "PIPE_DISCONNECTED and PIPE_WRITE_FAILED remain connection-layer terminal vocabulary from prior exact transport recovery",
            ],
            "notProven": [
                "ordinary named-pipe disconnect closes every outstanding result receiver immediately",
                "PIPE_WRITE_FAILED closes every outstanding result receiver immediately",
                "generation-scoped route removal and result-channel closure are the same operation",
                "all pending public calls should fail eagerly with BRIDGE_STOPPED on ordinary connection loss",
            ],
            "implementationDecision": "do not change pending-call teardown from R8-075 on this evidence",
        },
        "limits": [
            "this checkpoint uses non-executable metadata only and does not disassemble original host executable code",
            "global Rust oneshot/mpsc metadata exists in the binary but is not sufficient to assign a specific channel implementation to bridge_store from this pass",
            "no production behavior is changed",
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
