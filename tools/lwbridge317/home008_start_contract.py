"""Machine-instruction-backed original Home reconcile/launch polling contract.
No original runtime execution. Defines bounded *independent* original predicates.
"""
import hashlib,json,re
from pathlib import Path
import pefile
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
ROOT=Path(__file__).resolve().parents[2]
EXE=Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
SHA="4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
assert hashlib.sha256(EXE.read_bytes()).hexdigest()==SHA
pe=pefile.PE(str(EXE));base=pe.OPTIONAL_HEADER.ImageBase;md=Cs(CS_ARCH_X86,CS_MODE_64)
def dis(start,end):return {i.address-base:(i.mnemonic,i.op_str) for i in md.disasm(pe.get_data(start,end-start),base+start)}
reconcile=dis(0x203021,0x2057dc)
launch=dis(0x1d5009,0x1ddbb2)
add_duration=dis(0x5dc950,0x5dc9a0)
timer=dis(0x641035,0x6410b4)
clock=dis(0x2c9034,0x2c90b8)
def eq(items,rva,mn,operands):
 assert items.get(rva)==(mn,operands), f"{rva:x} expected {mn} {operands}, got {items.get(rva)}"
# Exact original default: serde JSON object key 13 bytes autoLaunchAll,
# value must tag Boolean (1); absent/nonboolean defaults true.
assert pe.get_data(0x83cf58,13)==b"autoLaunchAll"
eq(reconcile,0x203519,"cmp","byte ptr [rbx + 0x66b0], 5")
eq(reconcile,0x203529,"lea","rdx, [rip + 0x639a28]")
eq(reconcile,0x20353b,"test","rax, rax")
eq(reconcile,0x203540,"cmp","byte ptr [rax], 1")
eq(reconcile,0x203545,"mov","al, byte ptr [rax + 1]")
eq(reconcile,0x20354a,"mov","al, 1")
eq(reconcile,0x203551,"mov","byte ptr [rbx + 0x68d1], al")
eq(reconcile,0x203576,"call",hex(base+0x1ddbb2))
# Original launch initial bounded step: now (seconds+nanoseconds) + 5 seconds,
# 100ms timer loop while its readiness condition is still unavailable.
eq(launch,0x1d693f,"call",hex(base+0x5cfc70))
eq(launch,0x1d6944,"mov","r8d, 5")
eq(launch,0x1d6950,"call",hex(base+0x5dc950))
eq(launch,0x1d6955,"mov","qword ptr [r14 + 0x498], rax")
eq(launch,0x1d695c,"mov","dword ptr [r14 + 0x4a0], edx")
eq(launch,0x1d72c9,"call",hex(base+0x5cfc70))
eq(launch,0x1d72d0,"cmp","edx, dword ptr [r14 + 0x4a0]")
eq(launch,0x1d72dc,"cmp","rax, qword ptr [r14 + 0x498]")
eq(launch,0x1d7305,"mov","r8d, 0x5f5e100")
eq(launch,0x1d730b,"call",hex(base+0x641035))
eq(launch,0x1d733c,"call",hex(base+0x5d5da))
eq(add_duration,0x5dc95a,"add","rcx, r8")
eq(add_duration,0x5dc95f,"add","r9d, edx")
eq(add_duration,0x5dc962,"cmp","r9d, 0x3b9aca00")
eq(timer,0x64105a,"cmp","r8d, 0x3b9aca00")
# Original startup named-pipe-registry wait begins after async helper at 1dd10f:
# compare monotonic-looking clock output+90,000 to frame at +0x808;
# repeated 250ms timer, exit condition additionally checks named-pipe registry.
eq(launch,0x1dd114,"call",hex(base+0x2c9034))
eq(launch,0x1dd119,"add","rax, 0x15f90")
eq(launch,0x1dd11f,"mov","qword ptr [r14 + 0x808], rax")
eq(launch,0x1dd152,"call",hex(base+0x2c75ca))
eq(launch,0x1dd3b6,"mov","r8d, 0xee6b280")
eq(launch,0x1dd3bc,"call",hex(base+0x641035))
eq(launch,0x1dd3f2,"call",hex(base+0x2c9034))
eq(launch,0x1dd3f7,"cmp","rax, qword ptr [r14 + 0x808]")
eq(launch,0x1dd424,"call",hex(base+0x2c6a65))
eq(launch,0x1dd42b,"je",hex(base+0x1dd3a2))
# Clock conversion from native Windows file-time units (ticks per second = 1e7)
# to milliseconds: quotient x1000, remainder factor, documented separately.
eq(clock,0x2c908b,"imul","rax, rdx, 0x989680")
eq(clock,0x2c9095,"imul","rdx, rdx, 0x3e8")
assert int("15f90",16)==90000 and int("ee6b280",16)==250000000
# Production public request-default expression: preserved exact source snippet.
source=(ROOT/"src/LWBridge.Desktop/OverviewLifecycleService.cs").read_text(encoding="utf-8")
assert 'bool autoLaunchAll = !payload.TryGetProperty("autoLaunchAll", out JsonElement requested) ||' in source
assert 'requested.ValueKind is not (JsonValueKind.True or JsonValueKind.False) || requested.GetBoolean();' in source
def original_reconcile_bool(value):
 return True if value is None or not isinstance(value,bool) else value
