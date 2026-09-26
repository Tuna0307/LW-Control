#!/usr/bin/env python3
"""Recover native world-capture hook routing from LWBridge 0.3.1."""

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
HOOKS = [
    ("WorldPointManager.AddPointInfo", 0x3820F, 0x37510, 0x921F0),
    ("WorldPointManager.RemovePointInfo", 0x3823A, 0x37750, 0x921F8),
    ("WorldTileInfo.RemovePointInfo", 0x38265, 0x37770, 0x92200),
    ("WorldPointManager.ParseWorldPointRemove", 0x38290, 0x37570, 0x92208),
    ("WorldPointManager.ParseWorldPointFoldUp", 0x382BC, 0x37530, 0x92210),
    ("WorldMarchDataManager.AddMarch", 0x382E8, 0x374D0, 0x92218),
    ("WorldMarchDataManager.UpdateMarch", 0x38313, 0x377B0, 0x92220),
    ("WorldMarchDataManager.AddOrUpdateMarch", 0x3833E, 0x374F0, 0x92228),
    ("WorldMarchDataManager.TryRemoveMarch", 0x38369, 0x375B0, 0x92230),
    ("WorldTroopManager.UpdateTroop", 0x38395, 0x377D0, 0x92238),
]

FIELD_RESOLVERS = [
    ("PointInfo.pointIndex", 0x37B07, 0x37BAF, 0x91FC0),
    ("PointInfo.mainIndex", 0x37B1E, 0x37BC2, 0x91FC8),
    ("PointInfo.pointType", 0x37B35, 0x37BD5, 0x91FD0),
    ("WorldMarch._uuid", 0x37B4C, 0x37BE8, 0x91FD8),
    ("WorldMarch.pointIndex", 0x37B63, 0x37BFC, 0x91FE0),
]
GETTER_RESOLVERS = [
    ("GetResType", 0x37C4E, 0x37DD1, 0x92240),
    ("GetResLevel", 0x37C6B, 0x37DF6, 0x92248),
    ("GetWorldTreasureType", 0x37C8F, 0x37E1A, 0x92250),
    ("get_configId", 0x37CB6, 0x37E3E, 0x92258),
    ("GetMarchCurPosIndex", 0x37CD0, 0x37E63, 0x92260),
    ("GetMaxHP", 0x37CEA, 0x37E88, 0x92268),
    ("IsMonsterOrOrdinaryBoss", 0x37D05, 0x37EAD, 0x92270),
    ("IsOrdinaryBoss", 0x37D20, 0x37ED2, 0x92278),
    ("IsNormalType", 0x37D3B, 0x37EF7, 0x92280),
    ("GetSFSArray", 0x37D59, 0x37F1B, 0x92288),
    ("get_Count", 0x37D78, 0x37F3F, 0x92290),
    ("GetInt", 0x37D95, 0x37F59, 0x92298),
]

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

    def text_at(rva: int) -> str:
        off = int(pe.get_offset_from_rva(rva))
        out = bytearray()
        for value in data[off:off + 160]:
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

    def expect_rip(rva: int, mnemonic: str, target: int):
        ins = expect(rva, mnemonic)
        actual = rip_target(ins)
        require(actual == target + delta,
                f"{name}: 0x{rva:X} RIP target 0x{actual:X} != 0x{target + delta:X}")
        return ins

    def expect_direct(rva: int, mnemonic: str, target: int):
        ins = expect(rva, mnemonic)
        actual = direct_target(ins)
        require(actual == target,
                f"{name}: 0x{rva:X} direct target 0x{actual:X} != 0x{target:X}")
        return ins

    installed = []
    for label, start, detour, slot in HOOKS:
        label_ins = expect(start, "lea")
        label_target = rip_target(label_ins)
        require(label_target is not None and text_at(label_target) == label,
                f"{name}: hook label mismatch at 0x{start:X}")
        expect_rip(start + 7, "lea", slot)
        detour_ins = expect(start + 14, "lea")
        require(rip_target(detour_ins) == detour,
                f"{name}: hook detour mismatch for {label}")
        scan_off = int(pe.get_offset_from_rva(start))
        scan_rows = list(md.disasm(data[scan_off:scan_off + 48], base + start))
        calls = [ins for ins in scan_rows if ins.mnemonic == "call" and direct_target(ins) == 0x2BEC0]
        require(calls, f"{name}: missing hook installer call for {label}")
        installed.append({"name": label, "detourRva": f"0x{detour:X}",
                          "trampolineSlotRva": f"0x{slot + delta:X}"})
    fields = []
    for label, name_rva, store_rva, slot in FIELD_RESOLVERS:
        ins = expect(name_rva, "lea")
        target = rip_target(ins)
        require(target is not None and text_at(target) == label.split(".")[-1],
                f"{name}: field resolver label mismatch for {label}")
        expect_rip(store_rva, "mov", slot)
        fields.append({"field": label, "resolvedOffsetSlotRva": f"0x{slot + delta:X}"})

    getters = []
    for label, name_rva, store_rva, slot in GETTER_RESOLVERS:
        ins = expect(name_rva, "lea")
        target = rip_target(ins)
        require(target is not None and text_at(target) == label,
                f"{name}: getter resolver label mismatch for {label}")
        expect_rip(store_rva, "mov", slot)
        getters.append({"method": label, "callableSlotRva": f"0x{slot + delta:X}"})

    # Full-record detours call the original first, then tail into the producer.
    for call_rva, slot, jump_rva, producer in [
        (0x37519, 0x921F0, 0x37527, 0x34CC0),
        (0x374D9, 0x92218, 0x374E7, 0x34090),
        (0x377B9, 0x92220, 0x377C7, 0x34090),
        (0x374F9, 0x92228, 0x37507, 0x34090),
        (0x377D9, 0x92238, 0x377E7, 0x34090),
    ]:
        expect_rip(call_rva, "call", slot)
        expect_direct(jump_rva, "jmp", producer)
    # Point-removal detours preserve original behavior and feed the shared removal helper.
    expect_rip(0x37758, "call", 0x921F8)
    expect_direct(0x37765, "jmp", 0x39F20)
    expect_rip(0x3778A, "call", 0x92200)
    expect_direct(0x37797, "jmp", 0x39F20)
    expect_rip(0x3779E, "call", 0x92200)
    expect_direct(0x377AB, "jmp", 0x39F20)

    # Parse remove/fold-up capture pointIds before tail-calling the original handler.
    expect_direct(0x37548, "call", 0x361A0)
    expect_rip(0x37561, "jmp", 0x92210)
    expect_direct(0x37588, "call", 0x361A0)
    expect_rip(0x375A1, "jmp", 0x92208)
    point_ids = expect(0x361E7, "lea")
    point_ids_target = rip_target(point_ids)
    require(point_ids_target is not None and text_at(point_ids_target) == "pointIds",
            f"{name}: pointIds parse key changed")
    expect_rip(0x361F5, "mov", 0x92288)
    expect_rip(0x36215, "mov", 0x92290)
    expect_rip(0x36231, "mov", 0x92298)
    expect_direct(0x36245, "call", 0x39F20)

    # Shared direct point-removal helper only accepts positive IDs.
    expect(0x39F20, "test", "ecx, ecx")
    expect(0x39F22, "jle")
    expect(0x39F8E, "cmp", "rdx, 0x10000")
    expect_direct(0x3A028, "call", 0x3A210)
    # Canonical point capture: required pointIndex, mainIndex fallback, main-point-only admission.
    expect_rip(0x34E87, "mov", 0x91FC0)
    expect_rip(0x34E97, "mov", 0x91FC8)
    expect_rip(0x34EA5, "mov", 0x91FD0)
    expect(0x34EB3, "test", "r8d, r8d")
    expect(0x34EB6, "jle")
    expect(0x34EBE, "cmovle", "edx, r8d")
    expect(0x34EC6, "cmp", "r8d, edx")
    expect(0x34EC9, "sete", "byte ptr [rsp + 0x6c]")
    expect(0x34ECE, "jne")

    for rva, slot in [
        (0x35A88, 0x92240),
        (0x35AB7, 0x92248),
        (0x35C73, 0x92250),
        (0x35D30, 0x92258),
    ]:
        expect_rip(rva, "mov", slot)

    # Point pending map hashes/compares normalized mainIndex and replaces an existing key.
    expect(0x35DFC, "mov", "eax, dword ptr [rsp + 0x64]")
    expect(0x35E00, "cmp", "eax, dword ptr [rbx + 0x10]")
    expect(0x35F94, "cmp", "rdx, 0x10000")
    expect(0x35F9D, "cmp", "rbx, rdi")
    expect_direct(0x35FED, "call", 0x3A210)
    # March capture requires a positive first qword; R8-083 identifies this field as uuid.
    expect(0x342EE, "mov", "rax, qword ptr [rax + rbx]")
    expect(0x342F2, "test", "rax, rax")
    expect(0x342F5, "jle")
    expect(0x3436A, "mov", "qword ptr [rsp + 0x30], rax")
    for rva, slot in [
        (0x34478, 0x92268),
        (0x34671, 0x92270),
        (0x3469B, 0x92278),
        (0x346B5, 0x92280),
        (0x34870, 0x92260),
    ]:
        expect_rip(rva, "mov", slot)

    # March pending map hashes/compares the same qword identity and replaces existing UUIDs.
    expect(0x34A3D, "mov", "rsi, qword ptr [rsp + 0x30]")
    expect(0x34A4E, "cmp", "rsi, qword ptr [rbx + 0x10]")
    expect(0x34B08, "cmp", "rdx, 0x10000")
    expect(0x34B11, "cmp", "rbx, r11")
    expect_direct(0x34B47, "call", 0x3A210)

    # TryRemoveMarch preserves the original first, then admits/removes by its positive qword ID.
    expect_rip(0x375C2, "call", 0x92230)
    expect(0x375CD, "test", "rbx, rbx")
    expect(0x375D0, "jle")
    expect(0x3762C, "cmp", "rdx, 0x10000")
    expect_direct(0x376F0, "call", 0x3A210)
    return {
        "proxy": name,
        "sha256": hashlib.sha256(data).hexdigest(),
        "hooks": installed,
        "resolvedFields": fields,
        "resolvedGetters": getters,
        "producerRvas": {"point": "0x34CC0-0x3619D", "march": "0x34090-0x34CBD"},
        "removalRvas": {"parsePointIds": "0x361A0-0x36260", "pointRemoval": "0x39F20-0x3A079"},
        "queueSemantics": {
            "sharedRecordCeiling": 65536,
            "pointIdentity": "normalized mainIndex",
            "marchIdentity": "captured uuid",
            "replacementAtCeiling": True,
            "newEntryAtCeiling": "dropped path 0x3A210",
        },
    }

