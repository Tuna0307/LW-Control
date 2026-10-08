"""Hash-gated static disassembly workbench for Map decode (HOME-MAP-COMPLETION-010 d-map-decode).
Read-only on the reference executable. Requires pefile + capstone.
Usage: python d_disasm.py START END [--strings-only]   (RVAs)
"""
import hashlib, sys, bisect, re
from pathlib import Path
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86 import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

EXE = Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
EXPECTED = "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
_data = EXE.read_bytes()
assert hashlib.sha256(_data).hexdigest() == EXPECTED, "reference SHA-256 mismatch"
pe = pefile.PE(data=_data, fast_load=True)
pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]])
ranges = sorted((x.struct.BeginAddress, x.struct.EndAddress) for x in pe.DIRECTORY_ENTRY_EXCEPTION)
begins = [r[0] for r in ranges]
base = pe.OPTIONAL_HEADER.ImageBase
md = Cs(CS_ARCH_X86, CS_MODE_64); md.detail = True

def locate(rva):
    i = bisect.bisect_right(begins, rva) - 1
    return ranges[i] if i >= 0 and rva < ranges[i][1] else None

def getdata(rva, n):
    try:
        return pe.get_data(rva, n)
    except Exception:
        return b""

def printable_run(b, maxlen=140):
    out = bytearray()
    for c in b[:maxlen]:
        if 32 <= c < 127 or c in (9,):
            out.append(c)
        else:
            break
    return out.decode("ascii")

def dis(rva, end):
    return list(md.disasm(getdata(rva, end - rva), base + rva))

def string_at(rva, hint_len=None):
    b = getdata(rva, 160)
    if hint_len and 1 <= hint_len <= 150:
        s = b[:hint_len]
        if all(32 <= c < 127 for c in s):
            return s.decode("ascii")
    p = printable_run(b)
    return p if len(p) >= 3 else None

def render(rva, end):
    inst = dis(rva, end)
    lines = []
    for k, i in enumerate(inst):
        off = i.address - base
        suffix = ""
        if (i.mnemonic in ("call", "jmp") or i.mnemonic.startswith("j")) and i.operands and i.operands[0].type == X86_OP_IMM:
            t = i.operands[0].imm - base
            r = locate(t)
            suffix = " -> %#x" % t + (" fn=%#x-%#x" % r if r else "")
        for op in i.operands:
            if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                t = off + i.size + op.mem.disp
                if i.mnemonic == "lea":
                    # look ahead for length immediates
                    hint = None
                    for j in inst[k+1:k+5]:
                        if j.mnemonic == "mov" and len(j.operands) == 2 and j.operands[1].type == X86_OP_IMM and j.op_str.split(",")[0] in ("edx","r8d","r9d","ecx","esi","edi"):
                            hint = j.operands[1].imm; break
                    s = string_at(t, hint)
                    suffix += "  str=%r (@%#x)" % (s, t) if s else "  data=%#x" % t
                else:
                    suffix += "  mem=%#x" % t
        lines.append("%08x %-24s %-7s %-40s%s" % (off, i.bytes.hex(), i.mnemonic, i.op_str, suffix))
    return "\n".join(lines)

def find_bytes(pattern: bytes):
    out = []
    start = 0
    while True:
        k = _data.find(pattern, start)
        if k < 0: break
        out.append(k); start = k + 1
    return out

def file_off_to_rva(off):
    for s in pe.sections:
        if s.PointerToRawData <= off < s.PointerToRawData + s.SizeOfRawData:
            return s.VirtualAddress + off - s.PointerToRawData
    return None

def xrefs_to(target_rva, lo=0, hi=None):
    """all rip-relative lea refs to target in .text (scan)."""
    res = []
    for s in pe.sections:
        if not s.Name.startswith(b".text"): continue
        buf = s.get_data(); va = s.VirtualAddress
        for k in range(len(buf) - 7):
            # lea r64,[rip+disp32]: REX(48/4c) 8d modrm(05|0d|15|1d|25|2d|35|3d)
            if buf[k] in (0x48, 0x4c) and buf[k+1] == 0x8d and (buf[k+2] & 0xC7) == 0x05:
                disp = int.from_bytes(buf[k+3:k+7], "little", signed=True)
                if va + k + 7 + disp == target_rva:
                    res.append(va + k)
    return res

if __name__ == "__main__":
    a = sys.argv[1:]
    print(render(int(a[0], 0), int(a[1], 0)))

def call_xrefs(target_rva):
    """direct call/jmp rel32 xrefs to target in .text."""
    res = []
    for s in pe.sections:
        if not s.Name.startswith(b".text"): continue
        buf = s.get_data(); va = s.VirtualAddress
        k = buf.find(b"\xe8")
        while k != -1:
            if k + 5 <= len(buf):
                disp = int.from_bytes(buf[k+1:k+5], "little", signed=True)
                if va + k + 5 + disp == target_rva: res.append(va + k)
            k = buf.find(b"\xe8", k + 1)
    return res

def fn_of(rva):
    r = locate(rva)
    return r
