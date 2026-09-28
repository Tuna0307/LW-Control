#!/usr/bin/env python3
"""Verify LWBridge 0.3.1 proxy environment-variable surface."""
from __future__ import annotations
import argparse,hashlib,json,struct,re
from pathlib import Path
import pefile
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
from capstone.x86_const import X86_OP_MEM,X86_REG_RIP

EXPECTED="2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
PROXIES={
 "secure":(0x987F74,0x95800,"481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400"),
 "plain":(0xA1D774,0x96000,"c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794"),
}
ENV={
 "LWBRIDGE_PROFILE_ID":0x6FF98,
 "LWBRIDGE_INSTANCE_ID":0x6FFC0,
 "LWBRIDGE_PIPE_TOKEN":0x6FFF0,
 "LWBRIDGE_GAME_ROOT":0x70F58,
 "LWBRIDGE_XLUA_ORIGINAL_PATH":0x70FF0,
 "LWBRIDGE_XLUA_PROXY_BUNDLE_PATH":0x71800,
 "LWBRIDGE_MULTI_REQUIRED":0x718B8,
 "LWBRIDGE_BUILD_ID":0x718E8,
 "LWBRIDGE_DESCRIPTOR_SHA256":0x71910,
 "LWBRIDGE_MULTI_PROOF_PATH":0x71948,
 "LWBRIDGE_MULTI_PROOF":0x71980,
 "LWBRIDGE_HOST_DIAGNOSTIC":0x71B98,
 "LWBRIDGE_PROFILE_RUNTIME_ROOT":0x72428,
}
WRAPPER_149D0=[0x15874,0x16398,0x16E54,0x180E5,0x1B5A8,0x1D742]
WRAPPER_14BE0=[0x1D4AA,0x1D563,0x1D5DC,0x1D653,0x1D6CC,0x1D790]
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
def wide_at(pe,d,rva):
 o=int(pe.get_offset_from_rva(rva));out=[]
 while o+1<len(d):
  v=d[o]|(d[o+1]<<8);o+=2
  if v==0:break
  out.append(chr(v))
 return "".join(out)
