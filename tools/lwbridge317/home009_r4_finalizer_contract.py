"""HOME009 continuation: offline source-byte gates for finalizer/error projection.

Uses the immutable, hash-gated 0.3.17 PE; does not run original code or
protected service. A frame state or call instruction does not establish the
success/result of an external lease API.
"""
import json
import struct
import home009_native as n

BASE = n.BASE


def insns(start, end):
    return {i.address - BASE: i for i in n.dis_range(start, end)}


def call_at(ins, site, target):
    x = ins[site]
    assert x.mnemonic == "call" and int(x.op_str, 16) - BASE == target, (hex(site), x)
    return {"site": hex(site), "callee": hex(target)}


def inspect():
    finalizer = insns(0x1DDDE3, 0x1DE6F0)
    reconcile = insns(0x203021, 0x2057DC)
    dispatch = finalizer[0x1DDE18]
    assert dispatch.mnemonic == "movzx"
    assert dispatch.op_str == "eax, byte ptr [rdx + 0xe8]"
    assert finalizer[0x1DDE26].mnemonic == "movsxd"
    table = 0x83DF8C
    branch_destinations = {
        state: table + struct.unpack("<i", n.pe.get_data(table + state * 4, 4))[0]
        for state in range(5)
    }
    assert branch_destinations == {
        0: 0x1DDE2F,
        1: 0x1DE6E2,
        2: 0x1DE6EE,
        3: 0x1DE07B,
        4: 0x1DDF43,
    }, branch_destinations

    # State 4 is a continuation, not an unconditional release:
    # only substate 3 resumes the nested future at 0x1DE342.
    assert finalizer[0x1DDF5D].op_str == "eax, 3"
    assert finalizer[0x1DDF60].mnemonic == "je"
    assert int(finalizer[0x1DDF60].op_str, 16) - BASE == 0x1DE342
    nested = [
        call_at(finalizer, 0x1DE357, 0x1DE6F0),
        call_at(finalizer, 0x1DE37D, 0x1DFEE6),
        call_at(finalizer, 0x1DE3CF, 0x1E0BBD),
        call_at(finalizer, 0x1DE626, 0x1E0C27),
    ]
    assert finalizer[0x1DE35C].mnemonic == "movabs"
    assert "0x8000000000000001" in finalizer[0x1DE35C].op_str
    assert finalizer[0x1DE369].mnemonic == "jne"
    assert int(finalizer[0x1DE369].op_str, 16) - BASE == 0x1DE37A

    # The awaited inner future is itself a five-state frame. Its drop
    # helpers are not evidence of HTTP success or a second network call.
    inner = insns(0x1DE6F0, 0x1DF7A0)
    assert inner[0x1DE714].op_str == "eax, byte ptr [rdx + 0x85]"
    inner_table = 0x83DFA0
    inner_states = {
        k: inner_table + struct.unpack("<i", n.pe.get_data(inner_table + k * 4, 4))[0]
        for k in range(5)
    }
    assert inner_states == {
        0: 0x1DE72B, 1: 0x1DF79B, 2: 0x1DF7C9,
        3: 0x1DE8BF, 4: 0x1DE85D,
    }
    release_drop = insns(0x1DFEE6, 0x1DFF6F)
    assert release_drop[0x1DFEEF].op_str == "eax, byte ptr [rcx + 0x85]"
    assert release_drop[0x1DFF47].mnemonic == "call"
    assert release_drop[0x1DFF55].mnemonic == "call"
    struct_drop = insns(0x1E0BBD, 0x1E0C27)
    assert struct_drop[0x1E0BE3].mnemonic == "jmp"
    assert int(struct_drop[0x1E0BE3].op_str, 16) - BASE == 0x1DFEE6
    variant_drop = insns(0x1E0C27, 0x1E0C6C)
    assert variant_drop[0x1E0C4A].mnemonic == "call"
    assert variant_drop[0x1E0C67].mnemonic == "jmp" and int(variant_drop[0x1E0C67].op_str, 16) - BASE == 0x81E10

    # Source-backed cleanup ordering/branch. It drops a conditional result
    # after record removal and CONTINUES without using that result to abort.
    removal = call_at(finalizer, 0x1DE5C8, 0x2DD578)
    assert finalizer[0x1DE5CD].op_str == "eax, eax"
    assert finalizer[0x1DE5CF].op_str == "rax, qword ptr [rsi]"
    assert finalizer[0x1DE5D2].mnemonic == "jo"
    assert int(finalizer[0x1DE5D2].op_str, 16) - BASE == 0x1DE5E1
    dropped = call_at(finalizer, 0x1DE5DC, 0x1E16A0)
    after = call_at(finalizer, 0x1DE5ED, 0x3A0510)
    assert finalizer[0x1DE463].mnemonic == "call"
    assert int(finalizer[0x1DE463].op_str, 16) - BASE == 0x5CFC70
    assert finalizer[0x1DE468].op_str == "r8d, 0x3c"
    timer = call_at(finalizer, 0x1DE474, 0x5DC950)

    # The launch-Err variant is stored as 80 bytes by the caller, which then
    # projects it via a generic serde Value clone, not direct literal code
    # selection. A code-only or message-only rule is NOT derivable here.
    assert reconcile[0x205412].op_str == "r13, [rsp + 0x460]"
    assert reconcile[0x205422].op_str == "ecx, 0xa"
    assert reconcile[0x20542A].mnemonic == "rep movsq"
    assert reconcile[0x20543F].mnemonic == "jno"
    assert int(reconcile[0x20543F].op_str, 16) - BASE == 0x2054CB
    for site, reg, text, size in [
        (0x2054F2, "rcx", "profileI", 8),
        (0x2055AA, "dword ptr [rax]", "erro", 4),
    ]:
        x = reconcile[site]
        assert x.mnemonic in ("movabs", "mov")
        assert x.op_str.startswith(reg + ", "), (hex(site), x)
        assert text.encode() == int(x.op_str.split(", ")[1], 16).to_bytes(size, "little")
    projection = [
        call_at(reconcile, 0x20552D, 0x31EEB2),
        call_at(reconcile, 0x2055DB, 0x31EEB2),
        call_at(reconcile, 0x20569D, 0x1E16A0),
    ]
    assert reconcile[0x2055D0].op_str == "rdx, [rsp + 0x460]"

    # Recover which string is passed to the error JSON field, rather than
    # guessing whether the original uses its error code or its message.
    # Constructor 0x2A1A47 receives first string (rdx pointer, r8 length)
    # and second string (r9 pointer, fifth-argument length). The first is
    # stored at +0,+8,+0x10, the second at +0x18,+0x20,+0x28.
    constructor = insns(0x2A1A47, 0x2A1B0A)
    first = {
        0x2A1ADE: "qword ptr [rdi], rsi",
        0x2A1AE1: "qword ptr [rdi + 8], r15",
        0x2A1AE5: "qword ptr [rdi + 0x10], rsi",
    }
    second = {
        0x2A1AE9: "qword ptr [rdi + 0x18], r14",
        0x2A1AED: "qword ptr [rdi + 0x20], r12",
        0x2A1AF1: "qword ptr [rdi + 0x28], r14",
    }
    for address, expected in {**first, **second}.items():
        assert constructor[address].mnemonic == "mov"
        assert constructor[address].op_str == expected, hex(address)
    construct_site = insns(0x1D53F5, 0x1D5439)
    assert construct_site[0x1D542C].mnemonic == "call"
    assert int(construct_site[0x1D542C].op_str, 16) - BASE == 0x2A1A47
    assert construct_site[0x1D5420].op_str == "r8d, 0x11"
    # The string-clone caller 0x2055DB reads precisely the *first*
    # constructor member. Tag 3 is the serde JSON string case.
    clone = insns(0x31EEB2, 0x31EF22)
    assert clone[0x31EEBB].op_str == "rsi, qword ptr [rdx + 0x10]"
    assert clone[0x31EED1].op_str == "rbx, qword ptr [rdx + 8]"
    assert clone[0x31EEFF].op_str == "byte ptr [rdi], 3"
    return {
        "referenceSha256": n.SHA,
        "mode": "hash-gated source bytes; no original binary or lease service execution",
        "finalizer": {
            "rva": "0x1ddde3",
            "stateByteOffset": "0xe8",
            "provenDispatchTable": {str(k): hex(v) for k, v in branch_destinations.items()},
            "nestedContinuationCalls": nested,
            "innerFrameStateByteOffset": "0x85",
            "innerFrameDispatchTable": {str(k): hex(v) for k, v in inner_states.items()},
            "branchLocalDestructors": ["0x1dfee6", "0x1e0bbd", "0x1e0c27"],
            "conditionalRemoval": {"call": removal, "conditionalDrop": dropped,
                "continuesTo": after, "resultDoesNotAbortThatBranch": True},
            "durationArithmetic": {"clockCall": "0x1de463", "seconds": 60, "call": timer},
            "unproved": ["lease release response content", "actual timer observable", "protected request error precedence"],
        },
        "launchFailure": {
            "branchRva": "0x2054cb",
            "errorSource": "80-byte Err variant from 0x1d5009 at caller stack +0x460",
            "projection": projection,
            "value": "serde JSON string from the FIRST (code) member of the launch-error result; not the second (message) member",
            "firstStringConstructor": "0x2a1a47; first at +0/+8/+0x10, second at +0x18/+0x20/+0x28",
            "stringClone": "0x31eeb2 reads first+8/+0x10, emits JSON tag 3",
            "unproved": ["complete set of launch-error-producing states", "protected service error causes"],
        },
    }


if __name__ == "__main__":
    print(json.dumps(inspect(), indent=2))
