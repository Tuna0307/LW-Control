#!/usr/bin/env python3
"""Recover LWBridge 0.3.1 authorization projection and Automation request boundaries."""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path

import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86_const import X86_OP_MEM, X86_REG_RIP

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"

class InspectError(ValueError):
    pass

def require(ok: bool, message: str) -> None:
    if not ok:
        raise InspectError(message)

def inspect(binary: Path) -> dict[str, object]:
    data = binary.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    require(digest == EXPECTED_SHA256, f"unsupported reference SHA-256: {digest}")

    pe = pefile.PE(data=data, fast_load=False)
    base = int(pe.OPTIONAL_HEADER.ImageBase)
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True

    def rva_raw(raw: int) -> int:
        return int(pe.get_rva_from_offset(raw))

    def va_raw(raw: int) -> int:
        return base + rva_raw(raw)

    def ins_at(rva: int):
        off = int(pe.get_offset_from_rva(rva))
        return next(md.disasm(data[off:off + 16], base + rva))

    def expect(rva: int, mnemonic: str, op: str | None = None):
        ins = ins_at(rva)
        require(ins.mnemonic == mnemonic, f"0x{rva:X}: expected {mnemonic}, got {ins.mnemonic}")
        if op is not None:
            require(ins.op_str == op, f"0x{rva:X}: expected {op}, got {ins.op_str}")
        return ins

    def rip_rva(ins) -> int | None:
        for operand in ins.operands:
            if operand.type == X86_OP_MEM and operand.mem.base == X86_REG_RIP:
                return int(ins.address + ins.size + operand.mem.disp - base)
        return None

    def literal_at_rva(rva: int, expected: bytes) -> None:
        raw = int(pe.get_offset_from_rva(rva))
        require(data[raw:raw + len(expected)] == expected, f"literal mismatch at RVA 0x{rva:X}")

    # Exact EntitlementResponse serde field table.
    entitlement_fields = [
        ("planCode", 0xC7EE2B, 0xC7EE50),
        ("maxProfiles", 0xC7EE33, 0xC7EE60),
        ("expiresAt", 0xC7EDA2, 0xC7EE70),
        ("accountExpiresAt", 0xC7EE3E, 0xC7EE80),
        ("serverTime", 0xC7EDAB, 0xC7EE90),
    ]
    descriptor_rows = []
    for name, string_raw, desc_raw in entitlement_fields:
        bs = name.encode("ascii")
        require(data[string_raw:string_raw + len(bs)] == bs, f"{name} literal mismatch")
        ptr, length = struct.unpack_from("<QQ", data, desc_raw)
        require(ptr == va_raw(string_raw), f"{name} descriptor pointer mismatch")
        require(length == len(bs), f"{name} descriptor length mismatch")
        descriptor_rows.append({"name": name, "stringRaw": hex(string_raw), "descriptorRaw": hex(desc_raw)})

    entitlement_type = b"struct EntitlementResponse with 5 elements"
    require(data[0xC9BB7B:0xC9BB7B + len(entitlement_type)] == entitlement_type,
            "EntitlementResponse type text mismatch")
    ptr, length = struct.unpack_from("<QQ", data, 0xC9BBA8)
    require(ptr == va_raw(0xC9BB7B) and length == len(entitlement_type),
            "EntitlementResponse type descriptor mismatch")

    # SessionV2: six names are direct, serde declares seven elements.
    session_fields = [
        ("username", 0xC92450),
        ("expiresAt", 0xC92458),
        ("lastHeartbeatAt", 0xC92461),
        ("graceStartedAt", 0xC92470),
        ("encryptedToken", 0xC9247E),
        ("encryptedMetadata", 0xC9248C),
    ]
    for name, raw in session_fields:
        bs = name.encode("ascii")
        require(data[raw:raw + len(bs)] == bs, f"SessionV2 {name} mismatch")
    session_type = b"struct SessionV2 with 7 elements"
    require(data[0xC9249D:0xC9249D + len(session_type)] == session_type,
            "SessionV2 type text mismatch")

    # Shared non-role authorization-state accessor.
    expect(0x23DE54, "cmp", "rax, 0xa")
    expect(0x23DE5E, "movabs", "rcx, 0x7a69726f68747561")  # "authoriz"
    expect(0x23DE6B, "movzx", "eax, word ptr [rax + 8]")
    expect(0x23DE6F, "xor", "rax, 0x6465")                  # "ed"
    expect(0x23DE7C, "cmp", "rax, 5")
    expect(0x23DE86, "mov", "ecx, 0x63617267")             # "grac"
    expect(0x23DE94, "movzx", "eax, byte ptr [rax + 4]")
    expect(0x23DE98, "xor", "eax, 0x65")                   # "e"
    expect(0x23DEA0, "test", "rbx, rbx")
    expect(0x23DEA3, "setg", "cl")
    expect(0x23DEAA, "call", "0x14023f1c0")
    expect(0x23DEAF, "sub", "rax, rbx")
    expect(0x23DEB2, "cmp", "rax, 0xdbba1")
    expect(0x23DEB8, "jge", "0x14023deca")

    expired = expect(0x23DF6A, "lea")
    expired_rva = rip_rva(expired)
    require(expired_rva is not None, "ACCOUNT_EXPIRED target missing")
    literal_at_rva(expired_rva, b"ACCOUNT_EXPIRED")

    # AUTH_REQUIRED constructed inline in fallback branch.
    expect(0x23DF0E, "movabs", "rcx, 0x4445524955514552")
    expect(0x23DF1C, "movabs", "rcx, 0x5145525f48545541")

    # Shared role gate: authorization-state accessRole member + exact membership comparator.
    expect(0x2373C7, "movups", "xmm0, xmmword ptr [r14 + 0x310]")
    expect(0x2373DD, "call", "0x140646700")
    role_err = expect(0x23741B, "lea")
    role_err_rva = rip_rva(role_err)
    require(role_err_rva is not None, "ROLE_REQUIRED target missing")
    literal_at_rva(role_err_rva, b"ROLE_REQUIREDaccessRole")

    # Watermark role policy: ["premium","admin"] then ["admin"].
    premium_ptr, premium_len = struct.unpack_from("<QQ", data, 0x821488)
    admin_ptr, admin_len = struct.unpack_from("<QQ", data, 0x821498)
    premium_raw = int(pe.get_offset_from_rva(premium_ptr - base))
    admin_raw = int(pe.get_offset_from_rva(admin_ptr - base))
    require(data[premium_raw:premium_raw + premium_len] == b"premium", "premium descriptor mismatch")
    require(data[admin_raw:admin_raw + admin_len] == b"admin", "admin descriptor mismatch")

    first_roles = expect(0x127A4A, "lea")
    require(rip_rva(first_roles) == rva_raw(0x821488), "watermark first role slice mismatch")
    expect(0x127A58, "mov", "qword ptr [rbx + 0x1930], 2")

    auth_admin_ptr, auth_admin_len = struct.unpack_from("<QQ", data, 0x825D88)
    require(auth_admin_ptr == admin_ptr and auth_admin_len == admin_len,
            "auth-service admin descriptor mismatch")
    admin_only = expect(0x127CEC, "lea")
    require(rip_rva(admin_only) == rva_raw(0x825D88), "watermark admin-only slice mismatch")
    expect(0x127CFB, "mov", "r9d, 1")
    expect(0x127D07, "call", "0x140237398")

    # Correct old adjacency inference: Automation/Inspect have no direct premium/admin descriptor refs.
    forbidden = {rva_raw(0x821488), rva_raw(0x821498), rva_raw(0x825D88)}
    handlers = {
        "automation_configure": (0x110C7D, 0x1122A0),
        "automation_stop": (0x121C80, 0x122DA2),
        "automation_start": (0x14711B, 0x14809A),
        "automation_inspect": (0x15DF53, 0x15F565),
    }
    handler_refs = {}
    for name, (start, end) in handlers.items():
        off = int(pe.get_offset_from_rva(start))
        refs = []
        for ins in md.disasm(data[off:off + (end - start)], base + start):
            target = rip_rva(ins)
            if target in forbidden:
                refs.append(hex(ins.address - base))
        require(not refs, f"{name}: unexpected premium/admin descriptor refs {refs}")
        handler_refs[name] = refs

    # Exact provider request object keys.
    expect(0x111A2A, "mov", "dword ptr [rax], 0x6b736174")      # task
    expect(0x111ACA, "mov", "word ptr [rax + 4], 0x6769")      # config
    expect(0x111AD0, "mov", "dword ptr [rax], 0x666e6f63")
    configure_lea = expect(0x111BAA, "lea")

    expect(0x14778E, "mov", "dword ptr [rax], 0x6b736174")      # task
    expect(0x147831, "mov", "word ptr [rax + 4], 0x6769")      # config
    expect(0x147837, "mov", "dword ptr [rax], 0x666e6f63")
    start_lea = expect(0x147917, "lea")

    expect(0x122331, "mov", "dword ptr [rax], 0x6b736174")      # task
    expect(0x1223D1, "mov", "dword ptr [rax + 3], 0x736e6f69") # options
    expect(0x1223D8, "mov", "dword ptr [rax], 0x6974706f")
    stop_lea = expect(0x1224AF, "lea")

    expect(0x15E4C6, "mov", "dword ptr [rax], 0x6b736174")      # task only
    inspect_lea = expect(0x15E5A5, "lea")

    providers = {}
    for name, ins, expected in [
        ("configure", configure_lea, b"configureAutomationTask"),
        ("start", start_lea, b"startAutomationTask"),
        ("stop", stop_lea, b"stopAutomationTask"),
        ("inspect", inspect_lea, b"inspectAutomationTask"),
    ]:
        target = rip_rva(ins)
        require(target is not None, f"{name}: provider literal target missing")
        literal_at_rva(target, expected)
        providers[name] = expected.decode("ascii")

    return {
        "findingId": "LWB-R8-091",
        "date": "2026-09-27",
        "status": "RECOVERED CONTRACT / CORRECTION",
        "sourceIdentity": {"path": str(binary), "sha256": digest},
        "authorizationAdmission": {
            "acceptedPhasePaths": ["authorized", "grace"],
            "graceRequiresPositiveStartedAt": True,
            "graceFreshnessComparison": "now - graceStartedAt < 900001",
            "comparisonImmediateMs": 900001,
            "expiredError": "ACCOUNT_EXPIRED",
            "fallbackError": "AUTH_REQUIRED",
            "roleFieldOffset": "0x310",
            "roleComparator": "exact string membership",
        },
        "entitlementResponse": {
            "fieldCount": 5,
            "fields": [x[0] for x in entitlement_fields],
            "descriptors": descriptor_rows,
        },
        "sessionV2": {
            "declaredFieldCount": 7,
            "directFieldNames": [x[0] for x in session_fields],
            "seventhField": "UNKNOWN",
        },
        "watermarkRolePolicy": {
            "initialAllowedRoles": ["premium", "admin"],
            "laterRequiredRoles": ["admin"],
        },
        "automationCorrection": {
            "providerRequestShapes": {
                "configureAutomationTask": ["task", "config"],
                "startAutomationTask": ["task", "config"],
                "stopAutomationTask": ["task", "options"],
                "inspectAutomationTask": ["task"],
            },
            "premiumAdminDescriptorRefsInsideHandlers": handler_refs,
            "providers": providers,
            "correction": "premium/admin are not fields added by these four handlers; prior adjacency-based request-field inference is withdrawn",
        },
        "implementationDecision": {
            "doNotUseCurrentFrontendPlaceholdersAsAuthAuthority": True,
            "noLiveBehaviorChangeInThisCheckpoint": True,
            "nextRequiredWork": "recover/implement the shared authorization-state producer and entitlement/accessRole projection before unfencing auth-dependent live actions",
        },
        "limits": [
            "no credentials or private keys are read",
            "no auth network request is made",
            "SessionV2 seventh serde field remains unresolved",
            "this checkpoint does not claim current rebuild authorization parity",
        ],
    }

def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        result = inspect(args.binary)
    except (InspectError, OSError, pefile.PEFormatError) as exc:
        parser.error(str(exc))
    rendered = json.dumps(result, indent=2, sort_keys=True)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered + "\n", encoding="utf-8")
    print(rendered)
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
