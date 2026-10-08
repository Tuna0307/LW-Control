"""Derivative R1 matrix; accepted Campaign 007 inventory/history is never rewritten."""
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/"evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007"
OUT=BASE/"r1-2026-10-08"
source=json.loads((BASE/"reference-contract-matrix.json").read_text(encoding="utf-8-sig"))
changes={
 "H03":("R1_ORIGINAL_STOP_CONDITIONAL_NATIVE_BRANCH_TRACED","Original native Stop mismatch guard at 0x199d4b–0x199d6f, original full failure/state ordering remains unknown"),
 "H13":("R1_ORIGINAL_NATIVE_BODIES_AND_LARGE_CALLEE_STATICALLY_TRACED_POLICY_PARTIAL",
        "Available 0.3.17 start/stop/status/reconcile/restart + shared 0x1d5009–0x1ddbb2 native code traced; numeric 0.3.17 retry/timeout not decoded; current table comes from older 0.3.1"),
 "M18":("R1_ACTUAL_ENGINE_SINK_CANCEL_BUG_BASELINE_REPAIRED",
        "Actual controlled production engine+sink+Stop 4 scenarios PASS including positive staged Stop, late batch and publication overlap; 007 live was 0 staged"),
 "M20":("R1_ACCEPTED_ORIGINAL_NUMERIC_PAGE_SIZE_1_TO_200_CORRECTION",
        "Corrected original public numeric pageSize 0/-4->1, 201->200, missing/null default50; wrong types still reject"),
 "M21":("R1_ACCEPTED_ORIGINAL_NUMERIC_PAGE_MINIMUM_CORRECTION",
        "Corrected original public numeric page0/-7->1; wrong type still rejects"),
 "M26":("R1_ACTUAL_MOUNTED_APP_REAL_DATA_A_B_A_AND_RETIRED_A_RESPONSE",
        "Actual headless App mounted with inert bridge and 004 8008-record command playback, profile A8008->B0->A8008 plus deferred retired A page2"),
 "M33":("R1_ACTUAL_MOUNTED_APP_EN_LIGHT_JA_DARK_20_CASES",
        "Full canonical App/Home/Map mounted EN/light and JA/dark with current 004 real Resource command responses; original WebView/runtime visual witness forbidden")
}
for row in source["observations"]:
 if row["id"] in changes:
  status,scope=changes[row["id"]]
  row["r1SourceProofStatus"]=status
  row["r1ProofScope"]=scope
  row["r1OriginalRuntimeEquivalence"]=False
source["r1Derivative"]=True
source["originalCampaign007RecordUntouched"]=True
source["baseline"]="77e552f1e08e3cf5f67de24f82f5e066f04d0d60"
source["source"]="reference-contract-matrix.json"
source["proofScope"]="Derivative annotated obligations, not passed 49 exact equivalence checks"
assert len(source["observations"])==49 and len(changes)==7
assert all(any(r["id"]==k and "r1SourceProofStatus" in r for r in source["observations"]) for k in changes)
OUT.mkdir(exist_ok=True)
(OUT/"reference-contract-matrix-r1.json").write_text(json.dumps(source,indent=2)+"\n",encoding="utf-8")
print("R1_DERIVATIVE_MATRIX_49_OBLIGATIONS_7_SCOPED_ANNOTATIONS_PASS")
