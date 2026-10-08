"""Hash-gated static native workbench for Home 009 (offline reference ONLY).

Adds a whole-image call index, string cross references and function-range
helpers on top of the 008 workbench. It reports exact control edges and byte
facts; it never executes the reference program.

usage:
  home009_native.py callers 0x41e543 [...]   direct call/jmp sites into targets
  home009_native.py fn 0x41d84b              disassemble the unwind-table function
  home009_native.py strref "TerminateProcess"  locate rip-relative string references
  home009_native.py calls 0x199627           outgoing call targets of a function
"""
from __future__ import annotations
import bisect
import hashlib
import sys
from pathlib import Path

import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86 import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

EXE = Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
SHA = "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
raw = EXE.read_bytes()
assert hashlib.sha256(raw).hexdigest() == SHA, "reference executable identity mismatch"
pe = pefile.PE(data=raw)
pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]])
BASE = pe.OPTIONAL_HEADER.ImageBase
FUNCS = sorted((x.struct.BeginAddress, x.struct.EndAddress) for x in pe.DIRECTORY_ENTRY_EXCEPTION)
BEGINS = [f[0] for f in FUNCS]
md = Cs(CS_ARCH_X86, CS_MODE_64)
md.detail = True

IMPORTS = {}
for ent in pe.DIRECTORY_ENTRY_IMPORT:
    for imp in ent.imports:
        IMPORTS[imp.address - BASE] = f"{ent.dll.decode()}!{(imp.name or b'').decode()}"


def func_of(rva: int):
    i = bisect.bisect_right(BEGINS, rva) - 1
    return FUNCS[i] if i >= 0 and rva < FUNCS[i][1] else None


def text_section():
    for s in pe.sections:
        if s.Name.rstrip(b"\x00") == b".text":
            return s
    raise SystemExit(".text not found")


_cache = {}


def all_insns():
    if "ins" not in _cache:
        s = text_section()
        data = s.get_data()
        _cache["ins"] = list(md.disasm(data, BASE + s.VirtualAddress))
    return _cache["ins"]


def _build_indexes():
    """one pass: direct call/jmp targets and rip-relative references."""
    import json, os
    cache = os.environ.get("LW_HOME009_CACHE")
    if cache and os.path.exists(cache):
        blob = json.load(open(cache))
        if blob.get("sha") == SHA and "refs" in blob:
            _cache["idx"] = {int(k): [tuple(x) for x in v] for k, v in blob["idx"].items()}
            _cache["refs"] = {int(k): v for k, v in blob["refs"].items()}
            return
    idx, refs = {}, {}
    for i in all_insns():
        off = i.address - BASE
        if i.mnemonic in ("call", "jmp") and i.operands and i.operands[0].type == X86_OP_IMM:
            idx.setdefault(i.operands[0].imm - BASE, []).append((off, i.mnemonic))
        for op in i.operands:
            if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                refs.setdefault(off + i.size + op.mem.disp, []).append(off)
    _cache["idx"], _cache["refs"] = idx, refs
    if cache:
        json.dump({"sha": SHA, "idx": {str(k): v for k, v in idx.items()},
                   "refs": {str(k): v for k, v in refs.items()}}, open(cache, "w"))


def call_index():
    if "idx" not in _cache:
        _build_indexes()
    return _cache["idx"]


def ref_index():
    if "refs" not in _cache:
        _build_indexes()
    return _cache["refs"]


def find_string(needle: bytes):
    """rva of every occurrence of needle in the mapped image."""
    out = []
    for s in pe.sections:
        d = s.get_data()
        pos = d.find(needle)
        while pos >= 0:
            out.append(s.VirtualAddress + pos)
            pos = d.find(needle, pos + 1)
    return out


WINDOW = 0


def string_refs(needle: bytes):
    """sites that reference an address inside [occurrence-?, occurrence]. Rust strings are
    not NUL terminated and are packed, so a reference may point to the start of the
    containing literal: search refs whose target is within 0x120 bytes before the match."""
    refs = ref_index()
    out = []
    for occ in find_string(needle):
        for t in range(max(0, occ - WINDOW), occ + 1):
            for site in refs.get(t, []):
                f = func_of(site)
                out.append((occ, t, site, f))
    return out


