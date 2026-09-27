#!/usr/bin/env python3
"""Verify the authentic assembled-source capture window in original proxies."""
from __future__ import annotations
import argparse,hashlib,json
from pathlib import Path
import pefile
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
from capstone.x86_const import X86_OP_IMM,X86_OP_MEM,X86_REG_RIP

EXPECTED="2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
PROXIES={
 "secure":(0x987F74,0x95800,"481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400",0x8F170),
 "plain":(0xA1D774,0x96000,"c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794",0x90170),
}
BOOTSTRAP=(0x14D50,0x16287)
PACKAGE=0x1C0A0
COMPILER=0x16C10
ZEROIZER=0x3F350
class E(ValueError):pass
def req(v,m):
 if not v: raise E(m)
def ins(pe,d,rva):
 o=int(pe.get_offset_from_rva(rva));md=Cs(CS_ARCH_X86,CS_MODE_64);md.detail=True
 i=next(md.disasm(d[o:o+16],int(pe.OPTIONAL_HEADER.ImageBase)+rva),None)
 req(i is not None,f"missing instruction {rva:#x}");return i
def target(pe,i):
 base=int(pe.OPTIONAL_HEADER.ImageBase)
 for op in i.operands:
  if op.type==X86_OP_IMM:return int(op.imm-base)
 return None
def rip(pe,i):
 base=int(pe.OPTIONAL_HEADER.ImageBase)
 for op in i.operands:
  if op.type==X86_OP_MEM and op.mem.base==X86_REG_RIP:
   return int(i.address+i.size+op.mem.disp-base)
 raise E(f"no RIP target at {i.address-base:#x}")
def text(i):return f"{i.mnemonic} {i.op_str}"

def inspect_proxy(name,d,want_hash,source):
 req(hashlib.sha256(d).hexdigest()==want_hash,f"{name}: hash mismatch")
 pe=pefile.PE(data=d,fast_load=False)
 pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]])
 ranges={(int(e.struct.BeginAddress),int(e.struct.EndAddress)) for e in pe.DIRECTORY_ENTRY_EXCEPTION}
 req(BOOTSTRAP in ranges,f"{name}: bootstrap unwind range changed")
 req(target(pe,ins(pe,d,0x153E2))==PACKAGE,f"{name}: package call changed")
 req(rip(pe,ins(pe,d,0x1547A))==source+0x10,f"{name}: source length check changed")
 req(text(ins(pe,d,0x1547A)).startswith("cmp qword ptr [rip +"),f"{name}: source length opcode changed")
 # Package refresh clears then assigns parser output into this global on its replacement branches.
 for clear_at,assign_at,lea_at in [(0x1CDF9,0x1CE09,0x1CE02),(0x1CE17,0x1CE27,0x1CE20)]:
  req(target(pe,ins(pe,d,clear_at))==ZEROIZER,f"{name}: package source clear changed")
  req(rip(pe,ins(pe,d,lea_at))==source,f"{name}: package source target changed")
  req(target(pe,ins(pe,d,assign_at))==0x87C0,f"{name}: package source assignment changed")
 # Compiler reads exact source std::string pointer/capacity/length.
 req(rip(pe,ins(pe,d,0x15A08))==source,f"{name}: compile source address changed")
 req(rip(pe,ins(pe,d,0x15A0F))==source+0x18,f"{name}: compile capacity changed")
 req(rip(pe,ins(pe,d,0x15A17))==source,f"{name}: compile heap pointer changed")
 req(rip(pe,ins(pe,d,0x15A32))==source+0x10,f"{name}: compile length changed")
 req(target(pe,ins(pe,d,0x15A3E))==COMPILER,f"{name}: compiler call changed")
 # Final bootstrap cleanup zeroizes this same source global.
 req(rip(pe,ins(pe,d,0x16183))==source,f"{name}: final source cleanup pointer changed")
 req(target(pe,ins(pe,d,0x1618A))==ZEROIZER,f"{name}: final source zeroizer changed")
 # Exhaustive bootstrap RIP-relative refs to source object are exactly the expected six anchors.
 md=Cs(CS_ARCH_X86,CS_MODE_64);md.detail=True;md.skipdata=True
 o=int(pe.get_offset_from_rva(BOOTSTRAP[0]));refs=[]
 for i in md.disasm(d[o:o+(BOOTSTRAP[1]-BOOTSTRAP[0])],int(pe.OPTIONAL_HEADER.ImageBase)+BOOTSTRAP[0]):
  if i.id==0: continue
  for op in i.operands:
   if op.type==X86_OP_MEM and op.mem.base==X86_REG_RIP:
    t=int(i.address+i.size+op.mem.disp-int(pe.OPTIONAL_HEADER.ImageBase))
    if source<=t<source+0x20:
     refs.append(int(i.address-int(pe.OPTIONAL_HEADER.ImageBase)));break
 want=[0x1547A,0x15A08,0x15A0F,0x15A17,0x15A32,0x16183]
 req(refs==want,f"{name}: bootstrap source refs changed {[hex(x) for x in refs]}")
 return {"proxy":name,"sha256":hashlib.sha256(d).hexdigest(),"sourceGlobalRva":hex(source),
  "bootstrapRange":"0x14D50-0x16287",
  "packageCallRva":"0x153E2",
  "readyLengthCheckRva":"0x1547A",
  "compileWindow":{"sourceReadStartRva":"0x15A08","compilerCallRva":"0x15A3E","compilerRva":"0x16C10"},
  "finalZeroize":{"sourceAddressRva":"0x16183","zeroizerCallRva":"0x1618A","zeroizerRva":"0x3F350"},
  "bootstrapDirectSourceRefs":[hex(x) for x in refs]}
def inspect(path):
 outer=path.read_bytes();digest=hashlib.sha256(outer).hexdigest()
 req(digest==EXPECTED,f"unexpected SHA-256 {digest}")
 ope=pefile.PE(data=outer,fast_load=False);rows=[]
 for name,(rva,size,want,source) in PROXIES.items():
  off=int(ope.get_offset_from_rva(rva));rows.append(inspect_proxy(name,outer[off:off+size],want,source))
 return {"findingId":"LWB-R8-104","date":"2026-09-27","evidenceStatus":"RECOVERED CONTRACT",
  "sourceIdentity":{"path":str(path),"sha256":digest},
  "recoveredResult":{
   "window":"after package/auth call returns at 0x153E7 and bootstrap observes nonzero source length at 0x1547A, the authentic assembled source remains in the proxy-owned global through compiler read/call at 0x15A08-0x15A3E until final zeroization at 0x16183-0x1618A",
   "ownership":"package refresh assigns parser output into the same source global before bootstrap compiler consumption",
   "captureImpact":"a permitted authentic runtime capture can target the assembled source global during this bootstrap-owned pre-zeroization window; raw decrypted module-table capture is not required if this later source window is reachable"},
  "proxies":rows,
  "limits":["does not recover source bytes","does not prove current machine can enter the successful package/bootstrap state","does not inspect the protected envelope-consumer body","Map remains NOT WORKING"]}

def main():
 ap=argparse.ArgumentParser();ap.add_argument("binary",type=Path);ap.add_argument("--json",action="store_true");ap.add_argument("--output",type=Path);a=ap.parse_args()
 try:r=inspect(a.binary)
 except (E,OSError,pefile.PEFormatError) as e:ap.error(str(e))
 s=json.dumps(r,indent=2)
 if a.output:a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(s+"\n",encoding="utf-8")
 print(s if a.json else f"finding={r['findingId']} status={r['evidenceStatus']}")
if __name__=="__main__":raise SystemExit(main())