def inspect(binary: Path) -> dict[str, object]:
    outer = binary.read_bytes()
    digest = hashlib.sha256(outer).hexdigest()
    require(digest == EXPECTED_SHA256, f"unsupported reference SHA-256: {digest}")
    outer_pe = pefile.PE(data=outer, fast_load=False)
    proxies = [inspect_proxy(name, extract_proxy(outer, outer_pe, name))
               for name in ("secure", "plain")]
    return {
        "findingId": "LWB-R8-084",
        "date": "2026-09-27",
        "evidenceStatus": "RECOVERED CONTRACT",
        "sourceIdentity": {"path": str(binary), "sha256": digest},
        "recoveredResult": {
            "hookCount": 10,
            "pointProducer": "0x34CC0",
            "marchProducer": "0x34090",
            "pointRemovalHelper": "0x39F20",
            "parsePointRemovalHelper": "0x361A0",
            "parseRemovalKey": "pointIds",
        },
        "proxies": proxies,
        "limits": [
            "does not inspect or cross protected native region 0x3F8E0-0x40A6D",
            "does not recover XluaBridgeMapScanTick block traversal, coordinate order, request pacing, or retry/backoff",
            "resolved method/field names establish producer inputs; they do not by themselves prove every later host-normalized field meaning",
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
