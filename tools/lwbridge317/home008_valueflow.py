"""Relevant direct-call, immediate and RIP dataflow for original Home functions.
Does not infer meaning from numeric coincidence.
"""
from pathlib import Path
import hashlib, json, bisect, sys
import pefile
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
from capstone.x86 import X86_OP_IMM,X86_OP_MEM,X86_REG_RIP
REFERENCE=Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
H="4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
assert hashlib.sha256(REFERENCE.read_bytes()).hexdigest()==H
PE=pefile.PE(str(REFERENCE))
PE.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]])
functions=sorted((x.struct.BeginAddress,x.struct.EndAddress) for x in PE.DIRECTORY_ENTRY_EXCEPTION)
starts=[x[0] for x in functions];base=PE.OPTIONAL_HEADER.ImageBase
md=Cs(CS_ARCH_X86,CS_MODE_64);md.detail=True
def owner(r):
 i=bisect.bisect_right(starts,r)-1
 return functions[i] if i>=0 and r<functions[i][1] else None
seed={"start":(0x20715f,0x207ca9),"stop":(0x199627,0x19ac62),"status":(0x1a0da4,0x1a213c),"reconcile":(0x203021,0x2057dc),"restart":(0x2057dc,0x20715f),"shared_launch":(0x1d5009,0x1ddbb2),"close_stop":(0xe5725,0xe5884),"close_reconcile":(0x1ddc84,0x1ddde3)}
def text(rva):
 b=PE.get_data(rva,180);u=b.split(b"\x00",1)[0]
 return u.decode("ascii","ignore") if 4<=len(u)<=120 and all(32<=n<127 for n in u) else ""
def analyze(name,span):
 beg,end=span
 instructions=list(md.disasm(PE.get_data(beg,end-beg),base+beg))
 rows=[]
 for i,op in enumerate(instructions):
  own=[]
  for x in op.operands:
   if x.type==X86_OP_MEM and x.mem.base==X86_REG_RIP:
    target=op.address-base+op.size+x.mem.disp
    s=text(target)
    if s and any(t in s.lower() for t in ("autoreconnect","autolaunch","retry","timeout","maintenance","update","close","root","disconnect","recover","status","stopping","lease","launch")):
     own.append({"kind":"string","rva":hex(target),"text":s[:100]})
  if op.mnemonic=="call" and op.operands and op.operands[0].type==X86_OP_IMM:
   t=op.operands[0].imm-base
   if t in (0x641035,0x641410,0x5d5da,0x1ddbb2,0x1ddc84,0xe5725):
    own.append({"kind":"call","target":hex(t)})
  if own:
   window=[{"at":hex(x.address-base),"assembly":x.mnemonic+" "+x.op_str} for x in instructions[max(0,i-13):min(len(instructions),i+9)]]
   rows.append({"at":hex(op.address-base),"facts":own,"window":window})
 return {"name":name,"range":[hex(beg),hex(end)],"matches":rows,"instructionCount":len(instructions)}
result={"sha256":H,"classification":"STATIC_CROSS_REFERENCE_WINDOWS_NO_RUNTIME_ORIGINAL","functions":[analyze(k,v) for k,v in seed.items()]}
out=Path(__file__).resolve().parents[2]/"evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-NATIVE-SEMANTICS-008/startup-valueflow-windows.json"
out.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8")
for f in result["functions"]:
 print("\n",f["name"],"matches",len(f["matches"]))
 for row in f["matches"]:
  print(" ",row["at"],row["facts"])
