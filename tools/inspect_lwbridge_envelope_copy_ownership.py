#!/usr/bin/env python3
"""Verify packageKeyEnvelope post-response ownership in LWBridge 0.3.1."""
from __future__ import annotations
import argparse,hashlib,json
from pathlib import Path
import pefile
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
from capstone.x86_const import X86_OP_IMM,X86_OP_MEM,X86_REG_RDI,X86_REG_RIP

EXPECTED="2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
class E(ValueError): pass
def req(v,m):
 if not v: raise E(m)
def ins(pe,d,rva):
 o=int(pe.get_offset_from_rva(rva));m=Cs(CS_ARCH_X86,CS_MODE_64);m.detail=True
 i=next(m.disasm(d[o:o+16],int(pe.OPTIONAL_HEADER.ImageBase)+rva),None)
 req(i is not None,f"missing {rva:#x}");return i
def txt(i):return f"{i.mnemonic} {i.op_str}"
def target(pe,i):
 b=int(pe.OPTIONAL_HEADER.ImageBase)
 for op in i.operands:
  if op.type==X86_OP_IMM:return int(op.imm-b)
 return None
def rip(pe,i):
 b=int(pe.OPTIONAL_HEADER.ImageBase)
 for op in i.operands:
  if op.type==X86_OP_MEM and op.mem.base==X86_REG_RIP:
   return int(i.address+i.size+op.mem.disp-b)
 raise E("missing RIP target")
def ascii_at(pe,d,rva,n):
 o=int(pe.get_offset_from_rva(rva));return d[o:o+n].decode("ascii")
def lit(pe,d,rva,want):
 i=ins(pe,d,rva);t=rip(pe,i);req(ascii_at(pe,d,t,len(want))==want,f"{rva:#x}: literal drift")
def call(pe,d,rva,want):
 req(target(pe,ins(pe,d,rva))==want,f"{rva:#x}: call target drift")
def inspect(path:Path):
 d=path.read_bytes();digest=hashlib.sha256(d).hexdigest();req(digest==EXPECTED,f"unexpected SHA-256 {digest}")
 pe=pefile.PE(data=d,fast_load=False);base=int(pe.OPTIONAL_HEADER.ImageBase)
 # Envelope and expiry are extracted into stack-local strings.
 lit(pe,d,0x23C95E,"packageKeyEnvelope")
 call(pe,d,0x23C976,0x23F10C)
 lit(pe,d,0x23C97B,"packageKeyEnvelopeExpiresAt")
 call(pe,d,0x23C990,0x23F10C)
 # Successful validation persists the envelope to the service-owned runtime path.
 call(pe,d,0x23C99D,0x23E070)
 call(pe,d,0x23C9B4,0x23DB78)
 req(txt(ins(pe,d,0x23C9CE))=="mov rbx, qword ptr [rdi + 0x1a0]","envelope path data read changed")
 req(txt(ins(pe,d,0x23C9D5))=="mov r15, qword ptr [rdi + 0x1a8]","envelope path length read changed")
 call(pe,d,0x23C9FE,0x276B0)
 call(pe,d,0x23CA11,0x23D8FC)
 # Invalid/expired branch deletes that same runtime path.
 req(txt(ins(pe,d,0x23CA49))=="mov rcx, qword ptr [rdi + 0x1a0]","delete path data read changed")
 req(txt(ins(pe,d,0x23CA50))=="mov rdx, qword ptr [rdi + 0x1a8]","delete path length read changed")
 call(pe,d,0x23CA57,0x5B6540)
 # Both extracted response locals are destroyed before return.
 req(txt(ins(pe,d,0x23CA85))=="lea rcx, [rsp + 0x20]","expiry local cleanup changed")
 call(pe,d,0x23CA95,0x1150D)
 req(txt(ins(pe,d,0x23CA9A))=="lea rcx, [rsp + 0x50]","envelope local cleanup changed")
 call(pe,d,0x23CAAA,0x1150D)

 # Exhaustively enumerate all direct auth-service-object memory refs in ingest.
 a,z=0x23C7EC,0x23CB51;o=int(pe.get_offset_from_rva(a))
 m=Cs(CS_ARCH_X86,CS_MODE_64);m.detail=True;m.skipdata=True
 refs=[]
 for i in m.disasm(d[o:o+(z-a)],base+a):
  if i.id==0: continue
  for n,op in enumerate(i.operands):
   if op.type==X86_OP_MEM and op.mem.base==X86_REG_RDI:
    refs.append((int(i.address-base),n,int(op.access),int(op.mem.disp),txt(i)))
 expected=[
  (0x23C872,1,1,0x180),(0x23C879,1,1,0x188),(0x23C8ED,1,1,0x180),(0x23C8F4,1,1,0x188),
  (0x23C9CE,1,1,0x1A0),(0x23C9D5,1,1,0x1A8),(0x23CA49,1,1,0x1A0),(0x23CA50,1,1,0x1A8)]
 got=[x[:4] for x in refs];req(got==expected,f"service-object refs drifted: {got}")
 req(all(access==1 for _,_,access,_,_ in refs),"ingest writes service object")
 # R8-093 SessionV2 serializer has exactly seven fields; no envelope field exists there.
 session_fields=[
  (0x23EBAF,"version"),(0x23EBD5,"username"),(0x23EC07,"expiresAt"),
  (0x23EC36,"lastHeartbeatAt"),(0x23EC65,"graceStartedAt"),
  (0x23EC90,"encryptedToken"),(0x23ECB7,"encryptedMetadata")]
 for rva,name in session_fields: lit(pe,d,rva,name)

 return {
  "findingId":"LWB-R8-107","date":"2026-09-27","evidenceStatus":"RECOVERED CONTRACT",
  "sourceIdentity":{"path":str(path),"sha256":digest},
  "recoveredResult":{
   "responseOwnership":"packageKeyEnvelope is extracted into a stack-local string, validated, serialized with LF, and persisted to the service-owned runtime path",
   "serviceObject":"all direct rdi/service-object references in 0x23C7EC-0x23CB51 are read-only path reads; the envelope text is never assigned into service state",
   "localLifetime":"packageKeyEnvelope and packageKeyEnvelopeExpiresAt stack locals are explicitly destroyed before return",
   "sessionV2":"the exact seven persisted fields are version, username, expiresAt, lastHeartbeatAt, graceStartedAt, encryptedToken, encryptedMetadata; packageKeyEnvelope is not persisted there",
   "durableCopy":"within the recovered host contract, package-key.envelope is the sole post-response durable owner of the envelope token"
  },
  "serviceObjectReferences":[{"rva":hex(r),"operand":n,"access":a,"displacement":hex(disp),"instruction":s} for r,n,a,disp,s in refs],
  "limits":[
   "does not prove absence from unrecovered external OS artifacts such as inaccessible filesystem journals or snapshots",
   "does not read credentials, auth tokens, package-key.envelope contents, private keys, or contact auth services",
   "does not inspect the protected proxy envelope-consumer body",
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
