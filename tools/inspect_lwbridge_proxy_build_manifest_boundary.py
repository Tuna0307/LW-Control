#!/usr/bin/env python3
"""Verify the original proxy build-manifest boundary before package decrypt."""
from __future__ import annotations
import argparse,hashlib,json
from pathlib import Path
import pefile
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
from capstone.x86_const import X86_OP_IMM,X86_OP_MEM,X86_REG_RIP

EXPECTED="2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
PROXIES={
 "secure":(0x987F74,0x95800,"481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400"),
 "plain":(0xA1D774,0x96000,"c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794"),
}
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
 raise E("no RIP target")
def text_at(pe,d,rva,n=80):
 o=int(pe.get_offset_from_rva(rva));e=o
 while e<min(len(d),o+n) and 0x20<=d[e]<=0x7e:e+=1
 return d[o:e].decode("ascii","replace")
def lit(pe,d,at,want):
 got=text_at(pe,d,rip(pe,ins(pe,d,at)))
 req(got.startswith(want),f"{at:#x}: expected {want!r}, got {got!r}")

def inspect_proxy(name,d,want_hash):
 req(hashlib.sha256(d).hexdigest()==want_hash,f"{name}: hash mismatch")
 pe=pefile.PE(data=d,fast_load=False)
 calls={0x1CC14:0x40A70,0x1CC3A:0x3D260,0x1CC54:0x125C0,
        0x4111B:0x41740,0x41142:0x414E0}
 for at,want in calls.items():
  req(target(pe,ins(pe,d,at))==want,f"{name}: call {at:#x} changed")
 for at,want in [(0x40AF5,"build manifest missing"),(0x40D52,"build manifest invalid"),
                 (0x411A0,"build manifest mismatch"),(0x4153B,"LWBM1"),
                 (0x41579,"LWBM2"),(0x41897,"compositeSha256"),
                 (0x41905,"proxySha256"),(0x41976,"exportFingerprint")]:
  lit(pe,d,at,want)
 # Caller dataflow: validator outputs are rbp+a0, rbp+c0 and rbp+40.
 for at,want in [(0x1CC04,"lea r9, [rbp + 0xa0]"),
                 (0x1CBF8,"lea rcx, [rbp + 0xc0]"),
                 (0x1CBEF,"lea rcx, [rbp + 0x40]")]:
  i=ins(pe,d,at);got=f"{i.mnemonic} {i.op_str}"
  req(got==want,f"{name}: {at:#x} got {got}")
 # Package decrypt output is rbp-60; parser consumes that exact separate buffer.
 i=ins(pe,d,0x1CC26);req(f"{i.mnemonic} {i.op_str}"=="lea r9, [rbp - 0x60]",f"{name}: decrypt output changed")
 i=ins(pe,d,0x1CC50);req(f"{i.mnemonic} {i.op_str}"=="lea rcx, [rbp - 0x60]",f"{name}: parser input changed")
 return {"proxy":name,"sha256":hashlib.sha256(d).hexdigest(),
  "order":["0x1CC14 -> 0x40A70 build-manifest validation",
           "0x1CC3A -> 0x3D260 package validation/AES-GCM decrypt",
           "0x1CC54 -> 0x125C0 format-2 module-table parser"],
  "buildManifest":{"validatorRva":"0x40A70","formats":["LWBM1","LWBM2"],
   "errors":["build manifest missing","build manifest invalid","build manifest mismatch"],
   "bundleFields":["compositeSha256","proxySha256","exportFingerprint"]},
  "plaintextOwnership":"0x3D260 arg4/output rbp-0x60 is consumed as 0x125C0 arg1; it is distinct from 0x40A70 output locals"}
def inspect(path):
 outer=path.read_bytes();digest=hashlib.sha256(outer).hexdigest()
 req(digest==EXPECTED,f"unexpected SHA-256 {digest}")
 ope=pefile.PE(data=outer,fast_load=False);rows=[]
 for name,(rva,size,want) in PROXIES.items():
  off=int(ope.get_offset_from_rva(rva))
  rows.append(inspect_proxy(name,outer[off:off+size],want))
 return {"findingId":"LWB-R8-102","date":"2026-09-27","evidenceStatus":"RECOVERED CONTRACT",
  "sourceIdentity":{"path":str(path),"sha256":digest},
  "recoveredResult":{
   "manifestBoundary":"0x40A70 is a build-manifest/proxy-bundle validator, not the package plaintext producer",
   "decryptBoundary":"0x3D260 separately produces the decrypted buffer at rbp-0x60",
   "parserBoundary":"0x125C0 immediately consumes that same rbp-0x60 buffer as the format-2 module table",
   "impact":"do not pursue 0x40A70 as a hidden plaintext/cache boundary"},
  "proxies":rows,
  "limits":["does not recover package-key bytes or module plaintext","does not inspect the protected envelope-consumer body","Map remains NOT WORKING"]}

def main():
 ap=argparse.ArgumentParser();ap.add_argument("binary",type=Path);ap.add_argument("--json",action="store_true");ap.add_argument("--output",type=Path);a=ap.parse_args()
 try:r=inspect(a.binary)
 except (E,OSError,pefile.PEFormatError) as e:ap.error(str(e))
 s=json.dumps(r,indent=2)
 if a.output:a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(s+"\n",encoding="utf-8")
 print(s if a.json else f"finding={r['findingId']} status={r['evidenceStatus']}")
if __name__=="__main__":raise SystemExit(main())
