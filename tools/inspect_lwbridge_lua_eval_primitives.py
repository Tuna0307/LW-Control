#!/usr/bin/env python3
"""Verify LWBridge 0.3.1 diagnostic, Lua load/eval-hook primitives, and call_lua host admission."""
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
DIAG_SHA="221a8eac911494226a3b19d37609beb0300060ada05593226b00392c2f6c6685"
class E(ValueError):pass
def req(v,m):
 if not v:raise E(m)
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
  if op.type==X86_OP_MEM and op.mem.base==X86_REG_RIP:return int(i.address+i.size+op.mem.disp-b)
 raise E(f"no RIP target at {i.address-b:#x}")
def bytes_at(pe,d,rva,n):
 return d[int(pe.get_offset_from_rva(rva)):int(pe.get_offset_from_rva(rva))+n]
def starts(pe,d,rva,want):
 req(bytes_at(pe,d,rva,len(want))==want,f"{rva:#x}: expected {want!r}")
def call(pe,d,rva,want):
 req(target(pe,ins(pe,d,rva))==want,f"{rva:#x}: target drift")
def proxy_report(name,d,want_hash):
 digest=hashlib.sha256(d).hexdigest();req(digest==want_hash,f"{name}: proxy hash mismatch")
 pe=pefile.PE(data=d,fast_load=False)

 # Exact embedded host diagnostic.
 src=rip(pe,ins(pe,d,0x18F51));chunk=rip(pe,ins(pe,d,0x18F44))
 req(src==0x71BD0,f"{name}: diagnostic source RVA drift")
 req(chunk==0x720F0,f"{name}: diagnostic chunk RVA drift")
 req(txt(ins(pe,d,0x18F4B))=="mov r8d, 0x51d",f"{name}: diagnostic length drift")
 diag=bytes_at(pe,d,src,0x51D)
 req(hashlib.sha256(diag).hexdigest()==DIAG_SHA,f"{name}: diagnostic bytes drift")
 starts(pe,d,chunk,b"@lwbridge-host-diagnostic")
 for needle in [b"__XluaBridgeHostDriver",b"__LWBRIDGE_HOST_DIAGNOSTIC",b"host diagnostic unavailable"]:
  req(needle in diag,f"{name}: diagnostic missing {needle!r}")

 # Original Lua globals are registered through lua_pushcclosure + xlua_setglobal.
 req(rip(pe,ins(pe,d,0x151B5))==0x19BD0,f"{name}: __XluaBridgeLoad callback drift")
 starts(pe,d,rip(pe,ins(pe,d,0x151C5)),b"__XluaBridgeLoad")
 req(rip(pe,ins(pe,d,0x151BF))==0x90E88 if name=="secure" else rip(pe,ins(pe,d,0x151BF))==0x91E88,f"{name}: pushcclosure slot drift")
 req(rip(pe,ins(pe,d,0x151CF))==0x90E40 if name=="secure" else rip(pe,ins(pe,d,0x151CF))==0x91E40,f"{name}: setglobal slot drift")
 req(rip(pe,ins(pe,d,0x151D8))==0x19AB0,f"{name}: EvalHook callback drift")
 starts(pe,d,rip(pe,ins(pe,d,0x151E8)),b"__XluaBridgeEvalHook")

 # __XluaBridgeLoad: source required, arg2/arg3 optional, default chunk/mode, common loader.
 req(rip(pe,ins(pe,d,0x19C01))==(0x90E58 if name=="secure" else 0x91E58),f"{name}: lua_tolstring slot drift")
 starts(pe,d,rip(pe,ins(pe,d,0x19C6C)),b"source must be a string")
 req(txt(ins(pe,d,0x19C66))=="mov r8d, 0x17",f"{name}: source error length drift")
 req(txt(ins(pe,d,0x19D91))=="mov qword ptr [rbp - 0x21], 7",f"{name}: default chunk length drift")
 starts(pe,d,rip(pe,ins(pe,d,0x19DA1)),b"=(load)")
 call(pe,d,0x19EEE,0x16C10)
 req(txt(ins(pe,d,0x19EFA)).startswith("call qword ptr"),f"{name}: load failure nil push drift")
 req(txt(ins(pe,d,0x19F00))=="mov edx, 0xfffffffe",f"{name}: lua_insert index drift")
 req(txt(ins(pe,d,0x19F0E))=="mov esi, 2",f"{name}: failure return count drift")

 # EvalHook: boolean arm/disarm, 5s deadline, count hook 10k, timeout error.
 req(rip(pe,ins(pe,d,0x19ABD))==(0x90E60 if name=="secure" else 0x91E60),f"{name}: lua_toboolean slot drift")
 req(txt(ins(pe,d,0x19AF9))=="add rax, 0x1388",f"{name}: eval deadline drift")
 req(txt(ins(pe,d,0x19B18))=="mov r9d, 0x2710",f"{name}: hook count drift")
 req(txt(ins(pe,d,0x19B25))=="mov r8d, 8",f"{name}: hook mask drift")
 starts(pe,d,0x711A8,b"eval execution limit exceeded")

 return {
  "proxy":name,"sha256":digest,
  "diagnostic":{"sourceRva":hex(src),"chunkNameRva":hex(chunk),"length":len(diag),"sha256":hashlib.sha256(diag).hexdigest()},
  "luaGlobals":{
   "__XluaBridgeLoad":{"callbackRva":"0x19BD0","contract":"load-like: source required; optional chunk/mode; default chunk =(load), default text mode; success returns function, failure returns nil,error"},
   "__XluaBridgeEvalHook":{"callbackRva":"0x19AB0","contract":"arm/disarm 5-second count-hook guard; timeout raises eval execution limit exceeded"}
  }
 }
