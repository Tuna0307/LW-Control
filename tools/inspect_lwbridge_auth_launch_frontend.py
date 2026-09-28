#!/usr/bin/env python3
"""Verify original LWBridge 0.3.1 frontend auth -> launch orchestration."""
from __future__ import annotations
import argparse, hashlib, json
from pathlib import Path

API_SHA = "062ebd2362885af05e07bf4f0de00ed833293a83fefa6b54c26975fa1db9df47"
INDEX_SHA = "4d18cbc036a417b1a13a2c9905f1abc5dda1427531ad1ab4f7b03f4b269704b3"

class InspectError(ValueError): pass

def req(ok: bool, msg: str) -> None:
    if not ok: raise InspectError(msg)

def digest(p: Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()

def inspect(api_path: Path, index_path: Path) -> dict[str, object]:
    req(digest(api_path) == API_SHA, "reference API hash drifted")
    req(digest(index_path) == INDEX_SHA, "reference index hash drifted")
    api = api_path.read_text(encoding="utf-8")
    idx = index_path.read_text(encoding="utf-8")
    for s in [
        'function Y(){return U(`auth_state`)}',
        'function X(){return U(`auth_credentials`)}',
        'function ie(e,t){return U(`auth_login`,{username:e,password:t})}',
        'function ae(e,t,n){return U(`auth_activate`,{username:e,password:t,licenseCode:n})}',
        'function oe(e){return U(`auth_renew`,{licenseCode:e})}',
    ]: req(s in api, f"missing API contract: {s}")
    # Connect minified API exports to index imports used by AuthProvider.
    req('ye as W' in api and 'W as ee' in idx, "multi_entitlement_get alias chain drifted")
    req('_e as _t' in api and '_t as re' in idx, "profile_instances_reconcile alias chain drifted")
    req('ie as ut' in api and 'ut as je' in idx, "auth_login alias chain drifted")
    req('Y as O' in api and 'O as p' in idx, "auth_state alias chain drifted")
    for s in [
        'var Vt=`lwbridge.autoLaunchGame`;',
        'function Ht(e=localStorage){return e.getItem(Vt)!==`false`}',
        'function Ut(e=localStorage,t){e.setItem(Vt,String(t))}',
        'async function Wt(e,t,n){try{await t()}catch{}return n(e)}',
        'async function Gt(e,t,n){(e.phase===`authorized`||e.phase===`grace`)&&await n(t)}',
        'login:(e,t)=>b(()=>je(e,t),!0)',
        'credentials:async()=>{c(null);try{return await r()}',
        'e.credentials().then(e=>{c(t=>t||e.username),u(t=>t||e.password)}).catch(()=>void 0)',
        'label:`auth.autoLaunchGame`,checked:e.autoLaunchGame,onChange:e.setAutoLaunchGame',
    ]: req(s in idx, f"missing frontend orchestration: {s}")
    req('minLength:3,maxLength:50,required:!0' in idx, "username form bounds drifted")
    req('minLength:8,maxLength:72,required:!0' in idx, "password form bounds drifted")
    return {
      "findingId":"LWB-R8-116", "date":"2026-09-27", "status":"RECOVERED FRONTEND ORCHESTRATION",
      "sourceIdentity":{"api":{"path":str(api_path),"sha256":API_SHA},"index":{"path":str(index_path),"sha256":INDEX_SHA}},
      "login":{"command":"auth_login","request":{"username":"string","password":"string"},"postAuthLaunch":True},
      "credentialsPrefill":"auth screen requests auth_credentials and fills only currently-empty username/password state",
      "autoLaunch":{"storageKey":"lwbridge.autoLaunchGame","default":True,"disabledOnlyWhenStoredString":"false"},
      "authorizedOrGraceFlow":["best-effort multi_entitlement_get","profile_instances_reconcile(autoLaunchAll=<setting>)"],
      "formBounds":{"username":{"minLength":3,"maxLength":50},"password":{"minLength":8,"maxLength":72}},
      "limits":["does not read credential contents","does not make auth network requests","does not invoke auth_login","does not claim successful authorization or Map source recovery","Map remains NOT WORKING"]
    }

def main() -> int:
    ap=argparse.ArgumentParser(); ap.add_argument("api",type=Path); ap.add_argument("index",type=Path); ap.add_argument("--output",type=Path)
    a=ap.parse_args()
    try: r=inspect(a.api,a.index)
    except (InspectError,OSError,UnicodeError) as e: ap.error(str(e))
    s=json.dumps(r,indent=2)+"\n"
    if a.output: a.output.parent.mkdir(parents=True,exist_ok=True); a.output.write_text(s,encoding="utf-8")
    print(s,end=""); return 0
if __name__=="__main__": raise SystemExit(main())
