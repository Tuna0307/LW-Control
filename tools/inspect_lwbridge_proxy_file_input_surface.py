#!/usr/bin/env python3
"""Verify LWBridge 0.3.1 native bootstrap/runtime file-input ownership."""
from __future__ import annotations
import argparse,hashlib,json,struct,re
from pathlib import Path
import pefile
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
from capstone.x86_const import X86_OP_IMM,X86_OP_MEM,X86_REG_RIP

EXPECTED="2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
PROXIES={
 "secure":(0x987F74,0x95800,"481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400"),
 "plain":(0xA1D774,0x96000,"c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794"),
}
PATHS={
 "bridgeScripts":(0x71270,"\\LastWar-xLua-Bridge\\bridge-scripts.dat"),
 "authorizationTicket":(0x712F0,"\\bridge-runtime\\authorization.ticket"),
 "authorizationChallenge":(0x71390,"\\bridge-runtime\\authorization.challenge"),
 "packageKeyEnvelope":(0x71770,"\\bridge-runtime\\package-key.envelope"),
 "buildManifest":(0x717C0,"\\bridge-runtime\\build.manifest"),
 "proxyBundleJson":(0x71840,"\\Game\\LastWar_Data\\Plugins\\x86_64\\xlua-proxy-bundle.json"),
 "proxyLog":(0x73C10,"\\logs\\xlua-proxy.log"),
 "proxyLogFallback":(0x73C40,"\\bridge-runtime\\logs\\xlua-proxy.log"),
}
class E(ValueError):pass
def req(v,m):
 if not v:raise E(m)
def direct_callers(pe,d,target):
 sec=next(s for s in pe.sections if s.Name.rstrip(b"\0")==b".text")
 off=int(sec.PointerToRawData);trva=int(sec.VirtualAddress);code=d[off:off+int(sec.SizeOfRawData)]
 out=[]
 for n in range(len(code)-5):
  if code[n]!=0xE8:continue
  disp=struct.unpack_from("<i",code,n+1)[0];src=trva+n
  if src+5+disp==target:out.append(src)
 return out
def ins(pe,d,rva):
 base=int(pe.OPTIONAL_HEADER.ImageBase);o=int(pe.get_offset_from_rva(rva))
 m=Cs(CS_ARCH_X86,CS_MODE_64);m.detail=True
 i=next(m.disasm(d[o:o+16],base+rva),None);req(i is not None,f"missing {rva:#x}");return i
def target(pe,i):
 b=int(pe.OPTIONAL_HEADER.ImageBase)
 for op in i.operands:
  if op.type==X86_OP_IMM:return int(op.imm-b)
 return None
def rip(pe,i):
 b=int(pe.OPTIONAL_HEADER.ImageBase)
 for op in i.operands:
  if op.type==X86_OP_MEM and op.mem.base==X86_REG_RIP:return int(i.address+i.size+op.mem.disp-b)
 raise E(f"missing RIP ref")
def wide_at(pe,d,rva):
 o=int(pe.get_offset_from_rva(rva));out=[]
 while o+1<len(d):
  v=d[o]|(d[o+1]<<8);o+=2
  if v==0:break
  out.append(chr(v))
 return "".join(out)
