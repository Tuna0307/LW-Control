#!/usr/bin/env python3
"""Verify the recovered LWBridge xLua export-ABI fingerprint contract.

This inspector is intentionally build-specific and read-only. It validates the
immutable lwbridge-0.3.1.exe reference, inspects the embedded profile launcher
without executing it, reconstructs the export fingerprint byte stream, checks
the bundled secure/plain fingerprints, and optionally classifies a supplied
official game xlua.dll.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import struct
from typing import Any

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86_const import X86_OP_IMM


OUTER_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
LAUNCHER_SHA256 = "8f42adb9ed678445425e529cdee8f12a097c63053f9d4de314a758a8dfe362de"
IMAGE_BASE = 0x140000000

ASSETS = {
    "secureProxy": (0x987F74, 0x95800, "481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400"),
    "plainProxy": (0xA1D774, 0x96000, "c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794"),
    "proxyBundle": (0xAB3774, 0x24E, "261a54f92e79ccf397544cca30039f1dd7519943da2ba77632b0e6dce98db4da"),
    "legacyXlua": (0xAB39C2, 0xBF190, "f4979b11e1990a7c064a937a80231fbb11fa13f5f8a19d2a321569dd6e5b254a"),
    "profileLauncher": (0xB72B52, 0xA7200, LAUNCHER_SHA256),
}

CLASSIFIER_FUNCTION = (0x140030FD0, 0x14003205C)
SORT_FUNCTION = (0x140032720, 0x14003277D)
INSERTION_FUNCTION = (0x140034980, 0x140034A85)
SHA256_IV_RVA = 0x89318
SHA256_IV = (
    0x6A09E667,
    0xBB67AE85,
    0x3C6EF372,
    0xA54FF53A,
    0x510E527F,
    0x9B05688C,
    0x1F83D9AB,
    0x5BE0CD19,
)

LOCATORS = {
    "exportBaseRead": 0x1400314EA,
    "exportBaseValue": 0x140031512,
    "nameOrdinalLookup": 0x1400317DC,
    "nameOrdinalValue": 0x140031810,
    "ordinalAddBase": 0x140031817,
    "recordOrdinalStore": 0x14003183D,
    "sortCall": 0x14003187D,
    "headerTail": 0x1400318F4,
    "headerHead": 0x1400318FA,
    "recordSpan": 0x14003191D,
    "recordNameOffset": 0x140031A91,
    "formatCall": 0x140031AD9,
    "sha256IvLoad0": 0x140031C35,
    "sha256IvLoad1": 0x140031C40,
    "smallSortRecordSpan": 0x140032745,
    "smallSortCompareCall": 0x14003275B,
    "ordinalCompareLoad": 0x140034992,
    "ordinalCompare": 0x140034994,
    "ordinalAlreadyOrdered": 0x1400349CE,
}


class InspectError(ValueError):
    pass


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def rva_to_offset(pe: pefile.PE, rva: int) -> int:
    try:
        return int(pe.get_offset_from_rva(rva))
    except pefile.PEFormatError as exc:
        raise InspectError(f"RVA 0x{rva:X} is not file-backed") from exc


def asset_bytes(outer_pe: pefile.PE, outer: bytes, name: str) -> bytes:
    rva, size, expected_sha = ASSETS[name]
    offset = rva_to_offset(outer_pe, rva)
    payload = outer[offset : offset + size]
    if len(payload) != size:
        raise InspectError(f"embedded asset {name} is truncated")
    digest = sha256(payload)
    if digest != expected_sha:
        raise InspectError(
            f"embedded asset {name} SHA-256 {digest} does not match {expected_sha}"
        )
    return payload


def runtime_functions(pe: pefile.PE) -> set[tuple[int, int]]:
    pe.parse_data_directories(
        directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]]
    )
    return {
        (IMAGE_BASE + int(entry.struct.BeginAddress), IMAGE_BASE + int(entry.struct.EndAddress))
        for entry in getattr(pe, "DIRECTORY_ENTRY_EXCEPTION", [])
        if int(entry.struct.BeginAddress) < int(entry.struct.EndAddress)
    }


def instruction_map(pe: pefile.PE, data: bytes, begin: int, end: int) -> dict[int, Any]:
    offset = rva_to_offset(pe, begin - IMAGE_BASE)
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    return {
        instruction.address: instruction
        for instruction in md.disasm(data[offset : offset + (end - begin)], begin)
    }


def require_instruction(
    instructions: dict[int, Any], address: int, mnemonic: str, contains: str | None = None
) -> Any:
    instruction = instructions.get(address)
    if instruction is None:
        raise InspectError(f"missing instruction at preferred VA 0x{address:X}")
    if instruction.mnemonic != mnemonic:
        raise InspectError(
            f"unexpected instruction at 0x{address:X}: {instruction.mnemonic} {instruction.op_str}"
        )
    if contains is not None and contains not in instruction.op_str:
        raise InspectError(
            f"instruction 0x{address:X} no longer contains {contains!r}: {instruction.op_str}"
        )
    return instruction


def require_call_target(instructions: dict[int, Any], address: int, target: int) -> None:
    instruction = require_instruction(instructions, address, "call")
    if not instruction.operands or instruction.operands[0].type != X86_OP_IMM:
        raise InspectError(f"call at 0x{address:X} is not direct")
    if int(instruction.operands[0].imm) != target:
        raise InspectError(
            f"call at 0x{address:X} targets 0x{int(instruction.operands[0].imm):X}; expected 0x{target:X}"
        )


def verify_classifier_static(launcher: bytes) -> dict[str, Any]:
    if sha256(launcher) != LAUNCHER_SHA256:
        raise InspectError("embedded profile launcher hash changed")
    pe = pefile.PE(data=launcher, fast_load=False)
    if int(pe.OPTIONAL_HEADER.ImageBase) != IMAGE_BASE:
        raise InspectError("unexpected embedded profile launcher image base")
    functions = runtime_functions(pe)
    for function_range in (CLASSIFIER_FUNCTION, SORT_FUNCTION, INSERTION_FUNCTION):
        if function_range not in functions:
            raise InspectError(
                f"required runtime function 0x{function_range[0]:X}-0x{function_range[1]:X} is absent"
            )

    classifier = instruction_map(pe, launcher, *CLASSIFIER_FUNCTION)
    sorter = instruction_map(pe, launcher, *SORT_FUNCTION)
    insertion = instruction_map(pe, launcher, *INSERTION_FUNCTION)

    require_instruction(classifier, LOCATORS["exportBaseRead"], "lea", "[r12 + 0x10]")
    require_instruction(classifier, LOCATORS["exportBaseValue"], "mov", "dword ptr [rbp + 0xb8]")
    require_instruction(classifier, LOCATORS["nameOrdinalLookup"], "lea", "[r12 + rax*2]")
    require_instruction(classifier, LOCATORS["nameOrdinalValue"], "movzx", "word ptr [rbp + 0xb8]")
    require_instruction(classifier, LOCATORS["ordinalAddBase"], "add", "eax, r14d")
    require_instruction(classifier, LOCATORS["recordOrdinalStore"], "mov", "dword ptr [rbp + 0xb0], eax")
    require_call_target(classifier, LOCATORS["sortCall"], 0x140032720)
    require_instruction(classifier, LOCATORS["headerTail"], "mov", "word ptr [rax + 4], 0xa31")
    require_instruction(classifier, LOCATORS["headerHead"], "mov", "dword ptr [rax], 0x4558574c")
    require_instruction(classifier, LOCATORS["recordSpan"], "shl", "r12, 5")
    require_instruction(classifier, LOCATORS["recordNameOffset"], "lea", "[r15 + 8]")
    require_call_target(classifier, LOCATORS["formatCall"], 0x1400760D0)
    require_instruction(classifier, LOCATORS["sha256IvLoad0"], "movups")
    require_instruction(classifier, LOCATORS["sha256IvLoad1"], "movdqu")

    require_instruction(sorter, LOCATORS["smallSortRecordSpan"], "shl", "rdx, 5")
    require_call_target(sorter, LOCATORS["smallSortCompareCall"], 0x140034980)
    require_instruction(insertion, LOCATORS["ordinalCompareLoad"], "mov", "dword ptr [rdx]")
    require_instruction(insertion, LOCATORS["ordinalCompare"], "cmp", "dword ptr [rdx - 0x20]")
    require_instruction(insertion, LOCATORS["ordinalAlreadyOrdered"], "jae")

    iv_offset = rva_to_offset(pe, SHA256_IV_RVA)
    iv = struct.unpack_from("<8I", launcher, iv_offset)
    if iv != SHA256_IV:
        raise InspectError(f"SHA-256 initial state changed: {iv!r}")

    return {
        "launcherSha256": LAUNCHER_SHA256,
        "classifierFunctionPreferredVa": "0x140030FD0-0x14003205C",
        "sortFunctionPreferredVa": "0x140032720-0x14003277D",
        "insertionComparatorPreferredVa": "0x140034980-0x140034A85",
        "locatorsPreferredVa": {key: f"0x{value:X}" for key, value in LOCATORS.items()},
        "sha256InitialStateRva": f"0x{SHA256_IV_RVA:X}",
    }


def export_rows(data: bytes) -> tuple[int, list[tuple[int, bytes]]]:
    pe = pefile.PE(data=data, fast_load=False)
    if not hasattr(pe, "DIRECTORY_ENTRY_EXPORT"):
        raise InspectError("PE has no export directory")
    base = int(pe.DIRECTORY_ENTRY_EXPORT.struct.Base)
    rows: list[tuple[int, bytes]] = []
    for symbol in pe.DIRECTORY_ENTRY_EXPORT.symbols:
        if symbol.name is None:
            continue
        name = bytes(symbol.name)
        try:
            name.decode("utf-8")
        except UnicodeDecodeError as exc:
            raise InspectError("export name is not valid UTF-8") from exc
        rows.append((int(symbol.ordinal), name))
    rows.sort(key=lambda row: (row[0], row[1]))
    return base, rows


def export_fingerprint(data: bytes) -> dict[str, Any]:
    base, rows = export_rows(data)
    canonical = b"LWXE1\n" + b"".join(
        str(ordinal).encode("ascii") + b":" + name + b"\n"
        for ordinal, name in rows
    )
    return {
        "exportBase": base,
        "namedExportCount": len(rows),
        "canonicalPrefixHex": b"LWXE1\n".hex(),
        "recordFormat": "<decimal export ordinal>:<UTF-8 export name>\\n",
        "sort": "ascending export ordinal; export-name byte order breaks equal-ordinal ties",
        "fingerprintSha256": sha256(canonical),
    }


def classify(fingerprint: str, secure: str, plain: str) -> str:
    if fingerprint == secure:
        return "secure"
    if fingerprint == plain:
        return "plain"
    return "unsupported"


def inspect(
    reference: Path, game_xlua: Path | None, game_xlua_label: str | None
) -> dict[str, Any]:
    outer = reference.read_bytes()
    digest = sha256(outer)
    if digest != OUTER_SHA256:
        raise InspectError(f"unsupported lwbridge SHA-256 {digest}; expected {OUTER_SHA256}")
    outer_pe = pefile.PE(data=outer, fast_load=False)

    secure_proxy = asset_bytes(outer_pe, outer, "secureProxy")
    plain_proxy = asset_bytes(outer_pe, outer, "plainProxy")
    bundle_bytes = asset_bytes(outer_pe, outer, "proxyBundle")
    legacy_xlua = asset_bytes(outer_pe, outer, "legacyXlua")
    launcher = asset_bytes(outer_pe, outer, "profileLauncher")
    static = verify_classifier_static(launcher)

    bundle = json.loads(bundle_bytes.decode("utf-8"))
    secure_expected = str(bundle["secure"]["exportFingerprint"])
    plain_expected = str(bundle["plain"]["exportFingerprint"])
    if str(bundle["secure"]["proxySha256"]) != ASSETS["secureProxy"][2]:
        raise InspectError("secure proxy bundle hash disagrees with embedded bytes")
    if str(bundle["plain"]["proxySha256"]) != ASSETS["plainProxy"][2]:
        raise InspectError("plain proxy bundle hash disagrees with embedded bytes")

    legacy_result = export_fingerprint(legacy_xlua)
    plain_proxy_result = export_fingerprint(plain_proxy)
    secure_proxy_result = export_fingerprint(secure_proxy)
    if legacy_result["fingerprintSha256"] != plain_expected:
        raise InspectError("embedded legacy xLua does not reproduce the bundled plain ABI fingerprint")
    if plain_proxy_result["fingerprintSha256"] != plain_expected:
        raise InspectError("embedded plain proxy does not reproduce the bundled plain ABI fingerprint")

    game_result: dict[str, Any] | None = None
    if game_xlua is not None:
        game_bytes = game_xlua.read_bytes()
        game_result = {
            "sourceLabel": game_xlua_label or str(game_xlua),
            "sha256": sha256(game_bytes),
            **export_fingerprint(game_bytes),
        }
        game_result["classification"] = classify(
            str(game_result["fingerprintSha256"]), secure_expected, plain_expected
        )

    return {
        "findingId": "LWB-R5-002",
        "date": "2026-09-08",
        "scope": "xLua PE export ABI fingerprint algorithm and secure/plain selector",
        "evidenceStatus": "RECOVERED",
        "sourceIdentity": {
            "reference": str(reference),
            "referenceSha256": digest,
            "embeddedProfileLauncherSha256": LAUNCHER_SHA256,
            "coordinateSystem": "preferred launcher virtual addresses (VA); embedded asset RVAs are outer-PE RVAs",
        },
        "staticRecovery": static,
        "algorithm": {
            "prefixAscii": "LWXE1\\n",
            "recordFormat": "<decimal export ordinal>:<UTF-8 export name>\\n",
            "ordinal": "IMAGE_EXPORT_DIRECTORY.Base + AddressOfNameOrdinals[i]",
            "ordering": "ascending export ordinal; export-name byte order breaks equal-ordinal ties",
            "digest": "SHA-256 over prefix followed by every named-export record",
        },
        "bundle": {
            "schemaVersion": bundle.get("schemaVersion"),
            "secureExportFingerprint": secure_expected,
            "plainExportFingerprint": plain_expected,
            "secureProxySha256": bundle["secure"]["proxySha256"],
            "plainProxySha256": bundle["plain"]["proxySha256"],
        },
        "validation": {
            "embeddedLegacyXlua": {
                "sha256": ASSETS["legacyXlua"][2],
                **legacy_result,
                "classification": classify(
                    str(legacy_result["fingerprintSha256"]), secure_expected, plain_expected
                ),
            },
            "embeddedPlainProxy": {
                "sha256": ASSETS["plainProxy"][2],
                **plain_proxy_result,
                "classification": classify(
                    str(plain_proxy_result["fingerprintSha256"]), secure_expected, plain_expected
                ),
            },
            "embeddedSecureProxy": {
                "sha256": ASSETS["secureProxy"][2],
                **secure_proxy_result,
                "classification": classify(
                    str(secure_proxy_result["fingerprintSha256"]), secure_expected, plain_expected
                ),
                "limit": "proxy self-exports are not the selector input; this proxy contains additional forwarding/wrapper exports",
            },
            "suppliedGameXlua": game_result,
        },
        "limits": [
            "Static launcher recovery plus byte-for-byte fingerprint reproduction does not prove launch/injection success.",
            "The secure/plain selector is now recovered, but descriptor proof/ticket semantics and child-launcher input decoding remain unresolved.",
        ],
        "implementationImpact": [
            "R5 xLua ABI selector recovery can be marked complete.",
            "Production launch must still fail closed until the remaining launch-proof/ticket/input contracts are recovered.",
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("reference", type=Path, help="verified lwbridge-0.3.1.exe")
    parser.add_argument(
        "--game-xlua",
        type=Path,
        help="optional official game xlua.dll to fingerprint/classify read-only",
    )
    parser.add_argument(
        "--game-xlua-label",
        help="optional non-sensitive source label recorded instead of the supplied local path",
    )
    parser.add_argument("--output", type=Path, help="optional JSON evidence output")
    args = parser.parse_args()
    try:
        result = inspect(args.reference, args.game_xlua, args.game_xlua_label)
    except (InspectError, OSError, pefile.PEFormatError, KeyError, json.JSONDecodeError) as exc:
        parser.error(str(exc))
    rendered = json.dumps(result, indent=2, ensure_ascii=False) + "\n"
    if args.output is not None:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered, encoding="utf-8")
    print(rendered, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
