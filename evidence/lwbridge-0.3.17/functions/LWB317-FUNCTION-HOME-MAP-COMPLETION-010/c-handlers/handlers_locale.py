#!/usr/bin/env python3
"""Item 8: which Home error codes the ORIGINAL 0.3.17 frontend localizes, and how the clone locale compares.

Hash-gates the extracted original frontend assets (evidence/lwbridge-0.3.17/ui/frontend-package) and reads the
clone locale files read-only. Output: handlers-locale.json (next to this script).
usage: python handlers_locale.py
"""
from __future__ import annotations

import hashlib
import io
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[4]
ASSETS = REPO / "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets"
CLONE = REPO / "src/LWBridge.UI-0.3.17/src/locales"
SHA = {
    "index-BVfnK1wp.js": "44c4e4043825b7db296b64171951b27176f8850ff8d7d337cc991df4765524c6",
    "en-BisSXcTB.js": "0bf43d180eb93692a93b830b5d984e9d01ddeea524340f2334fc63cba527a731",
    "ja-UrbzJu-m.js": "35969183ab623b6701bc850fdf28bcf268c93127ff6c9693f77e588ee7bdbe38",
}
for name, want in SHA.items():
    got = hashlib.sha256((ASSETS / name).read_bytes()).hexdigest()
    assert got == want, f"{name} hash mismatch: {got}"

B = chr(96)          # backtick
BS = chr(92)         # backslash
LANGS = ["en", "zh-CN", "zh-TW", "ja", "ko", "vi", "id", "ru", "pt"]
q = B + "((?:[^" + B + BS + BS + "]|" + BS + BS + ".)*)" + B
entry = re.compile(r"[{,]([A-Za-z_][A-Za-z0-9_]*):\[((?:" + q + r",){8}" + q + r")\]")

idx = (ASSETS / "index-BVfnK1wp.js").read_text(encoding="utf-8")
a = idx.find("Mr={OFFICIAL_LAUNCHER_HOOK_FAILED")
b = idx.find("Nr={", a)
c = idx.find("Pr={", b)
d = idx.find("Object.freeze(Object.keys(Mr))", c)
assert 0 < a < b < c < d
tables = {"error": idx[a:b], "auth.error": idx[b:c], "update.error": idx[c:d]}
parsed = {}
for prefix, seg in tables.items():
    rows = {}
    for m in entry.finditer(seg):
        vals = re.findall(q, m.group(2))
        assert len(vals) == 9
        rows[m.group(1)] = dict(zip(LANGS, vals))
    parsed[prefix] = rows

# lookup algorithm (Ir/Lr in index-BVfnK1wp.js)
lookup_src = idx[idx.find("function Ir(e){"):idx.find("var Rr=(0,b.createContext)(null)")]
assert "match(/\\b[A-Z][A-Z0-9_]{2,}\\b/g)" in lookup_src
assert "[`error`,`auth.error`,`update.error`]" in lookup_src
assert "n=`common.actionFailed`" in lookup_src
conn_map_src = idx[idx.find("var Jr={offline:"):idx.find("function Yr(")]
CONN_KEYS = re.findall(r"([A-Za-z]+):`profile.connection.([A-Za-z]+)`", conn_map_src)


def chunk_value(fname, key):
    t = (ASSETS / fname).read_text(encoding="utf-8")
    m = re.search(r'"?' + re.escape(key) + r'"?\s*:\s*' + B + "((?:[^" + B + "])*)" + B, t)
    return m.group(1) if m else None


def clone_locale(lang):
    t = (CLONE / f"{lang}.js").read_text(encoding="utf-8")
    i = t.find("export default ")
    return json.loads(t[i + len("export default "): t.rfind("}") + 1])


clone = {lang: clone_locale(lang) for lang in ("en", "ja")}
CODES = ["INSTANCE_NOT_OWNED", "RECOVERY_RECORD_INVALID", "RECOVERY_PROCESS_MISMATCH", "RECOVERY_RECORD_NOT_FOUND",
         "STATE_UNAVAILABLE", "GAME_REPAIR_REQUIRED", "GAME_OPERATION_IN_PROGRESS", "GAME_OPERATION_CANCELLED",
         "BRIDGE_DISCONNECTED", "LAUNCH_FAILED", "GAME_CLOSE_FAILED", "GAME_CLOSE_TIMEOUT", "UNMANAGED_GAME_RUNNING"]
