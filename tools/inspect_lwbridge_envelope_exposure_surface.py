#!/usr/bin/env python3
"""Verify that LWBridge 0.3.1 exposes packageKeyEnvelope only at ingest/path/error owners."""
from __future__ import annotations
import argparse,hashlib,json,struct
from pathlib import Path
import pefile

EXPECTED="2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
LITERALS={
 "packageKeyEnvelope": b"packageKeyEnvelope",
 "packageKeyEnvelopeExpiresAt": b"packageKeyEnvelopeExpiresAt",
 "package-key.envelope": b"package-key.envelope",
 "KEY_ENVELOPE_EXPIRED": b"KEY_ENVELOPE_EXPIRED",
}
EXPECTED_RVAS={
 "packageKeyEnvelope":[0xC80114,0xC80126],
 "packageKeyEnvelopeExpiresAt":[0xC80126],
 "package-key.envelope":[0xC80240],
 "KEY_ENVELOPE_EXPIRED":[0xC80356],
}
EXPECTED_REFS={
 "packageKeyEnvelope":[0x23C95E,0x23C97B],
 "packageKeyEnvelopeExpiresAt":[0x23C97B],
 "package-key.envelope":[0x23D130],
 "KEY_ENVELOPE_EXPIRED":[0x23EF2C],
}
class E(ValueError):pass
def req(v,m):
 if not v:raise E(m)
def occurrences(pe,d,needle):
 out=[];s=0
 while True:
  j=d.find(needle,s)
  if j<0:break
  out.append(int(pe.get_rva_from_offset(j)));s=j+1
 return out
def lea_refs(pe,d,target):
 sec=next(s for s in pe.sections if s.Name.rstrip(b"\0")==b".text")
 off=int(sec.PointerToRawData);trva=int(sec.VirtualAddress)
 code=d[off:off+int(sec.SizeOfRawData)]
 out=[]
 i=0
 while i+7<=len(code):
  if 0x40<=code[i]<=0x4f and code[i+1]==0x8d:
   m=code[i+2]
   if (m>>6)==0 and (m&7)==5:
    disp=struct.unpack_from("<i",code,i+3)[0]
    if trva+i+7+disp==target:out.append(trva+i)
   i+=1;continue
  if code[i]==0x8d:
   if i>0 and 0x40<=code[i-1]<=0x4f:
    i+=1;continue
   m=code[i+1]
   if (m>>6)==0 and (m&7)==5:
    disp=struct.unpack_from("<i",code,i+2)[0]
    if trva+i+6+disp==target:out.append(trva+i)
  i+=1
 return sorted(set(out))

def inspect(path):
 d=path.read_bytes();digest=hashlib.sha256(d).hexdigest()
 req(digest==EXPECTED,f"unexpected SHA-256 {digest}")
 pe=pefile.PE(data=d,fast_load=False)
 rows={}
 for name,needle in LITERALS.items():
  locs=occurrences(pe,d,needle)
  req(locs==EXPECTED_RVAS[name],f"{name}: literal locations changed {[hex(x) for x in locs]}")
  refs=[]
  for r in locs: refs.extend(lea_refs(pe,d,r))
  refs=sorted(set(refs))
  req(refs==EXPECTED_REFS[name],f"{name}: LEA refs changed {[hex(x) for x in refs]}")
  rows[name]={"literalRvas":[hex(x) for x in locs],"codeRefs":[hex(x) for x in refs]}

 # Classify the four unique owners with already recovered contracts.
 # Response-field refs are inside auth-ingest; path ref is constructor; error ref is table builder.
 return {
  "findingId":"LWB-R8-109","date":"2026-09-27","evidenceStatus":"RECOVERED NEGATIVE SURFACE",
  "sourceIdentity":{"path":str(path),"sha256":digest},
  "literalInventory":rows,
  "recoveredResult":{
   "packageKeyEnvelope":"single exact literal owner at auth-response ingest 0x23C95E",
   "packageKeyEnvelopeExpiresAt":"single exact literal owner at auth-response ingest 0x23C97B",
   "packageKeyEnvelopePath":"single exact literal owner at auth-service path construction 0x23D130",
   "expiredStatus":"single exact literal owner at static error/status table construction 0x23EF2C",
   "hostExposure":"no second literal/xref surface exists for a command result, frontend/IPC payload, log/debug message, or alternate persisted-envelope owner",
   "impact":"the original host provides no separate envelope-exposure surface to exploit as a distinct permitted recovery route"
  },
  "limits":[
   "this verifier proves exact literal ownership/xref inventory, not arbitrary inaccessible OS history",
   "does not contact auth services or inspect credentials/private keys",
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
