#!/usr/bin/env python3
"""Verify LWBridge 0.3.1 native pipe -> Lua dispatch boundary."""
from __future__ import annotations
import argparse,hashlib,json,struct
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
 if not v:raise E(m)
def ins(pe,d,rva):
 o=int(pe.get_offset_from_rva(rva));m=Cs(CS_ARCH_X86,CS_MODE_64);m.detail=True
 i=next(m.disasm(d[o:o+16],int(pe.OPTIONAL_HEADER.ImageBase)+rva),None)
 req(i is not None,f"missing {rva:#x}");return i
def text(i):return f"{i.mnemonic} {i.op_str}".strip()
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
 raise E(f"missing RIP ref at {i.address-b:#x}")
def bytes_at(pe,d,rva,n):
 o=int(pe.get_offset_from_rva(rva));return d[o:o+n]
def starts(pe,d,rva,want):
 req(bytes_at(pe,d,rva,len(want))==want,f"{rva:#x}: expected {want!r}")
def direct_callers(pe,d,target_rva):
 sec=next(s for s in pe.sections if s.Name.rstrip(b"\0")==b".text")
 off=int(sec.PointerToRawData);trva=int(sec.VirtualAddress);code=d[off:off+int(sec.SizeOfRawData)]
 out=[]
 for n in range(len(code)-5):
  if code[n]!=0xE8:continue
  disp=struct.unpack_from("<i",code,n+1)[0];src=trva+n
  if src+5+disp==target_rva:out.append(src)
 return out
def proxy_report(name,d,want_hash):
 digest=hashlib.sha256(d).hexdigest();req(digest==want_hash,f"{name}: proxy hash mismatch")
 pe=pefile.PE(data=d,fast_load=False)
 slot_delta=0 if name=="secure" else 0x1000

 # Native update loop forwards each 0x20-byte record to one fixed Lua global.
 starts(pe,d,0x721C0,b"XluaBridgeHandlePipeMessage")
 req(rip(pe,ins(pe,d,0x1F943))==0x721C0,f"{name}: handler literal xref drift")
 req(text(ins(pe,d,0x1F940))=="mov r8, rbx",f"{name}: handler argument pointer drift")
 req(text(ins(pe,d,0x1F94A))=="mov rcx, rsi",f"{name}: handler Lua state drift")
 req(target(pe,ins(pe,d,0x1F94D))==0x13890,f"{name}: handler helper target drift")
 req(text(ins(pe,d,0x1F952))=="add rbx, 0x20",f"{name}: handler record stride drift")

 # The proxy native image does not contain the host command/call function-routing keys.
 req(b'"kind":"call"' not in d,f"{name}: unexpected native command/call literal")
 req(b'"fn"' not in d,f"{name}: unexpected native fn literal")

 # Helper 0x13890 receives (luaState, globalName, stringObject).
 req(text(ins(pe,d,0x138C3))=="mov rbx, r8",f"{name}: helper string argument drift")
 req(text(ins(pe,d,0x138C6))=="mov r13, rdx",f"{name}: helper global name drift")
 req(text(ins(pe,d,0x138C9))=="mov r12, rcx",f"{name}: helper Lua state drift")

 expected_slots={
  "xlua_getglobal":0x90E38+slot_delta,
  "lua_pushlstring":0x90E68+slot_delta,
  "lua_pcall":0x90DD8+slot_delta,
  "lua_tolstring":0x90E58+slot_delta,
  "lua_settop":0x90E50+slot_delta,
 }
 actual_slots={
  "xlua_getglobal":rip(pe,ins(pe,d,0x138D2)),
  "lua_pushlstring":rip(pe,ins(pe,d,0x1390B)),
  "lua_pcall":rip(pe,ins(pe,d,0x1392A)),
  "lua_tolstring":rip(pe,ins(pe,d,0x13952)),
  "lua_settop":rip(pe,ins(pe,d,0x13CC8)),
 }
 req(actual_slots==expected_slots,f"{name}: dispatch Lua API slots drift {actual_slots}")

 # getglobal(globalName)
 req(text(ins(pe,d,0x13909))=="call rax",f"{name}: getglobal call drift")
 # pushlstring uses standard MSVC string data/length at +0/+0x10 with SSO threshold 15.
 req(text(ins(pe,d,0x13912))=="cmp qword ptr [rbx + 0x18], 0xf",f"{name}: string SSO threshold drift")
 req(text(ins(pe,d,0x13919))=="mov rdx, qword ptr [rbx]",f"{name}: heap data pointer drift")
 req(text(ins(pe,d,0x1391E))=="mov rdx, rbx",f"{name}: inline data pointer drift")
 req(text(ins(pe,d,0x13921))=="mov r8, qword ptr [rbx + 0x10]",f"{name}: string length drift")
 req(text(ins(pe,d,0x13925))=="mov rcx, r12",f"{name}: pushlstring state drift")
 req(text(ins(pe,d,0x13928))=="call rax",f"{name}: pushlstring call drift")

 # pcall(L, 1, 0, 0)
 req(text(ins(pe,d,0x13931))=="xor r9d, r9d",f"{name}: pcall errfunc drift")
 req(text(ins(pe,d,0x13934))=="xor r8d, r8d",f"{name}: pcall nresults drift")
 req(text(ins(pe,d,0x13937))=="mov r15d, 1",f"{name}: pcall nargs seed drift")
 req(text(ins(pe,d,0x1393D))=="mov edx, r15d",f"{name}: pcall nargs drift")
 req(text(ins(pe,d,0x13940))=="mov rcx, r12",f"{name}: pcall state drift")
 req(text(ins(pe,d,0x13943))=="call rax",f"{name}: pcall call drift")

 # On error helper reads top error as string and later restores the stack.
 req(text(ins(pe,d,0x1395E))=="mov edx, 0xffffffff",f"{name}: error stack index drift")
 req(text(ins(pe,d,0x13963))=="mov rcx, r12",f"{name}: error tostring state drift")
 req(text(ins(pe,d,0x13966))=="call rax",f"{name}: error tostring call drift")
 req(text(ins(pe,d,0x13CD4))=="mov edx, 0xfffffffe",f"{name}: stack restore index drift")
 req(text(ins(pe,d,0x13CD9))=="mov rcx, r12",f"{name}: stack restore state drift")
 req(text(ins(pe,d,0x13CDC))=="call rax",f"{name}: stack restore call drift")

 # XluaBridgeHandlePipeMessage has exactly one native literal and one native use.
 loc=[]
 s=0
 while True:
  j=d.find(b"XluaBridgeHandlePipeMessage",s)
  if j<0:break
  loc.append(int(pe.get_rva_from_offset(j)));s=j+1
 req(loc==[0x721C0],f"{name}: handler literal inventory drift {[hex(x) for x in loc]}")

 return {
  "proxy":name,"sha256":digest,
  "nativeDispatch":{
   "fixedLuaGlobal":"XluaBridgeHandlePipeMessage",
   "globalLiteralRva":"0x721C0",
   "nativeCallSiteRva":"0x1F943-0x1F94D",
   "recordStrideBytes":32,
   "argument":"one MSVC std::string value pushed with lua_pushlstring",
   "pcall":"pcall(1,0,0)",
   "nativeFunctionRoutingKeysPresent":False,
  },
  "luaApiSlots":{k:hex(v) for k,v in actual_slots.items()},
  "interpretation":"native proxy forwards each queued serialized message string to fixed Lua global XluaBridgeHandlePipeMessage; payload.fn routing is below this boundary in unrecovered bridge Lua"
 }

