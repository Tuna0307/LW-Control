"""Read-only source-attributed native AOI subscription and LOD gate.

The producer can report a target tile and moved logical camera while
_curViewIndex still contains the prior region. A target echo is not a
source-acquisition proof. Keep rejection independent of event records.
"""
from __future__ import annotations

import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
LATEST="artifacts/map-013-live/native-causal-20261013-235042"
SEQUENCE=("native-first-region.json", "sequence-baseline-2.json",
          "sequence-baseline-3.json", "sequence-region-1.json",
          "sequence-region-2.json")
ZOOM="artifacts/map-013-live/native-causal-20261013-210353/zoom-pair-02-zoom.json"


def classify(doc: dict):
    count=doc.get("blockCount")
    cell=doc.get("blockSize")
    tx,ty=doc.get("targetTileX"),doc.get("targetTileY")
    observed=doc.get("requestedIndices")
    issues=[]
    target=None
    if (not isinstance(count,int) or count<=0 or not isinstance(cell,int) or cell<=0 or
        not isinstance(tx,int) or not isinstance(ty,int) or not 0<=tx<1000 or not 0<=ty<1000):
        issues.append("native_geometry_or_target_missing")
    else:
        target=ty//cell*count+tx//cell
    if not isinstance(observed,list) or not observed:
        issues.append("native_subscription_empty")
        observed=[]
    elif len(observed)!=len(set(observed)):
        issues.append("duplicate_native_subscriptions")
    if target is not None and target not in observed:
        issues.append("native_subscription_excludes_target")
    if (doc.get("serverLod"),cell,count)!=(0,10,100):
        issues.append("not_required_native_lod0")
    if doc.get("zoomFinalServerLod") is not None and (
        doc.get("zoomFinalServerLod"),doc.get("zoomFinalBlockSize"),
        doc.get("zoomFinalBlockCount"))!=(0,10,100):
        issues.append("post_zoom_coarse_native_lod")
    if doc.get("state")!="proven" or doc.get("map013NativeReplyState")!="matched":
        issues.append("not_fresh_source_matched")
    # An intentional intermediate sequence keep is not camera-restored.
    # It can be an acquisition-only observation but can NEVER qualify
    # for standalone production without the exact final group restoration.
    if doc.get("map013PositionRetained") is not True and (
        doc.get("cameraGeometryRestored") is not True or doc.get("cameraTileStable") is not True):
        issues.append("camera_not_restored")
    return {
        "targetNativeAoi":target,
        "nativeSubscribedAois":len(observed),
        "subscribedTargetPresent":target is not None and target in observed,
        "currentNativeAoiMin":min(observed) if observed else None,
        "currentNativeAoiMax":max(observed) if observed else None,
        "nativeLod":doc.get("serverLod"),"nativeZoomLod":doc.get("zoomFinalServerLod"),
        "nativeZoomBlockSize":doc.get("zoomFinalBlockSize"),
        "sourceReplyState":doc.get("map013NativeReplyState"),
        "issues":sorted(set(issues)),
    }


def main():
    results=[]
    for name in SEQUENCE:
        d=json.loads((ROOT/LATEST/name).read_text(encoding="utf-8"))
        results.append({"receipt":name,**classify(d)})
    d=json.loads((ROOT/ZOOM).read_text(encoding="utf-8"))
    results.append({"receipt":"historical-ZoomMax",**classify(d)})
    print(json.dumps({"candidate":"post-lens native consecutive source experiment",
                      "notAnAcceptedFasterScanner":True,
                      "result":"second region remains unsubscribed to its target; no fresh view reply; exact restoration unconfirmed",
                      "sourceBackedClassifications":results},indent=2))


if __name__=="__main__":
    main()
