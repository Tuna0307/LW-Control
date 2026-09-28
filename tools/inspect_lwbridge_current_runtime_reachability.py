#!/usr/bin/env python3
"""Inspect current-machine readiness for the authentic LWBridge package/bootstrap path."""
from __future__ import annotations
import argparse,base64,ctypes,hashlib,json,string
from pathlib import Path

BUILD_ID="9BupJXpEgm34lybhNhbbcQ"
HOST_SHA="2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
COMPOSITE_SHA="6a9296268ffeec941a05ae6c723469f7e740c982f70bf142f5a5c37365e21403"
PACKAGE_SHA="a1e23419c25c76bee16942922a643381e0d38a857902569d927f22ef02c99c8d"
LAUNCHER_SHA="8f42adb9ed678445425e529cdee8f12a097c63053f9d4de314a758a8dfe362de"
HOOK_SHA="a8b48120bbe3fb125d50c046acbd4f7c440a4d5e5775b8156fc291d01bd69d2d"
KEY_NAME="{2D337A4D-7E6C-49EF-9486-54F0A00D8A41}"
PROVIDER="Microsoft Software Key Storage Provider"
URLSAFE=set(string.ascii_letters+string.digits+"_-")
class E(ValueError): pass
def req(v,m):
 if not v: raise E(m)
def b64u(s):
 return base64.urlsafe_b64decode(s+"="*((4-len(s)%4)%4))
def sha_file(path:Path)->str:
 return hashlib.sha256(path.read_bytes()).hexdigest()

def cng_key_status():
 if not hasattr(ctypes,"windll"): return {"supported":False}
 n=ctypes.windll.ncrypt
 n.NCryptOpenStorageProvider.argtypes=[ctypes.POINTER(ctypes.c_void_p),ctypes.c_wchar_p,ctypes.c_uint32]
 n.NCryptOpenStorageProvider.restype=ctypes.c_long
 n.NCryptOpenKey.argtypes=[ctypes.c_void_p,ctypes.POINTER(ctypes.c_void_p),ctypes.c_wchar_p,ctypes.c_uint32,ctypes.c_uint32]
 n.NCryptOpenKey.restype=ctypes.c_long
 n.NCryptFreeObject.argtypes=[ctypes.c_void_p]; n.NCryptFreeObject.restype=ctypes.c_long
 p=ctypes.c_void_p(); k=ctypes.c_void_p()
 s1=int(n.NCryptOpenStorageProvider(ctypes.byref(p),PROVIDER,0))
 s2=None
 if s1==0:
  s2=int(n.NCryptOpenKey(p,ctypes.byref(k),KEY_NAME,0,0))
 if k.value: n.NCryptFreeObject(k)
 if p.value: n.NCryptFreeObject(p)
 u1=s1 & 0xffffffff
 u2=(s2 & 0xffffffff) if s2 is not None else None
 return {"supported":True,"provider":PROVIDER,"persistedKeyName":KEY_NAME,
         "providerOpenStatus":f"0x{u1:08X}","keyOpenStatus":f"0x{u2:08X}" if u2 is not None else None,
         "keyExists":s1==0 and s2==0}
