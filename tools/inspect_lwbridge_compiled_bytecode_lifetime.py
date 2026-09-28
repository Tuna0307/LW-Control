#!/usr/bin/env python3
"""Verify LWBridge 0.3.1 compiled bridge-bytecode format and lifetime."""
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
 raise E("missing RIP ref")
def ascii_at(pe,d,rva,n=96):
 o=int(pe.get_offset_from_rva(rva));e=o
 while e<min(len(d),o+n) and 32<=d[e]<127:e+=1
 return d[o:e].decode("ascii","replace")
def call(pe,d,rva,want):
 req(target(pe,ins(pe,d,rva))==want,f"{rva:#x}: target drift")
def direct_callers(pe,d,target_rva):
 sec=next(s for s in pe.sections if s.Name.rstrip(b"\0")==b".text")
 off=int(sec.PointerToRawData);trva=int(sec.VirtualAddress)
 code=d[off:off+int(sec.SizeOfRawData)];out=[]
 for n in range(len(code)-5):
  if code[n]!=0xE8:continue
  disp=struct.unpack_from("<i",code,n+1)[0];src=trva+n
  if src+5+disp==target_rva:out.append(src)
 return out

def rip_refs(pe,d,target_rva):
 sec=next(s for s in pe.sections if s.Name.rstrip(b"\0")==b".text")
 off=int(sec.PointerToRawData);trva=int(sec.VirtualAddress)
 code=d[off:off+int(sec.SizeOfRawData)]
 m=Cs(CS_ARCH_X86,CS_MODE_64);m.detail=True;m.skipdata=True;out=[]
 for i in m.disasm(code,int(pe.OPTIONAL_HEADER.ImageBase)+trva):
  if i.id==0:continue
  for op in i.operands:
   if op.type==X86_OP_MEM and op.mem.base==X86_REG_RIP:
    if int(i.address+i.size+op.mem.disp-int(pe.OPTIONAL_HEADER.ImageBase))==target_rva:
     out.append(int(i.address-int(pe.OPTIONAL_HEADER.ImageBase)));break
 return out

