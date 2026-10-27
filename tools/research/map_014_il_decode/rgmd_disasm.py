#!/usr/bin/env python3
"""Readable IL for RGMD (.rdl) methods with encrypted token / branch operands decoded.

Static, read-only. Usage:
  python rgmd_disasm.py --type WorldGetBlockMessage --method CSSetData
  python rgmd_disasm.py --token 0x0600351d
"""
from __future__ import annotations

import argparse
import struct
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "map_009_offline"))
from rgmd_il import MetadataImage, _read_compressed_uint  # noqa: E402
from dncil.cil.body.reader import read_method_body_from_bytes  # noqa: E402
from dnfile import stream  # noqa: E402
from rgmd_token import decode_token, ror32  # noqa: E402

DEFAULT = (Path.home() / "AppData/Local/FunFly/Last War-Survival Game/Game/LastWar_Data/Assemblies/Assembly-CSharp.rdl")

# Branch/int32 operand constants (fit so that all long-branch offsets are small and
# land on instruction boundaries; identical to the token constants in bits 0..19).
BA, BB, BC = 0x02090CBA, 0x10452197, 0x0572D2D2

LONG_BRANCH = {"br", "brfalse", "brtrue", "beq", "bge", "bgt", "ble", "blt", "bne.un", "bge.un", "bgt.un",
               "ble.un", "blt.un", "leave"}
TOKEN_OPS = {"call", "callvirt", "newobj", "ldfld", "ldflda", "stfld", "ldsfld", "ldsflda", "stsfld", "ldtoken",
             "castclass", "isinst", "box", "unbox", "unbox.any", "newarr", "initobj", "constrained.", "sizeof",
             "ldstr", "ldelem", "stelem", "ldelema", "ldftn", "ldvirtftn", "jmp", "ldobj", "stobj", "cpobj",
             "mkrefany", "refanyval", "calli"}


def decode_int32(enc: int) -> int:
    d = (((ror32(enc, 5) ^ BC) - BB) & 0xFFFFFFFF) ^ BA
    return d - (1 << 32) if d >> 31 else d


class Resolver:
    def __init__(self, img: MetadataImage):
        self.img = img
        self.T = img.tables
        us = next(s for s in img.streams if isinstance(s, stream.UserStringHeap))
        self.us = us.__data__

    def user_string(self, off: int) -> str:
        b = self.us[off]
        if b & 0x80 == 0:
            n, h = b, 1
        elif b & 0xC0 == 0x80:
            n, h = ((b & 0x3F) << 8) | self.us[off + 1], 2
        else:
            n, h = ((b & 0x1F) << 24) | (self.us[off + 1] << 16) | (self.us[off + 2] << 8) | self.us[off + 3], 4
        n >>= 1  # RGMD stores doubled lengths
        return self.us[off + h: off + h + n - 1].decode("utf-16-le", "replace")

    def coded_parent(self, ref) -> str:
        if ref is None or ref.table is None:
            return "?"
        name = ref.table.name
        if name in ("TypeDef", "TypeRef", "TypeSpec"):
            return self.img.type_name(name, ref.row_index)
        if name == "MethodDef":
            return self.img.method_label(ref.row_index)
        return f"{name}[{ref.row_index}]"

    def sig_text(self, blob: bytes) -> str:
        try:
            if blob and blob[0] == 0x06:
                return self.img.decode_field_signature(blob)
            ret, args = self.img.decode_method_signature(blob)
            return f"{ret}({', '.join(args)})"
        except Exception:
            return blob.hex()

    def method_def(self, row: int) -> str:
        m = self.T.MethodDef.rows[row - 1]
        return f"{self.img.method_label(row)}{self.sig_text(bytes(m.Signature.value)).split(')')[0].partition('(')[1]}" \
               f"{self.sig_text(bytes(m.Signature.value)).partition('(')[2]} -> " \
               f"{self.sig_text(bytes(m.Signature.value)).partition('(')[0]}"

    def token(self, tok: int) -> str:
        table, row = tok >> 24, tok & 0xFFFFFF
        try:
            if table == 0x70:
                return repr(self.user_string(row))
            if table == 0x04:
                f = self.T.Field.rows[row - 1]
                return f"{self.img.field_label(row)} : {self.img.decode_field_signature(bytes(f.Signature.value))}"
            if table == 0x06:
                return "[MethodDef] " + self.method_def(row)
            if table == 0x0A:
                mr = self.T.MemberRef.rows[row - 1]
                return f"[MemberRef] {self.coded_parent(mr.Class)}::{mr.Name} {self.sig_text(bytes(mr.Signature.value))}"
            if table == 0x2B:
                ms = self.T.MethodSpec.rows[row - 1]
                blob = bytes(ms.Instantiation.value)
                cnt, pos = _read_compressed_uint(blob, 1)
                args = []
                for _ in range(cnt):
                    a, pos = self.img.decode_type(blob, pos)
                    args.append(a)
                m = ms.Method
                inner = self.token(((0x06 if m.table.name == "MethodDef" else 0x0A) << 24) | m.row_index)
                return f"[MethodSpec <{', '.join(args)}>] {inner}"
            if table in (0x01, 0x02, 0x1B):
                return {1: "TypeRef", 2: "TypeDef", 0x1B: "TypeSpec"}[table] + " " + \
                    self.img.type_name({1: "TypeRef", 2: "TypeDef", 0x1B: "TypeSpec"}[table], row)
            if table == 0x11:
                return f"StandAloneSig[{row}]"
        except Exception as ex:  # pragma: no cover
            return f"0x{tok:08x} <{ex}>"
        return f"0x{tok:08x}"


