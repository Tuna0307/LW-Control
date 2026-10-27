#!/usr/bin/env python3
"""Current-build-only, read-only RGMD/IL method and reference inspector.

Uses rgmd_il.py, the selected metadata reader/IL decoder from
archive/research-offline-controller:tools/inspect_lastwar_rdl_metadata.py.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from rgmd_il import MetadataImage

SHA256 = "bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e"
DEFAULT = (Path.home() / "AppData/Local/FunFly/Last War-Survival Game"
           / "Game/LastWar_Data/Assemblies/Assembly-CSharp.rdl")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--assembly", type=Path, default=DEFAULT)
    ap.add_argument("--type", default="WorldPointManager", help="case insensitive type substring")
    ap.add_argument("--method", action="append", help="exact method name (repeatable)")
    ap.add_argument("--filter", action="append", help="case insensitive method-name substring (repeatable)")
    ap.add_argument("--instructions", action="store_true")
    ap.add_argument("--output", type=Path)
    args = ap.parse_args()
    with args.assembly.open("rb") as file:
        digest = hashlib.file_digest(file, "sha256").hexdigest()
    if digest != SHA256:
        raise ValueError(f"unsupported assembly hash: {digest}")
    image = MetadataImage.load(args.assembly)
    output = []
    for index, type_row in enumerate(image.tables.TypeDef.rows, 1):
        type_name = image.type_name("TypeDef", index)
        if args.type.lower() not in type_name.lower():
            continue
        for ref in type_row.MethodList:
            method = ref.row
            name = str(method.Name)
            if args.method and name not in args.method:
                continue
            if args.filter and not any(term.lower() in name.lower() for term in args.filter):
                continue
            ret, params = image.decode_method_signature(bytes(method.Signature.value))
            row = {"type": type_name, "method": name, "token": f"0x{0x06000000 | ref.row_index:08x}",
                   "rva": f"0x{method.Rva:x}", "returns": ret, "parameters": params}
            if args.instructions:
                try:
                    row["il"] = image.disassemble(method)
                except (ValueError, IndexError) as error:
                    row["parseError"] = str(error)
            output.append(row)
    result = {"assemblySha256": digest, "methods": output}
    content = json.dumps(result, indent=2, ensure_ascii=False)
    if args.output:
        if "map-009-offline" not in str(args.output.resolve()).lower():
            raise ValueError("managed reports must remain in ignored task scratch")
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(content + "\n", encoding="utf-8")
        print(f"wrote {len(output)} methods: {args.output}")
    else:
        print(content)


if __name__ == "__main__":
    main()
