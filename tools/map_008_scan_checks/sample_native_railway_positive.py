"""Passive narrow witness of live Railway marching objects in native AOI replies.

Uses game-owned result files already produced by a running authorised scan.
Never issues a game command; a missing positive remains a pending gate.
"""
import argparse
import json
import pathlib
import time


def main():
    p=argparse.ArgumentParser()
    p.add_argument("--source",type=pathlib.Path,required=True)
    p.add_argument("--stop-file",type=pathlib.Path,required=True)
    p.add_argument("--out",type=pathlib.Path,required=True)
    p.add_argument("--run",required=True)
    p.add_argument("--profile",required=True)
    p.add_argument("--server",type=int,required=True)
    a=p.parse_args()
    if a.stop_file.exists() or a.out.exists():
        raise ValueError("Existing witness or stop file; refusing to overwrite")
    seen=set()
    owner=None
    sampled=0
    found=0
    while not a.stop_file.exists():
        try:
            result=json.loads(a.source.read_text(encoding="utf-8"))
            rid=result.get("requestId")
            if (not rid or rid in seen or result.get("state")!="proven" or
                result.get("requestMode")!="coverage" or
                result.get("profileId")!=a.profile or
                result.get("positionRestoredAfterResponse") is not True or
                result.get("positionRestoredBeforeResponse") is not False):
                time.sleep(.06);continue
            seen.add(rid)
            identity=(result.get("profileId"),result.get("launchSessionId"),result.get("gamePid"))
            if owner is None:
                owner=identity
            elif owner!=identity:
                raise RuntimeError("Source session switched during native Railway witness")
            trains=[row for row in result.get("train_march_records") or []
                    if row.get("trainType")==2 and row.get("serverId")==a.server]
            sampled+=1
            if trains:
                a.out.parent.mkdir(parents=True,exist_ok=True)
                payload={k:result.get(k) for k in (
                    "state","requestId","requestMode","profileId","launchSessionId",
                    "gamePid","capturedAt","cameraTileStable","responseFlagsTransitioned",
                    "positionRestoredBeforeResponse","positionRestoredAfterResponse",
                    "deferRestoreUntilResponse","requestMethod","requestedIndices")}
                payload["samplerAssociatedRunId"]=a.run
                payload["train_march_records"]=trains
                with a.out.open("a",encoding="utf-8") as sink:
                    sink.write(json.dumps(payload,ensure_ascii=False,separators=(",",":"))+"\n")
                found+=len(trains)
                if found>=10:
                    break
        except (OSError,ValueError,json.JSONDecodeError):
            pass
        time.sleep(.06)
    print(json.dumps({"sourceRequestsChecked":sampled,"railwayNativePositiveRecords":found,
                      "result":"positive_native_witness" if found else "positive_not_observed"}),flush=True)


if __name__=="__main__":
    main()