def disasm(img: MetadataImage, res: Resolver, row: int) -> list[str]:
    m = img.tables.MethodDef.rows[row - 1]
    if m.Rva == 0:
        return ["  <no body (abstract/extern/runtime)>"]
    off = img.rva_to_offset(m.Rva)
    raw = bytearray(img.data[off: off + 0x40000])
    if (raw[0] & 3) in (0, 1):
        raw[0] |= 2
    body = read_method_body_from_bytes(bytes(raw))
    hdr = 12 if (raw[0] & 3) == 3 else 1
    out = []
    if hdr == 12:
        flags, maxstack, codesize, lsig = struct.unpack_from("<HHII", raw, 0)
        from rgmd_token import decode_raw
        lv = 0 if (not lsig or decode_raw(lsig) == 0) else decode_token(lsig)
        out.append(f"  // fat header maxstack={maxstack} codesize={codesize} locals={lv:#010x}")
        if lv:
            out.append("  // locals: " + locals_text(img, lv & 0xFFFFFF))
    offsets = {i.offset for i in body.instructions}
    for insn in body.instructions:
        name = insn.opcode.name
        rel = insn.offset - hdr
        text = ""
        op = insn.operand
        if name in TOKEN_OPS and op is not None and hasattr(op, "value"):
            try:
                tok = decode_token(op.value)
                text = res.token(tok)
            except ValueError as ex:
                text = f"<undecoded 0x{op.value:08x}: {ex}>"
        elif name in LONG_BRANCH:
            enc = struct.unpack("<I", bytes(insn.get_bytes())[-4:])[0]
            delta = decode_int32(enc)
            tgt = insn.offset + insn.size + delta
            mark = "" if tgt in offsets or tgt == hdr + len(b"") else " ?!not-on-boundary"
            text = f"IL_{tgt - hdr:04x}{mark}"
        elif name.endswith(".s") and name.startswith(("b", "leave")) and isinstance(op, int):
            text = f"IL_{op - hdr:04x}"
        elif name == "switch":
            rawb = bytes(insn.get_bytes())
            cnt = struct.unpack_from("<I", rawb, 1)[0]  # count is plain
            base = insn.offset + insn.size
            tg = [base + decode_int32(struct.unpack_from("<I", rawb, 5 + 4 * k)[0]) - hdr for k in range(cnt)]
            text = "(" + ", ".join(f"IL_{t:04x}" for t in tg) + ")"
        elif op is not None:
            text = str(op)
        out.append(f"  IL_{rel:04x}: {name}" + (f" {text}" if text else ""))
    return out


def locals_text(img: MetadataImage, row: int) -> str:
    try:
        blob = bytes(img.tables.StandAloneSig.rows[row - 1].Signature.value)
        if blob[0] != 0x07:
            return blob.hex()
        cnt, pos = _read_compressed_uint(blob, 1)
        items = []
        for i in range(cnt):
            t, pos = img.decode_type(blob, pos)
            items.append(f"V_{i}:{t}")
        return ", ".join(items)
    except Exception as ex:
        return f"<{ex}>"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--assembly", type=Path, default=DEFAULT)
    ap.add_argument("--type")
    ap.add_argument("--method", action="append")
    ap.add_argument("--token", action="append")
    ap.add_argument("--output", type=Path)
    a = ap.parse_args()
    img = MetadataImage.load(a.assembly)
    res = Resolver(img)
    rows = []
    for t in a.token or []:
        rows.append(int(t, 16) & 0xFFFFFF)
    if a.type:
        for ti, td in enumerate(img.tables.TypeDef.rows, 1):
            if img.type_name("TypeDef", ti) != a.type:
                continue
            for mr in td.MethodList:
                if not a.method or str(mr.row.Name) in a.method:
                    rows.append(mr.row_index)
    lines = []
    for r in rows:
        m = img.tables.MethodDef.rows[r - 1]
        lines.append(f"##### 0x{0x06000000 | r:08x} {res.method_def(r)}  rva=0x{m.Rva:x}")
        try:
            lines += disasm(img, res, r)
        except Exception as ex:
            lines.append(f"  <disasm error {ex}>")
        lines.append("")
    text = "\n".join(lines)
    if a.output:
        a.output.write_text(text, encoding="utf-8")
        print(f"wrote {a.output}")
    else:
        sys.stdout.reconfigure(encoding="utf-8")
        print(text)


if __name__ == "__main__":
    main()
