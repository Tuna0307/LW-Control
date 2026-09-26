#!/usr/bin/env python3
"""Recover safe native-capture getter-to-field population routes."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
PROXIES = {
    "secure": (0x987F74, 0x95800, "481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400", -0x1000),
    "plain": (0xA1D774, 0x96000, "c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794", 0),
}


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
    delta = PROXIES[name][3]
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

    def direct_target(ins) -> int | None:
        if not ins.operands or ins.operands[0].type != X86_OP_IMM:
            return None
        return int(ins.operands[0].imm - base)
    def expect(rva: int, mnemonic: str, op_text: str | None = None):
        ins = instruction(rva)
        require(ins.mnemonic == mnemonic, f"{name}: 0x{rva:X} mnemonic {ins.mnemonic}")
        if op_text is not None:
            require(ins.op_str == op_text, f"{name}: 0x{rva:X} operand {ins.op_str}")
        return ins

    def expect_rip(rva: int, mnemonic: str, target: int):
        ins = expect(rva, mnemonic)
        actual = rip_target(ins)
        require(
            actual == target + delta,
            f"{name}: 0x{rva:X} RIP target 0x{actual:X} != 0x{target + delta:X}",
        )
        return ins

    def expect_direct(rva: int, mnemonic: str, target: int):
        ins = expect(rva, mnemonic)
        actual = direct_target(ins)
        require(
            actual == target,
            f"{name}: 0x{rva:X} direct target 0x{actual:X} != 0x{target:X}",
        )
        return ins

    # Point record base is rbp-0xA0: ownerUid presence is field offset 0x1E0.
    expect(0x35E31, "cmp", "byte ptr [rbp + 0x140], 0")

    # Resource getters are only consulted for pointType == 7.
    expect(0x35A81, "cmp", "dword ptr [rsp + 0x68], 7")
    expect(0x35A86, "jne", "0x180035ae6")
    expect_rip(0x35A88, "mov", 0x92240)
    expect(0x35A99, "call", "rax")
    expect(0x35A9B, "mov", "dword ptr [rbp - 0x38], eax")
    expect(0x35A9E, "mov", "byte ptr [rbp - 0x34], 1")

    expect_rip(0x35AB7, "mov", 0x92248)
    expect(0x35AC8, "call", "rax")
    expect(0x35ACA, "mov", "dword ptr [rbp - 0x30], eax")
    expect(0x35ACD, "mov", "byte ptr [rbp - 0x2c], 1")

    # TreasurePointInfo class gate then GetWorldTreasureType -> point offset 0xA0.
    expect(0x35B17, "cmp", "r11, 0x11")
    expect(0x35B24, "movabs", "rdx, 0x6572757361657254")
    expect(0x35B37, "movabs", "rdx, 0x666e49746e696f50")
    expect(0x35B4A, "mov", "eax, 0x6f")
    expect_rip(0x35C73, "mov", 0x92250)
    expect(0x35C84, "call", "rax")
    expect(0x35C86, "mov", "ecx, eax")
    expect(0x35CCB, "mov", "dword ptr [rbp], ecx")
    expect(0x35CCE, "mov", "byte ptr [rbp + 4], al")
    # WorldSuppliesPoint class gate then get_configId -> point cfgId offset 0x40.
    expect(0x35CF1, "cmp", "r11, 0x12")
    expect(0x35CFA, "movabs", "rdx, 0x707553646c726f57")
    expect(0x35D0D, "movabs", "rdx, 0x696f507365696c70")
    expect(0x35D20, "mov", "eax, 0x746e")
    expect_rip(0x35D30, "mov", 0x92258)
    expect(0x35D41, "call", "rax")
    expect(0x35D43, "mov", "dword ptr [rbp - 0x60], eax")
    expect(0x35D46, "mov", "byte ptr [rbp - 0x5c], 1")

    # March record base is rbp-0xD0: ownerUid string slot 0xA8 is rbp-0x28.
    expect(0x34C73, "lea", "rcx, [rbp - 0x28]")

    # Preferred positive current-position getter -> march pointIndex offset 0x10.
    expect_rip(0x34870, "mov", 0x92260)
    expect(0x34881, "call", "rax")
    expect(0x34883, "test", "eax, eax")
    expect(0x34885, "jle", "0x1800348ac")
    expect(0x34887, "mov", "dword ptr [rsp + 0x40], eax")
    expect(0x3488B, "mov", "byte ptr [rsp + 0x44], 1")
    expect(0x348AC, "cmp", "byte ptr [rsp + 0x44], 0")
    expect(0x348B1, "jne", "0x180034970")
    # GetMaxHP -> nullable maxHp offset 0x38.
    expect_rip(0x34478, "mov", 0x92268)
    expect(0x34489, "call", "rax")
    expect(0x3448B, "mov", "cl, 1")
    expect(0x344A6, "mov", "dword ptr [rsp + 0x68], eax")
    expect(0x344AA, "mov", "byte ptr [rsp + 0x6c], cl")

    # Monster/type predicates -> nullable bools at offsets 0xA0/0xA2/0xA4.
    expect_rip(0x34671, "mov", 0x92270)
    expect(0x34682, "call", "rax")
    expect(0x3468F, "test", "eax, eax")
    expect(0x34691, "setg", "al")
    expect(0x34694, "mov", "byte ptr [rbp - 0x30], al")
    expect(0x34697, "mov", "byte ptr [rbp - 0x2f], 1")

    expect_rip(0x3469B, "mov", 0x92278)
    expect(0x346AC, "call", "rax")
    expect(0x346AE, "mov", "byte ptr [rbp - 0x2e], al")
    expect(0x346B1, "mov", "byte ptr [rbp - 0x2d], 1")

    expect_rip(0x346B5, "mov", 0x92280)
    expect(0x346C6, "call", "rax")
    expect(0x346C8, "mov", "byte ptr [rbp - 0x2c], al")
    expect(0x346CB, "mov", "byte ptr [rbp - 0x2b], 1")
    point_routes = [
        {"source": "GetResType", "field": "resType", "valueOffset": "0x68", "presenceOffset": "0x6C", "gate": "pointType == 7", "transform": "int32 direct"},
        {"source": "GetResLevel", "field": "resLevel", "valueOffset": "0x70", "presenceOffset": "0x74", "gate": "pointType == 7", "transform": "int32 direct"},
        {"source": "GetWorldTreasureType", "field": "treasureType", "valueOffset": "0xA0", "presenceOffset": "0xA4", "gate": "runtimeClass == TreasurePointInfo", "transform": "int32 direct; producer has a direct-field fallback when getter is unavailable"},
        {"source": "get_configId", "field": "cfgId", "valueOffset": "0x40", "presenceOffset": "0x44", "gate": "runtimeClass == WorldSuppliesPoint", "transform": "int32 direct"},
    ]
    march_routes = [
        {"source": "GetMarchCurPosIndex", "field": "pointIndex", "valueOffset": "0x10", "presenceOffset": "0x14", "gate": "getter result > 0", "transform": "positive result preferred; otherwise preserve existing value or enter direct-field fallback chain"},
        {"source": "GetMaxHP", "field": "maxHp", "valueOffset": "0x38", "presenceOffset": "0x3C", "gate": "getter available", "transform": "int32 direct"},
        {"source": "IsMonsterOrOrdinaryBoss", "field": "isMonster", "valueOffset": "0xA0", "presenceOffset": "0xA1", "gate": "resolved producer path", "transform": "result > 0"},
        {"source": "IsOrdinaryBoss", "field": "requiresRally", "valueOffset": "0xA2", "presenceOffset": "0xA3", "gate": "getter available", "transform": "returned byte"},
        {"source": "IsNormalType", "field": "normalType", "valueOffset": "0xA4", "presenceOffset": "0xA5", "gate": "getter available", "transform": "returned byte"},
    ]
    return {
        "proxy": name,
        "sha256": hashlib.sha256(data).hexdigest(),
        "recordBases": {"point": "rbp-0xA0", "march": "rbp-0xD0"},
        "pointRoutes": point_routes,
        "marchRoutes": march_routes,
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
    require(proxies[0]["pointRoutes"] == proxies[1]["pointRoutes"], "secure/plain point routes differ")
    require(proxies[0]["marchRoutes"] == proxies[1]["marchRoutes"], "secure/plain march routes differ")
    return {
        "findingId": "LWB-R8-086",
        "date": "2026-09-27",
        "evidenceStatus": "RECOVERED CONTRACT",
        "sourceIdentity": {"path": str(binary), "sha256": digest},
        "recoveredResult": {
            "pointGetterRoutes": proxies[0]["pointRoutes"],
            "marchGetterRoutes": proxies[0]["marchRoutes"],
            "recordBases": proxies[0]["recordBases"],
        },
        "proxies": proxies,
        "limits": [
            "covers only the nine safe-region resolved getter routes named in R8-084",
            "does not claim complete population semantics for every R8-083 point/march field",
            "does not inspect or cross protected native region 0x3F8E0-0x40A6D",
            "does not recover XluaBridgeMapScanTick traversal, coordinate order, per-tick work, or retry/backoff",
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
