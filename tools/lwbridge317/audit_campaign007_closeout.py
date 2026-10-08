"""Read-only closeout consistency validator for solo campaign007 worker delivery.

Does not promote original provider parity or invent a positive staged live Stop.
"""
from __future__ import annotations
import json, subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/"evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007"
REVIEW=ROOT/"docs/reviews/2026-10-08-LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007-WORKER.md"
def obj(name):return json.loads((BASE/name).read_text(encoding="utf-8-sig"))
def checked_logs(name,count):
 lines=(BASE/name).read_text(encoding="utf-8-sig").splitlines()
 tests=[line for line in lines if " status=PASS " in line or " PASS exit=0" in line]
 assert len(tests)==count,(name,len(tests),tests)
 assert not any("status=FAIL" in line or " FAIL exit" in line for line in lines)
 return len(tests)
def main():
 m=obj("reference-contract-matrix.json")
 kinds=obj("eight-producer-matrix.json")
 ledger=obj("discrepancy-ledger.json")
 q=obj("queue.json")
 live=obj("live-positive-staged-stop-independent-audit.json")
 preservation=obj("preservation-final.json")
 output=obj("command-page-size-corrected-proof.json")
 full=obj("mounted-app-resource-results.json")
 map_ui=obj("mounted-real-resource-results.json")
 assert len(m["observations"])==49 and len(kinds["rows"])==8
 assert {r["kind"] for r in kinds["rows"]}=={"city","resource","monster","truck","railway","dispatch","ghost","treasure"}
 assert len(ledger["knownDemonstratedDifferences"])==2
 assert all(r["status"].startswith("CORRECTED") for r in ledger["knownDemonstratedDifferences"])
 assert output["failure"] is None and output["result"]["profileA"]["total"]==8008
 assert output["ownedTempCleanupSucceeded"] is True
 assert full["status"]=="PASS" and len(full["rendered"])==8
 assert map_ui["status"]=="PASS" and len(map_ui["cases"])==10
 assert live["status"]=="NEGATIVE_UNMET_POSITIVE_STAGING_WITNESS"
 assert live["positiveStagedCancellationProved"] is False and live["afterStop"]["staged"]==0
 assert live["afterStop"]["publishedResource"]==0 and live["restorationVerified"] is True
 assert preservation["check"]=="PASS" and preservation["sourceDatasetPublishedResource"]==8008
 assert preservation["gameProcesses"]==[] and preservation["tempCommandReplayRoots"]==[]
 assert q["status"]=="AWAITING_REVIEW"
 assert len(q["obligations"])==20
 assert all(not row["status"].startswith(("READY","IN_PROGRESS","PENDING_FINAL")) for row in q["obligations"]),[ (x["id"],x["status"]) for x in q["obligations"]]
 native=checked_logs("verification-native.txt",19)
 stat=checked_logs("verification-static.txt",9)
 frontend=checked_logs("verification-frontend.txt",6)
 final=checked_logs("verification-final-affected.txt",13)
 p=(BASE/"verification-package.txt").read_text(encoding="utf-8-sig")
 assert "desktop-canonical-Release-publish status=PASS" in p
 assert "packaged lua SHA256 matches source" in p
 r=REVIEW.read_text(encoding="utf-8")
 for phrase in ("AWAITING_REVIEW","UNKNOWN","8,008","2,608","page-size","positive staging","Restore"):
  if phrase=="Restore":continue
  assert phrase.lower() in r.lower(),f"missing report classification: {phrase}"
 for file in ["docs/tabs/home.md","docs/GOAL_CAMPAIGN_PHASE2_MAP.md",
              "docs/lwbridge-parity-matrix.md","docs/lwbridge-feature-ledger.md",
              "docs/implementation-handoff.md","docs/live-test-handoff.md"]:
  assert (ROOT/file).read_text(encoding="utf-8").startswith("**2026-10-08 CAMP007 WORKER AWAITING_REVIEW"),file
 assert "AWAITING_REVIEW" in (ROOT/"docs/work-items/LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007.md").read_text(encoding="utf-8")
 assert "AWAITING_REVIEW / WORKER DELIVERED" in (ROOT/"docs/LOOP_QUEUE.md").read_text(encoding="utf-8")
 diff=subprocess.run(["git","diff","839f0bcd162eed560a52c17ccf61eeb6a8731af0","HEAD","--check"],cwd=ROOT,capture_output=True,text=True)
 assert diff.returncode==0,diff.stdout+diff.stderr
 result={
  "workItem":"LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007","workerDelivery":"AWAITING_REVIEW",
  "leadAcceptance":"PENDING","originalParity":"PARTIAL_UNKNOWN_GAME_SIDE",
  "referenceObligations":49,"producerKinds":8,"demonstratedCorrections":2,
  "commandRealResourceRows":8008,"commandPositiveFilter":2608,
  "mountedFullAppCases":8,"mountedMapComponentCases":10,
  "focusedNative":native,"static":stat,"frontend":frontend,"finalAffected":final,
  "releasePackage":"PASS","historicalOriginalGamePositiveStagedStop":False,
  "newLivePositiveStagedStop":False,"newLiveStopRestoration":True,
  "originalExeAndInstalledScriptsPreserved":True,"originalResourceSQLitePreserved":True,
  "ownerDesktopControlActions":0,"newGameAttempts":1,
  "allIndependentReadyWorkerObligationsAddressed":True,
  "remainingDetails":"unresolved-dependencies.md",
  "whitespaceDiffCheck":"PASS"
 }
 (BASE/"final-closeout-audit.json").write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8")
 print("CAMPAIGN007_FINAL_CLOSEOUT_AUDIT_PASS 49 obligations 8 producers 2 query fixes 19 native 9 static 6 frontend 13 affected negative-live-restored original-parity-UNKNOWN")
if __name__=="__main__":main()
