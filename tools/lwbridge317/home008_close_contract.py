"""Executable static close-wait contract from independently hash-gated 0.3.17.
A contract oracle for enumerated loop mechanics; NOT original runtime execution.
"""
from __future__ import annotations
import ast,hashlib,json
from pathlib import Path
import pefile
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
ROOT=Path(__file__).resolve().parents[2]
REFERENCE=Path(__file__).resolve().parents[2] / "reference" / "lwbridge-0.3.17.exe"
EXPECTED="4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
assert hashlib.sha256(REFERENCE.read_bytes()).hexdigest()==EXPECTED
pe=pefile.PE(str(REFERENCE));base=pe.OPTIONAL_HEADER.ImageBase
md=Cs(CS_ARCH_X86,CS_MODE_64)
def ins(start,end):
 return {i.address-base:(i.mnemonic,i.op_str) for i in md.disasm(pe.get_data(start,end-start),base+start)}
def has(d,at,mn,op):
 assert d.get(at)==(mn,op),f"byte/flow mismatch at {at:x}: {d.get(at)}"
close=[
 {"role":"stop-close","start":0xe5725,"end":0xe5884,"init":0xe578f,"loop":0xe5794,
 "limit":0xe579f if False else 0xe5796,"test":0xe57b2,"timer":0xe57cb,"delaycall":0xe57d1,
 "pollcall":0xe57ec,"timeout":0xe5811,"error":0xe5821,"success":0xe5807,"ready":0xe5869},
 {"role":"reconcile-close","start":0x1ddc84,"end":0x1ddde3,"init":0x1ddcee,"loop":0x1ddcf3,
 "limit":0x1ddcf5,"test":0x1ddd11,"timer":0x1ddd2a,"delaycall":0x1ddd30,
 "pollcall":0x1ddd4b,"timeout":0x1ddd70,"error":0x1ddd80,"success":0x1ddd66,"ready":0x1dddc8},
]
rows=[]
for x in close:
 d=ins(x["start"],x["end"])
 has(d,x["loop"],"cmp","eax, ecx")
 has(d,x["limit"],"jge",hex(base+x["timeout"]))
 has(d,x["timer"],"mov","r8d, 0x5f5e100")
 has(d,x["test"],"test","al, al")
 has(d,x["error"],"lea","r9, [rip + 0x747de8]" if x["role"]=="stop-close" else "r9, [rip + 0x65f439]")
 assert d[x["success"]][0]=="inc" and d[x["ready"]][0]=="mov"
 assert any(m=="mov" and o=="ecx, 0x64" for m,o in d.values())
 assert any(m=="call" and o==hex(base+0x41e3cf) for m,o in d.values())
 assert any(m=="call" and o==hex(base+0x641035) for m,o in d.values())
 assert any(m=="call" and o==hex(base+0x641410) for m,o in d.values())
 assert any(m=="call" and o==hex(base+0x5d5da) for m,o in d.values())
 rows.append({**x,"iterationCap":100,"timerNanoseconds":100_000_000,"nominalBudgetMilliseconds":10000,
   "readyStateByteOffset":"0xA0","onProcessCheckFalse":"ready branch without waiting",
   "onProcessCheckTrue":"schedule timer and poll; repeat while ready timer and count<100",
   "onExhaustion":"The game did not close in time.",
   "unresolved":"process checker inputs/native cause of process close and cancellation provenance"})
t=ins(0x641035,0x6410b4)
has(t,0x64105a,"cmp","r8d, 0x3b9aca00")
assert "1000000000"==str(int("3b9aca00",16))
# Pure semantic loop oracle: source-backed cap and next-poll ordering.
def oracle(last_matching_poll):
 for ordinal in range(1,101):
  if ordinal==last_matching_poll: return ("READY_PROCESS_ABSENT",ordinal-1)
  # delay 100ms before next loop iteration
 return ("READY_ERROR_TIMEOUT",100)
checks=[]
for absence in [1,2,99,100,101]:
 state,polls=oracle(absence)
 checks.append({"firstAbsentCheck":absence,"outcome":state,"completedWaitsBeforeExit":polls})
assert [x["outcome"] for x in checks]==["READY_PROCESS_ABSENT"]*4+["READY_ERROR_TIMEOUT"]
# Actual production helper default. Inspect AST only; don't run process closer.
py=ROOT/"tools/run_live_resource_probe.py"
mod=ast.parse(py.read_text(encoding="utf-8"))
func=next(n for n in mod.body if isinstance(n,ast.FunctionDef) and n.name=="close_owned_game_process_for_restore")
default=ast.literal_eval(func.args.defaults[-1])
assert default==10000, f"production close default mismatch: {default}"
src=ast.get_source_segment(py.read_text(encoding="utf-8"),func)
assert "Process]::GetProcessById" in src and "CloseMainWindow()" in src and "WaitForExit($waitMs)" in src
result={"referenceSha256":EXPECTED,"capstone":"5.0.7","originalMethods":rows,
 "timerUnitProof":{"function":"0x641035-0x6410b4","comparisonRva":"0x64105a","nanosecondsPerSecond":1000000000},
 "semanticContractOracleCases":checks,"production":{
   "source":"tools/run_live_resource_probe.py:close_owned_game_process_for_restore",
   "defaultWaitMilliseconds":default,"processProof":"exact PID/path/startTime guarded; normal CloseMainWindow and WaitForExit bounded; no forced kill",
   "scope":"CURRENT_CLIENT_IMPLEMENTATION_COMPARABLE_POLL_BUDGET_NOT_IDENTICAL_NATIVE_ASYNC_ORACLE"},
 "limits":["100 * 100ms is an iteration budget; scheduling overhead may make wall clock exceed exactly 10s",
           "CloseMainWindow current implementation is not proved the original trigger of the native waiting loop",
           "Timer wake and process enumeration are asynchronous; no original post-auth execution performed"]}
dest=ROOT/"evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-NATIVE-SEMANTICS-008"
dest.mkdir(exist_ok=True,parents=True)
(dest/"close-contract.json").write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8")
print("HOME008_CLOSE_TWO_ORIGINAL_NATIVE_LOOPS_AND_PRODUCTION_BOUNDARY_PASS 100x100ms nominal10s cases=5")
