"""Hash-gated x64 native semantic workbench for Home 008.
Reports exact control edges and instruction windows, not inferred contracts.
Requires python pefile and capstone. Offline reference ONLY.
"""
import hashlib,sys,json, bisect
from pathlib import Path
import pefile
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
from capstone.x86 import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP
exe=Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
expected="4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
assert hashlib.sha256(exe.read_bytes()).hexdigest()==expected
pe=pefile.PE(str(exe))
pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]])
ranges=sorted((x.struct.BeginAddress,x.struct.EndAddress) for x in pe.DIRECTORY_ENTRY_EXCEPTION)
begins=[x[0] for x in ranges]
base=pe.OPTIONAL_HEADER.ImageBase
md=Cs(CS_ARCH_X86,CS_MODE_64);md.detail=True
def locate(rva):
 i=bisect.bisect_right(begins,rva)-1
 return ranges[i] if i>=0 and rva<ranges[i][1] else None
def dis(rva,end):
 return list(md.disasm(pe.get_data(rva,end-rva),base+rva))
def render(rva,end,focus=None,radius=30):
 lines=[]
 inst=dis(rva,end)
 if focus is not None:
  idx=min(range(len(inst)),key=lambda i:abs(inst[i].address-base-focus))
  inst=inst[max(0,idx-radius):idx+radius+1]
 for i in inst:
  off=i.address-base
  suffix=""
  if i.mnemonic in ("call","jmp") or i.mnemonic.startswith("j"):
   if i.operands and i.operands[0].type==X86_OP_IMM:
    target=i.operands[0].imm-base
    rng=locate(target)
    suffix=" -> "+hex(target)+((" fn="+hex(rng[0])+"-"+hex(rng[1])) if rng else "")
  for op in i.operands:
   if op.type==X86_OP_MEM and op.mem.base==X86_REG_RIP:
    target=off+i.size+op.mem.disp
    try:
     b=pe.get_data(target,150)
     printable=b.split(b"\x00",1)[0]
     if 3<=len(printable)<=130 and all(32<=c<=126 for c in printable):
      suffix+=" str="+repr(printable.decode())
     else: suffix+=" data="+hex(target)
    except:pass
  lines.append(f"{off:08x} {i.bytes.hex():<28} {i.mnemonic:<8} {i.op_str:<46} {suffix}")
 return "\n".join(lines)
if __name__=="__main__":
 args=sys.argv[1:]; s=int(args[0],0);e=int(args[1],0)
 focus=int(args[2],0) if len(args)>2 else None
 radius=int(args[3]) if len(args)>3 else 35
 print(render(s,e,focus,radius) if focus is not None else render(s,e))
