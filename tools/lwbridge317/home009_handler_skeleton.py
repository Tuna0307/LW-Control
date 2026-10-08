"""Ordered static skeleton (string literals, IAT imports, direct callees) of original handler functions.

Usage: home009_handler_skeleton.py <rva> [...]   Prints address-ordered facts only; it does NOT decode
state-machine semantics. Hash-gated through home009_native (reference/lwbridge-0.3.17.exe).
"""
import re, sys
sys.path.insert(0, str(__import__("pathlib").Path(__file__).resolve().parent))
import home009_native as N

for a in sys.argv[1:]:
    f = N.func_of(int(a, 0))
    print(f"## fn {f[0]:#x}-{f[1]:#x} ({f[1]-f[0]:#x} bytes)")
    seen_call = {}
    for i in N.dis_range(*f):
        line = N.annotate(i)
        m = re.search(r"str='(.*)'$", line)
        if m:
            print(f"{i.address-N.BASE:#x} STR {m.group(1)!r}")
        m = re.search(r"IAT=(\S+)", line)
        if m:
            print(f"{i.address-N.BASE:#x} IAT {m.group(1)}")
        if i.mnemonic == "call" and "-> " in line:
            t = line.split("-> ")[1].split()[0]
            seen_call.setdefault(t, i.address-N.BASE)
    print("first-call sites:", ", ".join(f"{t}@{s:#x}" for t, s in sorted(seen_call.items(), key=lambda x: x[1])))
