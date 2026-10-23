"""Inspect pinned current Last War city metadata; does not start or modify game.

Examples:
    python tools/feature018_city_inventory.py --types 'City|Build|Home'
    python tools/feature018_city_inventory.py --type '^CityBuilding$'
    python tools/feature018_city_inventory.py --methods 'Move|Shift|Put|Build|Place|Grid'
"""
from __future__ import annotations

import argparse
import hashlib
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent / "map_009_offline"))
from rgmd_il import MetadataImage

EXPECTED_SHA256 = "bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e"
DEFAULT = Path.home() / "AppData/Local/FunFly/Last War-Survival Game/Game/LastWar_Data/Assemblies/Assembly-CSharp.rdl"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--assembly", type=Path, default=DEFAULT)
    parser.add_argument("--types", help="Print matching type names, no members")
    parser.add_argument("--type", help="Print full member signatures of matching types")
    parser.add_argument("--members", help="For --type, restrict member names by regex")
    parser.add_argument("--methods", help="Print all methods with matching name")
    parser.add_argument("--fields", help="Print all fields with matching name")
    parser.add_argument("--limit", type=int, default=150)
    args = parser.parse_args()
    if hashlib.sha256(args.assembly.read_bytes()).hexdigest() != EXPECTED_SHA256:
        raise RuntimeError("Installed Assembly-CSharp.rdl SHA-256 differs from established current client")
    img = MetadataImage.load(args.assembly)
    signatures = []
    for n, t in enumerate(img.tables.TypeDef.rows, 1):
        name = img.type_name("TypeDef", n)
        if args.types and re.search(args.types, name, re.I):
            signatures.append(f"TYPE {n} {name}")
        if args.type and re.search(args.type, name, re.I):
            signatures.append(f"TYPE 0x{0x02000000|n:08x} {name}")
            for f in t.FieldList:
                row = f.row
                if args.members and not re.search(args.members, str(row.Name), re.I):
                    continue
                try:
                    ft = img.decode_field_signature(bytes(row.Signature.value))
                except Exception:
                    ft = "<signature unavailable>"
                signatures.append(f"  FIELD 0x{0x04000000|f.row_index:08x} {row.Name}: {ft}")
            for m in t.MethodList:
                row = m.row
                if args.members and not re.search(args.members, str(row.Name), re.I):
                    continue
                try:
                    rt, parameters = img.decode_method_signature(bytes(row.Signature.value))
                except Exception:
                    rt, parameters = "<signature unavailable>", []
                signatures.append(f"  METHOD 0x{0x06000000|m.row_index:08x} {row.Name}({', '.join(parameters)}) -> {rt}")
        if args.methods or args.fields:
            for m in t.MethodList if args.methods else []:
                if re.search(args.methods, str(m.row.Name), re.I):
                    try:
                        rt, parameters = img.decode_method_signature(bytes(m.row.Signature.value))
                    except Exception:
                        rt, parameters = "<signature unavailable>", []
                    signatures.append(f"METHOD 0x{0x06000000|m.row_index:08x} {name}::{m.row.Name}({', '.join(parameters)}) -> {rt}")
            for f in t.FieldList if args.fields else []:
                if re.search(args.fields, str(f.row.Name), re.I):
                    try:
                        ft = img.decode_field_signature(bytes(f.row.Signature.value))
                    except Exception:
                        ft = "<signature unavailable>"
                    signatures.append(f"FIELD 0x{0x04000000|f.row_index:08x} {name}::{f.row.Name}: {ft}")
    for line in signatures[:args.limit]:
        print(line)
    if len(signatures) > args.limit:
        print(f"... {len(signatures) - args.limit} additional matches; narrow regex")


if __name__ == "__main__":
    main()