checks=[{"input":x,"originalDecision":original_reconcile_bool(x)}
        for x in (None,True,False,5,"false",{},[])]
assert [x["originalDecision"] for x in checks]==[True,True,False,True,True,True,True]
out={"sha256":SHA,"analysisClass":"EXACT_BYTE_VALUE_AND_CONTROL_FLOW_NOT_ORIGINAL_RUNTIME",
"reconcile":{"jsonKey":"autoLaunchAll","defaultWhenAbsent":True,
 "defaultWhenNonBoolean":True,"explicitFalse":False,"explicitTrue":True,
 "selector":"0x203519-0x203551","callback":"0x203576","oracleInputs":checks},
"launch":{"fiveSecondDeadline":{"producer":"0x1d693f-0x1d695c",
 "consumers":["0x1d72c9-0x1d72e9"],"sleeper":"0x1d7303-0x1d733c",
 "intervalNanoseconds":100000000,"deadlineSeconds":5,
 "purpose":"async returned-record readiness after 0x2a2887; full input type/response still incomplete"},
 "registryWait":{"producer":"0x1dd10f-0x1dd11f",
 "readyCall":"0x2c75ca (named pipe registry; contains STATE_UNAVAILABLE)",
 "pollCall":"0x2c6a65 after 0x1dd3f2 current-time check",
 "deadlineMilliseconds":90000,"pollNanoseconds":250000000,
 "exactOrdering":"record request -> store (clock_ms+90000) -> check registry -> wait/poll at 250ms; repeat only while below deadline and registry check branch says not ready",
 "unresolved":"registry result state and treatment of deadline as terminal error vs follow-on retry need more continuation tracing"},
 "timerNanosecondsPerSecond":1000000000},
 "productionComparison":{"source":"OverviewLifecycleService.cs:449-457",
 "defaultAutoLaunchMatches":True,"originalPrivate5sAnd90sMechanismIsDistinctFromCurrentClient":True,
 "doNotChangeCloneTimeoutSecondsWithoutEndToEndStateProof":True},
 "limits":["Milliseconds from 0x2c9034 uses FILETIME conversion; wall/monotonic clock provenance not fully decoded",
 "First 5s wait purpose and 90s registry failure propagation need further available-body tracing",
 "Health/retry threshold table and reset rules not identified in these snippets, no 0.3.1 promotion"]}
d=ROOT/"evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-NATIVE-SEMANTICS-008/startup-contract.json"
d.write_text(json.dumps(out,indent=2)+"\n",encoding="utf-8")
print("HOME008_ORIGINAL_STARTUP_DEFAULT5S90S_TIMER_100_250MS_STATIC_SEMANTIC_PASS cases=7")
