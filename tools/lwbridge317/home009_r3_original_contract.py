"""R3 source-byte and instruction assertions for original Home record/reconcile.

Static only: imports SHA-gated original reference PE; never executes it.
"""
from __future__ import annotations

import json
import re

import home009_native as n

BASE = n.BASE


def instructions(start: int, end: int):
    return {i.address - BASE: i for i in n.dis_range(start, end)}


def check() -> dict:
    serializer = instructions(0x2DC830, 0x2DCA00)
    # The field offsets are relative to the record pointer in RSI. The
    # in-memory record is 0xA8 bytes; JSON spelling is byte-exact.
    fields = (
        ("schemaVersion", 0x2DC872, 13, "+0xa1"),
        ("profileId", 0x2DC894, 9, "+0x10"),
        ("instanceId", 0x2DC8BB, 10, "+0x28"),
        ("encryptedPipeToken", 0x2DC8E2, 18, "+0x40"),
        ("pid", 0x2DC90C, 3, "+0x90"),
        ("processCreatedAt", 0x2DC92F, 16, "+0x00"),
        ("gameExecutable", 0x2DC959, 14, "+0x58"),
        ("buildId", 0x2DC988, 7, "+0x78"),
        ("startedAt", 0x2DC9B2, 9, "+0x98"),
        ("leaseRequired", 0x2DC9DC, 13, "+0xa0"),
    )
    result = []
    for field, address, size, offset in fields:
        ins = serializer[address]
        assert ins.mnemonic == "lea" and ins.op_str.startswith("rdx, [rip + "), (field, ins)
        match = re.search(r"0x[0-9a-f]+", ins.op_str)
        assert match is not None
        string_rva = address + ins.size + int(match.group(), 16)
        assert n.pe.get_data(string_rva, size) == field.encode(), (field, hex(string_rva))
        result.append({"key": field, "recordOffset": offset, "stringRva": hex(string_rva)})
    # Reconcile calls a validator; only its return == 1 is accepted.
    reconcile = instructions(0x203021, 0x2057DC)
    assert reconcile[0x203D8B].mnemonic == "call"
    assert int(reconcile[0x203D8B].op_str, 16) - BASE == 0x2DC61A
    assert reconcile[0x203D90].mnemonic == "cmp" and reconcile[0x203D90].op_str == "al, 1"
    assert reconcile[0x203D92].mnemonic == "jne"
    assert int(reconcile[0x203D92].op_str, 16) - BASE == 0x203E59
    validator = instructions(0x2DC61A, 0x2DC830)
    checks = {
        0x2DC6AA: "record gameExecutable path +0x60",
        0x2DC6D9: "normalized case-insensitive path length",
        0x2DC6F8: "case-insensitive path bytes",
        0x2DC7D0: "has processCreatedAt",
        0x2DC7D9: "has pid",
        0x2DC7E2: "record pid +0x94",
        0x2DC7EF: "expected process executable probe",
        0x2DC7FC: "process creation timestamp equality",
        0x2DC807: "record buildId len",
        0x2DC81B: "record buildId byte equality",
        0x2DC828: "final result bool",
    }
    for address in checks:
        assert address in validator, (hex(address), checks[address])
    assert validator[0x2DC7EF].mnemonic == "call"
    assert int(validator[0x2DC7EF].op_str, 16) - BASE == 0x41E3A2
    assert validator[0x2DC7D0].mnemonic == "cmp"
    assert validator[0x2DC7D9].mnemonic == "cmp"
    # Launch failure result is projected into a JSON string, not a numeric
    # result or arbitrary object. Code-vs-message depends on the producer.
    assert reconcile[0x2055DB].mnemonic == "call"
    assert int(reconcile[0x2055DB].op_str, 16) - BASE == 0x31EEB2
    string_clone = instructions(0x31EEB2, 0x31EF22)
    assert string_clone[0x31EEBB].op_str == "rsi, qword ptr [rdx + 0x10]"
    assert string_clone[0x31EED1].op_str == "rbx, qword ptr [rdx + 8]"
    assert string_clone[0x31EEFF].op_str == "byte ptr [rdi], 3"
    return {"referenceSha256": "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783",
            "mode": "static PE disassembly only", "recordJsonFields": result,
            "reconcileValidator": {"rva": "0x2dc61a", "acceptedReturn": 1,
                "errorWhenNotOne": "RECOVERY_PROCESS_MISMATCH",
                "required": list(checks.values())},
            "launchFailureErrorValue": "serde JSON string cloned from launch-error structure at 0x2055db; code/message origin not yet decoded"}


if __name__ == "__main__":
    print(json.dumps(check(), indent=2))
