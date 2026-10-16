"""Read-only, hash-admitted search of Mini Games symbols in the installed game RGMD.

Usage: python tools/feature018_mini_native.py --types monopoly sheep frontline chest
       python tools/feature018_mini_native.py --methods addSoldier unlockCell

Prints source tokens/signatures. Does not load or control a game session.
"""

from __future__ import annotations

import argparse
import hashlib
import sys
from pathlib import Path

TOOLS = Path(__file__).resolve().parent / "map_009_offline"
sys.path.insert(0, str(TOOLS))
from inspect_managed import DEFAULT, SHA256  # noqa: E402
from rgmd_il import MetadataImage  # noqa: E402


def inspect(assembly: Path, type_terms: list[str], method_terms: list[str], limit: int):
    with assembly.open("rb") as handle:
        digest = hashlib.file_digest(handle, "sha256").hexdigest()
    if digest != SHA256:
        raise ValueError(f"Unadmitted game assembly {digest}; expected {SHA256}")
    img = MetadataImage.load(assembly)
    matches = 0
    for ti, definition in enumerate(img.tables.TypeDef.rows, 1):
        name = img.type_name("TypeDef", ti)
        type_hit = type_terms and any(s in name.lower() for s in type_terms)
        method_hits = [m for m in definition.MethodList
                       if method_terms and any(s in str(m.row.Name).lower() for s in method_terms)]
        if not type_hit and not method_hits:
            continue
        print(f"TYPE {ti:#08x} {name}")
        for ref in definition.FieldList if type_hit else []:
            f = ref.row
            try:
                signature = img.decode_field_signature(bytes(f.Signature.value))
            except Exception:
                signature = "<undecoded>"
            print(f"  FIELD 0x{0x04000000 | ref.row_index:08x} {f.Name}: {signature}")
        for ref in definition.MethodList if type_hit else method_hits:
            m = ref.row
            try:
                returns, args = img.decode_method_signature(bytes(m.Signature.value))
                signature = f"{returns}({', '.join(args)})"
            except Exception:
                signature = "<undecoded>"
            print(f"  METHOD 0x{0x06000000 | ref.row_index:08x} {m.Name}: {signature}")
        matches += 1
        if matches == limit:
            print(f"TRUNCATED after {limit} matching types")
            break
    print(f"ADMITTED_SHA256 {digest} MATCHED_TYPES {matches}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--assembly", type=Path, default=DEFAULT)
    parser.add_argument("--types", nargs="*", default=[])
    parser.add_argument("--methods", nargs="*", default=[])
    parser.add_argument("--limit", type=int, default=80)
    args = parser.parse_args()
    if not args.types and not args.methods:
        parser.error("Provide --types or --methods")
    inspect(args.assembly, [s.lower() for s in args.types], [s.lower() for s in args.methods], args.limit)
