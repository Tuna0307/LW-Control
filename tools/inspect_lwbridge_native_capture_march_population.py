#!/usr/bin/env python3
"""Recover safe native-capture march/train population rules from LWBridge 0.3.1."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86_const import X86_OP_MEM, X86_REG_RIP

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
PROXIES = {
    "secure": (0x987F74, 0x95800, "481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400", -0x1000),
    "plain": (0xA1D774, 0x96000, "c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794", 0),
}

MARCH_BUNDLE_NAMES = [
    (0x32229, "_uuid"), (0x32277, "type"), (0x322C7, "targetPos"),
    (0x32317, "startPos"), (0x32367, "homePos"), (0x323B7, "ownerUid"),
    (0x32407, "ownerName"), (0x32457, "ownerServer"),
    (0x324A7, "ownerCurServerId"), (0x324F7, "allianceUid"),
    (0x32547, "allianceName"), (0x32597, "allianceAbbr"),
    (0x325E7, "power"), (0x32637, "startTime"), (0x32687, "endTime"),
    (0x326D7, "worldId"), (0x32727, "status"), (0x32777, "_curHp"),
    (0x327C7, "monsterId"), (0x32817, "monsterType"),
    (0x32867, "monsterSpecialType"), (0x328B7, "monsterRallyNum"),
    (0x32907, "train"),
]

TRAIN_BUNDLE_NAMES = [
    (0x33F27, "uuid"), (0x33F69, "cfgId"),
    (0x33FB7, "type"), (0x33FF7, "config"),
]

CONFIG_NAMES = [(0x36437, "id"), (0x36477, "quality"), (0x364B7, "carriageNum")]


class InspectError(ValueError):
    pass


def require(ok: bool, message: str) -> None:
    if not ok:
        raise InspectError(message)
def extract_proxy(outer: bytes, outer_pe: pefile.PE, name: str) -> bytes:
    rva, size, expected, _ = PROXIES[name]
    offset = int(outer_pe.get_offset_from_rva(rva))
    data = outer[offset:offset + size]
    digest = hashlib.sha256(data).hexdigest()
    require(digest == expected, f"{name} proxy SHA-256 mismatch: {digest}")
    return data


def inspect_proxy(name: str, data: bytes) -> dict[str, object]:
    pe = pefile.PE(data=data, fast_load=False)
    base = int(pe.OPTIONAL_HEADER.ImageBase)
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True

    def instruction(rva: int):
        off = int(pe.get_offset_from_rva(rva))
        return next(md.disasm(data[off:off + 16], base + rva))

    def rip_target(ins) -> int | None:
        for operand in ins.operands:
            if operand.type == X86_OP_MEM and operand.mem.base == X86_REG_RIP:
                return int(ins.address + ins.size + operand.mem.disp - base)
        return None
    def text_at(rva: int) -> str:
        off = int(pe.get_offset_from_rva(rva))
        out = bytearray()
        for value in data[off:off + 128]:
            if 0x20 <= value <= 0x7E:
                out.append(value)
            else:
                break
        return out.decode("ascii", "replace")

    def expect(rva: int, mnemonic: str, op_text: str | None = None):
        ins = instruction(rva)
        require(ins.mnemonic == mnemonic, f"{name}: 0x{rva:X} mnemonic {ins.mnemonic}")
        if op_text is not None:
            require(ins.op_str == op_text, f"{name}: 0x{rva:X} operand {ins.op_str}")
        return ins

    def expect_name(rva: int, expected: str):
        ins = expect(rva, "lea")
        target = rip_target(ins)
        require(target is not None and text_at(target) == expected,
                f"{name}: 0x{rva:X} name mismatch for {expected}")

    for rva, field in MARCH_BUNDLE_NAMES:
        expect_name(rva, field)
    for rva, field in TRAIN_BUNDLE_NAMES:
        expect_name(rva, field)
    for rva, field in CONFIG_NAMES:
        expect_name(rva, field)
    # Cached march bundle order copied into fixed register pairs.
    expect(0x3428B, "movups", "xmm6, xmmword ptr [rdi + 0x18]")
    expect(0x3428F, "movups", "xmm11, xmmword ptr [rdi + 0x28]")
    expect(0x3429D, "movups", "xmm15, xmmword ptr [rdi + 0x48]")
    expect(0x342A2, "movups", "xmm12, xmmword ptr [rdi + 0x58]")
    expect(0x342A7, "movups", "xmm13, xmmword ptr [rdi + 0x68]")
    expect(0x342AC, "movups", "xmm9, xmmword ptr [rdi + 0x78]")
    expect(0x342B1, "movups", "xmm14, xmmword ptr [rdi + 0x88]")
    expect(0x342B9, "movups", "xmm10, xmmword ptr [rdi + 0x98]")
    expect(0x342C1, "movups", "xmm7, xmmword ptr [rdi + 0xa8]")
    expect(0x342C8, "movups", "xmm8, xmmword ptr [rdi + 0xb8]")
    expect(0x342D0, "mov", "rdi, qword ptr [rdi + 0xc8]")

    # UUID/type direct population.
    expect(0x342DF, "movq", "rax, xmm6")
    expect(0x342EE, "mov", "rax, qword ptr [rax + rbx]")
    expect(0x3436A, "mov", "qword ptr [rsp + 0x30], rax")
    expect(0x3436F, "psrldq", "xmm6, 8")
    expect(0x3437F, "mov", "eax, dword ptr [rax + rbx]")
    expect(0x3439F, "mov", "qword ptr [rsp + 0x38], rax")
    # Remaining direct numeric source/destination anchors.
    expect(0x343A9, "psrldq", "xmm0, 8")
    expect(0x343B9, "mov", "eax, dword ptr [rax + rbx]")
    expect(0x343D9, "mov", "qword ptr [rsp + 0x48], rax")
    expect(0x343DE, "movq", "rax, xmm12")
    expect(0x343E9, "mov", "eax, dword ptr [rax + rbx]")
    expect(0x34409, "mov", "qword ptr [rsp + 0x50], rax")
    expect(0x34413, "psrldq", "xmm0, 8")
    expect(0x34423, "mov", "eax, dword ptr [rax + rbx]")
    expect(0x34443, "mov", "qword ptr [rsp + 0x58], rax")
    expect(0x34448, "movq", "rax, xmm10")
    expect(0x34453, "mov", "eax, dword ptr [rax + rbx]")
    expect(0x34473, "mov", "qword ptr [rsp + 0x60], rax")

    expect(0x344B8, "movq", "rax, xmm7")
    expect(0x344C3, "mov", "eax, dword ptr [rax + rbx]")
    expect(0x344E3, "mov", "qword ptr [rsp + 0x70], rax")
    expect(0x344E8, "psrldq", "xmm7, 8")
    expect(0x344F8, "mov", "eax, dword ptr [rax + rbx]")
    expect(0x34518, "mov", "qword ptr [rsp + 0x78], rax")
    expect(0x3451D, "movq", "rax, xmm8")
    expect(0x34528, "mov", "eax, dword ptr [rax + rbx]")
    expect(0x34548, "mov", "qword ptr [rbp - 0x80], rax")
    expect(0x3454C, "psrldq", "xmm8, 8")
    expect(0x3455D, "mov", "eax, dword ptr [rax + rbx]")
    expect(0x3457D, "mov", "qword ptr [rbp - 0x78], rax")

    expect(0x34581, "movq", "rax, xmm9")
    expect(0x3458C, "mov", "rcx, qword ptr [rax + rbx]")
    expect(0x3459B, "mov", "qword ptr [rbp - 0x70], rcx")
    expect(0x345BA, "psrldq", "xmm9, 8")
    expect(0x345CB, "mov", "rax, qword ptr [rax + rbx]")
    expect(0x345DA, "mov", "qword ptr [rbp - 0x60], rax")
    expect(0x345F9, "movq", "rax, xmm14")
    expect(0x34604, "mov", "rax, qword ptr [rax + rbx]")
    expect(0x34613, "mov", "qword ptr [rbp - 0x50], rax")
    expect(0x34632, "psrldq", "xmm10, 8")
    expect(0x34643, "mov", "rax, qword ptr [rax + rbx]")
    expect(0x34652, "mov", "qword ptr [rbp - 0x40], rax")
    # Five nullable IL2CPP strings map into the R8-083 string slots.
    expect(0x346CF, "movups", "xmm6, xmmword ptr [rsp + 0x20]")
    expect(0x346D8, "psrldq", "xmm0, 8")
    expect(0x346DD, "movq", "r8, xmm0")
    expect(0x346EC, "call", "0x18003a140")
    expect(0x346F4, "lea", "rcx, [rbp - 0x28]")

    expect(0x34712, "movq", "r8, xmm15")
    expect(0x34721, "call", "0x18003a140")
    expect(0x34729, "lea", "rcx, [rbp]")
    expect(0x34747, "psrldq", "xmm12, 8")
    expect(0x3474D, "movq", "r8, xmm12")
    expect(0x3475C, "call", "0x18003a140")
    expect(0x34764, "lea", "rcx, [rbp + 0x28]")
    expect(0x34782, "movq", "r8, xmm13")
    expect(0x34791, "call", "0x18003a140")
    expect(0x34799, "lea", "rcx, [rbp + 0x50]")
    expect(0x347B7, "psrldq", "xmm13, 8")
    expect(0x347BD, "movq", "r8, xmm13")
    expect(0x347CC, "call", "0x18003a140")
    expect(0x347D4, "lea", "rcx, [rbp + 0x78]")
    # pointIndex fallback after the preferred positive GetMarchCurPosIndex route:
    # targetPos (>0), then startPos (>0), then nullable homePos.
    expect(0x348AC, "cmp", "byte ptr [rsp + 0x44], 0")
    expect(0x348B7, "movq", "rax, xmm11")
    expect(0x348C2, "mov", "eax, dword ptr [rax + rbx]")
    expect(0x348F6, "test", "ecx, ecx")
    expect(0x348F8, "jg", "0x180034934")
    expect(0x348FA, "psrldq", "xmm11, 8")
    expect(0x3490B, "mov", "eax, dword ptr [rax + rbx]")
    expect(0x3493C, "test", "ecx, ecx")
    expect(0x3493E, "jg", "0x180034970")
    expect(0x34940, "movq", "rax, xmm6")
    expect(0x3494B, "mov", "eax, dword ptr [rax + rbx]")
    expect(0x3496B, "mov", "qword ptr [rsp + 0x40], rax")

    # train pointer is transformed into the exact nullable train slot at 0x170/0x1A8.
    expect(0x347F2, "cmp", "rdi, -1")
    expect(0x347F8, "mov", "rax, qword ptr [rdi + rbx]")
    expect(0x3480F, "lea", "rcx, [rbp + 0xe0]")
    expect(0x34816, "call", "0x180036260")
    expect(0x3481E, "movaps", "xmmword ptr [rbp + 0xa0], xmm0")
    expect(0x34829, "movaps", "xmmword ptr [rbp + 0xb0], xmm1")
    expect(0x34834, "movaps", "xmmword ptr [rbp + 0xc0], xmm0")
    expect(0x34840, "movsd", "qword ptr [rbp + 0xd0], xmm1")
    expect(0x3484C, "mov", "byte ptr [rbp + 0xd8], cl")
    # Train helper consumes outer uuid/cfgId/type/config in bundle order.
    expect(0x36306, "mov", "rsi, qword ptr [rdi + 0x18]")
    expect(0x3630A, "mov", "r14, qword ptr [rdi + 0x20]")
    expect(0x3630E, "mov", "r15, qword ptr [rdi + 0x28]")
    expect(0x36312, "mov", "rdi, qword ptr [rdi + 0x30]")
    expect(0x3632D, "mov", "rcx, qword ptr [rsi + rbx]")
    expect(0x3635E, "mov", "eax, dword ptr [r14 + rbx]")
    expect(0x36383, "mov", "eax, dword ptr [r15 + rbx]")
    expect(0x363AC, "mov", "r14, qword ptr [rdi + rbx]")

    # Nested config.id/quality/carriageNum are loaded in that order.
    expect(0x3651B, "mov", "rbx, qword ptr [rsi + 0x18]")
    expect(0x3651F, "mov", "rdi, qword ptr [rsi + 0x20]")
    expect(0x36523, "mov", "rsi, qword ptr [rsi + 0x28]")
    expect(0x36539, "mov", "eax, dword ptr [rbx + r14]")
    expect(0x3655E, "mov", "eax, dword ptr [rdi + r14]")
    expect(0x36583, "mov", "eax, dword ptr [rsi + r14]")
    expect(0x3662F, "mov", "byte ptr [r12 + 0x38], 1")
    expect(0x36637, "mov", "byte ptr [r12 + 0x38], 0")

    # isMonster fallback: absent getter -> positive raw monsterId if present.
    expect(0x34678, "test", "rax, rax")
    expect(0x3467B, "je", "0x180034686")
    expect(0x34686, "cmp", "byte ptr [rsp + 0x74], al")
    expect(0x3468A, "cmovne", "eax, dword ptr [rsp + 0x70]")
    expect(0x3468F, "test", "eax, eax")
    expect(0x34691, "setg", "al")
    raw_routes = [
        {"source": "_uuid", "field": "uuid", "valueOffset": "0x0", "transform": "positive qword required for record admission"},
        {"source": "type", "field": "marchType", "valueOffset": "0x8", "presenceOffset": "0xC"},
        {"source": "ownerServer", "field": "ownerServer", "valueOffset": "0x18", "presenceOffset": "0x1C"},
        {"source": "ownerCurServerId", "field": "ownerCurServerId", "valueOffset": "0x20", "presenceOffset": "0x24"},
        {"source": "worldId", "field": "worldId", "valueOffset": "0x28", "presenceOffset": "0x2C"},
        {"source": "status", "field": "status", "valueOffset": "0x30", "presenceOffset": "0x34"},
        {"source": "monsterId", "field": "monsterId", "valueOffset": "0x40", "presenceOffset": "0x44"},
        {"source": "monsterType", "field": "monsterType", "valueOffset": "0x48", "presenceOffset": "0x4C"},
        {"source": "monsterSpecialType", "field": "monsterSpecialType", "valueOffset": "0x50", "presenceOffset": "0x54"},
        {"source": "monsterRallyNum", "field": "monsterRallyNum", "valueOffset": "0x58", "presenceOffset": "0x5C"},
        {"source": "power", "field": "power", "valueOffset": "0x60", "presenceOffset": "0x68"},
        {"source": "startTime", "field": "startTime", "valueOffset": "0x70", "presenceOffset": "0x78"},
        {"source": "endTime", "field": "endTime", "valueOffset": "0x80", "presenceOffset": "0x88"},
        {"source": "_curHp", "field": "curHp", "valueOffset": "0x90", "presenceOffset": "0x98"},
        {"source": "ownerUid", "field": "ownerUid", "valueOffset": "0xA8", "transform": "IL2CPP string conversion"},
        {"source": "ownerName", "field": "ownerName", "valueOffset": "0xD0", "transform": "IL2CPP string conversion"},
        {"source": "allianceUid", "field": "allianceUid", "valueOffset": "0xF8", "transform": "IL2CPP string conversion"},
        {"source": "allianceName", "field": "allianceName", "valueOffset": "0x120", "transform": "IL2CPP string conversion"},
        {"source": "allianceAbbr", "field": "allianceAbbr", "valueOffset": "0x148", "transform": "IL2CPP string conversion"},
    ]
    train_routes = [
        {"source": "uuid", "field": "train.uuid", "valueOffset": "0x170+0x0", "presenceOffset": "0x170+0x8"},
        {"source": "cfgId", "field": "train.cfgId", "valueOffset": "0x170+0x10", "presenceOffset": "0x170+0x14"},
        {"source": "type", "field": "train.type", "valueOffset": "0x170+0x18", "presenceOffset": "0x170+0x1C"},
        {"source": "config.id", "field": "train.configId", "valueOffset": "0x170+0x20", "presenceOffset": "0x170+0x24"},
        {"source": "config.quality", "field": "train.quality", "valueOffset": "0x170+0x28", "presenceOffset": "0x170+0x2C"},
        {"source": "config.carriageNum", "field": "train.carriageNum", "valueOffset": "0x170+0x30", "presenceOffset": "0x170+0x34"},
    ]
    return {
        "proxy": name,
        "sha256": hashlib.sha256(data).hexdigest(),
        "marchBundleFields": [field for _, field in MARCH_BUNDLE_NAMES],
        "rawRoutes": raw_routes,
        "pointIndexRule": {
            "preferred": "positive GetMarchCurPosIndex",
            "fallbacks": ["positive targetPos", "positive startPos", "nullable homePos"],
        },
        "isMonsterRule": "IsMonsterOrOrdinaryBoss result > 0; if getter unavailable, present monsterId > 0",
        "trainBundleFields": [field for _, field in TRAIN_BUNDLE_NAMES],
        "trainConfigFields": [field for _, field in CONFIG_NAMES],
        "trainRoutes": train_routes,
        "trainRecordOffset": "0x170",
        "trainPresenceOffset": "0x1A8",
    }
def inspect(binary: Path) -> dict[str, object]:
    outer = binary.read_bytes()
    digest = hashlib.sha256(outer).hexdigest()
    require(digest == EXPECTED_SHA256, f"unsupported reference SHA-256: {digest}")
    outer_pe = pefile.PE(data=outer, fast_load=False)
    proxies = [
        inspect_proxy(name, extract_proxy(outer, outer_pe, name))
        for name in ("secure", "plain")
    ]
    keys = [
        "marchBundleFields", "rawRoutes", "pointIndexRule", "isMonsterRule",
        "trainBundleFields", "trainConfigFields", "trainRoutes",
        "trainRecordOffset", "trainPresenceOffset",
    ]
    for key in keys:
        require(proxies[0][key] == proxies[1][key], f"secure/plain differ for {key}")
    return {
        "findingId": "LWB-R8-087",
        "date": "2026-09-27",
        "evidenceStatus": "RECOVERED CONTRACT",
        "sourceIdentity": {"path": str(binary), "sha256": digest},
        "recoveredResult": {key: proxies[0][key] for key in keys},
        "proxies": proxies,
        "limits": [
            "covers safe-region march/train population only; point raw-field population remains separate",
            "does not inspect or cross protected native region 0x3F8E0-0x40A6D",
            "does not recover XluaBridgeMapScanTick traversal/order/coordinates, request pacing, or retry/backoff",
            "does not change production scanner behavior",
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
