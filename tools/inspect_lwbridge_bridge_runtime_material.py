#!/usr/bin/env python3
"""Verify the recovered LWBridge package-key/runtime-material boundary.

This inspector is build-specific and read-only.  It validates the immutable
lwbridge-0.3.1.exe, verifies the outer-host authorization-response field
extractors and runtime-material filename builder, and checks the embedded proxy
and bridge-package vocabulary needed for the PM7-B boundary.  It does not
decrypt bridge-scripts.dat, read user authorization files, or execute LWBridge
or Last War.
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

AUTH_RESPONSE_FUNCTION = (0x14023C7EC, 0x14023CB51)
RUNTIME_MATERIAL_FUNCTION = (0x14023CC15, 0x14023D5B8)

EXTRACT_HELPER = 0x14023F10C
ERROR_HELPER = 0x1402361FE
PATH_JOIN_HELPER = 0x1405C6750

AUTH_FIELDS = (
    {
        "name": "authorizationTicket",
        "xref": 0x14023C802,
        "lengthVa": 0x14023C80E,
        "length": 0x13,
        "callVa": 0x14023C81A,
    },
    {
        "name": "authorizationTicketExpiresAt",
        "xref": 0x14023C81F,
        "lengthVa": 0x14023C82B,
        "length": 0x1C,
        "callVa": 0x14023C834,
    },
    {
        "name": "packageKeyEnvelope",
        "xref": 0x14023C95E,
        "lengthVa": 0x14023C96A,
        "length": 0x12,
        "callVa": 0x14023C976,
    },
    {
        "name": "packageKeyEnvelopeExpiresAt",
        "xref": 0x14023C97B,
        "lengthVa": 0x14023C987,
        "length": 0x1B,
        "callVa": 0x14023C990,
    },
)

ENVELOPE_INVALID_BRANCHES = (
    {"xref": 0x14023CA27, "lengthVa": 0x14023CA2E, "callVa": 0x14023CA37},
    {"xref": 0x14023CA70, "lengthVa": 0x14023CA77, "callVa": 0x14023CA80},
)

RUNTIME_FILES = (
    {
        "name": "authorization.challenge",
        "xref": 0x14023D0F2,
        "callVa": 0x14023D107,
    },
    {
        "name": "authorization.ticket",
        "xref": 0x14023D111,
        "callVa": 0x14023D126,
    },
    {
        "name": "package-key.envelope",
        "xref": 0x14023D130,
        "callVa": 0x14023D145,
    },
    {
        "name": "build.manifest",
        "xref": 0x14023D153,
        "callVa": 0x14023D168,
    },
)

EMBEDDED = {
    "bridge-scripts.dat": {
        "offset": 0x868DE0,
        "size": 1_172_723,
        "sha256": "a1e23419c25c76bee16942922a643381e0d38a857902569d927f22ef02c99c8d",
    },
    "xlua-proxy-secure.dll": {
        "offset": 0x987374,
        "size": 612_352,
        "sha256": "481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400",
    },
    "xlua-proxy-plain.dll": {
        "offset": 0xA1CB74,
        "size": 614_400,
        "sha256": "c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794",
    },
}

PROXY_UTF16 = (
    "LWBRIDGE_PROFILE_RUNTIME_ROOT",
    r"\bridge-runtime\package-key.envelope",
    r"\bridge-runtime\build.manifest",
    r"\LastWar-xLua-Bridge\bridge-scripts.dat",
)

PROXY_ASCII = (
    "LWKE1",
    "key envelope missing",
    "key envelope expired",
    "key envelope invalid",
    "key envelope agreement failed",
    "key envelope decrypt failed",
    "package decrypt failed",
    "package integrity invalid",
    "package build mismatch",
    "LWBP2|",
)


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


def _call_target(instruction: Any) -> int | None:
    if instruction.mnemonic != "call" or not instruction.operands:
        return None
    operand = instruction.operands[0]
    return int(operand.imm) if operand.type == X86_OP_IMM else None


def _rip_target(instruction: Any) -> int | None:
    for operand in instruction.operands:
        if operand.type == X86_OP_MEM and operand.mem.base == X86_REG_RIP:
            return instruction.address + instruction.size + operand.mem.disp
    return None


def _text_at_va(
    pe: pefile.PE, data: bytes, image_base: int, va: int, size: int
) -> bytes:
    offset = int(pe.get_offset_from_rva(va - image_base))
    return data[offset : offset + size]


def _expect_rip_ascii(
    pe: pefile.PE,
    data: bytes,
    image_base: int,
    instructions: dict[int, Any],
    va: int,
    expected: str,
    mnemonic: str,
) -> int:
    instruction = _instruction(instructions, va, mnemonic)
    target = _rip_target(instruction)
    if target is None:
        raise InspectError(f"instruction 0x{va:X} has no RIP-relative operand")
    raw = expected.encode("ascii")
    if _text_at_va(pe, data, image_base, target, len(raw)) != raw:
        raise InspectError(
            f"RIP target 0x{target:X} from 0x{va:X} does not equal {expected!r}"
        )
    return target


def _expect_immediate(instructions: dict[int, Any], va: int, expected: int) -> None:
    instruction = _instruction(instructions, va)
    actual = _immediate(instruction)
    if actual != expected:
        raise InspectError(
            f"unexpected immediate at 0x{va:X}: {actual!r}; expected 0x{expected:X}"
        )


def _expect_call(instructions: dict[int, Any], va: int, target: int) -> None:
    instruction = _instruction(instructions, va, "call")
    actual = _call_target(instruction)
    if actual != target:
        raise InspectError(
            f"unexpected call target at 0x{va:X}: {actual!r}; expected 0x{target:X}"
        )


def _extract_asset(data: bytes, name: str) -> bytes:
    spec = EMBEDDED[name]
    offset = int(spec["offset"])
    size = int(spec["size"])
    asset = data[offset : offset + size]
    if len(asset) != size:
        raise InspectError(f"embedded {name} is truncated")
    digest = _sha256(asset)
    if digest != spec["sha256"]:
        raise InspectError(
            f"embedded {name} SHA-256 {digest} does not match {spec['sha256']}"
        )
    return asset


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
    for function in (AUTH_RESPONSE_FUNCTION, RUNTIME_MATERIAL_FUNCTION):
        if function not in functions:
            raise InspectError(
                f"runtime-function range is absent: 0x{function[0]:X}-0x{function[1]:X}"
            )

    auth = _instruction_map(pe, data, image_base, *AUTH_RESPONSE_FUNCTION)
    field_rows: list[dict[str, Any]] = []
    for field in AUTH_FIELDS:
        target = _expect_rip_ascii(
            pe,
            data,
            image_base,
            auth,
            int(field["xref"]),
            str(field["name"]),
            "lea",
        )
        _expect_immediate(auth, int(field["lengthVa"]), int(field["length"]))
        _expect_call(auth, int(field["callVa"]), EXTRACT_HELPER)
        field_rows.append(
            {
                "name": field["name"],
                "length": field["length"],
                "xrefPreferredVa": f"0x{int(field['xref']):X}",
                "stringPreferredVa": f"0x{target:X}",
                "extractCallPreferredVa": f"0x{int(field['callVa']):X}",
                "extractHelperPreferredVa": f"0x{EXTRACT_HELPER:X}",
            }
        )

    invalid_rows: list[dict[str, Any]] = []
    for branch in ENVELOPE_INVALID_BRANCHES:
        target = _expect_rip_ascii(
            pe,
            data,
            image_base,
            auth,
            int(branch["xref"]),
            "KEY_ENVELOPE_INVALID",
            "lea",
        )
        _expect_immediate(auth, int(branch["lengthVa"]), len("KEY_ENVELOPE_INVALID"))
        _expect_call(auth, int(branch["callVa"]), ERROR_HELPER)
        invalid_rows.append(
            {
                "xrefPreferredVa": f"0x{int(branch['xref']):X}",
                "stringPreferredVa": f"0x{target:X}",
                "errorCallPreferredVa": f"0x{int(branch['callVa']):X}",
                "errorHelperPreferredVa": f"0x{ERROR_HELPER:X}",
            }
        )

    runtime = _instruction_map(pe, data, image_base, *RUNTIME_MATERIAL_FUNCTION)
    file_rows: list[dict[str, Any]] = []
    for item in RUNTIME_FILES:
        target = _expect_rip_ascii(
            pe,
            data,
            image_base,
            runtime,
            int(item["xref"]),
            str(item["name"]),
            "lea",
        )
        _expect_call(runtime, int(item["callVa"]), PATH_JOIN_HELPER)
        file_rows.append(
            {
                "name": item["name"],
                "length": len(str(item["name"])),
                "xrefPreferredVa": f"0x{int(item['xref']):X}",
                "stringPreferredVa": f"0x{target:X}",
                "pathJoinCallPreferredVa": f"0x{int(item['callVa']):X}",
                "pathJoinHelperPreferredVa": f"0x{PATH_JOIN_HELPER:X}",
            }
        )

    package = _extract_asset(data, "bridge-scripts.dat")
    if package[:4] != b"LWBP":
        raise InspectError("embedded bridge-scripts.dat does not start with LWBP")
    version = struct.unpack_from("<I", package, 4)[0]
    build_len = struct.unpack_from("<I", package, 8)[0]
    build_end = 12 + build_len
    if build_end > len(package):
        raise InspectError("embedded bridge-scripts.dat build ID exceeds package length")
    try:
        build_id = package[12:build_end].decode("ascii")
    except UnicodeDecodeError as exc:
        raise InspectError("embedded bridge-scripts.dat build ID is not ASCII") from exc
    if version != 2 or build_id != "9BupJXpEgm34lybhNhbbcQ":
        raise InspectError(
            f"unexpected bridge package header version={version} buildId={build_id!r}"
        )
    if b"LWKE1" in package:
        raise InspectError("embedded bridge-scripts.dat unexpectedly contains an LWKE1 marker")

    proxy_rows: list[dict[str, Any]] = []
    for name in ("xlua-proxy-secure.dll", "xlua-proxy-plain.dll"):
        proxy = _extract_asset(data, name)
        utf16_hits = {}
        for value in PROXY_UTF16:
            offset = proxy.find(value.encode("utf-16le"))
            if offset < 0:
                raise InspectError(f"embedded {name} is missing UTF-16 marker {value!r}")
            utf16_hits[value] = f"0x{offset:X}"
        ascii_hits = {}
        for value in PROXY_ASCII:
            offset = proxy.find(value.encode("ascii"))
            if offset < 0:
                raise InspectError(f"embedded {name} is missing ASCII marker {value!r}")
            ascii_hits[value] = f"0x{offset:X}"
        proxy_rows.append(
            {
                "name": name,
                "sha256": EMBEDDED[name]["sha256"],
                "utf16Markers": utf16_hits,
                "asciiMarkers": ascii_hits,
            }
        )

    return {
        "findingId": "LWB-R6-039",
        "date": "2026-09-09",
        "scope": "PM7-B package-key envelope and bridge runtime-material boundary",
        "evidenceStatus": "RECOVERED",
        "sourceIdentity": {
            "artifact": "../LW/lwbridge-0.3.1.exe",
            "sha256": digest,
            "preferredImageBase": f"0x{image_base:X}",
            "coordinateSystem": "preferred virtual addresses (VA) for host code; raw offsets within embedded assets",
        },
        "authorizationResponse": {
            "runtimeFunctionPreferredVa": f"0x{AUTH_RESPONSE_FUNCTION[0]:X}-0x{AUTH_RESPONSE_FUNCTION[1]:X}",
            "fields": field_rows,
            "keyEnvelopeInvalidBranches": invalid_rows,
        },
        "runtimeMaterialPaths": {
            "runtimeFunctionPreferredVa": f"0x{RUNTIME_MATERIAL_FUNCTION[0]:X}-0x{RUNTIME_MATERIAL_FUNCTION[1]:X}",
            "files": file_rows,
        },
        "bridgePackage": {
            "sha256": EMBEDDED["bridge-scripts.dat"]["sha256"],
            "size": EMBEDDED["bridge-scripts.dat"]["size"],
            "magic": "LWBP",
            "version": version,
            "buildIdLength": build_len,
            "buildId": build_id,
            "headerLength": build_end,
            "containsLwke1Marker": False,
        },
        "proxies": proxy_rows,
        "result": [
            "The verified host authorization-response parser extracts authorizationTicket, authorizationTicketExpiresAt, packageKeyEnvelope, and packageKeyEnvelopeExpiresAt through the same field-extraction helper.",
            "The same host parser has two exact KEY_ENVELOPE_INVALID construction branches after the package-key fields are reached.",
            "A separate verified host runtime-material function constructs sibling authorization.challenge, authorization.ticket, package-key.envelope, and build.manifest paths through one path helper.",
            "Both embedded xLua proxies contain LWBRIDGE_PROFILE_RUNTIME_ROOT, the bridge-runtime package-key/build-manifest relative paths, LWKE1/key-envelope/package-decrypt vocabulary, and the bridge-scripts.dat path.",
            "The embedded bridge-scripts.dat is LWBP v2 with build ID 9BupJXpEgm34lybhNhbbcQ and contains no LWKE1 marker, so the key-envelope marker is not part of the embedded package bytes.",
        ],
        "reproduction": [
            "python tools\\inspect_lwbridge_bridge_runtime_material.py ..\\LW\\lwbridge-0.3.1.exe",
            "python tools\\inspect_lwbridge_bridge_runtime_material.py ..\\LW\\lwbridge-0.3.1.exe --output evidence\\lwbridge-implementation\\2026-09-09-r6-bridge-runtime-material.json",
        ],
        "validationAndLimits": {
            "validation": "static-only against the immutable verified LWBridge reference; no live Last War/LWBridge process was present during the checkpoint",
            "liveProven": False,
            "unknownBlocked": [
                "direct dataflow/call linkage from the parsed packageKeyEnvelope value into the package-key.envelope runtime-file contents",
                "the exact proxy function/dataflow that combines LWBRIDGE_PROFILE_RUNTIME_ROOT with the bridge-runtime package-key path and opens/reads it",
                "key-envelope agreement/decrypt algorithm inputs and protected bridge-scripts.dat plaintext",
                "the plaintext getWorldMapState/getCurrentServerId handler implementation and its linkage to current-client R6-034/R6-035 callables",
                "propagated bridge transport errors, readiness/null/source semantics, and the exact map scan state is unavailable condition",
                "live authoritative correlation against the current client",
            ],
        },
        "implementationImpact": {
            "pm7B": "narrows the handler boundary to an external runtime key-envelope artifact plus the protected LWBP package; handler semantics remain unavailable",
            "mapSummary": "remains fail-closed; no production provider is enabled by this static checkpoint",
            "nextTrace": "continue permitted call/data-flow analysis around the runtime-material producer or use a future available live bridge target for authoritative handler/result correlation; do not infer protected script plaintext",
        },
    }


def _text_report(result: dict[str, Any]) -> str:
    lines = [
        f"finding={result['findingId']} status={result['evidenceStatus']}",
        f"lwbridge_sha256={result['sourceIdentity']['sha256']}",
        "auth_fields="
        + ",".join(row["name"] for row in result["authorizationResponse"]["fields"]),
        "runtime_files="
        + ",".join(row["name"] for row in result["runtimeMaterialPaths"]["files"]),
        f"bridge_package={result['bridgePackage']['magic']}v{result['bridgePackage']['version']} build={result['bridgePackage']['buildId']}",
        f"bridge_package_contains_lwke1={result['bridgePackage']['containsLwke1Marker']}",
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
