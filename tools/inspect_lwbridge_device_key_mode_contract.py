#!/usr/bin/env python3
"""Verify LWBridge 0.3.1 device-key operation selection and propagation."""
from __future__ import annotations
import argparse,hashlib,json
from pathlib import Path
import pefile
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
from capstone.x86_const import X86_OP_IMM
EXPECTED="2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
class E(ValueError):pass
def req(v,m):
 if not v: raise E(m)
def ins(pe,d,rva):
 o=int(pe.get_offset_from_rva(rva));m=Cs(CS_ARCH_X86,CS_MODE_64);m.detail=True
 i=next(m.disasm(d[o:o+16],int(pe.OPTIONAL_HEADER.ImageBase)+rva),None)
 req(i is not None,f"missing {rva:#x}");return i
def txt(i):return f"{i.mnemonic} {i.op_str}"
def tgt(pe,i):
 b=int(pe.OPTIONAL_HEADER.ImageBase)
 for op in i.operands:
  if op.type==X86_OP_IMM:return int(op.imm-b)
 return None
def eq(pe,d,rva,want):
 got=txt(ins(pe,d,rva));req(got==want,f"{rva:#x}: {got!r} != {want!r}")
def call(pe,d,rva,want):
 req(tgt(pe,ins(pe,d,rva))==want,f"{rva:#x}: target changed")
def inspect(path:Path):
 d=path.read_bytes();digest=hashlib.sha256(d).hexdigest();req(digest==EXPECTED,f"unexpected SHA-256 {digest}")
 pe=pefile.PE(data=d,fast_load=False)
 # Login: source wrapper -> nested wrapper state 0 / operation 0.
 eq(pe,d,0xF1D51,"lea r15, [rdi + 0x58]")
 eq(pe,d,0xF2A91,"mov qword ptr [rdi + 0xe0], rax")
 eq(pe,d,0xF2A9F,"mov word ptr [rdi + 0xe8], 0x100")
 eq(pe,d,0xF2AA8,"xor ecx, ecx"); call(pe,d,0xF2AAA,0xF1E0D)
 eq(pe,d,0xF1E0D,"mov qword ptr [rdi + 0xd0], rax")
 eq(pe,d,0xF1E14,"mov byte ptr [rdi + 0xd8], 0")
 eq(pe,d,0xF1E1B,"mov byte ptr [rdi + 0xd9], cl")
 call(pe,d,0xF1E32,0xF3E63)
 # Supervisor: explicit operation 1.
 eq(pe,d,0xF7C3B,"mov word ptr [rsi + 0x140], 0")
 eq(pe,d,0xF7C4B,"mov cl, 1"); call(pe,d,0xF7C57,0xF75E0)
 eq(pe,d,0xF75EC,"mov byte ptr [rsi + 0x130], 0")
 eq(pe,d,0xF75F3,"mov byte ptr [rsi + 0x131], cl")
 call(pe,d,0xF760A,0xF3E63)
 # Cleanup calibration: state 0 / operation 2.
 eq(pe,d,0xF47AA,"lea rsi, [rbx + 0x10]")
 eq(pe,d,0xF48F0,"mov word ptr [rbx + 0x90], 0x200")
 call(pe,d,0xF47BC,0xF3E63)
 # Wrapper reads state and operation, then enters spawn chain.
 eq(pe,d,0xF3E7B,"movzx eax, byte ptr [rdx + 0x80]")
 eq(pe,d,0xF3E97,"mov cl, byte ptr [r14 + 0x81]")
 call(pe,d,0xF3EC3,0x467D95)
 eq(pe,d,0x467D9F,"mov ebx, ecx")
 eq(pe,d,0x467DCC,"mov r8d, ebx")
 call(pe,d,0x467DD2,0x467FBB)
 call(pe,d,0x467FDA,0x468519)
 eq(pe,d,0x46857C,"mov ecx, r8d")
 call(pe,d,0x468582,0x1BDEE9)
 # Blocking-task allocation stores operation at task+0x40.
 eq(pe,d,0x1BDEFB,"mov ebx, ecx")
 eq(pe,d,0x1BDF2E,"lea rcx, [r8 + 0x80]")
 eq(pe,d,0x1BDF7E,"mov byte ptr [r8 + 0xc0], bl")
 eq(pe,d,0x1BDFA5,"mov rax, rcx")
 # Poll chain passes task+0x40 unchanged into the worker payload.
 eq(pe,d,0x96880,"lea rdi, [rsi + 0x20]"); call(pe,d,0x96887,0x8927F)
 call(pe,d,0x892A8,0x1C8C49)
 eq(pe,d,0x1C8C65,"lea r14, [rdx + 0x20]"); call(pe,d,0x1C8C83,0x318D61)
 eq(pe,d,0x318D67,"movzx edi, byte ptr [rdx]")
 eq(pe,d,0x318D6A,"mov byte ptr [rdx], 3")
 eq(pe,d,0x318D7E,"test edi, edi"); call(pe,d,0x318D80,0x318D96)
 eq(pe,d,0x318D82,"cmp edi, 1"); call(pe,d,0x318D85,0x318DD4)
 call(pe,d,0x318D8F,0x39ECBD)
 call(pe,d,0x318D9E,0x39ED6B)
 call(pe,d,0x318DD4,0x39F03D)
 # Helper identities: open-only calls open+export; open-or-create branches on exact missing subtype.
 call(pe,d,0x39ED19,0x39F0CF)
 call(pe,d,0x39ED44,0x39EE94)
 call(pe,d,0x39EDCA,0x39F0CF)
 eq(pe,d,0x39EDCF,"cmp byte ptr [rbx], 1")
 eq(pe,d,0x39EDD4,"cmp byte ptr [rsp + 0x39], 0")
 call(pe,d,0x39EE3E,0x39EE94)
 return {
  "findingId":"LWB-R8-106","date":"2026-09-27","evidenceStatus":"RECOVERED CONTRACT",
  "sourceIdentity":{"path":str(path),"sha256":digest},
  "recoveredResult":{
   "loginMode":{"value":0,"meaning":"open-or-create persisted ECDH key, then export public key"},
   "supervisorMode":{"value":1,"meaning":"open existing persisted ECDH key, then export public key"},
   "cleanupMode":{"value":2,"meaning":"delete persisted key or accept already-missing state"},
   "propagation":"auth wrapper operation byte is stored at Tokio task+0x40 and later consumed unchanged by 0x318D61",
   "reachabilityImpact":"a missing persisted key is locally self-provisionable by the normal original login path; package-key.envelope remains the current missing package-decrypt material"
  },
  "limits":["does not create or export a live private key","does not contact auth services","does not recover package-key.envelope or package key","Map remains NOT WORKING"]
 }

def main():
 ap=argparse.ArgumentParser();ap.add_argument("binary",type=Path);ap.add_argument("--json",action="store_true");ap.add_argument("--output",type=Path);a=ap.parse_args()
 try:r=inspect(a.binary)
 except (E,OSError,pefile.PEFormatError) as exc:ap.error(str(exc))
 s=json.dumps(r,indent=2)
 if a.output:
  a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(s+"\n",encoding="utf-8")
 print(s if a.json else f"finding={r['findingId']} login={r['recoveredResult']['loginMode']['value']} supervisor={r['recoveredResult']['supervisorMode']['value']} cleanup={r['recoveredResult']['cleanupMode']['value']}")
if __name__=="__main__":raise SystemExit(main())
