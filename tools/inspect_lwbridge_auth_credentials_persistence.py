#!/usr/bin/env python3
"""Hash-locked verifier for LWBridge 0.3.1 auth credential persistence/restore."""
from __future__ import annotations
import argparse,hashlib,json
from pathlib import Path
import pefile
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
from capstone.x86_const import X86_OP_IMM

REF_SHA="2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
class E(ValueError): pass
def req(v,m):
 if not v: raise E(m)
def sha(p:Path): return hashlib.sha256(p.read_bytes()).hexdigest()

def inspect(path:Path):
 req(sha(path)==REF_SHA,"reference hash drifted")
 data=path.read_bytes(); pe=pefile.PE(data=data,fast_load=False); base=pe.OPTIONAL_HEADER.ImageBase
 md=Cs(CS_ARCH_X86,CS_MODE_64); md.detail=True
 def raw(rva,n): return data[pe.get_offset_from_rva(rva):pe.get_offset_from_rva(rva)+n]
 def ins(rva,n=15):
  return next(md.disasm(raw(rva,n),base+rva))
 def expect(rva,mnemonic,*parts):
  i=ins(rva); req(i.mnemonic==mnemonic,f"{rva:#x}: expected {mnemonic}, got {i.mnemonic} {i.op_str}")
  for p in parts:req(p in i.op_str,f"{rva:#x}: missing {p!r} in {i.op_str!r}")
  return i
 def call_target(rva):
  i=expect(rva,"call"); req(i.operands and i.operands[0].type==X86_OP_IMM,f"{rva:#x}: not direct call")
  return int(i.operands[0].imm-base)
 def cstr(rva,n=80): return raw(rva,n).split(b"\0",1)[0].decode("ascii")
 # constructor path literals are adjacent length-delimited bytes
 req(raw(0xC801A9,17)==b"auth-session.json","legacy session path drifted")
 req(raw(0xC801BA,21)==b"auth-credentials.json","legacy credential path drifted")
 req(raw(0xC801CF,20)==b"auth-session.v2.json","v2 session path drifted")
 req(raw(0xC801E3,24)==b"auth-credentials.v2.json","v2 credential path drifted")
 # v2-first credential restore
 expect(0x23A265,"mov","[rdx + 0x120]"); expect(0x23A26C,"mov","[rdi + 0x128]")
 req(call_target(0x23A27B)==0x5B2060,"v2 credential read helper drifted")
 expect(0x23A30A,"cmp","al, 2")
 req(call_target(0x23A327)==0x39F5CE,"v2 secure decode helper drifted")
 # legacy fallback + exact JSON keys
 expect(0x23A3E0,"mov","[rdi + 0xe0]"); expect(0x23A3E7,"mov","[rdi + 0xe8]")
 req(cstr(0x837526).startswith("version"),"version key drifted")
 req(cstr(0xC7FED2).startswith("username"),"username key drifted")
 req(cstr(0xC7FF9D).startswith("encryptedPassword"),"encryptedPassword key drifted")
 expect(0x23A47A,"cmp","byte ptr [rax], 2"); expect(0x23A483,"cmp","qword ptr [rax + 8], 0"); expect(0x23A48E,"cmp","qword ptr [rax + 0x10], 1")
 req(call_target(0x23A505)==0x39F7D2,"legacy secure decode helper drifted")
 req(call_target(0x23A5CB)==0x239D61,"legacy migration save call drifted")
 # save/protect/v2 serialize/persist
 req(call_target(0x239E96)==0x39F4C5,"password protect helper drifted")
 expect(0x239F0E,"mov","byte ptr [rsp + 0xd0], 2")
 expect(0x239F30,"mov","[rbx + 0x120]"); expect(0x239F37,"mov","[rbx + 0x128]")
 req(call_target(0x239F83)==0x23E7EE,"credential v2 serializer call drifted")
 req(call_target(0x23A0C1)==0x23D8FC,"credential persistence helper drifted")
 # serializer fields
 req(cstr(0x837526).startswith("version"),"serializer version field drifted")
 req(cstr(0xC7FED2).startswith("username"),"serializer username field drifted")
 req(cstr(0xC7FF9D).startswith("encryptedPassword"),"serializer encryptedPassword field drifted")
 return {
  "findingId":"LWB-R8-117","date":"2026-09-27","status":"RECOVERED CONTRACT",
  "referenceSha256":REF_SHA,
  "constructorPaths":["auth-session.json","auth-credentials.json","auth-session.v2.json","auth-credentials.v2.json"],
  "restore":{"functionRange":"0x23A24C-0x23A60A","v2PathOffsets":["+0x120","+0x128"],"v2Version":2,"v2DecodeHelper":"0x39F5CE","legacyPathOffsets":["+0xE0","+0xE8"],"legacyFields":["version","username","encryptedPassword"],"legacyVersionRepresentation":"JSON number variant with integer-one checks at 0x23A47A/0x23A483/0x23A48E","legacyDecodeHelper":"0x39F7D2","migrationSave":"0x239D61"},
  "save":{"functionRange":"0x239D61-0x23A111","protectHelper":"0x39F4C5","version":2,"serializer":"0x23E7EE-0x23E8DA","fields":["version","username","encryptedPassword"],"v2PathOffsets":["+0x120","+0x128"],"persistenceHelper":"0x23D8FC"},
  "limits":["does not read credential values or browser/password-manager stores","does not make auth requests","does not bypass authorization"]
 }

def main():
 ap=argparse.ArgumentParser();ap.add_argument("reference",type=Path);ap.add_argument("--output",type=Path);a=ap.parse_args()
 try:r=inspect(a.reference)
 except (E,OSError,StopIteration) as exc:ap.error(str(exc))
 s=json.dumps(r,indent=2)+"\n"
 if a.output:a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(s,encoding="utf-8")
 print(s,end="")
if __name__=="__main__":raise SystemExit(main())
