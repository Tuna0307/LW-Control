"""Scan all method bodies; histogram table-codes (top byte of ror(enc,5)) per opcode."""
import sys, json
from collections import Counter, defaultdict
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "map_009_offline"))
from rgmd_il import MetadataImage
from dncil.cil.body.reader import read_method_body_from_bytes

ASM = Path.home() / "AppData/Local/FunFly/Last War-Survival Game/Game/LastWar_Data/Assemblies/Assembly-CSharp.rdl"
img = MetadataImage.load(ASM)
ror = lambda v, n: ((v >> n) | (v << (32 - n))) & 0xFFFFFFFF
TOKOPS = {"call", "callvirt", "newobj", "ldfld", "ldflda", "stfld", "ldsfld", "ldsflda", "stsfld", "ldtoken",
          "castclass", "isinst", "box", "unbox", "unbox.any", "newarr", "initobj", "constrained.", "sizeof", "ldstr",
          "ldelem", "stelem", "ldelema", "ldftn", "ldvirtftn", "jmp", "ldobj", "stobj", "cpobj", "mkrefany",
          "refanyval", "calli"}
hist = defaultdict(Counter)
raw = []
for mi, m in enumerate(img.tables.MethodDef.rows, 1):
    if m.Rva == 0:
        continue
    try:
        off = img.rva_to_offset(m.Rva)
        b = bytearray(img.data[off:off + 0x40000])
        if b and (b[0] & 3) in (0, 1):
            b[0] |= 2
        body = read_method_body_from_bytes(bytes(b))
    except Exception:
        continue
    for insn in body.instructions:
        if insn.opcode.name in TOKOPS and insn.operand is not None and hasattr(insn.operand, "value"):
            e = insn.operand.value
            x = ror(e, 5)
            hist[insn.opcode.name][x >> 24] += 1
            raw.append((mi, insn.offset, insn.opcode.name, e))
for op, c in sorted(hist.items()):
    print(op, {hex(k): v for k, v in c.most_common(12)})
json.dump(raw, open(Path(__file__).with_name("all_operands.json"), "w"))
