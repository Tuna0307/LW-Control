"""R1 static Home handler/callee trace on hash-gated original bytes only.
This is disassembly/xref evidence, not an original-runtime oracle or a
claim that control-flow predicates have been semantically reconstructed.
"""
import bisect, hashlib, json
from pathlib import Path
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86 import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

REFERENCE=Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
EXPECTED="4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
BASE_DIR=Path(__file__).resolve().parents[2] / "evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007/r1-2026-10-08"
LABELS={"start":(0x20715f,0x207ca9),"stop":(0x199627,0x19ac62),
        "status":(0x1a0da4,0x1a213c),"reconcile":(0x203021,0x2057dc),
        "update_restart":(0x2057dc,0x20715f)}
data=REFERENCE.read_bytes()
assert hashlib.sha256(data).hexdigest()==EXPECTED, "Reference hash mismatch"
pe=pefile.PE(data=data)
pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]])
runtime=sorted((e.struct.BeginAddress,e.struct.EndAddress) for e in pe.DIRECTORY_ENTRY_EXCEPTION)
begins=[r[0] for r in runtime]
image=pe.OPTIONAL_HEADER.ImageBase
md=Cs(CS_ARCH_X86,CS_MODE_64);md.detail=True
def fn_for(rva):
    i=bisect.bisect_right(begins,rva)-1
    return runtime[i] if i>=0 and rva<runtime[i][1] else None
def candidate_text(rva):
    try:
        raw=pe.get_data(rva,160)
        value=raw.split(b"\x00",1)[0]
        if 4<=len(value)<=130 and all(32<=c<=126 for c in value):
            return value.decode("ascii")
    except Exception: pass
    return None
def inspect(start,end,full=False):
    body=pe.get_data(start,end-start)
    out={"range":[hex(start),hex(end)],"sha256":hashlib.sha256(body).hexdigest(),
         "instructionCount":0,"directCalls":[],"branches":[],"dataReferences":[]}
    insns=list(md.disasm(body,image+start))
    out["instructionCount"]=len(insns)
    for i,ins in enumerate(insns):
        if ins.mnemonic=="call" and ins.operands and ins.operands[0].type==X86_OP_IMM:
            t=ins.operands[0].imm-image
            matched=fn_for(t)
            out["directCalls"].append({"at":hex(ins.address-image),"target":hex(t),
                "targetRange":list(map(hex,matched)) if matched else None})
        if ins.mnemonic.startswith("j") and len(out["branches"])<90:
            out["branches"].append({"at":hex(ins.address-image),"op":ins.mnemonic,"target":ins.op_str})
        for operand in ins.operands:
            if operand.type==X86_OP_MEM and operand.mem.base==X86_REG_RIP:
                target=ins.address-image+ins.size+operand.mem.disp
                value=candidate_text(target)
                if value:
                    out["dataReferences"].append({"at":hex(ins.address-image),"rva":hex(target),
                        "text":value[:120],"instruction":ins.mnemonic+" "+ins.op_str})
    out["dataReferences"]=list({(x["at"],x["rva"]):x for x in out["dataReferences"]}.values())
    if full: out["instructionSamples"]=[
        {"rva":hex(ins.address-image),"asm":ins.mnemonic+" "+ins.op_str}
        for i,ins in enumerate(insns) if i<36 or ins.mnemonic=="call" or
          (ins.mnemonic.startswith("j") and i<125)]
    return out
primary={k:inspect(*v,True) for k,v in LABELS.items()}
direct={}
for label,row in primary.items():
    for call in row["directCalls"]:
        rng=call["targetRange"]
        if rng is not None:
            beg,end=[int(s,16) for s in rng]
            if end-beg<=4096 and beg not in [v[0] for v in LABELS]:
                direct.setdefault((beg,end),set()).add(label)
callees=[]
for (beg,end),callers in sorted(direct.items()):
    entry=inspect(beg,end)
    entry["calledBy"]=sorted(callers)
    callees.append(entry)
largeCallee=inspect(0x1d5009,0x1ddbb2)
output={"referenceSha256":EXPECTED,"classification":"EXACT_BYTES_AND_STATIC_DISASSEMBLY_ONLY",
 "sharedAvailableLargeCallee":largeCallee,
 "limitations":["Indirect calls, async state machines, inlined helpers and external APIs are not reconstructed",
 "Plaintext strings and branches alone are not proven startup/retry/timeout policies",
 "Available undecoded native callees are unfinished recovery, not missing service input"],
 "primary":primary,"boundedDirectCallees":callees,
 "calleeExclusions":"Functions >4096 bytes require further scoped control/dataflow tracing"}
BASE_DIR.mkdir(parents=True,exist_ok=True)
path=BASE_DIR/"home-native-static-trace.json"
path.write_text(json.dumps(output,indent=2)+"\n",encoding="utf-8")
print("R1_HOME_STATIC_TRACE",path)
for label,item in primary.items():
    print(label,"instructions",item["instructionCount"],"calls",len(item["directCalls"]),
          "strings",[(i["at"],i["text"][:65]) for i in item["dataReferences"][:13]])
print("R1_BOUNDED_CALLEES",len(callees),"with_strings",sum(bool(c["dataReferences"]) for c in callees))
print("R1_SHARED_LARGE_CALLEE",largeCallee["instructionCount"],"calls",len(largeCallee["directCalls"]),"strings",[(x["at"],x["text"][:100]) for x in largeCallee["dataReferences"][:35]])