def callers(target: int):
    out = []
    for site, mn in call_index().get(target, []):
        f = func_of(site)
        out.append((site, mn, f))
    return out


def dis_range(start: int, end: int):
    return list(md.disasm(pe.get_data(start, end - start), BASE + start))


def annotate(i):
    off = i.address - BASE
    suf = ""
    if (i.mnemonic in ("call", "jmp") or i.mnemonic.startswith("j")) and i.operands and i.operands[0].type == X86_OP_IMM:
        t = i.operands[0].imm - BASE
        f = func_of(t)
        suf = f" -> {t:#x}" + (f" fn={f[0]:#x}" if f else "")
    for op in i.operands:
        if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
            t = off + i.size + op.mem.disp
            if t in IMPORTS:
                suf += f" IAT={IMPORTS[t]}"
            else:
                try:
                    b = pe.get_data(t, 150).split(b"\x00", 1)[0]
                    if 3 <= len(b) <= 130 and all(32 <= c <= 126 for c in b):
                        suf += " str=" + repr(b.decode())
                    else:
                        suf += f" data={t:#x}"
                except Exception:
                    pass
    return f"{off:08x} {i.bytes.hex():<24} {i.mnemonic:<7} {i.op_str:<44}{suf}"


def render(start: int, end: int):
    return "\n".join(annotate(i) for i in dis_range(start, end))


def main(argv):
    cmd = argv[0]
    if cmd == "callers":
        for a in argv[1:]:
            t = int(a, 0)
            print(f"== callers of {t:#x}")
            for site, mn, f in callers(t):
                print(f"  {site:#x} {mn} in fn {f[0]:#x}-{f[1]:#x}" if f else f"  {site:#x} {mn} (no fn)")
    elif cmd == "fn":
        t = int(argv[1], 0)
        f = func_of(t)
        print(render(f[0], f[1]))
    elif cmd == "range":
        print(render(int(argv[1], 0), int(argv[2], 0)))
    elif cmd == "calls":
        f = func_of(int(argv[1], 0))
        seen = {}
        for i in dis_range(*f):
            if i.mnemonic == "call" and i.operands and i.operands[0].type == X86_OP_IMM:
                seen.setdefault(i.operands[0].imm - BASE, []).append(i.address - BASE)
        for t, sites in sorted(seen.items()):
            print(f"{t:#x} <- {', '.join(hex(s) for s in sites)}")
    elif cmd == "strref":
        for occ, t, site, f in string_refs(argv[1].encode()):
            print(f"literal@{occ:#x} ref-target={t:#x} (-{occ - t:#x}) site={site:#x}" + (f" fn={f[0]:#x}-{f[1]:#x}" if f else ""))
    else:
        raise SystemExit(__doc__)


if __name__ == "__main__":
    main(sys.argv[1:])


def abs_pointer_slots(rva: int):
    """image locations (rva) that hold the absolute 8-byte pointer BASE+rva (e.g. Rust &str pieces)."""
    import struct
    v = struct.pack("<Q", BASE + rva)
    out = []
    for s in pe.sections:
        d = s.get_data()
        p = d.find(v)
        while p >= 0:
            out.append(s.VirtualAddress + p)
            p = d.find(v, p + 1)
    return out


def literal_users(needle: bytes, lead: int = 0):
    """for each literal: pointer slots (fmt piece tables) and the code sites that reference those slots."""
    refs = ref_index()
    rows = []
    for occ in find_string(needle):
        for start in range(occ - lead, occ + 1):
            for slot in abs_pointer_slots(start):
                sites = []
                for t in range(slot - 0x40, slot + 1):
                    for site in refs.get(t, []):
                        sites.append((t, site, func_of(site)))
                rows.append((occ, start, slot, sites))
    return rows
