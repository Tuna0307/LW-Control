#!/usr/bin/env python3
"""Verify original proxy zeroizing cleanup for package key and plaintext."""
from __future__ import annotations
import argparse,hashlib,json
from pathlib import Path
import pefile
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
from capstone.x86_const import X86_OP_IMM

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
def text(i):return f"{i.mnemonic} {i.op_str}"
def target(pe,i):
 base=int(pe.OPTIONAL_HEADER.ImageBase)
 for op in i.operands:
  if op.type==X86_OP_IMM:return int(op.imm-base)
 return None
def inspect_proxy(name,d,want_hash):
 req(hashlib.sha256(d).hexdigest()==want_hash,f"{name}: hash mismatch")
 pe=pefile.PE(data=d,fast_load=False)
 # Common loader cleanup calls key-vector zeroizer, then plaintext/string zeroizer.
 req(text(ins(pe,d,0x1D0EB))=="lea rcx, [rsp + 0x48]",f"{name}: key cleanup pointer changed")
 req(target(pe,ins(pe,d,0x1D0F0))==0x3F4A0,f"{name}: key cleanup target changed")
 req(text(ins(pe,d,0x1D0F5))=="lea rcx, [rbp - 0x60]",f"{name}: plaintext cleanup pointer changed")
 req(target(pe,ins(pe,d,0x1D0F9))==0x3F350,f"{name}: plaintext cleanup target changed")
 # Key vector zeroizer: begin=[rcx], end=[rcx+8], write zero byte until begin==end.
 checks={
 0x3F4AA:"mov rax, qword ptr [rcx]",
 0x3F4B0:"mov rdx, qword ptr [rcx + 8]",
 0x3F4C0:"mov byte ptr [rax], 0",
 0x3F4C3:"lea rax, [rax + 1]",
 0x3F4C7:"sub rdx, 1",
 0x3F4CB:"jne 0x18003f4c0",
 0x3F4CD:"mov rax, qword ptr [rcx]",
 0x3F4D0:"mov rdi, qword ptr [rcx + 8]",
 0x3F4D9:"mov qword ptr [rcx + 8], rax",
 }
 for r,w in checks.items(): req(text(ins(pe,d,r))==w,f"{name}: {r:#x} got {text(ins(pe,d,r))}")
 # Plaintext/string zeroizer: length=[rcx+0x10], select inline/heap data, write zero byte length times.
 checks2={
 0x3F359:"mov rcx, qword ptr [rcx + 0x10]",
 0x3F362:"cmp qword ptr [rbx + 0x18], 0xf",
 0x3F36C:"mov rax, qword ptr [rbx]",
 0x3F370:"mov byte ptr [rax], 0",
 0x3F373:"lea rax, [rax + 1]",
 0x3F377:"sub rcx, 1",
 0x3F37B:"jne 0x18003f370",
 0x3F37D:"mov qword ptr [rbx + 0x10], 0",
 0x3F392:"mov byte ptr [rax], 0",
 }
 for r,w in checks2.items(): req(text(ins(pe,d,r))==w,f"{name}: {r:#x} got {text(ins(pe,d,r))}")
 # Context anchors: decrypt output and parser input are the same rbp-0x60 buffer.
 req(text(ins(pe,d,0x1CC26))=="lea r9, [rbp - 0x60]",f"{name}: decrypt output changed")
 req(target(pe,ins(pe,d,0x1CC3A))==0x3D260,f"{name}: decrypt call changed")
 req(text(ins(pe,d,0x1CC50))=="lea rcx, [rbp - 0x60]",f"{name}: parser input changed")
 req(target(pe,ins(pe,d,0x1CC54))==0x125C0,f"{name}: parser call changed")
 return {"proxy":name,"sha256":hashlib.sha256(d).hexdigest(),
  "keyCleanup":{"callRva":"0x1D0F0","helperRva":"0x3F4A0","object":"rsp+0x48 vector",
    "behavior":"zero every byte in [begin,end), then set end=begin before later storage release"},
  "plaintextCleanup":{"callRva":"0x1D0F9","helperRva":"0x3F350","object":"rbp-0x60 string/vector output",
    "behavior":"zero every byte across logical length, set length=0 and terminate before later storage release"},
 }
def inspect(path):
 outer=path.read_bytes();digest=hashlib.sha256(outer).hexdigest()
 req(digest==EXPECTED,f"unexpected SHA-256 {digest}")
 ope=pefile.PE(data=outer,fast_load=False);rows=[]
 for name,(rva,size,want) in PROXIES.items():
  off=int(ope.get_offset_from_rva(rva))
  rows.append(inspect_proxy(name,outer[off:off+size],want))
 return {"findingId":"LWB-R8-103","date":"2026-09-27","evidenceStatus":"RECOVERED CONTRACT",
  "sourceIdentity":{"path":str(path),"sha256":digest},
  "recoveredResult":{
   "packageKeyLifetime":"the caller-owned 32-byte package-key vector is explicitly zeroized by 0x3F4A0 on common cleanup",
   "plaintextLifetime":"the decrypted module-table buffer is explicitly zeroized by 0x3F350 on common cleanup",
   "captureImpact":"authentic key/plaintext capture must occur before loader cleanup; post-return memory residue is not an intended recovery boundary"},
  "proxies":rows,
  "limits":["does not expose package-key value or plaintext bytes","does not inspect the protected envelope-consumer body","Map remains NOT WORKING"]}

def main():
 ap=argparse.ArgumentParser();ap.add_argument("binary",type=Path);ap.add_argument("--json",action="store_true");ap.add_argument("--output",type=Path);a=ap.parse_args()
 try:r=inspect(a.binary)
 except (E,OSError,pefile.PEFormatError) as e:ap.error(str(e))
 s=json.dumps(r,indent=2)
 if a.output:a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(s+"\n",encoding="utf-8")
 print(s if a.json else f"finding={r['findingId']} status={r['evidenceStatus']}")
if __name__=="__main__":raise SystemExit(main())
