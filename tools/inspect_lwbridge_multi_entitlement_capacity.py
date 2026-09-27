#!/usr/bin/env python3
"""Verify LWBridge 0.3.1 multi-entitlement capacity ownership and refresh boundaries."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

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

    def ins_at(rva: int):
        off = int(pe.get_offset_from_rva(rva))
        return next(md.disasm(data[off:off + 16], base + rva))

    def expect(rva: int, mnemonic: str, contains: str | None = None):
        ins = ins_at(rva)
        require(ins.mnemonic == mnemonic, f"0x{rva:X}: expected {mnemonic}, got {ins.mnemonic}")
        if contains is not None:
            require(contains in ins.op_str, f"0x{rva:X}: expected operand containing {contains!r}, got {ins.op_str!r}")
        return ins

    def direct_call_target(rva: int) -> int:
        ins = expect(rva, "call")
        require(ins.operands and ins.operands[0].type == X86_OP_IMM, f"0x{rva:X}: expected direct call")
        return int(ins.operands[0].imm - base)

    def rip_target(ins) -> int | None:
        for op in ins.operands:
            if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                return int(ins.address + ins.size + op.mem.disp - base)
        return None

    def read_ascii(rva: int, length: int) -> str:
        raw = int(pe.get_offset_from_rva(rva))
        return data[raw:raw + length].decode("ascii")

    def assert_rip_literal(rva: int, mnemonic: str, literal: str) -> None:
        ins = expect(rva, mnemonic)
        target = rip_target(ins)
        require(target is not None, f"0x{rva:X}: missing RIP target")
        require(read_ascii(target, len(literal)) == literal, f"0x{rva:X}: expected {literal!r}")

    # Public commands and service endpoints.
    assert_rip_literal(0x18C6B4, "lea", "multi_entitlement_get")
    require(direct_call_target(0x18C7FB) == 0x0E993A, "multi_entitlement_get fetch helper mismatch")
    assert_rip_literal(0x0E99EC, "lea", "/api/multi/entitlement")

    assert_rip_literal(0x1488F3, "lea", "multi_activate")
    assert_rip_literal(0x148B99, "lea", "licenseCode")
    assert_rip_literal(0x148E98, "lea", "/api/multi/activate")
    require(direct_call_target(0x1490C5) == 0x23420C, "multi_activate parser mismatch")

    # Both explicit get and long-lived subsystem use the same fetch future/parser.
    require(direct_call_target(0x0E744D) == 0x0E993A, "automatic entitlement fetch mismatch")
    require(direct_call_target(0x0E9C17) == 0x23420C, "entitlement fetch parser mismatch")

    # Exact EntitlementResponse identity and service-unavailable boundary.
    assert_rip_literal(0x234247, "lea", "EntitlementResponse")
    assert_rip_literal(0x23428F, "lea", "SERVICE_UNAVAILABLE")

    # Service maxProfiles normalization:
    # raw==2 -> 2; raw==5 -> 5; all other values -> 1.
    expect(0x234488, "cmp", "sil, 5")
    expect(0x23448C, "sete", "al")
    expect(0x23448F, "shl", "al, 2")
    expect(0x234492, "inc", "al")
    expect(0x234494, "cmp", "sil, 2")
    expect(0x234498, "movzx", "eax, al")
    expect(0x23449B, "mov", "ecx, 2")
    expect(0x2344A0, "cmovne", "ecx, eax")
    expect(0x2344A3, "mov", "byte ptr [r14 + 0x79], cl")

    # The parser publishes/refines the shared entitlement state.
    require(direct_call_target(0x2344CD) == 0x234906, "entitlement publication helper mismatch")
    require(direct_call_target(0x2344D8) == 0x2349BE, "entitlement projection helper mismatch")

    # Event publisher owns exact bridge://multi-entitlement.
    assert_rip_literal(0x23497C, "lea", "bridge://multi-entitlement")

    # Exact public/full entitlement object field vocabulary.
    fields = [
        ("phase", 0x234E4C, 5),
        ("planCode", 0x234E79, 8),
        ("maxProfiles", 0x234EAB, 11),
        ("expiresAt", 0x234EDA, 9),
        ("accountExpiresAt", 0x234F09, 16),
        ("serverTime", 0x234F38, 10),
        ("graceExpiresAt", 0x234F63, 14),
        ("errorCode", 0x234F8D, 9),
    ]
    for name, rva, length in fields:
        ins = expect(rva, "lea")
        target = rip_target(ins)
        require(target is not None, f"{name}: missing field target")
        require(read_ascii(target, length) == name, f"{name}: field literal mismatch")

    # maxProfiles lives at public/shared-state +0xA8.
    expect(0x234EA4, "lea", "r9, [rsi + 0xa8]")
    expect(0x234D79, "mov", "byte ptr [rsi + 0xa8], al")

    # Exact phase vocabulary and selection literals.
    phase_sites = [
        ("restricted", 0x234BF4, 10),
        ("grace", 0x234BFB, 5),
        ("initializing", 0x234C09, 12),
        ("authorized", 0x234C17, 10),
        ("single", 0x234C1E, 6),
    ]
    for phase, rva, length in phase_sites:
        ins = expect(rva, "lea")
        target = rip_target(ins)
        require(target is not None, f"{phase}: missing phase target")
        require(read_ascii(target, length) == phase, f"{phase}: phase literal mismatch")

    # Phase selection is branch-exact, but internal boolean meanings remain unnamed.
    expect(0x234C25, "test", "r15b, r15b")
    expect(0x234C28, "cmovne", "rdx, rcx")
    expect(0x234C2C, "test", "r12b, r12b")
    expect(0x234C2F, "cmovne", "rdx, r8")

    # Public maxProfiles uses normalized capacity except the internal
    # initialization/restriction gates force it to 1.
    expect(0x234C82, "mov", "r12b, byte ptr [rbx + 0x79]")
    expect(0x234D3C, "movzx", "eax, r12b")
    expect(0x234D40, "test", "bpl, bpl")
    expect(0x234D43, "mov", "ecx, 1")
    expect(0x234D48, "cmove", "eax, ecx")
    expect(0x234D4B, "test", "r14b, r14b")
    expect(0x234D4E, "cmovne", "eax, ecx")
    expect(0x234D79, "mov", "byte ptr [rsi + 0xa8], al")

    # Capacity extractor consumes the exact shared-state maxProfiles byte.
    require(direct_call_target(0x0DE890) == 0x2349BE, "capacity extractor shared-state projection mismatch")
    expect(0x0DE895, "mov", "bl, byte ptr [r14 + 0xa8]")

    # Multi status mirror is a distinct six-field projection.
    mirror_fields = [
        ("multiPhase", 0x24B564, 10),
        ("multiMaxProfiles", 0x24B596, 16),
        ("multiExpiresAt", 0x24B5C8, 14),
        ("multiServerTime", 0x24B5F7, 15),
        ("multiGraceExpiresAt", 0x24B629, 19),
        ("multiErrorCode", 0x24B653, 14),
    ]
    for name, rva, length in mirror_fields:
        ins = expect(rva, "lea")
        target = rip_target(ins)
        require(target is not None, f"{name}: missing mirror target")
        require(read_ascii(target, length) == name, f"{name}: mirror literal mismatch")

    # Automatic subsystem is not merely a timer; it also owns lease recovery.
    assert_rip_literal(0x0E79DC, "lea", "MULTI_LEASE_RECOVERY_MISSING")
    assert_rip_literal(0x0E88AD, "lea", "leaseProof")
    assert_rip_literal(0x0E8F33, "lea", "multi_lease_expired")

    return {
        "findingId": "LWB-R8-096",
        "date": "2026-09-27",
        "status": "RECOVERED CONTRACT",
        "sourceIdentity": {"path": str(binary), "sha256": digest},
        "commands": {
            "multi_entitlement_get": {
                "handlerRva": "0x18C614-0x18CC4D",
                "endpoint": "/api/multi/entitlement",
                "fetchHelperRva": "0x0E993A-0x0E9CD2",
            },
            "multi_activate": {
                "handlerRva": "0x14884F-0x14964C",
                "requestField": "licenseCode",
                "endpoint": "/api/multi/activate",
                "responseParserRva": "0x23420C-0x23463B",
            },
        },
        "entitlementState": {
            "event": "bridge://multi-entitlement",
            "fields": [name for name, _, _ in fields],
            "phases": [phase for phase, _, _ in phase_sites],
            "maxProfilesOffset": "0xA8",
        },
        "capacityPolicy": {
            "serviceNormalization": {
                "2": 2,
                "5": 5,
                "other": 1,
            },
            "publicProjection": "use normalized capacity except two internal initialization/restriction gates force 1",
            "consumer": "shared entitlement-state byte +0xA8",
            "profileMutationAcceptedDomainFromR8_063": [1, 2, 5],
        },
        "refreshOwnership": {
            "explicitCommand": "multi_entitlement_get performs a service refresh through the common fetch helper",
            "automaticSubsystem": "0x0E6E89-0x0E902D calls the same fetch helper and also owns lease recovery/proof/expiry work",
            "exactAutomaticCadence": "UNKNOWN",
        },
        "statusMirror": [name for name, _, _ in mirror_fields],
        "limits": [
            "internal boolean meanings that select single/initializing/authorized/grace/restricted are not renamed without stronger evidence",
            "exact automatic entitlement refresh cadence remains unresolved",
            "per-instance lease scheduler/proof lifecycle is only partially recovered and is not implemented by this checkpoint",
            "no credentials, private keys, or live service requests are used",
            "no production behavior is changed",
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
