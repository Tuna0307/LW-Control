"""Inert checks for the LWBridge 0.3.17 original Lua recovery contract.

These checks parse only carved static evidence and synthetic plaintext fixtures.
They do not read the owner CNG key, authentication material, or attempt original
package decryption.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import struct
import unittest

REPO = Path(__file__).resolve().parents[1]
ROOT = (
    REPO
    / "evidence"
    / "lwbridge-0.3.17"
    / "functions"
    / "LWB317-FUNCTION-ORIGINAL-LUA-RECOVERY-006"
    / "c-extracted"
)

EXPECTED_PACKAGE_SHA256 = "c215b5aa87619f547f2d999e58d1a1cb4780b49fec7040362732257d86d4fa82"
EXPECTED_SECURE_SHA256 = "ae8bba866df80e9c924923d1f59d49e824c784c8ba9b47780fe2f58305e00a86"
EXPECTED_PLAIN_SHA256 = "266129c6c92f3ae89001493d61ccfdf4f5dac1fe66cff4fbb4799e6c01a6c902"
EXPECTED_BUNDLE_SHA256 = "d6b3c71208a658d3d4ff05ab1af2e5abee3f5bab05839e15d6c97f6d58cffd1c"

ALLOWED = set(b"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789._")


class ModuleTableError(ValueError):
    pass


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def parse_module_table(blob: bytes) -> list[tuple[str, bytes]]:
    """Independent inert model of the source-recovered 0.3.17 table grammar."""
    if len(blob) < 4:
        raise ModuleTableError("invalid module table")

    count = struct.unpack_from("<I", blob, 0)[0]
    if not (1 <= count <= 1024):
        raise ModuleTableError("invalid metadata")

    pos = 4
    out: list[tuple[str, bytes]] = []
    bootstrap_seen = False

    for _ in range(count):
        if pos + 8 > len(blob):
            raise ModuleTableError("invalid entry metadata")
        name_len, source_len = struct.unpack_from("<II", blob, pos)
        pos += 8

        if name_len == 0 or pos + name_len > len(blob):
            raise ModuleTableError("invalid entry")
        name_raw = blob[pos : pos + name_len]
        pos += name_len

        if any(byte not in ALLOWED for byte in name_raw):
            raise ModuleTableError("invalid entry")
        try:
            name = name_raw.decode("ascii")
        except UnicodeDecodeError as exc:
            raise ModuleTableError("invalid entry") from exc

        if pos + source_len > len(blob):
            raise ModuleTableError("invalid entry metadata")
        source = blob[pos : pos + source_len]
        pos += source_len

        if name == "bootstrap":
            if bootstrap_seen:
                raise ModuleTableError("duplicate bootstrap")
            bootstrap_seen = True

        out.append((name, source))

    if pos != len(blob):
        raise ModuleTableError("trailing package data")
    if not bootstrap_seen:
        raise ModuleTableError("bootstrap missing")
    return out


def build_table(entries: list[tuple[str, bytes]]) -> bytes:
    body = bytearray(struct.pack("<I", len(entries)))
    for name, source in entries:
        encoded = name.encode("ascii")
        body.extend(struct.pack("<II", len(encoded), len(source)))
        body.extend(encoded)
        body.extend(source)
    return bytes(body)


class OriginalLuaRecoveryContractChecks(unittest.TestCase):
    def test_carved_assets_match_fixed_0317_identities(self):
        self.assertEqual(digest(ROOT / "bridge-scripts.dat"), EXPECTED_PACKAGE_SHA256)
        self.assertEqual(digest(ROOT / "xlua-proxy-secure.dll"), EXPECTED_SECURE_SHA256)
        self.assertEqual(digest(ROOT / "xlua-proxy-plain.dll"), EXPECTED_PLAIN_SHA256)
        self.assertEqual(digest(ROOT / "xlua-proxy-bundle.json"), EXPECTED_BUNDLE_SHA256)

        manifest = json.loads((ROOT / "manifest.json").read_text(encoding="utf-8"))
        self.assertFalse(manifest["decryption"]["attempted"])
        self.assertEqual(
            manifest["assets"]["bridge-scripts.dat"]["parsed"]["buildId"],
            "yoWZOvy8GWpsTNLrcZGcYQ",
        )

    def test_lwbp2_header_is_self_consistent(self):
        blob = (ROOT / "bridge-scripts.dat").read_bytes()
        self.assertEqual(blob[:4], b"LWBP")
        self.assertEqual(struct.unpack_from("<I", blob, 4)[0], 2)
        build_len = struct.unpack_from("<I", blob, 8)[0]
        self.assertEqual(build_len, 22)
        self.assertEqual(blob[12:34], b"yoWZOvy8GWpsTNLrcZGcYQ")
        cipher_len = struct.unpack_from("<I", blob, 46)[0]
        self.assertEqual(cipher_len, 1_418_032)
        self.assertEqual(50 + cipher_len + 16, len(blob))

    def test_valid_synthetic_module_table(self):
        table = build_table(
            [
                ("alpha.beta", b"return 7"),
                ("bootstrap", b"return require('alpha.beta')"),
                ("UPPER_9", b"return true"),
            ]
        )
        parsed = parse_module_table(table)
        self.assertEqual([name for name, _ in parsed], ["alpha.beta", "bootstrap", "UPPER_9"])
        self.assertEqual(parsed[0][1], b"return 7")

    def test_module_table_rejects_missing_bootstrap(self):
        with self.assertRaisesRegex(ModuleTableError, "bootstrap missing"):
            parse_module_table(build_table([("alpha", b"return 1")]))

    def test_module_table_rejects_duplicate_bootstrap(self):
        with self.assertRaisesRegex(ModuleTableError, "duplicate bootstrap"):
            parse_module_table(
                build_table(
                    [
                        ("bootstrap", b"return 1"),
                        ("bootstrap", b"return 2"),
                    ]
                )
            )

    def test_module_table_rejects_invalid_name(self):
        with self.assertRaisesRegex(ModuleTableError, "invalid entry"):
            parse_module_table(
                build_table(
                    [
                        ("bootstrap", b"return 1"),
                        ("bad/name", b"return 2"),
                    ]
                )
            )

    def test_module_table_rejects_trailing_data(self):
        table = build_table([("bootstrap", b"return 1")]) + b"\x00"
        with self.assertRaisesRegex(ModuleTableError, "trailing package data"):
            parse_module_table(table)

    def test_module_table_rejects_zero_or_oversized_count(self):
        with self.assertRaisesRegex(ModuleTableError, "invalid metadata"):
            parse_module_table(struct.pack("<I", 0))
        with self.assertRaisesRegex(ModuleTableError, "invalid metadata"):
            parse_module_table(struct.pack("<I", 1025))


if __name__ == "__main__":
    unittest.main()