def proxy_report(name,d,want_hash):
 digest=hashlib.sha256(d).hexdigest();req(digest==want_hash,f"{name}: proxy hash mismatch")
 pe=pefile.PE(data=d,fast_load=False)
 global_delta=0 if name=="secure" else 0x1000

 for key,(rva,want) in PATHS.items():
  req(wide_at(pe,d,rva)==want,f"{name}: {key} path drift")

 # Path constructor caller ownership.
 req(direct_callers(pe,d,0x12080)==[0x169AB],f"{name}: challenge path callers drift")
 req(direct_callers(pe,d,0x12240)==[0x1BB49],f"{name}: ticket path callers drift")
 req(direct_callers(pe,d,0x12400)==[0x1CBE1],f"{name}: manifest path callers drift")
 req(direct_callers(pe,d,0x1B300)==[0x1C447,0x1C8B8],f"{name}: envelope path callers drift")
 req(direct_callers(pe,d,0x1B570)==[0x1CBB6],f"{name}: bundle path callers drift")
 req(direct_callers(pe,d,0x1E390)==[0x1C386],f"{name}: bridge scripts path callers drift")

 # Bounded text reader is not generic source loading: exact two callers only.
 req(direct_callers(pe,d,0x1BD80)==[0x1C45B,0x1D969],f"{name}: bounded text reader callers drift")
 req(target(pe,ins(pe,d,0x1C45B))==0x1BD80,f"{name}: envelope reader call drift")
 req(target(pe,ins(pe,d,0x1D969))==0x1BD80,f"{name}: multi-proof reader call drift")
 req(f"{ins(pe,d,0x1C44D).mnemonic} {ins(pe,d,0x1C44D).op_str}".strip()=="mov r8d, 0x1000",f"{name}: envelope max drift")
 req(f"{ins(pe,d,0x1D95B).mnemonic} {ins(pe,d,0x1D95B).op_str}".strip()=="mov r8d, 0x1000",f"{name}: multi-proof max drift")

 # Multi-proof path is explicitly sourced from LWBRIDGE_MULTI_PROOF_PATH and inline proof from LWBRIDGE_MULTI_PROOF.
 req(wide_at(pe,d,0x71948)=="LWBRIDGE_MULTI_PROOF_PATH",f"{name}: multi proof path env drift")
 req(wide_at(pe,d,0x71980)=="LWBRIDGE_MULTI_PROOF",f"{name}: multi proof env drift")
 req(rip(pe,ins(pe,d,0x1D737))==0x71948,f"{name}: multi proof path read drift")
 req(rip(pe,ins(pe,d,0x1D784))==0x71980,f"{name}: inline multi proof read drift")

 # bridge-scripts.dat has one dedicated binary reader call only.
 req(direct_callers(pe,d,0x1B910)==[0x1CAFD],f"{name}: bridge package reader callers drift")
 req(target(pe,ins(pe,d,0x1CAFD))==0x1B910,f"{name}: bridge package read call drift")

 # Stored package path global is assigned from constructor and consumed in package/auth path.
 req(rip(pe,ins(pe,d,0x1C38E))==0x8F150+global_delta,f"{name}: package path global assignment drift")
 req(rip(pe,ins(pe,d,0x1CAF2))==0x8F150+global_delta,f"{name}: package path global consume drift")

 # Build manifest + bundle flow enters post-protected R8-102 validator 0x40A70.
 req(target(pe,ins(pe,d,0x1CC14))==0x40A70,f"{name}: manifest validator call drift")
 req(target(pe,ins(pe,d,0x1CC3A))==0x3D260,f"{name}: package decrypt call drift")
 req(target(pe,ins(pe,d,0x1CC54))==0x125C0,f"{name}: module parser call drift")

 # Ticket reader has one caller; challenge path feeds atomic/runtime writer path.
 req(direct_callers(pe,d,0x1BB20)==[0x202A6],f"{name}: ticket reader callers drift")
 req(direct_callers(pe,d,0x4720)==[0x169BF],f"{name}: challenge persistence helper callers drift")

 return {
  "proxy":name,"sha256":digest,
  "staticPaths":[{"name":k,"rva":hex(r),"value":v} for k,(r,v) in PATHS.items()],
  "typedInputs":{
   "bridgeScripts":"dedicated package reader 0x1B910, sole caller 0x1CAFD",
   "packageKeyEnvelope":"bounded text reader 0x1BD80, max 0x1000",
   "multiProofPath":"same bounded reader 0x1BD80, max 0x1000, proof validator input",
   "authorizationTicket":"dedicated ticket reader 0x1BB20",
   "authorizationChallenge":"challenge generation/reuse persistence path",
   "buildManifestAndBundle":"R8-102 identity validator 0x40A70",
   "originalXlua":"DLL/library path from LWBRIDGE_XLUA_ORIGINAL_PATH",
   "profileRuntimeRoot":"runtime/log root, not script source"
  }
 }

def inspect(path):
 outer=path.read_bytes();digest=hashlib.sha256(outer).hexdigest()
 req(digest==EXPECTED,f"unexpected SHA-256 {digest}")
 ope=pefile.PE(data=outer,fast_load=False);rows=[]
 for name,(rva,size,want) in PROXIES.items():
  off=int(ope.get_offset_from_rva(rva));rows.append(proxy_report(name,outer[off:off+size],want))
 return {
  "findingId":"LWB-R8-114","date":"2026-09-27","evidenceStatus":"RECOVERED NEGATIVE FILE-SOURCE SURFACE",
  "sourceIdentity":{"path":str(path),"sha256":digest},
  "recoveredResult":{
   "bootstrapInputs":"all recovered product-owned native bootstrap/runtime file inputs relevant to source recovery are semantically typed",
   "boundedReader":"0x1BD80 has exactly two callers: package-key.envelope and LWBRIDGE_MULTI_PROOF_PATH",
   "bridgeScripts":"bridge-scripts.dat uses dedicated reader 0x1B910 with exactly one caller in package/auth flow",
   "alternateSource":"no recovered native file path or reader provides arbitrary Lua source/eval input/alternate chunk loading",
   "impact":"no file-based bootstrap shortcut supersedes the authentic package/source path; return to R8-104 legitimate-source-state bottleneck after this surface is closed"
  },
  "proxies":rows,
  "limits":[
   "this is not a claim about every generic CRT/internal filesystem operation",
   "does not inspect the protected envelope-consumer body 0x3F8E0-0x40A6D",
   "does not recover bridge Lua, source bytes, or XluaBridgeHandlePipeMessage routing",
   "does not run the game, send pipe traffic, use live process memory, or contact auth services",
   "Map remains NOT WORKING"
  ]
 }

def main():
 ap=argparse.ArgumentParser();ap.add_argument("binary",type=Path);ap.add_argument("--json",action="store_true");ap.add_argument("--output",type=Path);a=ap.parse_args()
 try:r=inspect(a.binary)
 except (E,OSError,pefile.PEFormatError) as exc:ap.error(str(exc))
 s=json.dumps(r,indent=2)
 if a.output:
  a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(s+"\n",encoding="utf-8")
 print(s if a.json else f"finding={r['findingId']} status={r['evidenceStatus']}")
if __name__=="__main__":raise SystemExit(main())
