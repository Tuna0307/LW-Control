#!/usr/bin/env python3
"""Read-only decoder for the current LWLF v3 native xLua LENC format."""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
import zlib
from pathlib import Path
from typing import Any

import pefile

try:
    from .install_loader_probe import LUA_ENTRY, discover_paths, read_lwlf
except ImportError:  # direct script execution
    from install_loader_probe import LUA_ENTRY, discover_paths, read_lwlf


EXPECTED_XLUA_SHA256 = "21eb704afdb7e528f4b90fa1b90bf414c221b06ba990d625aaaaed31b292740f"
EXPECTED_LUA_ENTRY_SHA256 = "50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137"
EXPECTED_KEY = bytes.fromhex(
    "e916bd5e0105ffd6514ca6d01177e39d26eaca762d9cbb899b6a1cdfa4f43255"
)
EXPECTED_NONCE = bytes.fromhex("835e212a03453039b83ee25a")
KEY_TABLE_RVAS = (0x7F180, 0x7F2E0)
SECRET_SIZE = 44
LENC_MAGIC = b"LENC"
NATIVE_TRANSFORM_RVA = 0x27564
NATIVE_KEY_NONCE_RVA = 0x27918
NATIVE_ZERO_RVA = 0x279BC


class LencV3Error(ValueError):
    pass


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def derive_secret_from_tables(table_a: bytes, table_b: bytes) -> tuple[bytes, bytes]:
    if len(table_a) != SECRET_SIZE or len(table_b) != SECRET_SIZE:
        raise LencV3Error("xLua key tables must each contain exactly 44 bytes")
    secret = bytes(left ^ right for left, right in zip(table_a, table_b))
    return secret[:32], secret[32:]


def _leaf_byte(pe: pefile.PE, target_va: int) -> int:
    image_base = int(pe.OPTIONAL_HEADER.ImageBase)
    target_rva = target_va - image_base
    try:
        offset = pe.get_offset_from_rva(target_rva)
    except pefile.PEFormatError as exc:
        raise LencV3Error(f"key-table target RVA 0x{target_rva:X} is not mapped") from exc
    leaf = pe.__data__[offset : offset + 3]
    if len(leaf) < 3 or leaf[0] != 0xB0 or leaf[2] != 0xC3:
        raise LencV3Error(
            f"key-table target RVA 0x{target_rva:X} no longer has 'mov al, imm8; ret' shape"
        )
    return leaf[1]


def _read_secret_table(pe: pefile.PE, table_rva: int) -> bytes:
    try:
        offset = pe.get_offset_from_rva(table_rva)
    except pefile.PEFormatError as exc:
        raise LencV3Error(f"key table RVA 0x{table_rva:X} is not mapped") from exc
    raw = pe.__data__[offset : offset + SECRET_SIZE * 8]
    if len(raw) != SECRET_SIZE * 8:
        raise LencV3Error(f"key table RVA 0x{table_rva:X} is truncated")
    values = []
    for index in range(SECRET_SIZE):
        target_va = struct.unpack_from("<Q", raw, index * 8)[0]
        values.append(_leaf_byte(pe, target_va))
    return bytes(values)


def derive_xlua_key_nonce(path: Path) -> dict[str, Any]:
    data = path.read_bytes()
    actual_hash = sha256_bytes(data)
    if actual_hash != EXPECTED_XLUA_SHA256:
        raise LencV3Error(
            f"unsupported xlua.dll SHA-256 {actual_hash}; expected {EXPECTED_XLUA_SHA256}"
        )
    pe = pefile.PE(data=data, fast_load=False)
    table_a = _read_secret_table(pe, KEY_TABLE_RVAS[0])
    table_b = _read_secret_table(pe, KEY_TABLE_RVAS[1])
    key, nonce = derive_secret_from_tables(table_a, table_b)
    if key != EXPECTED_KEY or nonce != EXPECTED_NONCE:
        raise LencV3Error("derived xLua LENC key/nonce no longer match the current-build evidence")
    return {
        "sha256": actual_hash,
        "table_a_hex": table_a.hex(),
        "table_b_hex": table_b.hex(),
        "key": key,
        "nonce": nonce,
    }


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


def chacha8_core_no_feedforward_block(key: bytes, nonce: bytes, counter: int) -> bytes:
    """Reproduce RVA 0x27564: ChaCha8 rounds without normal feed-forward."""
    if len(key) != 32:
        raise LencV3Error("LENC key must be 32 bytes")
    if len(nonce) != 12:
        raise LencV3Error("LENC nonce must be 12 bytes")
    if not 0 <= counter <= 0xFFFFFFFF:
        raise LencV3Error("LENC counter must fit in uint32")

    state = list(struct.unpack("<4I", b"expand 32-byte k"))
    state.extend(struct.unpack("<8I", key))
    state.append(counter)
    state.extend(struct.unpack("<3I", nonce))

    # Native helper RVA 0x27564 executes four ChaCha double rounds. Unlike the
    # standard block function, it XORs this working state directly with the
    # payload and never adds the original state back at the end.
    for _ in range(4):
        _quarter_round(state, 0, 4, 8, 12)
        _quarter_round(state, 1, 5, 9, 13)
        _quarter_round(state, 2, 6, 10, 14)
        _quarter_round(state, 3, 7, 11, 15)
        _quarter_round(state, 0, 5, 10, 15)
        _quarter_round(state, 1, 6, 11, 12)
        _quarter_round(state, 2, 7, 8, 13)
        _quarter_round(state, 3, 4, 9, 14)
    return struct.pack("<16I", *state)