def proxy_report(name,d,want_hash):
 digest=hashlib.sha256(d).hexdigest();req(digest==want_hash,f"{name}: proxy hash mismatch")
 pe=pefile.PE(data=d,fast_load=False)

 # Wrapper has two branches: direct text load versus compile/LENC/load.
 flag=rip(pe,ins(pe,d,0x16C34))
 direct_slot=rip(pe,ins(pe,d,0x16C3D))
 req(ascii_at(pe,d,rip(pe,ins(pe,d,0x16C44))).startswith("t"),f"{name}: direct mode drift")
 req(ascii_at(pe,d,rip(pe,ins(pe,d,0x174A4))).startswith("luaL_loadbufferx"),f"{name}: direct loader symbol drift")
 req(rip(pe,ins(pe,d,0x1752B))==direct_slot,f"{name}: luaL_loadbufferx slot drift")

 # Compiler output vector is local to the wrapper.
 req(txt(ins(pe,d,0x16C94))=="movdqu xmmword ptr [rbp - 0x31], xmm0",f"{name}: vector init drift")
 req(txt(ins(pe,d,0x16C99))=="mov qword ptr [rbp - 0x21], 0",f"{name}: vector capacity init drift")
 call(pe,d,0x16CCF,0x26380)
 call(pe,d,0x16D01,0x27150)

 # Finalizer inserts LENC at vector.begin, then transforms bytes after the 4-byte prefix.
 req(txt(ins(pe,d,0x27156))=="mov rdx, qword ptr [rcx]",f"{name}: LENC insertion position drift")
 req(txt(ins(pe,d,0x2715E))=="mov r9d, 4",f"{name}: LENC size drift")
 req(txt(ins(pe,d,0x27164))=="mov dword ptr [rsp + 0x30], 0x434e454c",f"{name}: LENC marker drift")
 call(pe,d,0x2716F,0x260B0)
 req(txt(ins(pe,d,0x27174))=="mov rcx, qword ptr [rbx]",f"{name}: transform begin drift")
 req(txt(ins(pe,d,0x27177))=="mov rdx, qword ptr [rbx + 8]",f"{name}: transform end drift")
 req(txt(ins(pe,d,0x2717E))=="add rcx, 4",f"{name}: LENC skip drift")
 req(txt(ins(pe,d,0x27182))=="sub rdx, 4",f"{name}: transform length drift")
 req(target(pe,ins(pe,d,0x2718B))==0x26740,f"{name}: transform target drift")

 # ChaCha-family signature and in-place XOR.
 req(txt(ins(pe,d,0x26759))=="mov ebx, 0x3320646e",f"{name}: ChaCha constant 1 drift")
 req(txt(ins(pe,d,0x2675E))=="mov ebp, 0x79622d32",f"{name}: ChaCha constant 2 drift")
 req(txt(ins(pe,d,0x26763))=="mov r10d, 0x61707865",f"{name}: ChaCha constant 3 drift")
 req(txt(ins(pe,d,0x26873))=="mov eax, 0x6b206574",f"{name}: ChaCha constant 4 drift")
 req(txt(ins(pe,d,0x270C0))=="xor byte ptr [r8 + rdx], cl",f"{name}: in-place XOR drift")

 # Encoded vector is synchronously consumed by xluaL_loadbuffer.
 load_slot=rip(pe,ins(pe,d,0x16D23))
 req(ascii_at(pe,d,rip(pe,ins(pe,d,0x17559))).startswith("xluaL_loadbuffer"),f"{name}: xLua loader symbol drift")
 req(rip(pe,ins(pe,d,0x175B9))==load_slot,f"{name}: xLua loader slot drift")
 req(txt(ins(pe,d,0x16D06))=="mov r8, qword ptr [rbp - 0x29]",f"{name}: vector end read drift")
 req(txt(ins(pe,d,0x16D0A))=="mov rdx, qword ptr [rbp - 0x31]",f"{name}: vector begin read drift")
 req(txt(ins(pe,d,0x16D2A))=="sub r8d, edx",f"{name}: vector length calc drift")
 req(txt(ins(pe,d,0x16D33))=="call rax",f"{name}: xLua load call drift")

 # The same local vector storage is released before the wrapper returns.
 req(txt(ins(pe,d,0x16D82))=="mov rax, qword ptr [rbp - 0x31]",f"{name}: vector cleanup begin drift")
 call(pe,d,0x16DC3,0x48B90)
 req(ins(pe,d,0x16DE0).mnemonic=="ret",f"{name}: wrapper return drift")

 # Wrapper caller inventory is closed.
 callers=direct_callers(pe,d,0x16C10)
 req(callers==[0x15A3E,0x18F58,0x19EEE],f"{name}: wrapper callers drift {[hex(x) for x in callers]}")
 req(ascii_at(pe,d,rip(pe,ins(pe,d,0x15A2B))).startswith("@bridge-scripts.dat"),f"{name}: bridge chunk drift")
 req(ascii_at(pe,d,rip(pe,ins(pe,d,0x18F44))).startswith("@lwbridge-host-diagnostic"),f"{name}: diagnostic chunk drift")

 # lua_dump is confined to the compiler API surface; no later proxy dump/export path.
 dump_slot=rip(pe,ins(pe,d,0x16C7B))
 refs=rip_refs(pe,d,dump_slot)
 req(refs==[0x16C7B,0x17F4D,0x17F87],f"{name}: lua_dump slot refs drift {[hex(x) for x in refs]}")
 req(ascii_at(pe,d,rip(pe,ins(pe,d,0x17F39))).startswith("lua_dump"),f"{name}: lua_dump symbol drift")

 # Bridge-source lifetime remains later than owned encoded-vector lifetime.
 call(pe,d,0x15A3E,0x16C10)
 call(pe,d,0x1618A,0x3F350)

 return {
  "proxy":name,"sha256":digest,
  "wrapperBranchFlagRva":hex(flag),
  "directTextLoaderSlotRva":hex(direct_slot),
  "encodedLoaderSlotRva":hex(load_slot),
  "compiledVector":{"object":"wrapper stack local at rbp-0x31","prefix":"LENC","transform":"ChaCha-family in-place stream XOR after 4-byte LENC prefix","consumer":"xluaL_loadbuffer","release":"heap storage freed inside wrapper before return","zeroizedBeforeFree":False},
  "wrapperCallers":[hex(x) for x in callers],
  "luaDumpSlotRva":hex(dump_slot),
  "luaDumpSlotRefs":[hex(x) for x in refs]
 }
def inspect(path):
 outer=path.read_bytes();digest=hashlib.sha256(outer).hexdigest()
 req(digest==EXPECTED,f"unexpected SHA-256 {digest}")
 ope=pefile.PE(data=outer,fast_load=False);rows=[]
 for name,(rva,size,want) in PROXIES.items():
  off=int(ope.get_offset_from_rva(rva))
  rows.append(proxy_report(name,outer[off:off+size],want))
 return {
  "findingId":"LWB-R8-110","date":"2026-09-27","evidenceStatus":"RECOVERED NEGATIVE LIFETIME",
  "sourceIdentity":{"path":str(path),"sha256":digest},
  "recoveredResult":{
   "directTextBranch":"when the wrapper branch flag is nonzero, source is passed directly to luaL_loadbufferx in text mode and no compiled-bytecode vector is created",
   "compiledBranch":"otherwise lua_dump writes to a wrapper-local byte vector; LENC is inserted at the beginning, bytes after LENC are transformed in place with a ChaCha-family stream transform, and the result is synchronously passed to xluaL_loadbuffer",
   "ownedLifetime":"the encoded vector remains wrapper-local and its heap storage is freed before 0x16C10 returns; bootstrap source zeroization at 0x1618A occurs later in caller execution",
   "retention":"the proxy has no owned global/file/cache copy of this raw encoded vector and no later lua_dump-based function export surface",
   "semanticCaveat":"xluaL_loadbuffer may retain the loaded Lua function/prototype in Lua state; R8-110 does not claim that semantic representation disappears, only that the proxy does not retain the raw LENC buffer",
   "residueCaveat":"the vector is freed rather than explicitly zeroized, so unowned allocator residue is not ruled out; that is not a stable retained artifact boundary"
  },
  "proxies":rows,
  "limits":[
   "does not recover any bridge bytecode or Lua source bytes",
   "does not inspect live process memory or allocator residue",
   "does not create a new runtime lua_dump/export mechanism",
   "does not contact auth services or inspect the protected envelope-consumer body",
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
