#!/usr/bin/env python3
"""Verify original proxy bridge-source lifetime/xref boundaries."""
from __future__ import annotations
import argparse, hashlib, json, struct
from pathlib import Path
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86_const import X86_OP_MEM, X86_REG_RIP

EXPECTED="2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
PROXIES={
 "secure":(0x987F74,0x95800,"481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400",0x8F170),
 "plain":(0xA1D774,0x96000,"c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794",0x90170),
}
EXPECTED_REFS=[
 0x11F09,0x1547A,0x15A08,0x15A0F,0x15A17,0x15A32,0x16183,
 0x1C17F,0x1C214,0x1C2EC,0x1C5D4,0x1C6B6,0x1C7A4,0x1CA42,
 0x1CDF2,0x1CE02,0x1CE10,0x1CE20,0x1D333,
 0x6E774,0x6E781,0x6E7B1,0x6E7BC,0x6E7C7,
]
class E(ValueError): pass
def req(v,m):
 if not v: raise E(m)
def refs_for_proxy(d, source_rva):
 pe=pefile.PE(data=d,fast_load=False)
 base=int(pe.OPTIONAL_HEADER.ImageBase)
 text=next(s for s in pe.sections if s.Name.rstrip(b"\0")==b".text")
 off=int(text.PointerToRawData); va=int(text.VirtualAddress)
 md=Cs(CS_ARCH_X86,CS_MODE_64); md.detail=True; md.skipdata=True
 refs=[]
 for i in md.disasm(d[off:off+int(text.SizeOfRawData)],base+va):
  if i.id==0: continue
  for op in i.operands:
   if op.type==X86_OP_MEM and op.mem.base==X86_REG_RIP:
    target=int(i.address+i.size+op.mem.disp-base)
    if source_rva <= target < source_rva+0x20:
     refs.append(int(i.address-base))
     break
 return refs

def inspect_proxy(name,d,want_hash,source_rva):
 got_hash=hashlib.sha256(d).hexdigest()
 req(got_hash==want_hash,f"{name}: proxy hash mismatch")
 refs=refs_for_proxy(d,source_rva)
 req(refs==EXPECTED_REFS,f"{name}: source xrefs changed: {[hex(x) for x in refs]}")
 # Ensure no obvious data-table pointer constants hide another static source owner.
 rva32=struct.pack("<I",source_rva)
 va64=struct.pack("<Q",0x180000000+source_rva)
 req(d.find(rva32)<0,f"{name}: embedded source RVA constant found")
 req(d.find(va64)<0,f"{name}: embedded source VA constant found")
 return {
  "proxy":name,
  "sha256":got_hash,
  "sourceGlobalRva":hex(source_rva),
  "directRipRelativeReferenceCount":len(refs),
  "directRipRelativeReferenceRvas":[hex(x) for x in refs],
  "referenceClusters":{
   "preBootstrapPresenceCheck":["0x1547a"],
   "compilerRead":["0x15a08","0x15a0f","0x15a17","0x15a32"],
   "bootstrapCleanup":["0x16183"],
   "packageRefreshAndClear":["0x1c17f","0x1c214","0x1c2ec","0x1c5d4","0x1c6b6","0x1c7a4","0x1ca42","0x1cdf2","0x1ce02","0x1ce10","0x1ce20","0x1d333"],
   "finalDestructor":["0x6e774","0x6e781","0x6e7b1","0x6e7bc","0x6e7c7"],
   "statusErrorClear":["0x11f09"],
  },
  "embeddedPointerConstants":{"rva32":False,"absoluteVa64":False},
 }
def inspect(path):
 outer=path.read_bytes()
 digest=hashlib.sha256(outer).hexdigest()
 req(digest==EXPECTED,f"unexpected SHA-256 {digest}")
 ope=pefile.PE(data=outer,fast_load=False)
 rows=[]
 for name,(rva,size,want,sg) in PROXIES.items():
  off=int(ope.get_offset_from_rva(rva))
  rows.append(inspect_proxy(name,outer[off:off+size],want,sg))
 return {
  "findingId":"LWB-R8-101",
  "date":"2026-09-27",
  "evidenceStatus":"RECOVERED CONTRACT",
  "sourceIdentity":{"path":str(path),"sha256":digest},
  "recoveredResult":{
   "sourceLifetime":"the assembled bridge source has only the verified direct global-reference clusters in both proxies",
   "persistenceResult":"no direct source-global xref or embedded pointer constant exposes a second static file/cache persistence owner",
   "recoveryImpact":"authentic source still has to arise through package processing or a genuinely new external/runtime artifact",
  },
  "proxies":rows,
  "limits":["does not recover plaintext source bytes","does not inspect the protected envelope-consumer body","Map remains NOT WORKING"],
 }
def main():
 ap=argparse.ArgumentParser()
 ap.add_argument("binary",type=Path)
 ap.add_argument("--json",action="store_true")
 ap.add_argument("--output",type=Path)
 a=ap.parse_args()
 try:r=inspect(a.binary)
 except (E,OSError,pefile.PEFormatError) as e:ap.error(str(e))
 s=json.dumps(r,indent=2)
 if a.output:
  a.output.parent.mkdir(parents=True,exist_ok=True)
  a.output.write_text(s+"\n",encoding="utf-8")
 print(s if a.json else f"finding={r['findingId']} status={r['evidenceStatus']}")

if __name__=="__main__":
 raise SystemExit(main())
