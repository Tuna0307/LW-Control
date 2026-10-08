"""Locatable, read-only original Home native control/dataflow windows.
Extract selected relevant error/argument/terminal states with surrounding ASM.
NO private-service invocation or original-process execution.
"""
from pathlib import Path
import hashlib,json,pefile
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
EXE=Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
EXPECTED="4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
assert hashlib.sha256(EXE.read_bytes()).hexdigest()==EXPECTED
pe=pefile.PE(str(EXE));md=Cs(CS_ARCH_X86,CS_MODE_64)
base=pe.OPTIONAL_HEADER.ImageBase
focus={
 "stop-instance-mismatch":(0x199627,0x19ac62,0x199d78),
 "stop-user-reason":(0x199627,0x19ac62,0x199b51),
 "status-state-unavailable":(0x1a0da4,0x1a213c,0x1a11c2),
 "start-close-unmanaged":(0x20715f,0x207ca9,0x207784),
 "reconcile-root-missing":(0x203021,0x2057dc,0x203844),
 "reconcile-auto-launch":(0x203021,0x2057dc,0x203529),
 "restart-bridge-update":(0x2057dc,0x20715f,0x20658e),
 "shared-launch-unmanaged":(0x1d5009,0x1ddbb2,0x1d678a),
 "shared-launch-descriptor":(0x1d5009,0x1ddbb2,0x1d8ec0),
 "shared-launch-lease":(0x1d5009,0x1ddbb2,0x1d8137),
 "shared-launch-task-failure":(0x1d5009,0x1ddbb2,0x1dc2bc),
 "stop-close-timeout-helper":(0xe5725,0xe5884,0xe5780),
 "reconcile-close-timeout-helper":(0x1ddc84,0x1ddde3,0x1ddcd0),
}
sections=[]
for name,(start,end,target) in focus.items():
    insns=list(md.disasm(pe.get_data(start,end-start),base+start))
    nearest=min(range(len(insns)),key=lambda i:abs((insns[i].address-base)-target))
    lo=max(0,nearest-18);hi=min(len(insns),nearest+25)
    sections.append({"name":name,"function":[hex(start),hex(end)],"focusRva":hex(target),
       "neighbors":[{"rva":hex(ins.address-base),"asm":ins.mnemonic+" "+ins.op_str}
          for ins in insns[lo:hi]]})
dest=Path(__file__).resolve().parents[2]/"evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007/r1-2026-10-08/home-focus-native-windows.json"
dest.write_text(json.dumps({"sha256":EXPECTED,"scope":"EXACT_BYTES static disassembly windows, not semantic predicate proof","windows":sections},indent=2)+"\n",encoding="utf-8")
for section in sections:
    print("WINDOW",section["name"],"at",section["focusRva"])
    for i in section["neighbors"]:
        print(i["rva"],i["asm"])
