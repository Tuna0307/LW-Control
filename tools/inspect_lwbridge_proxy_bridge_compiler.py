#!/usr/bin/env python3
"""Recover the original proxy Lua compiler and bridge-scripts text-load boundary."""

from __future__ import annotations
import argparse, hashlib, json
from pathlib import Path
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

EXPECTED="2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
PROXIES={
 "secure":(0x987F74,0x95800,"481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400"),
 "plain":(0xA1D774,0x96000,"c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794"),
}
class E(ValueError):pass

def req(ok,msg):
 if not ok: raise E(msg)

def ins(pe,d,rva):
 o=int(pe.get_offset_from_rva(rva)); md=Cs(CS_ARCH_X86,CS_MODE_64);md.detail=True
 row=next(md.disasm(d[o:o+16],int(pe.OPTIONAL_HEADER.ImageBase)+rva),None)
 req(row is not None,f"missing instruction 0x{rva:X}"); return row

def rip_rva(pe,i):
 base=int(pe.OPTIONAL_HEADER.ImageBase)
 for op in i.operands:
  if op.type==X86_OP_MEM and op.mem.base==X86_REG_RIP:
   return int(i.address+i.size+op.mem.disp-base)
 raise E(f"no RIP operand at 0x{i.address-base:X}")

def text_at(pe,d,rva,n=96):
 o=int(pe.get_offset_from_rva(rva)); e=o
 while e<min(len(d),o+n) and 0x20<=d[e]<=0x7e:e+=1
 return d[o:e].decode("ascii","replace")

def direct_target(pe,i):
 base=int(pe.OPTIONAL_HEADER.ImageBase)
 for op in i.operands:
  if op.type==X86_OP_IMM:return int(op.imm-base)
 return None

def proxy_report(name,d):
 pe=pefile.PE(data=d,fast_load=False)
 # Loader resolves the five compiler API functions.
 names={0x17152:"luaL_newstate",0x17F03:"lua_close",0x17F1E:"luaL_loadbufferx",0x17F39:"lua_dump",0x17886:"lua_tolstring"}
 for rva,want in names.items():
  i=ins(pe,d,rva); got=text_at(pe,d,rip_rva(pe,i))
  req(got.startswith(want),f"{name}: {rva:X} expected {want}, got {got!r}")
 stores=[0x17EFC,0x17F17,0x17F32,0x17F4D,0x17F64]
 slots=[rip_rva(pe,ins(pe,d,r)) for r in stores]
 # compile wrapper reads same slots in API order and calls compiler.
 loads=[0x16C5A,0x16C65,0x16C70,0x16C7B,0x16C86]
 req([rip_rva(pe,ins(pe,d,r)) for r in loads]==slots,f"{name}: compiler API slot mismatch")
 req(direct_target(pe,ins(pe,d,0x16CCF))==0x26380,f"{name}: compiler call mismatch")
 # Compiler API order: newstate, close, loadbufferx, dump, tolstring.
 checks={0x2641F:"call rax",0x2644D:"call qword ptr [r15 + 0x10]",0x2646C:"call qword ptr [r15 + 0x20]",
         0x266C4:"call qword ptr [r15 + 0x18]",0x266CD:"call qword ptr [r15 + 8]"}
 for rva,want in checks.items():
  i=ins(pe,d,rva); got=f"{i.mnemonic} {i.op_str}"
  req(got==want,f"{name}: {rva:X} got {got}")
 req(rip_rva(pe,ins(pe,d,0x266BD))==0x26360,f"{name}: lua_dump writer callback mismatch")
 mode_rva=rip_rva(pe,ins(pe,d,0x26435)); req(d[int(pe.get_offset_from_rva(mode_rva))]==ord("t"),f"{name}: compiler mode != t")
 for rva,s in [(0x26429,"bridge compiler state unavailable"),(0x26596,"bridge compile failed"),(0x266EF,"bridge bytecode dump failed"),(0x266F8,"bridge compiler unavailable")]:
  req(text_at(pe,d,rip_rva(pe,ins(pe,d,rva))).startswith(s),f"{name}: missing {s}")
 # bridge-scripts bootstrap loads a std::string source as text mode.
 src=rip_rva(pe,ins(pe,d,0x15A08)); cap=rip_rva(pe,ins(pe,d,0x15A0F)); ptr=rip_rva(pe,ins(pe,d,0x15A17)); length=rip_rva(pe,ins(pe,d,0x15A32))
 req((cap,ptr,length)==(src+0x18,src,src+0x10),f"{name}: source std::string layout mismatch")
 req(text_at(pe,d,rip_rva(pe,ins(pe,d,0x15A2B))).startswith("@bridge-scripts.dat"),f"{name}: chunk name mismatch")
 req(d[int(pe.get_offset_from_rva(rip_rva(pe,ins(pe,d,0x15A1F))))]==ord("t"),f"{name}: bootstrap mode != t")
 req(direct_target(pe,ins(pe,d,0x15A3E))==0x16C10,f"{name}: bootstrap compile wrapper mismatch")
 # Package/auth path owns the same source global and has the decrypt/load markers.
 req(rip_rva(pe,ins(pe,d,0x1CDF2))==src and rip_rva(pe,ins(pe,d,0x1CE02))==src,f"{name}: package source-global ownership mismatch")
 req(text_at(pe,d,rip_rva(pe,ins(pe,d,0x1CCAB))).startswith("package decrypt failed"),f"{name}: decrypt marker missing")
 req(text_at(pe,d,rip_rva(pe,ins(pe,d,0x1CFE6))).startswith("loaded script package format="),f"{name}: loaded-package marker missing")
 return {"proxy":name,"sha256":hashlib.sha256(d).hexdigest(),"compilerApiSlotsRva":[hex(x) for x in slots],
         "sourceGlobalRva":hex(src),"functions":{"compileWrapper":"0x16C10-0x16DE1","bridgeCompiler":"0x26380-0x26735",
         "bootstrap":"0x14D50-0x16287","packageAuthorization":"0x1C0A0-0x1D46F"},
         "bridgeScriptsChunk":"@bridge-scripts.dat","compileMode":"t","dumpWriterRva":"0x26360"}