def inspect(profile_root:Path, extracted_root:Path):
 challenge_path=profile_root/"authorization.challenge"
 manifest_path=profile_root/"build.manifest"
 ticket_path=profile_root/"authorization.ticket"
 envelope_path=profile_root/"package-key.envelope"
 req(challenge_path.is_file(),"authorization.challenge missing")
 req(manifest_path.is_file(),"build.manifest missing")
 cb=challenge_path.read_bytes()
 req(cb.endswith(b"\n"),"challenge missing LF")
 c=cb[:-1].decode("ascii")
 req(len(c)==43 and all(ch in URLSAFE for ch in c),"challenge format mismatch")
 cdec=b64u(c); req(len(cdec)==32,"challenge decoded length mismatch")
 mt=manifest_path.read_text(encoding="utf-8").strip()
 parts=mt.split("."); req(len(parts)==2,"manifest must have two segments")
 payload=b64u(parts[0]).decode("utf-8")
 sig=b64u(parts[1]); req(len(sig)==64,"manifest signature length mismatch")
 f=payload.split("|"); req(len(f)==10,"manifest field count mismatch")
 expected=[("0","LWBM2"),("1",BUILD_ID),("2","0.3.1"),("3",HOST_SHA),("4",COMPOSITE_SHA),
           ("5",PACKAGE_SHA),("6",LAUNCHER_SHA),("7",HOOK_SHA),("8","1")]
 for idx,w in expected: req(f[int(idx)]==w,f"manifest field {idx} mismatch")
 req(f[9].isdigit(),"manifest field 9 is not decimal")
 req(sha_file(extracted_root/"bridge-scripts.dat")==PACKAGE_SHA,"bridge-scripts.dat hash mismatch")
 req(sha_file(extracted_root/"lwbridge-profile-launcher.exe")==LAUNCHER_SHA,"launcher hash mismatch")
 req(sha_file(extracted_root/"lwbridge-multi-hook.dll")==HOOK_SHA,"multi-hook hash mismatch")
 bundle=json.loads((extracted_root/"xlua-proxy-bundle.json").read_text(encoding="utf-8"))
 req(bundle.get("compositeSha256")==COMPOSITE_SHA,"proxy bundle composite hash mismatch")
 key=cng_key_status()
 return {
  "findingId":"LWB-R8-105",
  "date":"2026-09-27",
  "evidenceStatus":"CURRENT-MACHINE REACHABILITY",
  "profileRuntime":{"path":str(profile_root),
   "authorizationChallenge":{"present":True,"fileBytes":len(cb),"encodedChars":len(c),"decodedBytes":len(cdec),
     "contentsRecorded":False},
   "buildManifest":{"present":True,"fileBytes":manifest_path.stat().st_size,"payloadFields":len(f),
     "format":f[0],"buildId":f[1],"version":f[2],"hostSha256":f[3],"compositeSha256":f[4],
     "packageSha256":f[5],"launcherSha256":f[6],"multiHookSha256":f[7],"field8":f[8],
     "field9Decimal":f[9],"field9Semantics":"not assigned by this checkpoint","signatureBytes":len(sig),
     "fileSha256":sha_file(manifest_path)},
   "authorizationTicket":{"present":ticket_path.is_file()},
   "packageKeyEnvelope":{"present":envelope_path.is_file()}},
  "deviceKey":key,
  "extractedRuntime":{"path":str(extracted_root),"packageHashMatch":True,"launcherHashMatch":True,
    "multiHookHashMatch":True,"proxyCompositeHashMatch":True},
  "reachability":{
    "currentProxyPrerequisitesReady": bool(key.get("keyExists")) and envelope_path.is_file(),
    "currentMissingPrerequisites":[
      *([] if key.get("keyExists") else ["persisted ECDH_P256 device key is not currently openable under the recovered key identity"]),
      *([] if envelope_path.is_file() else ["package-key.envelope is absent"]),
    ],
    "interpretation":"current at-rest prerequisites are incomplete; this current-machine check does not prove that a missing device key cannot be provisioned by the original host lifecycle",
    "observedButNotAssertedAsDirectBlocker":[
      *([] if ticket_path.is_file() else ["authorization.ticket is absent"])
    ],
    "networkRequestsMade":False,
    "privateKeyReadOrExported":False,
  },
  "limits":[
    "build.manifest signature is structurally present but is not cryptographically reverified by this checkpoint",
    "build.manifest field 9 semantics remain unassigned",
    "no package-key value, envelope contents, or assembled Lua source bytes are recovered",
  ],
 }

def main():
 ap=argparse.ArgumentParser()
 ap.add_argument("--profile-runtime-root",type=Path,required=True)
 ap.add_argument("--extracted-runtime-root",type=Path,required=True)
 ap.add_argument("--output",type=Path)
 ap.add_argument("--json",action="store_true")
 a=ap.parse_args()
 try:r=inspect(a.profile_runtime_root,a.extracted_runtime_root)
 except (E,OSError,ValueError,json.JSONDecodeError) as exc:ap.error(str(exc))
 s=json.dumps(r,indent=2)
 if a.output:
  a.output.parent.mkdir(parents=True,exist_ok=True)
  a.output.write_text(s+"\n",encoding="utf-8")
 print(s if a.json else f"finding={r['findingId']} ready={r['reachability']['currentProxyPrerequisitesReady']} missing={len(r['reachability']['currentMissingPrerequisites'])}")
if __name__=="__main__":
 raise SystemExit(main())
