#!/usr/bin/env python3
"""Verify LWBridge 0.3.1 host-diagnostic and internal Lua load/eval-hook contracts."""
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
DIAG_SHA="221a8eac911494226a3b19d37609beb0300060ada05593226b00392c2f6c6685"
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
def ascii_at(pe,d,rva,n=160):
 o=int(pe.get_offset_from_rva(rva));e=o
 while e<min(len(d),o+n) and 32<=d[e]<127:e+=1
 return d[o:e].decode("ascii","replace")
def assert_symbol(pe,d,lea_rva,want):
 got=ascii_at(pe,d,rip(pe,ins(pe,d,lea_rva)))
 req(got.startswith(want),f"{lea_rva:#x}: expected symbol {want!r}, got {got!r}")
def direct_callers(pe,d,target_rva):
 sec=next(s for s in pe.sections if s.Name.rstrip(b"\0")==b".text")
 off=int(sec.PointerToRawData);trva=int(sec.VirtualAddress)
 code=d[off:off+int(sec.SizeOfRawData)];out=[]
 for n in range(len(code)-5):
  if code[n]!=0xE8:continue
  disp=struct.unpack_from("<i",code,n+1)[0];src=trva+n
  if src+5+disp==target_rva:out.append(src)
 return out
def proxy_report(name,d,want_hash):
 digest=hashlib.sha256(d).hexdigest();req(digest==want_hash,f"{name}: proxy hash mismatch")
 pe=pefile.PE(data=d,fast_load=False)
 slot_delta=0 if name=="secure" else 0x1000

 # Embedded host diagnostic: exact chunk name, source pointer/length and source identity.
 req(rip(pe,ins(pe,d,0x18F44))==0x720F0,f"{name}: diagnostic chunk RVA drift")
 req(ascii_at(pe,d,0x720F0).startswith("@lwbridge-host-diagnostic"),f"{name}: diagnostic chunk name drift")
 req(text(ins(pe,d,0x18F4B))=="mov r8d, 0x51d",f"{name}: diagnostic length drift")
 src=rip(pe,ins(pe,d,0x18F51));req(src==0x71BD0,f"{name}: diagnostic source RVA drift")
 so=int(pe.get_offset_from_rva(src));source=d[so:so+0x51D]
 req(hashlib.sha256(source).hexdigest()==DIAG_SHA,f"{name}: diagnostic source hash drift")
 req(target(pe,ins(pe,d,0x18F58))==0x16C10,f"{name}: diagnostic loader call drift")
 txt=source.decode("utf-8")
 for marker in ["UpdateManager","UpdateBeat","__LWBRIDGE_HOST_DIAGNOSTIC","__XluaBridgeHostDriver","host_registered","host_tick"]:
  req(marker in txt,f"{name}: diagnostic marker missing {marker}")
 for forbidden in ["string.dump","debug.","__XluaBridgeLoad","__XluaBridgeEvalHook","XluaBridgeMapScanTick"]:
  req(forbidden not in txt,f"{name}: unexpected introspection marker {forbidden}")

 # Global registration: push C closure, then xlua_setglobal.
 assert_symbol(pe,d,0x176DC,"xlua_setglobal")
 setglobal_slot=rip(pe,ins(pe,d,0x17763));req(setglobal_slot==0x90E40+slot_delta,f"{name}: setglobal slot drift")
 assert_symbol(pe,d,0x17BD1,"lua_pushcclosure")
 pushc_slot=rip(pe,ins(pe,d,0x17C58));req(pushc_slot==0x90E88+slot_delta,f"{name}: pushcclosure slot drift")

 req(rip(pe,ins(pe,d,0x151B5))==0x19BD0,f"{name}: __XluaBridgeLoad callback drift")
 req(rip(pe,ins(pe,d,0x151BF))==pushc_slot,f"{name}: load pushcclosure slot drift")
 req(ascii_at(pe,d,rip(pe,ins(pe,d,0x151C5))).startswith("__XluaBridgeLoad"),f"{name}: load global name drift")
 req(rip(pe,ins(pe,d,0x151CF))==setglobal_slot,f"{name}: load setglobal slot drift")

 req(rip(pe,ins(pe,d,0x151D8))==0x19AB0,f"{name}: __XluaBridgeEvalHook callback drift")
 req(rip(pe,ins(pe,d,0x151E2))==pushc_slot,f"{name}: evalhook pushcclosure slot drift")
 req(ascii_at(pe,d,rip(pe,ins(pe,d,0x151E8))).startswith("__XluaBridgeEvalHook"),f"{name}: evalhook global name drift")
 req(rip(pe,ins(pe,d,0x151F2))==setglobal_slot,f"{name}: evalhook setglobal slot drift")

 # Exact Lua API slots used by the two callbacks.
 slots={}
 for sym,lea_rva,store_rva,want in [
  ("lua_sethook",0x17416,0x1749D,0x90DF0),
  ("luaL_loadbufferx",0x174A4,0x1752B,0x90DF8),
  ("lua_tolstring",0x17886,0x17904,0x90E58),
  ("lua_toboolean",0x1790B,0x17992,0x90E60),
  ("lua_pushlstring",0x17999,0x17A20,0x90E68),
  ("lua_pushnil",0x17A27,0x17AAE,0x90E70),
  ("lua_pushboolean",0x17AB5,0x17B3C,0x90E78),
  ("lua_insert",0x17B43,0x17BCA,0x90E80),
 ]:
  assert_symbol(pe,d,lea_rva,sym);slot=rip(pe,ins(pe,d,store_rva));req(slot==want+slot_delta,f"{name}: {sym} slot drift")
  slots[sym]=slot

 # __XluaBridgeLoad(source, chunkName?, mode?): standard load-style result shape.
 for rva in [0x19C01,0x19C20,0x19C3C]:
  req(rip(pe,ins(pe,d,rva))==slots["lua_tolstring"],f"{name}: loader argument read drift at {rva:#x}")
 req(rip(pe,ins(pe,d,0x19C60))==slots["lua_pushnil"],f"{name}: missing-source nil drift")
 req(rip(pe,ins(pe,d,0x19C76))==slots["lua_pushlstring"],f"{name}: missing-source error push drift")
 req(ascii_at(pe,d,rip(pe,ins(pe,d,0x19C6C))).startswith("source must be a string"),f"{name}: missing-source message drift")
 req(text(ins(pe,d,0x19C7C))=="mov eax, 2",f"{name}: missing-source result count drift")
 req(ascii_at(pe,d,rip(pe,ins(pe,d,0x19DA1))).startswith("=(load)"),f"{name}: default chunk name drift")
 req(ascii_at(pe,d,rip(pe,ins(pe,d,0x19EB2))).startswith("t"),f"{name}: default load mode drift")
 req(target(pe,ins(pe,d,0x19EEE))==0x16C10,f"{name}: dynamic loader wrapper call drift")
 req(rip(pe,ins(pe,d,0x19EFA))==slots["lua_pushnil"],f"{name}: load-error nil drift")
 req(rip(pe,ins(pe,d,0x19F08))==slots["lua_insert"],f"{name}: load-error reorder drift")
 req(text(ins(pe,d,0x19F0E))=="mov esi, 2",f"{name}: load-error result count drift")
 req(text(ins(pe,d,0x19F7E))=="mov eax, esi",f"{name}: loader return count drift")

 # __XluaBridgeEvalHook(bool): install/clear count hook and return boolean.
 req(rip(pe,ins(pe,d,0x19ABD))==slots["lua_toboolean"],f"{name}: evalhook boolean read drift")
 req(text(ins(pe,d,0x19AF9))=="add rax, 0x1388",f"{name}: evalhook 5s deadline drift")
 req(text(ins(pe,d,0x19B18))=="mov r9d, 0x2710",f"{name}: evalhook count interval drift")
 req(text(ins(pe,d,0x19B25))=="mov r8d, 8",f"{name}: evalhook mask drift")
 req(rip(pe,ins(pe,d,0x19B1E))==0x199C0,f"{name}: eval hook callback drift")
 req(rip(pe,ins(pe,d,0x19B3A))==slots["lua_sethook"],f"{name}: evalhook sethook slot drift")
 req(rip(pe,ins(pe,d,0x19B58))==slots["lua_sethook"],f"{name}: evalhook clear slot drift")
 req(rip(pe,ins(pe,d,0x19BA6))==slots["lua_pushboolean"],f"{name}: evalhook result push drift")
 req(text(ins(pe,d,0x19BAC))=="mov eax, 1",f"{name}: evalhook result count drift")

 # Hook hard limits and exact error message construction.
 req(text(ins(pe,d,0x199F2))=="add qword ptr [rbx + rax], 0x2710",f"{name}: eval instruction accumulation drift")
 req(text(ins(pe,d,0x199FA))=="cmp qword ptr [rbx + rax], 0x4c4b40",f"{name}: eval instruction hard cap drift")
 req(text(ins(pe,d,0x19A61))=="mov r8d, 0x1d",f"{name}: eval error length drift")
 req(rip(pe,ins(pe,d,0x19A8C))==slots["lua_pushlstring"],f"{name}: eval error push drift")
 first=ascii_at(pe,d,rip(pe,ins(pe,d,0x19A4F)),32)
 req(first.startswith("eval execution l"),f"{name}: eval error prefix drift")

 # Both callbacks are Lua registrations, not direct native-call entrypoints.
 req(direct_callers(pe,d,0x19BD0)==[],f"{name}: unexpected direct native caller of __XluaBridgeLoad")
 req(direct_callers(pe,d,0x19AB0)==[],f"{name}: unexpected direct native caller of __XluaBridgeEvalHook")

 # "original_source=" is bootstrap DLL-origin metadata, not Lua source content.
 req(ascii_at(pe,d,rip(pe,ins(pe,d,0x181E6))).startswith(" original_source="),f"{name}: original_source label drift")
 req(ascii_at(pe,d,rip(pe,ins(pe,d,0x180EB))).startswith("self_dir"),f"{name}: self_dir label drift")
 req(ascii_at(pe,d,rip(pe,ins(pe,d,0x180F2))).startswith("runtime_env"),f"{name}: runtime_env label drift")
 req(ascii_at(pe,d,rip(pe,ins(pe,d,0x18102))).startswith("plain_fallback"),f"{name}: plain_fallback label drift")
 req(ascii_at(pe,d,rip(pe,ins(pe,d,0x18109))).startswith("secure"),f"{name}: secure label drift")

 return {
  "proxy":name,"sha256":digest,
  "diagnostic":{"sourceRva":hex(src),"length":len(source),"sha256":DIAG_SHA,"chunkName":"@lwbridge-host-diagnostic"},
  "globals":{"__XluaBridgeLoad":"0x19BD0","__XluaBridgeEvalHook":"0x19AB0"},
  "apiSlots":{k:hex(v) for k,v in slots.items()}|{"lua_pushcclosure":hex(pushc_slot),"xlua_setglobal":hex(setglobal_slot)},
  "loadContract":{
   "arguments":"source, optional chunkName, optional mode",
   "defaults":{"chunkName":"=(load)","mode":"t"},
   "success":"returns one loaded function",
   "failure":"returns nil,error (two results)"
  },
  "evalHookContract":{
   "enabled":"5-second deadline; LUA_MASKCOUNT-style mask 8; hook interval 10000; accumulated hard cap 5000000",
   "disabled":"clears hook",
   "result":"returns one boolean",
   "limitError":"eval execution limit exceeded"
  }
 },source
