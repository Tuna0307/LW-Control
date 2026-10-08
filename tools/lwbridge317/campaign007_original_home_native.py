"""Hash-gated original 0.3.17 Home command native xref inventory.

Uses accepted original executable and shared PE string/descriptor/xref inspector.
No live original program, proprietary service, protected controller or auth state.
Xrefs are exact bytes DISCOVERY, not proven launcher/recovery policy.
"""
from __future__ import annotations
import argparse,json,sys
from pathlib import Path
HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
from inspect_map_surface import native_surface

HOME_MARKERS=[
 "profile_instance_start","profile_instance_stop",
 "profile_instance_status","profile_instances_reconcile",
 "profile_instances_update_and_restart","profile_list","profile_select",
 "game_root_status","game_root_select","local_game_launch_status",
 "game_recovery_status","local_config_get","local_config_set",
 "set_automation","autoLaunchAll","closeUnmanaged",
 "GAME_OPERATION_IN_PROGRESS","GAME_ROOT_NOT_FOUND",
 "GAME_RUNNING","BRIDGE_HOST_BUSY","gameLaunchBusy",
]
def main():
    ap=argparse.ArgumentParser()
    ap.add_argument("referenceExe",type=Path)
    ap.add_argument("--output",type=Path,required=True)
    args=ap.parse_args()
    native=native_surface(args.referenceExe,HOME_MARKERS)
    rows=[]
    for name,record in native["markers"].items():
        xrefs=record["xrefs"]
        normal=sorted(set(entry.get("functionRva") for entry in xrefs
              if entry.get("functionRva") and entry.get("functionRva") != "0x218023-0x21C5B4"))
        rows.append({
            "name":name,"stringOccurrences":len(record["occurrences"]),
            "xrefCount":len(xrefs),"candidateNonRegistrationHandlers":normal,
            "stringOffsets":[entry["raw"] for entry in record["occurrences"][:6]],
            "xrefInstructionRvas":[entry["instructionRva"] for entry in xrefs[:9]]
        })
    out={
       "workItem":"LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007",
       "evidenceType":"ORIGINAL_HASH_LOCKED_NATIVE_STRING_AND_DISASSEMBLY_XREF_DISCOVERY_ONLY",
       "originalExeSha256":native["sha256"],"originalExePath":native["path"],
       "sourceTool":"tools/lwbridge317/inspect_map_surface.py:native_surface",
       "markers":rows,
       "inferOriginalLauncherTimeoutRetry":"NO: lexical/native handler xrefs do not prove internal retry or timer policy",
       "inferOriginalProtectedController":"NO: encrypted package keys/material unavailable",
       "noOriginalServiceAccess":True
    }
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(out,indent=2)+"\n",encoding="utf-8")
    for row in rows:
        print(f"HOME_R17_MARKER {row['name']} string={row['stringOccurrences']} xref={row['xrefCount']} funcs={','.join(row['candidateNonRegistrationHandlers'][:5])}")
    print("CAMPAIGN007_ORIGINAL_HOME_NATIVE_XREF_DISCOVERY_PASS")

if __name__=="__main__":
    main()
