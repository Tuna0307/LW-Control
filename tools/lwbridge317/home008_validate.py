"""008 independent read-only preservation/proof-scope validator.
Only writes new 008 closeout.json; accepted earlier packets are immutable.
"""
from pathlib import Path
import copy,hashlib,json,sqlite3,subprocess,os
ROOT=Path(__file__).resolve().parents[2]
E=ROOT/"evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-NATIVE-SEMANTICS-008"
R1=ROOT/"evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007"
EXE=Path(__file__).resolve().parents[2] / "reference" / "lwbridge-0.3.17.exe"
ORIGINAL="4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
INSTALLED=Path(r"C:\Users\chimw\AppData\LocalLow\FunFly\Last War-Survival Game\lwScripts")
TRIPLET={
 "LWScripts.data":"248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22",
 "LWScripts.txt":"d2f9bcc165f41427d4b3b7b9243e4a483bf316519a16b4ce9be658ba9afa783a",
 "version.txt":"785f3ec7eb32f30b90cd0fcf3657d388b5ff4297f2f9716ff66e9b69c05ddd09"}
def sha(p):
 h=hashlib.sha256()
 with p.open("rb") as f:
  for c in iter(lambda:f.read(1024*1024),b""):h.update(c)
 return h.hexdigest()
def load(p):return json.loads(p.read_text(encoding="utf-8-sig"))
def suite(name,count):
 log=(E/f"checks-{name}.txt").read_text(encoding="utf-8-sig")
 n=sum("status=PASS exit=0" in s for s in log.splitlines())
 assert n==count,(name,n,count)
 assert "status=FAIL" not in log
 return n
def honesty(r):
 assert r["status"]=="AWAITING_REVIEW"
 assert not r["originalFullParity"] and not r["originalRuntimeExecuted"]
 assert r["fixedHomeCodeCount"]==0
 assert r["closeNativeNominalWaitMs"]==10000
 assert r["autoLaunchAll"]["absent"] and not r["autoLaunchAll"]["explicitFalse"]
 assert r["nativeFullRecoveryRetryTableKnown"] is False
 assert r["availableStaticRecoveryRemaining"]>0
 assert r["newGameLaunches"]==0 and r["desktopActions"]==0
 assert r["acceptedMapRegressionPassed"] is True
 return True
