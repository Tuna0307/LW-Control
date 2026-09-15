#!/usr/bin/env python3
"""Verify the secure-proxy package-key path/read boundary for LWBridge 0.3.1.

This build-specific inspector is read-only. It hash-gates lwbridge-0.3.1.exe,
extracts the embedded secure xLua proxy, verifies the exact
``\\bridge-runtime\\package-key.envelope`` path constructor, its bounded text
reader call, and a second direct path consumer. It does not inspect the
previously restricted consumer body at RVA 0x3F8E0, recover the envelope field
grammar, link parsed fields to the agreement helper, decrypt bridge-scripts.dat,
or execute LWBridge/Last War.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

import capstone
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP


EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
PROXY_OFFSET = 0x987374
PROXY_SIZE = 612_352
PROXY_SHA256 = "481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400"
EXPECTED_IMAGE_BASE = 0x180000000

PACKAGE_KEY_SUFFIX = "\\bridge-runtime\\package-key.envelope"
PACKAGE_KEY_SUFFIX_RAW = 0x70B70
PATH_FUNCTION = (0x1B300, 0x1B4BF)
TEXT_READER = (0x1BD80, 0x1C09F)
ENCLOSING_LOADER = (0x1C0A0, 0x1D46F)
OPAQUE_PATH_CONSUMER = (0x3F8E0, 0x40A6D)


class InspectError(ValueError):
    pass


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _instructions(pe: pefile.PE, data: bytes, begin_rva: int, end_rva: int):
    offset = pe.get_offset_from_rva(begin_rva)
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    return list(
        md.disasm(
            data[offset : offset + (end_rva - begin_rva)],
            pe.OPTIONAL_HEADER.ImageBase + begin_rva,
        )
    )


def _call_target(ins) -> int | None:
    if ins.mnemonic != "call" or not ins.operands:
        return None
    op = ins.operands[0]
    return op.imm if op.type == X86_OP_IMM else None


def _rip_target(ins) -> int | None:
    for op in ins.operands:
        if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
            return ins.address + ins.size + op.mem.disp
    return None


def _require_instruction(
    ins_by_rva: dict[int, Any],
    rva: int,
    mnemonic: str,
    contains: str | None = None,
):
    ins = ins_by_rva.get(rva)
    if ins is None:
        raise InspectError(f"missing instruction at RVA 0x{rva:X}")
    if ins.mnemonic != mnemonic:
        raise InspectError(f"RVA 0x{rva:X} mnemonic {ins.mnemonic!r}; expected {mnemonic!r}")
    if contains is not None and contains not in ins.op_str:
        raise InspectError(f"RVA 0x{rva:X} operands {ins.op_str!r}; expected fragment {contains!r}")
    return ins


def inspect(path: Path) -> dict[str, Any]:
    host = path.read_bytes()
    digest = _sha256(host)
    if digest != EXPECTED_SHA256:
        raise InspectError(f"unsupported lwbridge SHA-256 {digest}; expected {EXPECTED_SHA256}")

    proxy = host[PROXY_OFFSET : PROXY_OFFSET + PROXY_SIZE]
    proxy_digest = _sha256(proxy)
    if proxy_digest != PROXY_SHA256:
        raise InspectError(f"embedded secure proxy SHA-256 {proxy_digest}; expected {PROXY_SHA256}")

    pe = pefile.PE(data=proxy, fast_load=False)
    base = pe.OPTIONAL_HEADER.ImageBase
    if base != EXPECTED_IMAGE_BASE:
        raise InspectError(f"secure proxy image base 0x{base:X}; expected 0x{EXPECTED_IMAGE_BASE:X}")

    pe.parse_data_directories(
        directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]]
    )
    unwind_ranges = {
        (entry.struct.BeginAddress, entry.struct.EndAddress)
        for entry in pe.DIRECTORY_ENTRY_EXCEPTION
    }
    for expected in (PATH_FUNCTION, TEXT_READER, ENCLOSING_LOADER, OPAQUE_PATH_CONSUMER):
        if expected not in unwind_ranges:
            raise InspectError(
                f"unwind range 0x{expected[0]:X}-0x{expected[1]:X} is missing"
            )

    encoded_suffix = PACKAGE_KEY_SUFFIX.encode("utf-16le")
    if proxy[PACKAGE_KEY_SUFFIX_RAW : PACKAGE_KEY_SUFFIX_RAW + len(encoded_suffix)] != encoded_suffix:
        raise InspectError(
            f"package-key suffix missing at raw offset 0x{PACKAGE_KEY_SUFFIX_RAW:X}"
        )
    suffix_rva = pe.get_rva_from_offset(PACKAGE_KEY_SUFFIX_RAW)
    suffix_va = base + suffix_rva

    decoded: dict[str, list[Any]] = {}
    ins_by_rva: dict[int, Any] = {}
    for name, bounds in {
        "path_function": PATH_FUNCTION,
        "text_reader": TEXT_READER,
        "enclosing_loader": ENCLOSING_LOADER,
    }.items():
        rows = _instructions(pe, proxy, *bounds)
        decoded[name] = rows
        for ins in rows:
            ins_by_rva[ins.address - base] = ins

    # Exact package-key path constructor.
    path_refs = []
    for ins in decoded["path_function"]:
        if _rip_target(ins) == suffix_va:
            path_refs.append(ins.address - base)
    if path_refs != [0x1B373, 0x1B38F]:
        raise InspectError(
            "package-key suffix references drifted: "
            + ", ".join(f"0x{x:X}" for x in path_refs)
        )
    if _call_target(_require_instruction(ins_by_rva, 0x1B337, "call")) != base + 0x16360:
        raise InspectError("runtime-root helper call target drifted")
    _require_instruction(ins_by_rva, 0x1B34D, "cmp", "0x24")
    _require_instruction(ins_by_rva, 0x1B36D, "mov", "0x48")
    if _call_target(_require_instruction(ins_by_rva, 0x1B37A, "call")) != base + 0x6AB00:
        raise InspectError("package-key suffix copy helper drifted")
    _require_instruction(ins_by_rva, 0x1B386, "mov", "0x24")
    _require_instruction(ins_by_rva, 0x1B399, "mov", "0x24")
    if _call_target(_require_instruction(ins_by_rva, 0x1B3A1, "call")) != base + 0x2480:
        raise InspectError("package-key suffix append helper drifted")

    # First loader branch: construct the path, then bounded text load with a
    # caller-supplied 0x1000 maximum.
    if _call_target(_require_instruction(ins_by_rva, 0x1C447, "call")) != base + PATH_FUNCTION[0]:
        raise InspectError("first package-key path call target drifted")
    _require_instruction(ins_by_rva, 0x1C44D, "mov", "0x1000")
    _require_instruction(ins_by_rva, 0x1C453, "mov", "rdx, rax")
    if _call_target(_require_instruction(ins_by_rva, 0x1C45B, "call")) != base + TEXT_READER[0]:
        raise InspectError("package-key text-reader call target drifted")

    # Bounded reader behavior. Internal C++ stream helpers are intentionally
    # left unnamed; only the exact control/data behavior is asserted here.
    _require_instruction(ins_by_rva, 0x1BDAC, "mov", "rsi, r8")
    if _call_target(_require_instruction(ins_by_rva, 0x1BDDC, "call")) != base + 0x2970:
        raise InspectError("text-reader stream-open helper drifted")
    if _call_target(_require_instruction(ins_by_rva, 0x1BE9E, "call")) != base + 0x6B80:
        raise InspectError("text-reader length helper drifted")
    _require_instruction(ins_by_rva, 0x1BEAA, "test", "rdi, rdi")
    _require_instruction(ins_by_rva, 0x1BEB3, "cmp", "rdi, rsi")
    _require_instruction(ins_by_rva, 0x1BEB6, "ja")
    if _call_target(_require_instruction(ins_by_rva, 0x1BEC6, "call")) != base + 0x3010:
        raise InspectError("text-reader allocation helper drifted")
    if _call_target(_require_instruction(ins_by_rva, 0x1BED6, "call")) != base + 0x6620:
        raise InspectError("text-reader stream-position helper drifted")
    if _call_target(_require_instruction(ins_by_rva, 0x1BEF4, "call")) != base + 0x64C0:
        raise InspectError("text-reader buffer-fill helper drifted")
    _require_instruction(ins_by_rva, 0x1BF02, "test", "6")
    _require_instruction(ins_by_rva, 0x1BFAA, "cmp", "0xd")
    _require_instruction(ins_by_rva, 0x1BFBC, "cmp", "0xa")
    _require_instruction(ins_by_rva, 0x1BFC2, "mov", "[rbp + 0x80]")
    _require_instruction(ins_by_rva, 0x1BFD5, "mov", "byte ptr [rax + rcx], 0")

    # Second loader branch: exact path is passed directly as RCX to the opaque
    # consumer. The body at 0x3F8E0 is deliberately not inspected here because
    # the earlier exact-target query against it was rejected (SB-79).
    if _call_target(_require_instruction(ins_by_rva, 0x1C8B8, "call")) != base + PATH_FUNCTION[0]:
        raise InspectError("second package-key path call target drifted")
    _require_instruction(ins_by_rva, 0x1C8DF, "mov", "rcx, rax")
    if _call_target(_require_instruction(ins_by_rva, 0x1C8E2, "call")) != base + OPAQUE_PATH_CONSUMER[0]:
        raise InspectError("opaque package-key path consumer target drifted")
    _require_instruction(ins_by_rva, 0x1C8E7, "test", "al, al")

    return {
        "findingId": "LWB-R6-046",
        "date": "2026-09-09",
        "scope": "PM7-B secure-proxy package-key path construction and bounded file-read boundary",
        "evidenceLabel": "RECOVERED",
        "source": {
            "path": str(path),
            "sha256": digest,
            "embeddedProxy": "xlua-proxy-secure.dll",
            "embeddedProxySha256": proxy_digest,
            "embeddedProxyHostRawOffset": f"0x{PROXY_OFFSET:X}",
            "coordinateSystem": "secure-proxy preferred-image RVAs/VAs plus raw offsets",
        },
        "tools": {
            "python": "3.12.10",
            "pefile": pefile.__version__,
            "capstone": capstone.__version__,
        },
        "packageKeyPath": {
            "suffix": PACKAGE_KEY_SUFFIX,
            "suffixUtf16CodeUnits": len(PACKAGE_KEY_SUFFIX),
            "suffixBytes": len(encoded_suffix),
            "proxyRawOffset": f"0x{PACKAGE_KEY_SUFFIX_RAW:X}",
            "proxyRva": f"0x{suffix_rva:X}",
            "pathFunction": {
                "beginRva": f"0x{PATH_FUNCTION[0]:X}",
                "endRva": f"0x{PATH_FUNCTION[1]:X}",
                "suffixReferenceRvas": [f"0x{x:X}" for x in path_refs],
                "runtimeRootHelperRva": "0x16360",
            },
        },
        "boundedTextReader": {
            "beginRva": f"0x{TEXT_READER[0]:X}",
            "endRva": f"0x{TEXT_READER[1]:X}",
            "pathBuilderCallRva": "0x1C447",
            "readerCallRva": "0x1C45B",
            "callerMaxBytes": 4096,
            "behavior": [
                "The reader receives its maximum size in R8 and copies it to RSI at RVA 0x1BDAC.",
                "The reader rejects a computed length that is nonpositive or greater than the caller maximum before allocating the destination buffer.",
                "After the internal stream buffer-fill path succeeds, the reader removes trailing CR (0x0D) and LF (0x0A) bytes and NUL-terminates the returned string.",
                "Failure branches return an empty string; internal C++ stream helper identities are intentionally left unnamed.",
            ],
        },
        "opaquePathConsumer": {
            "enclosingLoaderBeginRva": f"0x{ENCLOSING_LOADER[0]:X}",
            "enclosingLoaderEndRva": f"0x{ENCLOSING_LOADER[1]:X}",
            "secondPathBuilderCallRva": "0x1C8B8",
            "pathToRcxRva": "0x1C8DF",
            "consumerCallRva": "0x1C8E2",
            "consumerTargetRva": f"0x{OPAQUE_PATH_CONSUMER[0]:X}",
            "consumerUnwindEndRva": f"0x{OPAQUE_PATH_CONSUMER[1]:X}",
            "successTestRva": "0x1C8E7",
            "bodyInspected": False,
        },
        "result": [
            "Secure-proxy function RVA 0x1B300-0x1B4BF appends the exact 36-code-unit/72-byte UTF-16 suffix \\bridge-runtime\\package-key.envelope to a runtime-root value returned by helper RVA 0x16360.",
            "Enclosing loader RVA 0x1C0A0-0x1D46F calls that path constructor at RVA 0x1C447 and immediately passes the returned path to bounded text reader RVA 0x1BD80 with an exact 0x1000-byte caller maximum.",
            "The reader rejects nonpositive/oversized input, fills a destination string through internal stream helpers, returns empty on failure, and strips trailing CR/LF bytes on success.",
            "The same enclosing loader calls the path constructor again at RVA 0x1C8B8, passes the returned path directly as RCX to RVA 0x3F8E0 at 0x1C8E2, and tests AL for success. This source-attributes an exact package-key.envelope path consumer without inspecting its restricted body.",
        ],
        "reproduction": [
            "python tools\\inspect_lwbridge_proxy_package_key_reader.py ..\\LW\\lwbridge-0.3.1.exe",
            "python tools\\inspect_lwbridge_proxy_package_key_reader.py ..\\LW\\lwbridge-0.3.1.exe --output evidence\\lwbridge-implementation\\2026-09-09-r6-proxy-package-key-reader.json",
            "python -m py_compile tools\\inspect_lwbridge_proxy_package_key_reader.py",
        ],
        "limits": {
            "staticOnly": True,
            "secureProxyOnly": True,
            "notProven": [
                "the internal field grammar or parser behavior of path consumer RVA 0x3F8E0",
                "which parsed envelope field supplies the 65-byte peer public key to agreement helper RVA 0x3DD00",
                "envelope nonce/tag/ciphertext field ownership or decrypted package-key serialization",
                "the package-file caller and exact AES key/nonce/tag/ciphertext ownership at RVA 0x3DA4A",
                "plain-proxy instruction parity",
                "protected getWorldMapState/getCurrentServerId handler plaintext and authoritative readiness/error mapping",
                "live current-client behavior",
            ],
            "restrictionPreserved": "SB-79 remains in force. This inspector proves the direct path-to-RCX callsite into RVA 0x3F8E0 from the already identified enclosing loader and does not query or disassemble the consumer body.",
            "interpretationRule": "Only hash-locked literals, unwind ranges, exact instructions and direct calls outside the restricted consumer body are treated as recovered. No envelope field meanings are inferred from call adjacency or local stack layout.",
        },
        "implementationImpact": {
            "publicBehaviorEnabled": False,
            "closedGap": "exact package-key.envelope path constructor plus bounded file-read contract and direct opaque path-consumer callsite",
            "pm7B": "continue from permitted caller/output semantics around RVA 0x3F8E0 or an independent artifact path to parsed-field ownership into agreement helper RVA 0x3DD00; do not replay SB-79",
            "mapSummary": "remains fail-closed",
        },
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
    encoded = json.dumps(result, indent=2, ensure_ascii=False) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded, encoding="utf-8")
    else:
        print(encoded, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
