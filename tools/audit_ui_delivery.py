"""Read-only release audit against accepted frontend and imported candidate source."""
from pathlib import Path
from subprocess import run
import json

research = Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW-Control")
candidate = Path(r"C:\Users\chimw\.codex\worktrees\home-feature-release\LW-Control")
ui_prefix = "src/LWBridge.UI-0.3.17/"
accepted = "75b7b215679a8ea7986af72bdbdd7039c5278b70"
imported = "c8acbf38fa387bc3b8236d0615a6cc832eb4478a"

def git(*args):
    return run(["git","-C",str(research),*args],capture_output=True,check=True).stdout

def files_at(rev):
    return git("ls-tree","-r","--name-only",rev,"--",ui_prefix).decode().splitlines()

def blob(rev, f):
    r = run(["git","-C",str(research),"show",f"{rev}:{f}"],capture_output=True)
    return r.stdout if r.returncode==0 else None

def norm(b, f):
    if b is None or f.lower().endswith((".png", ".jpg", ".jpeg", ".ico", ".webp", ".gif")):
        return b
    return b.replace(b"\r\n", b"\n")

files=sorted(set(files_at(accepted))|set(files_at(imported)))
changes=[]
missing=[]
out_of_sync=[]
for f in files:
    before, after = blob(accepted,f),blob(imported,f)
    current_path=candidate / f
    current=current_path.read_bytes() if current_path.exists() else None
    if after is not None and norm(after,f)!=norm(current,f):
        out_of_sync.append(f)
    if current is None and after is not None:
        missing.append(f)
    if norm(before,f)!=norm(after,f):
        changes.append({"path":f,"kind":"added" if before is None else "deleted" if after is None else "modified",
                        "candidate_matches_import":norm(after,f)==norm(current,f)})
verification_manifests = {ui_prefix+"package.json", ui_prefix+"package-lock.json"}
expected = sorted(x for x in out_of_sync if x in verification_manifests)
unexpected = sorted(x for x in out_of_sync if x not in verification_manifests)
print(json.dumps({"accepted_file_count":len(files_at(accepted)), "imported_file_count":len(files_at(imported)),
"post_accepted_changes":changes,"missing_imports":missing,
"intentional_verification_dependency_updates":expected,
"unexpected_candidate_import_mismatches":unexpected},indent=2))
if missing or unexpected:
    raise SystemExit("Original product import mismatch; audit failed")