def transform_payload(payload: bytes, key: bytes, nonce: bytes) -> bytes:
    output = bytearray(len(payload))
    for offset in range(0, len(payload), 64):
        counter = offset // 64
        if counter > 0xFFFFFFFF:
            raise LencV3Error("LENC payload exceeds the native uint32 block counter")
        stream = chacha8_core_no_feedforward_block(key, nonce, counter)
        block = payload[offset : offset + 64]
        output[offset : offset + len(block)] = bytes(
            source ^ mask for source, mask in zip(block, stream)
        )
    return bytes(output)


def encode_lenc_bytes(plaintext: bytes, key: bytes, nonce: bytes, *, compress: bool = True) -> bytes:
    """Encode bytes for the current LWLF-v3 native xLua LENC loader.

    The native transform is XOR based, so encryption and decryption use the same
    stream operation.  The loader only enters its inflate branch for a 78 DA zlib
    stream; Python's level-9 zlib output has that header and round-trips through
    the recovered native decoder.
    """
    if not isinstance(plaintext, (bytes, bytearray)):
        raise LencV3Error("LENC plaintext must be bytes")
    payload = zlib.compress(bytes(plaintext), 9) if compress else bytes(plaintext)
    if compress and not payload.startswith(b"\x78\xDA"):
        raise LencV3Error("compressed LENC payload did not produce the required 78 DA header")
    return LENC_MAGIC + transform_payload(payload, key, nonce)


def decode_lenc_bytes(entry: bytes, key: bytes, nonce: bytes) -> dict[str, Any]:
    if len(entry) < 4 or not entry.startswith(LENC_MAGIC):
        raise LencV3Error("entry does not begin with the LENC magic")
    transformed = transform_payload(entry[4:], key, nonce)
    compressed = transformed.startswith(b"\x78\xDA")
    if compressed:
        try:
            decoded = zlib.decompress(transformed)
        except zlib.error as exc:
            raise LencV3Error(f"native LENC payload has 78DA header but zlib inflate failed: {exc}") from exc
    else:
        decoded = transformed
    return {
        "transformed": transformed,
        "decoded": decoded,
        "zlib_inflated": compressed,
    }


def inspect_installed_entry(xlua: Path, scripts: Path, entry_name: str) -> dict[str, Any]:
    native = derive_xlua_key_nonce(xlua)
    file_version, content_version, entries = read_lwlf(scripts)
    if file_version != 3:
        raise LencV3Error(f"expected LWLF file version 3, got {file_version}")
    mapped = dict(entries)
    if entry_name not in mapped:
        raise LencV3Error(f"entry {entry_name!r} is missing from the script package")
    entry = bytes(mapped[entry_name])
    entry_hash = sha256_bytes(entry)
    if entry_name == LUA_ENTRY and entry_hash != EXPECTED_LUA_ENTRY_SHA256:
        raise LencV3Error(
            f"current LuaEntry SHA-256 changed: {entry_hash}; expected {EXPECTED_LUA_ENTRY_SHA256}"
        )
    decoded = decode_lenc_bytes(entry, native["key"], native["nonce"])
    transformed = decoded["transformed"]
    plaintext = decoded["decoded"]
    return {
        "xlua": str(xlua),
        "xlua_sha256": native["sha256"],
        "scripts": str(scripts),
        "file_version": file_version,
        "content_version": content_version,
        "entry": entry_name,
        "entry_size": len(entry),
        "entry_sha256": entry_hash,
        "entry_prefix_hex": entry[:32].hex(),
        "native_helpers": {
            "transform_rva": f"0x{NATIVE_TRANSFORM_RVA:X}",
            "key_nonce_rva": f"0x{NATIVE_KEY_NONCE_RVA:X}",
            "zero_rva": f"0x{NATIVE_ZERO_RVA:X}",
            "rounds": 8,
            "feed_forward": False,
        },
        "key_hex": native["key"].hex(),
        "nonce_hex": native["nonce"].hex(),
        "table_a_hex": native["table_a_hex"],
        "table_b_hex": native["table_b_hex"],
        "transformed_prefix_hex": transformed[:64].hex(),
        "zlib_inflated": decoded["zlib_inflated"],
        "decoded_size": len(plaintext),
        "decoded_sha256": sha256_bytes(plaintext),
        "decoded_prefix_hex": plaintext[:96].hex(),
        "lua_signature": plaintext[:4] == b"\x1bLua",
        "lua_version_byte": plaintext[4] if len(plaintext) > 4 else None,
        "read_only": True,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--xlua", type=Path)
    parser.add_argument("--scripts", type=Path)
    parser.add_argument("--entry", default=LUA_ENTRY)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    paths = discover_paths()
    xlua = args.xlua or (
        paths["game_exe"].parent / "LastWar_Data" / "Plugins" / "x86_64" / "xlua.dll"
    )
    scripts = args.scripts or paths["data"]
    try:
        result = inspect_installed_entry(xlua, scripts, args.entry)
    except (LencV3Error, OSError, EOFError, ValueError, pefile.PEFormatError) as exc:
        output = {"ok": False, "error": str(exc), "error_type": type(exc).__name__}
        print(json.dumps(output, indent=2) if args.json else f"REFUSED: {exc}")
        return 2
    output = {"ok": True, **result}
    print(json.dumps(output, indent=2) if args.json else output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
