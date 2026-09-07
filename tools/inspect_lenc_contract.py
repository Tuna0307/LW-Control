#!/usr/bin/env python3
"""Read-only inspector for the managed BaseUtils LENC contract evidence.

The tool validates the current BaseUtils.rdl/script-package identity, parses the
LencCodec static constructor through the in-memory RDL header repair, and records
the managed ChaCha constants discrepancy. Subsequent xLua tracing proved this is
not the live LWLF-v3 decoder; see tools/extract_lenc_v3.py. It never writes game
files.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

try:
    from .install_loader_probe import LUA_ENTRY, discover_paths, read_lwlf
    from .inspect_baseutils_rdl import _find_text_section
    from .rdl_il import decode_metadata_token, encoded_field_token_rid_mod8, instruction_rows, parse_method_body_file
except ImportError:  # direct script execution
    from install_loader_probe import LUA_ENTRY, discover_paths, read_lwlf
    from inspect_baseutils_rdl import _find_text_section
    from rdl_il import decode_metadata_token, encoded_field_token_rid_mod8, instruction_rows, parse_method_body_file

EXPECTED_BASEUTILS_SHA256 = "b20865f42c9272e0fca2b6deb2e9142576b12dcf42f11ad1a8816ddd59b952d6"
EXPECTED_LUA_ENTRY_SHA256 = "50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137"
LENC_CCTOR_FILE_OFFSET = 0x37A8
CHACHA_CCTOR_FILE_OFFSET = 0x2B3B
EXPECTED_KEY_TOKEN = 0x679F3862
EXPECTED_MAGIC_TOKEN = 0x679F0E22
CHACHA_CONSTANTS_TOKEN = 0x679F9902
EXPECTED_MAGIC = b"LENC"
EXPECTED_ROUNDS = 8
EXPECTED_NONCE = bytes(12)
EXPECTED_LUA53_PREFIX = bytes.fromhex(
    "1b4c7561530019930d0a1a0a0404080878560000000000000000000000287740"
)

# Current-build FieldRVA evidence for every 32-byte static initializer near the
# compiler-generated private implementation data. The BaseUtils hash gate above
# makes these offsets build-specific and fail-closed rather than guessed.
STATIC32_FIELDS: dict[int, int] = {
    1467: 0x33E75,
    1469: 0x33E9B,
    1470: 0x33EBB,
    1472: 0x33EE7,
    1477: 0x33F47,
    1478: 0x33F67,
}


class LencContractError(ValueError):
    pass


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def select_rid_mod8_candidate(token_value: int, candidates: dict[int, Any]) -> tuple[int, Any]:
    residue = encoded_field_token_rid_mod8(token_value)
    matches = [(rid, value) for rid, value in candidates.items() if rid % 8 == residue]
    if len(matches) != 1:
        raise LencContractError(
            f"encoded token 0x{token_value:08X} has RID residue {residue}, "
            f"but {len(matches)} candidates match"
        )
    return matches[0]


def static_field_access_sites(
    data: bytes, text_start: int, text_size: int, token_value: int
) -> dict[str, list[int]]:
    """Find exact CIL static-field access encodings inside the current .text range."""
    if text_start < 0 or text_size < 0 or text_start + text_size > len(data):
        raise LencContractError("invalid .text range")
    if not 0 <= token_value <= 0xFFFFFFFF:
        raise LencContractError("field token must be a 32-bit unsigned integer")
    token = token_value.to_bytes(4, "little")
    result: dict[str, list[int]] = {}
    for name, opcode in (("ldsfld", 0x7E), ("ldsflda", 0x7F), ("stsfld", 0x80)):
        needle = bytes((opcode,)) + token
        sites: list[int] = []
        cursor = text_start
        text_end = text_start + text_size
        while True:
            position = data.find(needle, cursor, text_end)
            if position < 0:
                break
            sites.append(position)
            cursor = position + 1
        result[name] = sites
    return result


def _rotl32(value: int, shift: int) -> int:
    return ((value << shift) & 0xFFFFFFFF) | (value >> (32 - shift))


def _quarter_round(state: list[int], a: int, b: int, c: int, d: int) -> None:
    state[a] = (state[a] + state[b]) & 0xFFFFFFFF
    state[d] = _rotl32(state[d] ^ state[a], 16)
    state[c] = (state[c] + state[d]) & 0xFFFFFFFF
    state[b] = _rotl32(state[b] ^ state[c], 12)
    state[a] = (state[a] + state[b]) & 0xFFFFFFFF
    state[d] = _rotl32(state[d] ^ state[a], 8)
    state[c] = (state[c] + state[d]) & 0xFFFFFFFF
    state[b] = _rotl32(state[b] ^ state[c], 7)


def standard_chacha_block(key: bytes, nonce: bytes, counter: int, rounds: int) -> bytes:
    if len(key) != 32 or len(nonce) != 12 or rounds <= 0 or rounds % 2:
        raise LencContractError("ChaCha trial requires key=32, nonce=12, positive even rounds")
    constants = b"expand 32-byte k"
    state = [int.from_bytes(constants[i : i + 4], "little") for i in range(0, 16, 4)]
    state.extend(int.from_bytes(key[i : i + 4], "little") for i in range(0, 32, 4))
    state.append(counter & 0xFFFFFFFF)
    state.extend(int.from_bytes(nonce[i : i + 4], "little") for i in range(0, 12, 4))
    working = state.copy()
    for _ in range(rounds // 2):
        _quarter_round(working, 0, 4, 8, 12)
        _quarter_round(working, 1, 5, 9, 13)
        _quarter_round(working, 2, 6, 10, 14)
        _quarter_round(working, 3, 7, 11, 15)
        _quarter_round(working, 0, 5, 10, 15)
        _quarter_round(working, 1, 6, 11, 12)
        _quarter_round(working, 2, 7, 8, 13)
        _quarter_round(working, 3, 4, 9, 14)
    return b"".join(
        ((working[index] + state[index]) & 0xFFFFFFFF).to_bytes(4, "little")
        for index in range(16)
    )


def inspect_contract(baseutils: Path, script_package: Path) -> dict[str, Any]:
    base_data = baseutils.read_bytes()
    base_hash = sha256_bytes(base_data)
    if base_hash != EXPECTED_BASEUTILS_SHA256:
        raise LencContractError(
            f"unsupported BaseUtils.rdl SHA-256 {base_hash}; expected {EXPECTED_BASEUTILS_SHA256}"
        )

    parsed = parse_method_body_file(baseutils, LENC_CCTOR_FILE_OFFSET)
    rows = instruction_rows(parsed)
    ldtoken_values = [row["operand"] for row in rows if row["opcode"] == "ldtoken"]
    if ldtoken_values != [EXPECTED_MAGIC_TOKEN, EXPECTED_KEY_TOKEN]:
        raise LencContractError(
            "LencCodec..cctor ldtoken sequence changed: "
            + ", ".join(f"0x{value:08X}" for value in ldtoken_values)
        )

    static32 = {
        rid: base_data[offset : offset + 32] for rid, offset in STATIC32_FIELDS.items()
    }
    decoded_key_token = decode_metadata_token(EXPECTED_KEY_TOKEN)
    if decoded_key_token >> 24 != 0x04:
        raise LencContractError(
            f"key operand decoded to non-FieldDef token 0x{decoded_key_token:08X}"
        )
    key_rid = decoded_key_token & 0x00FFFFFF
    if key_rid not in static32:
        raise LencContractError(f"decoded key FieldDef RID {key_rid} has no expected initializer")
    key = static32[key_rid]
    if key != bytes(range(32)):
        raise LencContractError(
            f"selected LENC key FieldDef {key_rid} no longer contains 00..1F"
        )

    magic_offset = 0x33A1D
    if base_data[magic_offset : magic_offset + 4] != EXPECTED_MAGIC:
        raise LencContractError("embedded LENC magic changed")

    chacha_cctor = parse_method_body_file(baseutils, CHACHA_CCTOR_FILE_OFFSET)
    chacha_rows = instruction_rows(chacha_cctor)
    if [row["opcode"] for row in chacha_rows] != ["ldc.i4.0", "newarr", "stsfld", "ret"]:
        raise LencContractError("ChaCha20..cctor instruction shape changed")
    if chacha_rows[2]["operand"] != CHACHA_CONSTANTS_TOKEN:
        raise LencContractError(
            f"ChaCha20..cctor static field token changed: 0x{int(chacha_rows[2]['operand']):08X}"
        )
    text = _find_text_section(base_data)
    constants_accesses = static_field_access_sites(
        base_data,
        int(text["raw_pointer"]),
        int(text["raw_size"]),
        CHACHA_CONSTANTS_TOKEN,
    )
    cctor_store_offset = CHACHA_CCTOR_FILE_OFFSET + int(chacha_rows[2]["offset"])
    if constants_accesses["stsfld"] != [cctor_store_offset]:
        raise LencContractError(
            "ChaCha20.Constants has an unexpected managed stsfld site: "
            + ", ".join(f"0x{site:X}" for site in constants_accesses["stsfld"])
        )

    _, _, entries = read_lwlf(script_package)
    mapped = dict(entries)
    if LUA_ENTRY not in mapped:
        raise LencContractError(f"{LUA_ENTRY} missing from script package")
    official = mapped[LUA_ENTRY]
    official_hash = sha256_bytes(official)
    if official_hash != EXPECTED_LUA_ENTRY_SHA256:
        raise LencContractError(
            f"official Lua entry SHA-256 changed: {official_hash}"
        )
    if not official.startswith(EXPECTED_MAGIC):
        raise LencContractError("official Lua entry no longer starts with LENC")

    payload = official[4:]
    trial_stream = standard_chacha_block(key, EXPECTED_NONCE, 0, EXPECTED_ROUNDS)
    trial_plain_prefix = bytes(
        payload[index] ^ trial_stream[index]
        for index in range(min(len(EXPECTED_LUA53_PREFIX), len(payload), len(trial_stream)))
    )

    return {
        "baseutils": str(baseutils),
        "baseutils_sha256": base_hash,
        "lenc_cctor_file_offset": LENC_CCTOR_FILE_OFFSET,
        "lenc_cctor_header": {
            "original": f"0x{parsed.original_header_byte:02X}",
            "repaired": f"0x{parsed.repaired_header_byte:02X}",
        },
        "magic": EXPECTED_MAGIC.decode("ascii"),
        "magic_token": f"0x{EXPECTED_MAGIC_TOKEN:08X}",
        "key_token": f"0x{EXPECTED_KEY_TOKEN:08X}",
        "key_token_decoded": f"0x{decoded_key_token:08X}",
        "key_token_rid_mod8": encoded_field_token_rid_mod8(EXPECTED_KEY_TOKEN),
        "key_field_rid": key_rid,
        "key_hex": key.hex(),
        "nonce_hex": EXPECTED_NONCE.hex(),
        "rounds": EXPECTED_ROUNDS,
        "chacha_constants": {
            "field_token": f"0x{CHACHA_CONSTANTS_TOKEN:08X}",
            "field_token_decoded": f"0x{decode_metadata_token(CHACHA_CONSTANTS_TOKEN):08X}",
            "cctor_file_offset": CHACHA_CCTOR_FILE_OFFSET,
            "cctor_opcodes": [row["opcode"] for row in chacha_rows],
            "managed_access_sites": {
                opcode: [f"0x{site:X}" for site in sites]
                for opcode, sites in constants_accesses.items()
            },
            "managed_store_count": len(constants_accesses["stsfld"]),
            "only_managed_store_is_cctor": constants_accesses["stsfld"] == [cctor_store_offset],
        },
        "official_lua_entry": {
            "path": LUA_ENTRY,
            "size": len(official),
            "sha256": official_hash,
            "prefix_hex": official[:32].hex(),
        },
        "standard_chacha8_trial": {
            "counter": 0,
            "constants": "expand 32-byte k",
            "plain_prefix_hex": trial_plain_prefix.hex(),
            "expected_lua53_prefix_hex": EXPECTED_LUA53_PREFIX.hex(),
            "lua53_prefix_match": trial_plain_prefix == EXPECTED_LUA53_PREFIX,
        },
        "managed_path_scope": (
            "Managed ChaCha20.Constants is initialized as an empty uint array in the "
            "current RDL body even though Xor indexes four words from it. This discrepancy "
            "belongs to the managed path. Installed LWLF version 3 bypasses this path and "
            "is decoded by native xLua; see tools/extract_lenc_v3.py."
        ),
        "read_only": True,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseutils", type=Path)
    parser.add_argument("--scripts", type=Path)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    paths = discover_paths()
    baseutils = args.baseutils or paths["baseutils"]
    scripts = args.scripts or paths["data"]
    try:
        result = inspect_contract(baseutils, scripts)
    except (LencContractError, OSError, EOFError, ValueError) as exc:
        output = {"ok": False, "error": str(exc), "error_type": type(exc).__name__}
        print(json.dumps(output, indent=2) if args.json else f"REFUSED: {exc}")
        return 2
    output = {"ok": True, **result}
    print(json.dumps(output, indent=2) if args.json else output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