def main():
 assert sha(EXE)==ORIGINAL,"original reference hash changed"
 assert all(sha(INSTALLED/name)==digest for name,digest in TRIPLET.items()),"installed protected originals changed"
 close=load(E/"close-contract.json");start=load(E/"startup-contract.json")
 assert close["referenceSha256"]==ORIGINAL and len(close["originalMethods"])==2
 assert {x["nominalBudgetMilliseconds"] for x in close["originalMethods"]}=={10000}
 assert start["sha256"]==ORIGINAL and start["launch"]["registryWait"]["deadlineMilliseconds"]==90000
 assert start["launch"]["fiveSecondDeadline"]["deadlineSeconds"]==5
 prod=load(E/"production-reconcile-inert.json")
 assert prod["status"]=="PASS" and len(prod["cases"])==8 and prod["gameActions"]==0
 assert all(x["expectedStart"]==(x["actualStarts"]==1) and x["onceOnly"] for x in prod["cases"])
 n={x:suite(x,y) for x,y in (("package",3),("native",13),("frontend",3),("static",6))}
 pkg=(E/"checks-package.txt").read_text(encoding="utf-8-sig")
 assert len([l for l in pkg.splitlines() if l.startswith("asset ") and l.endswith(" PASS")])==4
 assert "packaged lua/source digest " in pkg
 obs=load(E/"semantic-obligations.json")
 assert obs["fullOriginalHomeParity"] is False and obs["noProductChanges"] is True
 assert len(obs["items"])==10
 ready=[item for item in obs["items"] if item["status"]=="READY_STATIC_NATIVE_RECOVERY_INCOMPLETE"]
 assert len(ready)>=3
 # Check fully accepted earlier engine/Map source unchanged from lead checkpoint.
 diff=subprocess.run(["git","diff","--name-only","3d2fd106eb06110f4993dc74e845521297b1c5b6","HEAD","--",
                     "src/LWBridge.Desktop","src/LWBridge.Map-0.3.17","src/LWBridge.UI-0.3.17"],
                    cwd=ROOT,text=True,capture_output=True,check=True)
 assert diff.stdout.strip()=="", "unexpected Home/Map production modification: "+diff.stdout
 historical=subprocess.run(["git","diff","--name-only","3d2fd106eb06110f4993dc74e845521297b1c5b6","HEAD","--",str(R1.relative_to(ROOT))],
                           cwd=ROOT,text=True,capture_output=True,check=True)
 assert not historical.stdout.strip(),"historical Campaign007 evidence modified"
 safe=load(R1/"safe-real-db-snapshot.json")
 db=Path(safe["originalSourcePath"]);copied=Path(safe["snapshotPath"])
 assert sha(db)==safe["sourceBytesSha256BeforeAfter"] and sha(copied)==safe["snapshotSha256"]
 for suffix,item in safe["originalSidecarHashes"].items():
  if suffix.startswith("-"):
   side=Path(str(db)+suffix)
   assert side.is_file() and sha(side)==item["sha256"]
 with sqlite3.connect(copied.resolve().as_uri()+"?mode=ro",uri=True) as c:
  c.execute("PRAGMA query_only=ON")
  assert c.execute("SELECT count(*) FROM map_records WHERE kind='resource' AND server_id=2212").fetchone()[0]==8008
  assert c.execute("PRAGMA integrity_check").fetchone()[0]=="ok"
 ps=subprocess.run(["tasklist","/FO","CSV","/NH"],capture_output=True,text=True,check=True)
 active=[s for s in ps.stdout.splitlines() if s.lower().startswith(('"lastwar.exe"','"lwbridge.desktop.exe"'))]
 assert active==[],"unexpected live game/bridge process present"
 assert not (Path(os.environ["TEMP"])/"LWB317-008-PACKAGE").exists(),"package temp not removed"
 assert not list(Path(os.environ["TEMP"]).glob("LWB317-HOME008-*")),"test temp not removed"
 current={"status":"AWAITING_REVIEW","workItem":"LWB317-FUNCTION-HOME-NATIVE-SEMANTICS-008",
   "startingCheckpoint":"3d2fd106eb06110f4993dc74e845521297b1c5b6",
   "originalFullParity":False,"originalRuntimeExecuted":False,"nativeFullRecoveryRetryTableKnown":False,
   "closeNativeNominalWaitMs":10000,"autoLaunchAll":{"absent":True,"nonBool":True,"explicitFalse":False},
   "decodedNativeBoundaries":["two 100x100ms original async close waits","startup autoLaunchAll JSON bool fallback",
   "5s/100ms selected launch-poll bound","90s/250ms named-pipe registry bound"],
   "fixedHomeCodeCount":0,"availableStaticRecoveryRemaining":len(ready),
   "newGameLaunches":0,"desktopActions":0,"acceptedMapRegressionPassed":True,
   "checks":n,"protectedOriginalExeAndScriptsPreserved":True,"archived004ResourceRows":8008,
   "historicalCampaign007AndR1EvidenceUntouched":True,"cleanupOk":True,
   "scope":"native opcode/dataflow + isolated production adaptation tests; original full recovery UNKNOWN"}
 assert honesty(current)
 rejects=[]
 def mutation(label,change):
  altered=copy.deepcopy(current);change(altered)
  try:honesty(altered)
  except AssertionError:rejects.append(label)
  else:raise AssertionError("dishonest closeout accepted "+label)
 mutation("false-Home-full-parity",lambda d:d.__setitem__("originalFullParity",True))
 mutation("false-0.3.17-backoff-table-known",lambda d:d.__setitem__("nativeFullRecoveryRetryTableKnown",True))
 mutation("false-close-15-seconds",lambda d:d.__setitem__("closeNativeNominalWaitMs",15000))
 mutation("lost-recoverable-native-work",lambda d:d.__setitem__("availableStaticRecoveryRemaining",0))
 mutation("false-original-runtime",lambda d:d.__setitem__("originalRuntimeExecuted",True))
 mutation("accepted-Map-regression-erased",lambda d:d.__setitem__("acceptedMapRegressionPassed",False))
 current["metadataNegativeProbes"]=rejects
 (E/"closeout.json").write_text(json.dumps(current,indent=2)+"\n",encoding="utf-8")
 print("HOME008_PRESERVATION_SCOPE_VALIDATOR_PASS native13 static6 frontend3 package3; negatives6; original unchanged")
if __name__=="__main__":main()
