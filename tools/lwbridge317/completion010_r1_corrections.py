"""Preserve 40/42 initial sweep and record independently verifiable corrections.
No game or updater activity. Do NOT rewrite final-checks-r1.json failures.
"""
import json
from pathlib import Path
import subprocess
import sys
import time

root=Path(__file__).resolve().parents[2]
output=root/"evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-COMPLETION-010/R1/final-sweep"
previous=json.loads((output/"final-checks-r1.json").read_text(encoding="utf-8"))
dll=str(root/"tests/LWBridge.Desktop.Checks/bin/Release/net10.0-windows10.0.17763.0/LWBridge.Desktop.Checks.dll")
runs=[
 ("native--overview-official-settle-check",["dotnet",dll,"--overview-official-settle-check"]),
 ("native-default-flow",["dotnet",dll]),
]
result=[]
for idx,(name,command) in enumerate(runs):
    t=time.perf_counter()
    print("R1_CORRECTED_START",name,flush=True)
    try:
        p=subprocess.run(command,cwd=root,capture_output=True,text=True,errors="replace",timeout=180)
        code,body=p.returncode,p.stdout+"\n--- STDERR ---\n"+p.stderr
    except Exception as error:
        code,body=-1,repr(error)
    logname=f"corrected-{idx+1}-{name}.txt"
    (output/logname).write_text(body,encoding="utf-8")
    result.append({"name":name,"exitCode":code,"seconds":round(time.perf_counter()-t,2),
                   "log":logname,"command":command,
                   "priorFailureLog":next(x["log"] for x in previous if x["name"]==name),
                   "reasonForCorrection":"source-backed Home post-publication cleanup / configured-root vs active-root historical-test expectation"})
    print("R1_CORRECTED_RESULT",name,code,flush=True)
passes={x["name"]:x["exitCode"] for x in previous}
for row in result: passes[row["name"]]=row["exitCode"]
ok=sum(v==0 for v in passes.values())
packet={"initialSweepPassed":sum(x["exitCode"]==0 for x in previous),
        "initialSweepTotal":len(previous),
        "initialRawResultsPreserved":"final-checks-r1.json",
        "corrected":result,
        "effectivePassed":ok,
        "effectiveTotal":len(passes),
        "allZero":ok==len(passes),
        "liveGameLaunches":0}
(output/"corrected-checks-r1.json").write_text(json.dumps(packet,indent=2)+"\n",encoding="utf-8")
print(f"R1_EFFECTIVE_TOTAL {ok}/{len(passes)}",flush=True)
sys.exit(0 if ok==len(passes) else 1)
