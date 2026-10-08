"""R1 read-only evidence consistency and preservation validator.
Writes only the NEW R1 evidence subtree; never mutates accepted 007/004 packets.
Includes negative semantic/structural mutation probes against claim validation.
"""
from __future__ import annotations
import hashlib,json,sqlite3,subprocess,os
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/"evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007"
R1=BASE/"r1-2026-10-08"
LEAD=BASE/"lead-review-2026-10-08"
REFERENCE=Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
LOCAL=Path(r"C:\Users\chimw\AppData\LocalLow\FunFly\Last War-Survival Game\lwScripts")
EXPECTED_EXE="4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
TRIPLET={
 "LWScripts.data":"248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22",
 "LWScripts.txt":"d2f9bcc165f41427d4b3b7b9243e4a483bf316519a16b4ce9be658ba9afa783a",
 "version.txt":"785f3ec7eb32f30b90cd0fcf3657d388b5ff4297f2f9716ff66e9b69c05ddd09"
}
def sha(path):
 h=hashlib.sha256()
 with path.open("rb") as f:
  for chunk in iter(lambda:f.read(1024*1024),b""):h.update(chunk)
 return h.hexdigest()
def read(path):return json.loads(path.read_text(encoding="utf-8-sig"))
def suite(name,expected):
 lines=(R1/f"checks-{name}.txt").read_text(encoding="utf-8-sig").splitlines()
 passed=[s for s in lines if "status=PASS exit=0" in s]
 assert len(passed)==expected,(name,len(passed),expected)
 assert not any("status=FAIL" in s for s in lines)
 return len(passed)
def old(name):return read(BASE/name)
def scoped_status(assembled):
 if any(x["status"].startswith(("READY","IN_PROGRESS","PENDING_FINAL")) for x in assembled["obligations"]):
  raise ValueError("R1 ready obligation reopened without being handled")
 if assembled["live"]["007_positive_staged_stop"] is True:raise ValueError("007 positive live staging was not observed")
 if assembled["launch"]["007_real_game_attempts"]!=1 or assembled["launch"]["R1_real_game_attempts"]!=0:
  raise ValueError("inconsistent 007/R1 real launch counts")
 if assembled["queries"]["numericCorrections"]!=["M20_pageSize","M21_page"]:
  raise ValueError("lost either accepted numeric query correction")
 if assembled["proofs"]["mountedAppR1Cases"]!=20:raise ValueError("mounted profile cases not measured")
 if assembled["proofs"]["originalFullHomeParity"] is True:raise ValueError("unsupported original Home proof")
 if any(row.get("evidenceClass")=="original-native-semantic" for row in assembled["mutations"] if row["kind"]=="structural"):
  raise ValueError("structural mutation disguised as original-semantic proof")
 if not all(c["stagedAfterStop"]==0 and c["publishedAfterStop"]==1 for c in assembled["engineCases"]):
  raise ValueError("late staging not retired")
 return True
