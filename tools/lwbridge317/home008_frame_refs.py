"""Exact frame-offset producer/consumer references inside original shared native launch."""
import json,hashlib,re
from pathlib import Path
import pefile
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
P=Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
H="4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
assert hashlib.sha256(P.read_bytes()).hexdigest()==H
pe=pefile.PE(str(P));base=pe.OPTIONAL_HEADER.ImageBase
ins=list(Cs(CS_ARCH_X86,CS_MODE_64).disasm(pe.get_data(0x1d5009,0x1ddbb2-0x1d5009),base+0x1d5009))
offsets=["0x498","0x4a0","0x808","0x860","0x864","0x86c","0x86f","0x68d1"]
rows={}
for term in offsets:
 target="[r14 + "+term+"]"
 hits=[]
 for index,i in enumerate(ins):
  if target not in i.op_str:continue
  hits.append({"at":hex(i.address-base),"op":i.mnemonic+" "+i.op_str,
    "pre":[hex(o.address-base)+" "+o.mnemonic+" "+o.op_str for o in ins[max(0,index-11):index]],
    "post":[hex(o.address-base)+" "+o.mnemonic+" "+o.op_str for o in ins[index+1:index+8]]})
 rows[term]=hits
dest=Path(__file__).resolve().parents[2]/"evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-NATIVE-SEMANTICS-008/launch-frame-dataflow.json"
dest.write_text(json.dumps({"sourceSha256":H,"status":"FRAME_STATIC_DEFINITION_USE_NOT_FULL_ASYNC_CONTRACT","offsets":rows},indent=2)+"\n")
for term,hits in rows.items():
 print("\nOFFSET",term,len(hits))
 for row in hits:
  print(row["at"],row["op"])
  if term in ("0x498","0x4a0","0x808"): print(" BEF",row["pre"][-5:])
