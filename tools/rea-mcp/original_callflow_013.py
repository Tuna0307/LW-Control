#!/usr/bin/env python3
"""Hash-admitted read-only PE unwind/source-call-flow inventory.

Capstone disassembly and Ghidra pseudocode are independent evidence types;
this inventory intentionally makes no claim that a native call target is the
original scanner provider or that indirect branches are fully recovered.
"""
from __future__ import annotations

from collections import Counter
import hashlib
import json
from pathlib import Path

import capstone
import pefile

ORIGINAL = Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
SHA = "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
ENTRIES = (0x14013A8C1, 0x140143D67, 0x140159925, 0x140427120, 0x140191253)


def inventory() -> dict:
    data = ORIGINAL.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if digest != SHA:
        raise ValueError("original executable hash mismatch")
    pe = pefile.PE(data=data)
    base = pe.OPTIONAL_HEADER.ImageBase
    unwind = {
        base + p.struct.BeginAddress: (base + p.struct.EndAddress)
        for p in pe.DIRECTORY_ENTRY_EXCEPTION
    }
    decoder = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
    decoder.detail = True
    results = []
    for entry in ENTRIES:
        end = unwind.get(entry)
        if end is None:
            raise ValueError(f"no source-backed unwind start {entry:x}")
        b = data[pe.get_offset_from_rva(entry - base):pe.get_offset_from_rva(entry - base) + end - entry]
        lines = list(decoder.disasm(b, entry))
        calls = []
        jumps = []
        returns = []
        indirect = []
        groups = Counter()
        for ins in lines:
            if ins.group(capstone.CS_GRP_CALL):
                group = "call"
            elif ins.group(capstone.CS_GRP_JUMP):
                group = "jump"
            elif ins.group(capstone.CS_GRP_RET):
                group = "return"
            else:
                continue
            groups[group] += 1
            operand = ins.operands[0] if ins.operands else None
            target = (hex(operand.imm) if operand is not None and operand.type == capstone.x86.X86_OP_IMM else None)
            item = {"at": hex(ins.address), "instruction": f"{ins.mnemonic} {ins.op_str}", "target": target}
            if target is None:
                indirect.append(item)
            if group == "call":
                calls.append(item)
            elif group == "jump":
                jumps.append(item)
            else:
                returns.append(item)
        results.append({
            "entry": hex(entry), "unwindEndExclusive": hex(end), "unwindBytes": end-entry,
            "decodedInstructionCount": len(lines), "decodedBytes": sum(ins.size for ins in lines),
            "nonlinearOrUnknownBoundaries": sum(ins.size for ins in lines) != len(b),
            "counts": dict(groups), "calls": calls, "jumps": jumps, "returns": returns,
            "indirectCount": len(indirect), "indirectSites": indirect,
        })
    return {"originalSha256": digest, "format": "x64 PE .pdata bounded and Capstone decoded",
            "evidenceType": "static branch inventory; not verified decompiler value flow", "functions": results}


if __name__ == "__main__":
    destination = Path("artifacts/rea-toolchain/map013-monitored/original-callflow.json")
    destination.parent.mkdir(parents=True, exist_ok=True)
    doc = inventory()
    destination.write_text(json.dumps(doc, indent=2), encoding="utf-8")
    for f in doc["functions"]:
        print(f["entry"], "bytes", f["unwindBytes"], "instructions", f["decodedInstructionCount"],
              "calls", len(f["calls"]), "jumps", len(f["jumps"]), "indirect", f["indirectCount"],
              "decoded_complete", not f["nonlinearOrUnknownBoundaries"])