def inspect(path):
 outer=path.read_bytes(); digest=hashlib.sha256(outer).hexdigest(); req(digest==EXPECTED,f"unexpected SHA-256 {digest}")
 ope=pefile.PE(data=outer,fast_load=False); rows=[]
 for name,(rva,size,want) in PROXIES.items():
  off=int(ope.get_offset_from_rva(rva)); d=outer[off:off+size]; req(hashlib.sha256(d).hexdigest()==want,f"{name} proxy hash mismatch")
  rows.append(proxy_report(name,d))
 return {"findingId":"LWB-R8-098","date":"2026-09-27","evidenceStatus":"RECOVERED CONTRACT",
         "sourceIdentity":{"path":str(path),"sha256":digest},
         "recoveredResult":{"bridgeCompiler":"temporary lua state -> luaL_loadbufferx(source, chunk, mode='t') -> on success lua_dump(writer, strip=0) -> close state",
         "compileErrors":["bridge compiler unavailable","bridge compiler state unavailable","bridge compile failed","bridge bytecode dump failed"],
         "bridgeScriptsLoad":"package/auth path owns the source std::string later passed to the compiler wrapper as @bridge-scripts.dat in text mode before original xLua load",
         "captureImplication":"the original bridge script source exists as a contiguous std::string in proxy memory after successful package processing and before/during bootstrap compilation"},
         "proxies":rows,
         "limits":["does not recover the plaintext bytes themselves","does not prove an authentic package can currently be admitted/decrypted","does not yet recover XluaBridgeMapScanTick traversal logic"],
         "reproduction":[r"python tools\inspect_lwbridge_proxy_bridge_compiler.py ..\LW\lwbridge-0.3.1.exe --json"]}

def main():
 ap=argparse.ArgumentParser();ap.add_argument("binary",type=Path);ap.add_argument("--json",action="store_true");ap.add_argument("--output",type=Path);a=ap.parse_args()
 try:r=inspect(a.binary)
 except (E,OSError,pefile.PEFormatError) as e:ap.error(str(e))
 s=json.dumps(r,indent=2)
 if a.output:a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(s+"\n",encoding="utf-8")
 print(s if a.json else f"finding={r['findingId']} status={r['evidenceStatus']}")
if __name__=="__main__":raise SystemExit(main())
