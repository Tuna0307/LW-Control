#!/usr/bin/env python3
"""Extract only the source-proven embedded LWBridge 0.3.17 payload resources.

This is a static file-carving tool. It does not launch LWBridge, read runtime
authentication state, access the persisted CNG private key, or decrypt the
original package.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import pefile

REFERENCE = Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
REFERENCE_SHA256 = "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"

ASSETS = {
    "bridge-scripts.dat": {
        "offset": 0x8EED88,
        "size": 1_418_098,
        "sha256": "c215b5aa87619f547f2d999e58d1a1cb4780b49fec7040362732257d86d4fa82",
        "kind": "LWBPv2-encrypted-package",
    },
    "xlua-proxy-secure.dll": {
        "offset": 0xA4919B,
        "size": 619_008,
        "sha256": "ae8bba866df80e9c924923d1f59d49e824c784c8ba9b47780fe2f58305e00a86",
        "kind": "PE32+-dll",
    },
    "xlua-proxy-plain.dll": {
        "offset": 0xAE039B,
        "size": 620_544,
        "sha256": "266129c6c92f3ae89001493d61ccfdf4f5dac1fe66cff4fbb4799e6c01a6c902",
        "kind": "PE32+-dll",
    },
    "xlua-proxy-bundle.json": {
        "offset": 0xB77B9B,
        "size": 590,
        "sha256": "d6b3c71208a658d3d4ff05ab1af2e5abee3f5bab05839e15d6c97f6d58cffd1c",
        "kind": "json",
    },
}


class ExtractError(RuntimeError):
    pass


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ExtractError(message)


def validate_lwbp2(data: bytes) -> dict[str, object]:
    require(len(data) >= 66, "LWBP package too short")
    require(data[:4] == b"LWBP", "LWBP magic changed")
    version = int.from_bytes(data[4:8], "little")
    build_len = int.from_bytes(data[8:12], "little")
    require(version == 2, f"LWBP version changed: {version}")
    require(build_len == 22, f"LWBP build length changed: {build_len}")
    build = data[12 : 12 + build_len].decode("ascii")
    nonce_off = 12 + build_len
    cipher_len_off = nonce_off + 12
    cipher_len = int.from_bytes(data[cipher_len_off : cipher_len_off + 4], "little")
    ciphertext_off = cipher_len_off + 4
    tag_off = ciphertext_off + cipher_len
    require(tag_off + 16 == len(data), "LWBP declared length mismatch")
    return {
        "magic": "LWBP",
        "version": version,
        "buildId": build,
        "nonceHex": data[nonce_off:cipher_len_off].hex(),
        "ciphertextLength": cipher_len,
        "tagHex": data[tag_off:].hex(),
        "ciphertextOffset": ciphertext_off,
        "tagOffset": tag_off,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-root", type=Path, required=True)
    args = parser.parse_args()

    try:
        host = REFERENCE.read_bytes()
        require(sha256(host) == REFERENCE_SHA256, "reference EXE identity changed")

        root = args.output_root.resolve()
        root.mkdir(parents=True, exist_ok=True)

        manifest: dict[str, object] = {
            "schema": 1,
            "reference": {
                "path": str(REFERENCE),
                "sha256": REFERENCE_SHA256,
            },
            "assets": {},
            "decryption": {
                "attempted": False,
                "reason": (
                    "signed package-key.envelope is not among supplied artifacts; "
                    "matching persisted CNG private key is owner state and was not accessed/exported"
                ),
            },
        }

        for name, spec in ASSETS.items():
            offset = int(spec["offset"])
            size = int(spec["size"])
            carved = host[offset : offset + size]
            actual = sha256(carved)
            require(actual == spec["sha256"], f"{name} hash changed: {actual}")
            out = root / name
            out.write_bytes(carved)

            row: dict[str, object] = {
                "offset": f"0x{offset:X}",
                "size": size,
                "sha256": actual,
                "kind": spec["kind"],
                "output": str(out),
            }
            if name == "bridge-scripts.dat":
                row["parsed"] = validate_lwbp2(carved)
            elif name.endswith(".dll"):
                pe = pefile.PE(data=carved, fast_load=False)
                row["peValid"] = True
                row["machine"] = f"0x{int(pe.FILE_HEADER.Machine):X}"
                row["entryPointRva"] = f"0x{int(pe.OPTIONAL_HEADER.AddressOfEntryPoint):X}"
            elif name.endswith(".json"):
                parsed = json.loads(carved.decode("utf-8"))
                require(parsed["secure"]["proxySha256"] == ASSETS["xlua-proxy-secure.dll"]["sha256"],
                        "bundle secure proxy hash mismatch")
                require(parsed["plain"]["proxySha256"] == ASSETS["xlua-proxy-plain.dll"]["sha256"],
                        "bundle plain proxy hash mismatch")
                row["parsed"] = parsed

            manifest["assets"][name] = row

        manifest_path = root / "manifest.json"
        manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        print(json.dumps(manifest, indent=2))
        return 0
    except (OSError, ExtractError, pefile.PEFormatError, json.JSONDecodeError) as exc:
        parser.error(str(exc))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
