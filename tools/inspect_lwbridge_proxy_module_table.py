#!/usr/bin/env python3
"""Recover LWBridge 0.3.1 decrypted bridge module-table -> Lua source contract."""
from __future__ import annotations
import argparse, hashlib, json
from pathlib import Path
import pefile
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
from capstone.x86_const import X86_OP_IMM,X86_OP_MEM,X86_REG_RIP

EXPECTED="2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
PROXIES={
 "secure":(0x987F74,0x95800,"481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400",0x8F170),
 "plain":(0xA1D774,0x96000,"c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794",0x90170),
}
class E(ValueError): pass
def req(v,m):
 if not v: raise E(m)
def ins(pe,d,rva):
 o=int(pe.get_offset_from_rva(rva)); md=Cs(CS_ARCH_X86,CS_MODE_64); md.detail=True
 i=next(md.disasm(d[o:o+16],int(pe.OPTIONAL_HEADER.ImageBase)+rva),None)
 req(i is not None,f"missing instruction 0x{rva:X}"); return i
def rip(pe,i):
 base=int(pe.OPTIONAL_HEADER.ImageBase)
 for op in i.operands:
  if op.type==X86_OP_MEM and op.mem.base==X86_REG_RIP:return int(i.address+i.size+op.mem.disp-base)
 raise E("no rip operand")
def target(pe,i):
 base=int(pe.OPTIONAL_HEADER.ImageBase)
 for op in i.operands:
  if op.type==X86_OP_IMM:return int(op.imm-base)
 return None
def at(pe,d,rva,n):
 return d[int(pe.get_offset_from_rva(rva)):int(pe.get_offset_from_rva(rva))+n]

def inspect_proxy(name,d,source_global):
 pe=pefile.PE(data=d,fast_load=False)
 # Post-AES caller -> module-table parser.
 req(target(pe,ins(pe,d,0x1CC54))==0x125C0,f"{name}: parser caller changed")
 # Parser header and count.
 req(ins(pe,d,0x1261F).mnemonic=="mov" and ins(pe,d,0x1261F).op_str=="dword ptr [r8], 2",f"{name}: format output changed")
 req(ins(pe,d,0x1265E).op_str=="eax, 0x3ff",f"{name}: module-count bound changed")
 # Exact source-construction literals.
 literals={
 0x71658:b"local __bridge_preload = package.preload\n",
 0x716B0:b"bootstrap",
 0x716E8:b'package.loaded["',
 0x716D8:b'"] = nil\n',
 0x71718:b'__bridge_preload["',
 0x71700:b'"] = function(...)\n',
 0x7172C:b"\nend\n",
 0x71628:b"invalid module table",
 0x71688:b"invalid entry metadata",
 0x716A0:b"invalid entry",
 0x716C0:b"duplicate bootstrap",
 0x71738:b"trailing package data",
 0x71750:b"bootstrap missing",
 0x71640:b"invalid metadata",
 }
 for rva,b in literals.items(): req(at(pe,d,rva,len(b))==b,f"{name}: literal mismatch 0x{rva:X}")
 # Source global is replaced from parser output only on the successful refresh branch.
 req(rip(pe,ins(pe,d,0x1CDF2))==source_global,f"{name}: source clear global changed")
 req(rip(pe,ins(pe,d,0x1CE02))==source_global,f"{name}: source assignment global changed")
 # Entry parse anchors: u32 name length at entry+0, u32 source length at entry+4.
 anchors={
 0x12736:"movzx ecx, byte ptr [rdx + rsi + 3]",
 0x12752:"movzx eax, byte ptr [rdx + rsi]",
 0x1278B:"movzx r13d, byte ptr [rsi + rdx + 7]",
 0x127A4:"movzx eax, byte ptr [rsi + rdx + 4]",
 0x129CB:"lea rax, [r15 + rsi]",
 0x13207:"add rsi, 8",
 }
 for rva,want in anchors.items():
  i=ins(pe,d,rva); got=f"{i.mnemonic} {i.op_str}"; req(got==want,f"{name}: {rva:X}: {got}")
 # Character mask is exact: lowercase direct branch plus . 0-9 A-Z _ mask.
 i=ins(pe,d,0x129AB); req(i.mnemonic=="movabs" and i.op_str=="r8, 0x21ffffff80ffd",f"{name}: name mask changed")
 # Final acceptance requires exact consumption + non-empty bootstrap, then copies assembled source to output.
 req(ins(pe,d,0x132D1).mnemonic=="cmp" and ins(pe,d,0x132D1).op_str=="rsi, rdi",f"{name}: trailing-data check changed")
 req(ins(pe,d,0x132DA).mnemonic=="test" and ins(pe,d,0x132DA).op_str=="r14, r14",f"{name}: bootstrap check changed")
 req(target(pe,ins(pe,d,0x1333B))==0x87C0,f"{name}: output assignment helper changed")
 return {"proxy":name,"sha256":hashlib.sha256(d).hexdigest(),"moduleTableParserRva":"0x125C0",
  "sourceGlobalRva":hex(source_global),"formatOut":2,"moduleCount":{"encoding":"u32 little-endian","min":1,"max":1024},
  "entry":{"layout":["u32 nameLength","u32 sourceLength","name[nameLength]","source[sourceLength]"],
           "nameAllowed":".0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz"},
  "bootstrap":{"name":"bootstrap","mustProduceNonEmptySource":True,"appendedRawAtEnd":True},
  "generatedSource":{"prefix":"local __bridge_preload = package.preload\n",
    "perNonBootstrap":'package.loaded["<name>"] = nil\\n__bridge_preload["<name>"] = function(...)\\n<source>\\nend\\n'},
  "terminalChecks":["all decrypted bytes consumed","non-empty bootstrap present"]}

def main():
 ap=argparse.ArgumentParser();ap.add_argument("binary",type=Path);ap.add_argument("--json",action="store_true");ap.add_argument("--output",type=Path);a=ap.parse_args()
 outer=a.binary.read_bytes(); h=hashlib.sha256(outer).hexdigest(); req(h==EXPECTED,f"unexpected SHA-256 {h}")
 ope=pefile.PE(data=outer,fast_load=False); rows=[]
 for name,(rva,size,want,sg) in PROXIES.items():
  o=int(ope.get_offset_from_rva(rva)); d=outer[o:o+size]; req(hashlib.sha256(d).hexdigest()==want,f"{name}: hash mismatch")
  rows.append(inspect_proxy(name,d,sg))
 result={"findingId":"LWB-R8-099","date":"2026-09-27","evidenceStatus":"RECOVERED CONTRACT",
  "sourceIdentity":{"path":str(a.binary),"sha256":h},
  "recoveredResult":{"aesPlaintext":"format-2 module table, not final concatenated Lua source",
    "assembledSource":"module table parser synthesizes package.preload wrappers for non-bootstrap modules and appends bootstrap source raw",
    "mapRecoveryImpact":"once authentic AES plaintext is obtained, modules can be split exactly and XluaBridgeMapScanTick searched directly without reconstructing traversal"},
  "proxies":rows,
  "limits":["authentic AES plaintext/module bytes are not yet recovered","does not inspect the protected envelope-consumer body","Map remains NOT WORKING"],
  "reproduction":[r"python tools\inspect_lwbridge_proxy_module_table.py ..\LW\lwbridge-0.3.1.exe --json"]}
 s=json.dumps(result,indent=2)
 if a.output:a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(s+"\n",encoding="utf-8")
 print(s if a.json else f"finding={result['findingId']} status={result['evidenceStatus']}")
if __name__=="__main__":main()