def inspect(path):
 outer=path.read_bytes();digest=hashlib.sha256(outer).hexdigest()
 req(digest==EXPECTED,f"unexpected SHA-256 {digest}")
 ope=pefile.PE(data=outer,fast_load=False);rows=[]
 for name,(rva,size,want) in PROXIES.items():
  off=int(ope.get_offset_from_rva(rva));rows.append(proxy_report(name,outer[off:off+size],want))
 return {
  "findingId":"LWB-R8-112","date":"2026-09-27","evidenceStatus":"RECOVERED DISPATCH BOUNDARY",
  "sourceIdentity":{"path":str(path),"sha256":digest},
  "recoveredResult":{
   "hostWireContext":"R7-097 host call envelope carries payload.fn and payload.args",
   "nativeProxyBoundary":"the native proxy does not inspect payload.fn; it forwards each queued serialized message to fixed Lua global XluaBridgeHandlePipeMessage as one string argument",
   "luaRoutingOwnership":"mapping payload.fn to provider/function behavior belongs below the native boundary in the unrecovered bridge Lua",
   "loadReachability":"call_lua(fn=__XluaBridgeLoad, ...) is NOT proven equivalent/reachable merely because __XluaBridgeLoad is a Lua global; XluaBridgeHandlePipeMessage routing must be recovered first",
   "impact":"do not use generic call_lua to invoke __XluaBridgeLoad as a recovery shortcut unless authentic bridge-Lua dispatch evidence establishes that route"
  },
  "proxies":rows,
  "limits":[
   "does not recover XluaBridgeHandlePipeMessage Lua source or its function allowlist/provider table",
   "does not rule out the encrypted bridge Lua intentionally exposing __XluaBridgeLoad",
   "does not send any pipe command or run the game",
   "does not use live process memory, auth/network activity, or the protected envelope-consumer body",
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