EXTRA = ["GAME_ROOT_NOT_FOUND", "INVALID_GAME_ROOT", "GAME_RUNNING", "PROFILE_ALREADY_RUNNING", "PROFILE_RUNNING",
         "PROFILE_NOT_FOUND", "PROFILE_LOCKED", "INVALID_PROFILE_ORDER", "INVALID_PROFILE_NOTE", "INVALID_REQUEST",
         "GAME_DISCONNECTED", "BRIDGE_START_TIMEOUT", "PROFILE_LIMIT_REACHED", "PROFILE_ID_REQUIRED",
         "PROFILE_RUNTIME_UNAVAILABLE", "INVALID_PROFILE_ID", "INSTANCE_MISMATCH", "INSTANCE_NOT_FOUND",
         "SERVER_MAINTENANCE", "INVALID_PAYLOAD", "AUTOMATION_NOT_IMPLEMENTED", "BRIDGE_HOST_BUSY"]
FALLBACK_KEY = "common.actionFailed"
out = {"assetHashes": SHA, "languageOrder": LANGS,
       "lookup": "Lr(t,error,fallback='common.actionFailed'): codes = error.code + every [A-Z][A-Z0-9_]{2,} token in the "
                 "error message (deduplicated, reversed); for each code try error.<C>, auth.error.<C>, update.error.<C>; first "
                 "translated hit wins; otherwise t(fallback)",
       "fallbackText": {"en": chunk_value("en-BisSXcTB.js", FALLBACK_KEY), "ja": chunk_value("ja-UrbzJu-m.js", FALLBACK_KEY)},
       "cloneFallbackText": {"en": clone["en"][FALLBACK_KEY], "ja": clone["ja"][FALLBACK_KEY]},
       "connectionStateMap": CONN_KEYS, "codes": {}}
for code in CODES + EXTRA:
    hit = [p for p, rows in parsed.items() if code in rows]
    row = {"originalUiTables": hit}
    if "error" in hit:
        row["original"] = {"en": parsed["error"][code]["en"], "ja": parsed["error"][code]["ja"]}
    row["cloneHasKey"] = {"en": ("error." + code) in clone["en"], "ja": ("error." + code) in clone["ja"]}
    if row["cloneHasKey"]["en"]:
        row["clone"] = {"en": clone["en"]["error." + code], "ja": clone["ja"]["error." + code]}
        assert "error" in hit, f"{code} localized in the clone but not in the original table"
        assert row["clone"] == row.get("original"), f"{code} text differs between original and clone"
    row["resultingTextWhenUnlocalized"] = None if hit else "fallback common.actionFailed"
    out["codes"][code] = row

# parity of the whole error.* block
orig_err = {("error." + k): v for k, v in parsed["error"].items()}
clone_err = {k: v for k, v in clone["en"].items() if k.startswith("error.")}
out["errorBlockParity"] = {
    "originalCount": len(orig_err), "cloneCount": len(clone_err),
    "onlyInOriginal": sorted(set(orig_err) - set(clone_err)), "onlyInClone": sorted(set(clone_err) - set(orig_err)),
    "enTextDiffs": sorted(k for k in set(orig_err) & set(clone_err) if orig_err[k]["en"] != clone_err[k]),
}
conn_keys = sorted(f"profile.connection.{k}" for _, k in CONN_KEYS)
out["connectionKeysInClone"] = {k: (k in clone["en"]) for k in conn_keys + ["profile.connection.maintenance"]}
for code in ("INSTANCE_NOT_OWNED", "RECOVERY_RECORD_INVALID", "RECOVERY_PROCESS_MISMATCH", "RECOVERY_RECORD_NOT_FOUND",
             "STATE_UNAVAILABLE", "GAME_REPAIR_REQUIRED", "GAME_OPERATION_IN_PROGRESS", "GAME_OPERATION_CANCELLED",
             "BRIDGE_DISCONNECTED", "GAME_CLOSE_FAILED"):
    assert out["codes"][code]["originalUiTables"] == [], code
for code in ("LAUNCH_FAILED", "GAME_CLOSE_TIMEOUT", "UNMANAGED_GAME_RUNNING"):
    assert out["codes"][code]["originalUiTables"] == ["error"], code
(HERE / "handlers-locale.json").write_text(json.dumps(out, ensure_ascii=False, indent=2), encoding="utf-8")
print("OK locale:", {k: v["originalUiTables"] for k, v in out["codes"].items() if k in CODES})
