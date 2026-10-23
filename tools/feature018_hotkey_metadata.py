"""Hash-gated, read-only current Last War metadata review for Hotkeys recovery.

The installed client is never started or modified. This script lists candidate
methods/signatures only; names alone are not evidence of callable gameplay APIs.
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent / "map_009_offline"))
from inspect_managed import DEFAULT, SHA256  # noqa: E402
from rgmd_il import MetadataImage  # noqa: E402


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--assembly", type=Path, default=DEFAULT)
    parser.add_argument("--pattern", action="append", default=[])
    parser.add_argument("--type", default="")
    parser.add_argument("--limit", type=int, default=150)
    parser.add_argument("--instructions", action="store_true")
    parser.add_argument("--compact", action="store_true")
    parser.add_argument("--exclude-generated", action="store_true")
    args = parser.parse_args()
    digest = hashlib.sha256(args.assembly.read_bytes()).hexdigest()
    if digest != SHA256:
        raise ValueError(f"Current client hash changed: {digest}; expected {SHA256}")
    image = MetadataImage.load(args.assembly)
    patterns = [p.lower() for p in (args.pattern or
        ["shield", "march", "equip", "formation", "reinforc", "relocat", "teleport", "frontline", "speedup"])]
    output = []
    for i, typedef in enumerate(image.tables.TypeDef.rows, 1):
        typename = image.type_name("TypeDef", i)
        if args.type and args.type.lower() not in typename.lower():
            continue
        if args.exclude_generated and (typename.startswith(("Protobuf.", "XLua.", "Google.", "System.")) or "+" in typename):
            continue
        matching_type = any(p in typename.lower() for p in patterns)
        for m in typedef.MethodList:
            name = str(m.row.Name)
            if not (matching_type or any(p in name.lower() for p in patterns)):
                continue
            ret, params = image.decode_method_signature(bytes(m.row.Signature.value))
            row = {"type": typename, "method": name,
                   "token": f"0x{0x06000000 | m.row_index:08x}",
                   "returns": ret, "params": params}
            if args.instructions:
                try:
                    row["instructions"] = image.disassemble(m.row)[:180]
                except (ValueError, IndexError) as error:
                    row["decodeError"] = str(error)
            output.append(row)
            if len(output) >= args.limit:
                if args.compact:
                    for item in output:
                        print(f'{item["token"]} {item["type"]}::{item["method"]}({", ".join(item["params"])}) -> {item["returns"]}')
                    print("TRUNCATED")
                    return
                print(json.dumps({"assemblySha256": digest, "truncated": True,
                                  "methods": output}, ensure_ascii=False, indent=2))
                return
    if args.compact:
        for item in output:
            print(f'{item["token"]} {item["type"]}::{item["method"]}({", ".join(item["params"])}) -> {item["returns"]}')
        return
    print(json.dumps({"assemblySha256": digest, "truncated": False,
                      "methods": output}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