def main():
 assert sha(REFERENCE)==EXPECTED_EXE,"original exe altered"
 assert all(sha(LOCAL/name)==digest for name,digest in TRIPLET.items()),"LocalLow original scripts changed"
 trace=read(R1/"home-native-static-trace.json")
 assert trace["referenceSha256"]==EXPECTED_EXE and len(trace["primary"])==5
 assert len(trace["boundedDirectCallees"])>=120
 assert trace["sharedAvailableLargeCallee"]["instructionCount"]>6000
 checkCounts={label:suite(label,count) for label,count in (("native",21),("static",12),("frontend",6),("package",3))}
 package=(R1/"checks-package.txt").read_text(encoding="utf-8-sig")
 assert package.count("package-file ")==4 and "Lua package/source SHA256 " in package
 lead=read(LEAD/"review-observations.json")
 assert lead["campaign007StopStagedRows"]==0 and lead["originalHomeNativeBodiesAvailable"]==5
 assert lead["preservationLaunchCountFieldRejected"] is True
 past=old("final-closeout-audit.json")
 assert past["newGameAttempts"]==1 and past["newLivePositiveStagedStop"] is False
 preservation=old("preservation-final.json")
 assert preservation["newGameLaunchesCampaign007"]==0,"historical contradictory field changed; retain for audit"
 assert old("live-positive-staged-stop-001.json")["positiveStagingCountBeforeStop"]==0
 assert len(old("discrepancy-ledger.json")["knownDemonstratedDifferences"])==2
 assert len(old("reference-contract-matrix.json")["observations"])==49
 assert len(old("eight-producer-matrix.json")["rows"])==8
 queue=read(R1/"queue.json")
 assert queue["status"]=="AWAITING_REVIEW" and len(queue["obligations"])==3
 assert [x["id"] for x in queue["obligations"]]==["R007-01","R007-02","R007-03"]
 matrix=read(R1/"reference-contract-matrix-r1.json")
 assert len(matrix["observations"])==49 and matrix["originalCampaign007RecordUntouched"]
 assert {r["id"] for r in matrix["observations"] if "r1SourceProofStatus" in r}=={"H03","H13","M18","M20","M21","M26","M33"}
 engine=read(R1/"engine-sink-stop-proof.json")
 assert len(engine["cases"])==4 and engine["livePositiveStageStop"] is False
 assert [c["scenario"] for c in engine["cases"]]==[
  "cancel-during-deferred-batch","late-batch-return-after-cancel",
  "positive-stage-then-stop","stop-during-publication-transition"]
 baseline=read(R1/"late-stage-baseline-failure.json")
 assert "staged=2" in baseline["observation"]
 mounted=read(R1/"mounted-app-profiles.json")
 assert mounted["status"]=="PASS" and mounted["cases"]==20 and mounted["externalGameActions"]==0
 assert sum(x["phase"]=="retired-A-deferred-page2" for x in mounted["rendered"])==2
 assert sum(x["phase"]=="retired-A-rejected-response-no-B-leak" for x in mounted["rendered"])==2
 assert sum(x["phase"]=="A-recovery-after-retired-error" for x in mounted["rendered"])==2
 safe=old("safe-real-db-snapshot.json")
 original=Path(safe["originalSourcePath"])
 copy=Path(safe["snapshotPath"])
 assert original.is_file() and copy.is_file()
 assert sha(original)==safe["sourceBytesSha256BeforeAfter"] and sha(copy)==safe["snapshotSha256"]
 for suffix,val in safe["originalSidecarHashes"].items():
  path=Path(str(original)+suffix) if suffix.startswith("-") else None
  if path is not None:assert path.is_file() and sha(path)==val["sha256"]
 with sqlite3.connect(copy.resolve().as_uri()+"?mode=ro",uri=True) as c:
  c.execute("PRAGMA query_only=ON")
  assert c.execute("SELECT count(*) FROM map_records WHERE kind='resource' AND server_id=2212").fetchone()[0]==8008
  assert c.execute("SELECT count(*) FROM scan_records").fetchone()[0]==0
  assert c.execute("PRAGMA integrity_check").fetchone()[0]=="ok"
 process=subprocess.run(["tasklist","/FO","CSV","/NH"],text=True,capture_output=True,check=True)
 game=[x for x in process.stdout.splitlines() if x.lower().startswith(('"lastwar.exe"','"lwbridge.desktop.exe"'))]
 assert not game, "game/clone process not cleaned"
 history={
  "workItem":"LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007-R1",
  "startingLeadCheckpoint":"77e552f1e08e3cf5f67de24f82f5e066f04d0d60",
  "status":"AWAITING_REVIEW","leadAcceptance":"PENDING",
  "live":{"004_completedResource8008":True,"007_positive_staged_stop":False,
          "007_terminal":"cancelled_before_batch_return","R1_additional_launches":0},
  "launch":{"004_completedPlusActiveStop":"retained separate prior 004 runs",
           "007_real_game_attempts":1,"R1_real_game_attempts":0,
           "historicalPreservationFieldSaysZeroIncorrect":True},
  "queries":{"numericCorrections":["M20_pageSize","M21_page"],"correctedOriginalContractScope":"public numeric query only"},
  "proofs":{"sourceNativeHandlers":5,"boundedCallees":len(trace["boundedDirectCallees"]),
            "mountedAppR1Cases":len(mounted["rendered"]),"mapComponent007Cases":10,
            "realCurrentResourceRows":8008,"realPositiveNameKeyRows":2608,
            "originalFullHomeParity":False,"originalMapProtectedProviderParity":False},
  "freshCheckCounts":checkCounts,
  "obligations":queue["obligations"],
  "mutations":[
    {"kind":"structural","count":4,"evidenceClass":"static/provider-guard","claim":"detect missing guard/source shape, not semantic parity"},
    {"kind":"semantic","count":1,"evidenceClass":"production-late-response","claim":"baseline canceled run retained 2 staged rows; corrected durable cancellation removes them"},
    {"kind":"semantic","count":2,"evidenceClass":"public-query-original-contract","claim":"preserved numeric M20 and M21 mismatches and corrections"}
  ],
  "engineCases":engine["cases"],"referenceSha256":EXPECTED_EXE,
  "installedOriginalScriptsPreserved":True,"original004SqliteAndSidecarsPreserved":True,
  "gameProcesses":[],"ownerDesktopActionsR1":0,
  "sourceOriginalState":"native Home partially decoded; encrypted controller missing authenticated matching original package key/CNG owner state",
  "remaining":"r1-2026-10-08/unresolved-dependencies.md"}
 assert scoped_status(history)
 probes=[]
 def rejects(label,edit):
  changed=json.loads(json.dumps(history));edit(changed)
  try:scoped_status(changed)
  except ValueError:probes.append({"case":label,"detected":True})
  else:raise AssertionError("R1 validator accepted dishonest "+label)
 rejects("reopened-ready-R007-01",lambda d:d["obligations"][0].__setitem__("status","READY_PENDING_SEMANTIC_RETRY"))
 rejects("false-live-positive-stage",lambda d:d["live"].__setitem__("007_positive_staged_stop",True))
 rejects("false-no-007-launch",lambda d:d["launch"].__setitem__("007_real_game_attempts",0))
 rejects("drop-page-size-correction",lambda d:d["queries"].__setitem__("numericCorrections",["M21_page"]))
 rejects("structural-as-semantic",lambda d:d["mutations"][0].__setitem__("evidenceClass","original-native-semantic"))
 rejects("staged-after-cancel",lambda d:d["engineCases"][1].__setitem__("stagedAfterStop",2))
 history["validatorMutationProbes"]=probes
 R1.mkdir(exist_ok=True)
 (R1/"closeout.json").write_text(json.dumps(history,indent=2)+"\n",encoding="utf-8")
 print("R1_CLOSEOUT_PRESERVATION_VALIDATION_PASS",len(probes),"dishonest-metadata inverses; native5 real8008 app20 engine4; no new game launches")
if __name__=="__main__":main()