def inspect(path):
 outer=path.read_bytes();digest=hashlib.sha256(outer).hexdigest()
 req(digest==EXPECTED,f"unexpected SHA-256 {digest}")
 ope=pefile.PE(data=outer,fast_load=False);rows=[];sources=[]
 for name,(rva,size,want) in PROXIES.items():
  off=int(ope.get_offset_from_rva(rva));row,source=proxy_report(name,outer[off:off+size],want)
  rows.append(row);sources.append(source)
 req(sources[0]==sources[1],"secure/plain diagnostic source differs")

 # The outer host knows the eval-log filename for feedback collection.
 locs=[];s=0;needle=b"xlua_proxy_eval.log"
 while True:
  j=outer.find(needle,s)
  if j<0:break
  locs.append(int(ope.get_rva_from_offset(j)));s=j+1
 req(locs==[0xC80971],f"host xlua_proxy_eval.log literal drift {[hex(x) for x in locs]}")

 return {
  "findingId":"LWB-R8-111",
  "date":"2026-09-27",
  "evidenceStatus":"RECOVERED CONTRACT / NEGATIVE RECOVERY ROUTE",
  "sourceIdentity":{"path":str(path),"sha256":digest},
  "recoveredResult":{
   "hostDiagnostic":"both proxies embed the same 1309-byte @lwbridge-host-diagnostic Lua chunk; it probes update-loop registration/counters only and contains no recovered debug/string.dump/source-export behavior",
   "luaLoad":"__XluaBridgeLoad is registered as a Lua global C closure. It implements load(source, chunkName?, mode?) with defaults =(load) and t, returning a loaded function on success or nil,error on failure",
   "evalHook":"__XluaBridgeEvalHook is registered as a Lua global C closure. It installs/clears a protected count hook with a 5-second deadline, 10000-instruction hook interval and 5000000 accumulated hard cap",
   "nativeReachability":"neither callback has a direct native E8 caller and neither helper name is a PE export; they are Lua-side facilities, not recovered native/host entrypoints by themselves",
   "originalSourceLabel":"the proxy log field original_source= records real-xLua origin (runtime_env or self_dir) together with secure/plain_fallback mode; it is not assembled Lua source content",
   "feedbackLog":"the outer host contains the exact xlua_proxy_eval.log filename for feedback-log collection, but this contract does not show a host command that supplies Lua source"
  },
  "proxies":rows,
  "diagnosticSource":{"length":len(sources[0]),"sha256":hashlib.sha256(sources[0]).hexdigest()},
  "limits":[
   "does not rule out the unrecovered encrypted bridge Lua calling __XluaBridgeLoad or __XluaBridgeEvalHook internally",
   "does not prove any pipe-message eval command because the bridge script source remains unavailable",
   "does not capture loaded Lua functions or source",
   "does not use live process memory, authentication, network activity, or the protected envelope-consumer body",
   "Map remains NOT WORKING"
  ]
 },sources[0]

def main():
 ap=argparse.ArgumentParser();ap.add_argument("binary",type=Path);ap.add_argument("--json",action="store_true");ap.add_argument("--output",type=Path);ap.add_argument("--diagnostic-source-output",type=Path);a=ap.parse_args()
 try:r,source=inspect(a.binary)
 except (E,OSError,pefile.PEFormatError,UnicodeDecodeError) as exc:ap.error(str(exc))
 s=json.dumps(r,indent=2)
 if a.output:
  a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(s+"\n",encoding="utf-8")
 if a.diagnostic_source_output:
  a.diagnostic_source_output.parent.mkdir(parents=True,exist_ok=True);a.diagnostic_source_output.write_bytes(source)
 print(s if a.json else f"finding={r['findingId']} status={r['evidenceStatus']}")
if __name__=="__main__":raise SystemExit(main())
