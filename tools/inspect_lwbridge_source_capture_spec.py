#!/usr/bin/env python3
"""Emit and verify the offline R8-108 assembled-source capture specification."""
from __future__ import annotations
import argparse, hashlib, json
from pathlib import Path
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

EXPECTED="2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
PROXIES={
 "secure":(0x987F74,0x95800,"481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400",0x8F170),
 "plain":(0xA1D774,0x96000,"c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794",0x90170),
}
PREFIX="local __bridge_preload = package.preload\n"
class E(ValueError):pass
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
 raise E(f"no RIP target at {i.address-b:#x}")
def inspect_proxy(name,d,want_hash,source):
 digest=hashlib.sha256(d).hexdigest();req(digest==want_hash,f"{name}: proxy hash mismatch")
 pe=pefile.PE(data=d,fast_load=False)

 # Exact ready/compiler/zeroize anchors from R8-104.
 req(rip(pe,ins(pe,d,0x1547A))==source+0x10,f"{name}: ready length field drift")
 req(txt(ins(pe,d,0x1547A)).startswith("cmp qword ptr [rip +"),f"{name}: ready opcode drift")
 req(rip(pe,ins(pe,d,0x15A08))==source,f"{name}: source object address drift")
 req(rip(pe,ins(pe,d,0x15A0F))==source+0x18,f"{name}: capacity field drift")
 req(txt(ins(pe,d,0x15A0F)).endswith(", 0xf"),f"{name}: SSO threshold drift")
 req(rip(pe,ins(pe,d,0x15A17))==source,f"{name}: heap pointer field drift")
 req(txt(ins(pe,d,0x15A17)).startswith("cmova rdx, qword ptr"),f"{name}: heap select drift")
 req(rip(pe,ins(pe,d,0x15A32))==source+0x10,f"{name}: length field drift")
 req(target(pe,ins(pe,d,0x15A3E))==0x16C10,f"{name}: compiler target drift")
 req(rip(pe,ins(pe,d,0x16183))==source,f"{name}: cleanup source drift")
 req(target(pe,ins(pe,d,0x1618A))==0x3F350,f"{name}: zeroizer target drift")

 return {
  "proxy":name,
  "sha256":digest,
  "sourceGlobalRva":hex(source),
  "msvcStringLayout":{
   "objectBytes":32,
   "dataOrHeapPointerOffset":"0x0",
   "lengthOffset":"0x10",
   "capacityOffset":"0x18",
   "smallStringCapacityThreshold":15,
   "selectionContract":"if capacity <= 15, bytes are inline at object+0x0; otherwise object+0x0 is the heap pointer"
  },
  "window":{
   "readyLengthCheckRva":"0x1547A",
   "compilerReadStartRva":"0x15A08",
   "compilerCallRva":"0x15A3E",
   "finalSourceAddressRva":"0x16183",
   "finalZeroizerCallRva":"0x1618A"
  }
 }

def inspect(path):
 outer=path.read_bytes();digest=hashlib.sha256(outer).hexdigest()
 req(digest==EXPECTED,f"unexpected SHA-256 {digest}")
 ope=pefile.PE(data=outer,fast_load=False);rows=[]
 for name,(rva,size,want,source) in PROXIES.items():
  off=int(ope.get_offset_from_rva(rva))
  rows.append(inspect_proxy(name,outer[off:off+size],want,source))
 return {
  "findingId":"LWB-R8-108",
  "date":"2026-09-27",
  "evidenceStatus":"CAPTURE SPECIFICATION",
  "sourceIdentity":{"path":str(path),"sha256":digest},
  "captureSpecification":{
   "targetIdentity":"accept only a loaded module whose on-disk SHA-256 exactly matches one of the recovered secure/plain proxy hashes",
   "processAccess":"read-only observation only; no target-memory writes, patches, injected code, or authentication/network actions",
   "sourceObject":"moduleBase + sourceGlobalRva",
   "stableReadRule":"read the 32-byte string object, read exactly its declared logical bytes, then re-read the 32-byte object and discard the observation if the object changed",
   "readyRule":"do not accept zero-length source; capture during the R8-104 nonzero pre-zeroization window",
   "acceptancePrefix":PREFIX,
   "acceptancePrefixSource":"R8-099 exact assembled-source contract",
   "postCaptureCheck":"search the captured assembled source for XluaBridgeMapScanTick; absence is a finding, not permission to use fallback source",
   "overwriteRule":"never overwrite an existing capture artifact"
  },
  "proxies":rows,
  "implementationStatus":{
   "liveReader":"NOT AVAILABLE",
   "reason":"environment blocked further implementation of the live process-memory reader; the rejected operation was not rerouted",
   "offlineSpecification":"READY"
  },
  "limits":[
   "does not capture any live source bytes",
   "does not launch LastWar or LWBridge",
   "does not authenticate or contact remote services",
   "does not inspect the protected envelope-consumer body",
   "Map remains NOT WORKING"
  ]
 }

def main():
 ap=argparse.ArgumentParser()
 ap.add_argument("binary",type=Path)
 ap.add_argument("--json",action="store_true")
 ap.add_argument("--output",type=Path)
 a=ap.parse_args()
 try:r=inspect(a.binary)
 except (E,OSError,pefile.PEFormatError) as exc:ap.error(str(exc))
 s=json.dumps(r,indent=2)
 if a.output:
  a.output.parent.mkdir(parents=True,exist_ok=True)
  a.output.write_text(s+"\n",encoding="utf-8")
 print(s if a.json else f"finding={r['findingId']} status={r['evidenceStatus']} liveReader={r['implementationStatus']['liveReader']}")
if __name__=="__main__":raise SystemExit(main())