def proxy_report(name,d,want_hash):
 digest=hashlib.sha256(d).hexdigest();req(digest==want_hash,f"{name}: proxy hash mismatch")
 pe=pefile.PE(data=d,fast_load=False);base=int(pe.OPTIONAL_HEADER.ImageBase)
 # Exact wide LWBRIDGE_* inventory.
 found={}
 for m in re.finditer(rb"(?:[A-Z0-9_]\x00){4,}",d):
  try:s=m.group().decode("utf-16le")
  except UnicodeDecodeError:continue
  if not s.startswith("LWBRIDGE_"):continue
  found[s]=int(pe.get_rva_from_offset(m.start()))
 req(found==ENV,f"{name}: env inventory drift {found}")

 # Import identity.
 getenv_slot=None
 for ent in pe.DIRECTORY_ENTRY_IMPORT:
  for imp in ent.imports:
   if imp.name and imp.name.decode("ascii","replace")=="GetEnvironmentVariableW":
    getenv_slot=int(imp.address-base)
 req(getenv_slot==0x6F100,f"{name}: getenv IAT drift {getenv_slot!r}")

 # Generic environment readers have a closed constant-key caller set.
 req(direct_callers(pe,d,0x149D0)==WRAPPER_149D0,f"{name}: 0x149D0 callers drift")
 req(direct_callers(pe,d,0x14BE0)==WRAPPER_14BE0,f"{name}: 0x14BE0 callers drift")

 md=Cs(CS_ARCH_X86,CS_MODE_64);md.detail=True
 def ins(rva):
  o=int(pe.get_offset_from_rva(rva));i=next(md.disasm(d[o:o+16],base+rva),None);req(i is not None,f"{name}: missing {rva:#x}");return i
 def rip(i):
  for op in i.operands:
   if op.type==X86_OP_MEM and op.mem.base==X86_REG_RIP:return int(i.address+i.size+op.mem.disp-base)
  raise E(f"{name}: missing RIP target")
 def txt(rva):
  i=ins(rva);return f"{i.mnemonic} {i.op_str}".strip()

 # All constant keys passed to the two wrappers.
 wrapper_keys={
  0x15869:"LWBRIDGE_PROFILE_RUNTIME_ROOT",
  0x1638D:"LWBRIDGE_GAME_ROOT",
  0x16E49:"LWBRIDGE_XLUA_ORIGINAL_PATH",
  0x180DA:"LWBRIDGE_XLUA_ORIGINAL_PATH",
  0x1B59D:"LWBRIDGE_XLUA_PROXY_BUNDLE_PATH",
  0x1D737:"LWBRIDGE_MULTI_PROOF_PATH",
  0x1D49F:"LWBRIDGE_MULTI_REQUIRED",
  0x1D557:"LWBRIDGE_BUILD_ID",
  0x1D5D0:"LWBRIDGE_PROFILE_ID",
  0x1D647:"LWBRIDGE_INSTANCE_ID",
  0x1D6C0:"LWBRIDGE_DESCRIPTOR_SHA256",
  0x1D784:"LWBRIDGE_MULTI_PROOF",
 }
 for rva,key in wrapper_keys.items():
  req(rip(ins(rva))==ENV[key],f"{name}: wrapper key {key} drift")

 # Direct GetEnvironmentVariableW reads: identity/pipe + diagnostic + runtime log root.
 direct_keys={
  0x90C5:"LWBRIDGE_PROFILE_ID",
  0x90E1:"LWBRIDGE_INSTANCE_ID",
  0x90FD:"LWBRIDGE_PIPE_TOKEN",
  0x2006E:"LWBRIDGE_HOST_DIAGNOSTIC",
  0x42B7A:"LWBRIDGE_PROFILE_RUNTIME_ROOT",
  0x42C0C:"LWBRIDGE_PROFILE_RUNTIME_ROOT",
 }
 for rva,key in direct_keys.items():
  req(rip(ins(rva))==ENV[key],f"{name}: direct key {key} drift")

 # Diagnostic requires exactly one wchar "1" and is gated at 2000 ms.
 req(txt(0x20063)=="mov r8d, 2",f"{name}: diagnostic buffer size drift")
 req(txt(0x2007B)=="cmp eax, 1",f"{name}: diagnostic length check drift")
 req(txt(0x20080)=="cmp word ptr [rsp + 0x34], 0x31",f"{name}: diagnostic value check drift")
 req(txt(0x2008F)=="cmp rax, 0x7d0",f"{name}: diagnostic cadence drift")
 req(txt(0x2009E)=="call 0x180018f00",f"{name}: diagnostic runner drift")

 # Secondary PROFILE_RUNTIME_ROOT owner constructs normal proxy log, not eval/source input.
 req(wide_at(pe,d,0x73C10)=="\\logs\\xlua-proxy.log",f"{name}: proxy log suffix drift")
 req(wide_at(pe,d,0x73C40)=="\\bridge-runtime\\logs\\xlua-proxy.log",f"{name}: proxy log fallback drift")

 return {
  "proxy":name,"sha256":digest,
  "getEnvironmentVariableWIatRva":hex(getenv_slot),
  "environmentVariables":[{"name":k,"rva":hex(v)} for k,v in ENV.items()],
  "genericReaderCallers":{
   "0x149D0":[hex(x) for x in WRAPPER_149D0],
   "0x14BE0":[hex(x) for x in WRAPPER_14BE0],
  },
  "diagnostic":{
   "variable":"LWBRIDGE_HOST_DIAGNOSTIC",
   "enabledValue":"1",
   "minimumIntervalMs":2000,
   "runnerRva":"0x18F00"
  },
  "runtimeRootSecondaryUse":"constructs \\logs\\xlua-proxy.log / bridge-runtime fallback",
 }

def inspect(path):
 outer=path.read_bytes();digest=hashlib.sha256(outer).hexdigest()
 req(digest==EXPECTED,f"unexpected SHA-256 {digest}")
 ope=pefile.PE(data=outer,fast_load=False);rows=[]
 for name,(rva,size,want) in PROXIES.items():
  off=int(ope.get_offset_from_rva(rva));rows.append(proxy_report(name,outer[off:off+size],want))
 return {
  "findingId":"LWB-R8-113","date":"2026-09-27","evidenceStatus":"RECOVERED NEGATIVE CONFIG SURFACE",
  "sourceIdentity":{"path":str(path),"sha256":digest},
  "recoveredResult":{
   "inventory":"both proxies expose the same exact 13 LWBRIDGE_* wide environment variables",
   "readerClosure":"all calls to both generic GetEnvironmentVariableW wrappers use constant keys from that same 13-name inventory; direct reads are likewise members of it",
   "diagnostic":"LWBRIDGE_HOST_DIAGNOSTIC is enabled only by exact wide value 1 and invokes the embedded host diagnostic at most once per 2000 ms",
   "runtimeRoot":"LWBRIDGE_PROFILE_RUNTIME_ROOT also owns the ordinary xlua-proxy.log path construction",
   "sourceInput":"no environment variable provides Lua source, eval input, alternate bridge-script file, chunk path, or script override"
  },
  "proxies":rows,
  "limits":[
   "proves the native proxy environment-variable surface, not every possible file/registry/protocol input in the product",
   "does not recover encrypted bridge Lua or XluaBridgeHandlePipeMessage routing",
   "does not send pipe traffic, run the game, use live process memory, contact auth services, or inspect the protected envelope-consumer body",
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
