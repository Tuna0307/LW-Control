"""Verify rgmd_token.decode_token on every token operand in Assembly-CSharp.rdl."""
import json, sys
from collections import Counter, defaultdict
from pathlib import Path
HERE = Path(__file__).parent
sys.path.insert(0, str(HERE.parent / "map_009_offline"))
from rgmd_il import MetadataImage
from dnfile import stream
from rgmd_token import decode_token, encode_token

img = MetadataImage.load(Path.home() / "AppData/Local/FunFly/Last War-Survival Game/Game/LastWar_Data/Assemblies/Assembly-CSharp.rdl")
T = img.tables
sizes = {1: "TypeRef", 2: "TypeDef", 4: "Field", 6: "MethodDef", 0xA: "MemberRef", 0x1B: "TypeSpec",
         0x2B: "MethodSpec", 0x11: "StandAloneSig"}
sizes = {k: len(getattr(T, v).rows) for k, v in sizes.items()}
us = next(s for s in img.streams if isinstance(s, stream.UserStringHeap)).__data__
starts = set(); p = 1
while p < len(us):
    b = us[p]
    if b & 0x80 == 0: n = b; h = 1
    elif b & 0xC0 == 0x80: n = ((b & 0x3F) << 8) | us[p + 1]; h = 2
    else: n = ((b & 0x1F) << 24) | (us[p + 1] << 16) | (us[p + 2] << 8) | us[p + 3]; h = 4
    starts.add(p); p += h + (n >> 1)
EXPECT = {
    "ldfld": {4, 0xA}, "ldflda": {4, 0xA}, "stfld": {4, 0xA}, "ldsfld": {4, 0xA}, "stsfld": {4, 0xA}, "ldsflda": {4, 0xA},
    "call": {6, 0xA, 0x2B}, "callvirt": {6, 0xA, 0x2B}, "newobj": {6, 0xA}, "ldftn": {6, 0xA, 0x2B}, "ldvirtftn": {6, 0xA, 0x2B},
    "ldstr": {0x70},
}
TYPEOPS = {1, 2, 0x1B}
raw = json.load(open(HERE / "all_operands.json"))
stat = defaultdict(Counter)
for mi, off, op, e in raw:
    t = decode_token(e)
    assert encode_token(t) == e
    tab, row = t >> 24, t & 0xFFFFFF
    exp = EXPECT.get(op, TYPEOPS if op != "ldtoken" else TYPEOPS | {4, 6, 0xA, 0x2B})
    if tab not in exp:
        stat[op][f"badtable_{tab:x}"] += 1
    elif tab == 0x70:
        stat[op]["ok" if row in starts else "badstr"] += 1
    elif not (1 <= row <= sizes[tab]):
        stat[op]["oob"] += 1
    elif tab == 0xA:
        sig0 = bytes(T.MemberRef.rows[row - 1].Signature.value)[0]
        isfield = sig0 == 0x06
        expf = op in ("ldfld", "ldflda", "stfld", "ldsfld", "stsfld", "ldsflda")
        stat[op]["ok_memberref" if isfield == expf or op == "ldtoken" else "memberref_kind_mismatch"] += 1
    else:
        stat[op]["ok"] += 1
for op, c in sorted(stat.items()):
    print(op, dict(c))
