#!/usr/bin/env python3
"""Recover native-capture point/march/train serializers from LWBridge 0.3.1."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RDI, X86_REG_RIP

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
PROXIES = {
    "secure": (0x987F74, 0x95800, "481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400"),
    "plain": (0xA1D774, 0x96000, "c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794"),
}
FUNCS = {"point": (0x30F70, 0x31FD3), "march": (0x30820, 0x30F65), "train": (0x31FE0, 0x321FB)}
POINT_KEYS = "pointIndex mainIndex isMainPoint pointType serverId srcServerId worldId uuid ownerUid allianceId playerName alAbbr name power id itemId cfgId level quality isSpecial resType resLevel state curHp curMaxHp shieldEndTime protectEndTime completionTime expiredTime taskExpireTime actEndTime gatherMarchUuid gatherUid gatherAllianceId stolenCount heroCount rewarded memberCount ownerServer treasureType rewardedCount diggingCount fromPoint multiple lastHpTime unavailableTime startTime expireTime createTime recoverSpeed fireSpeed complete runtimeClass eventId allianceAbbr ownerName killerId".split()
MARCH_KEYS = "uuid marchType pointIndex ownerServer ownerCurServerId worldId status maxHp monsterId monsterType monsterSpecialType monsterRallyNum power startTime endTime curHp isMonster requiresRally normalType ownerUid ownerName allianceUid allianceName allianceAbbr train".split()
TRAIN_KEYS = "uuid cfgId type configId quality carriageNum".split()

class InspectError(ValueError):
    pass

def require(ok: bool, message: str) -> None:
    if not ok:
        raise InspectError(message)
def extract_proxy(outer: bytes, outer_pe: pefile.PE, name: str) -> bytes:
    rva, size, expected = PROXIES[name]
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

    def text_at(rva: int) -> str:
        try:
            off = int(pe.get_offset_from_rva(rva))
        except Exception:
            return ""
        out = bytearray()
        for value in data[off:off + 96]:
            if 0x20 <= value <= 0x7E:
                out.append(value)
            else:
                break
        return out.decode("ascii", "replace")

    def rip_text(ins) -> str:
        for operand in ins.operands:
            if operand.type == X86_OP_MEM and operand.mem.base == X86_REG_RIP:
                return text_at(int(ins.address + ins.size + operand.mem.disp - base))
        return ""

    def call_target(ins) -> int | None:
        if ins.mnemonic != "call" or not ins.operands:
            return None
        operand = ins.operands[0]
        if operand.type != X86_OP_IMM:
            return None
        return int(operand.imm - base)

    def rdi_offsets(rows) -> list[int]:
        out = []
        for ins in rows:
            for operand in ins.operands:
                if operand.type == X86_OP_MEM and operand.mem.base == X86_REG_RDI:
                    out.append(int(operand.mem.disp))
        return out

    def fields_for(kind: str) -> list[dict[str, object]]:
        begin, end = FUNCS[kind]
        off = int(pe.get_offset_from_rva(begin))
        rows = list(md.disasm(data[off:off + (end - begin)], base + begin))
        keys = []
        for index, ins in enumerate(rows):
            value = rip_text(ins)
            if value and value.replace("_", "").isalnum() and value not in {"true", "false", "null"}:
                keys.append((index, value, int(ins.address - base)))
        fields = []
        for n, (index, key, key_rva) in enumerate(keys):
            stop = keys[n + 1][0] if n + 1 < len(keys) else len(rows)
            window = rows[index:stop]
            calls = [target for target in (call_target(ins) for ins in window) if target is not None]
            texts = {rip_text(ins) for ins in window}
            nullable = "null" in texts
            offsets = rdi_offsets(window)
            field_kind = "unknown"
            value_offset = None
            presence_offset = None

            if 0x387C0 in calls:
                field_kind = "string"
                nullable = True
                for ins in reversed(rows[max(0, index - 5):index]):
                    if ins.mnemonic == "lea" and len(ins.operands) >= 2:
                        mem = ins.operands[1]
                        if mem.type == X86_OP_MEM and mem.mem.base == X86_REG_RDI:
                            value_offset = int(mem.mem.disp)
                            break
            elif 0x31FE0 in calls:
                field_kind = "object:train"
                for ins in window:
                    if ins.mnemonic == "cmp" and ins.operands and ins.operands[0].type == X86_OP_MEM and ins.operands[0].mem.base == X86_REG_RDI:
                        presence_offset = int(ins.operands[0].mem.disp)
                    if ins.mnemonic == "lea" and len(ins.operands) >= 2 and ins.operands[1].type == X86_OP_MEM and ins.operands[1].mem.base == X86_REG_RDI:
                        value_offset = int(ins.operands[1].mem.disp)
            elif 0x300E0 in calls:
                field_kind = "f32"
                for ins in window:
                    if ins.mnemonic == "cmp" and ins.operands and ins.operands[0].type == X86_OP_MEM and ins.operands[0].mem.base == X86_REG_RDI:
                        presence_offset = int(ins.operands[0].mem.disp)
                    if ins.mnemonic == "movss" and len(ins.operands) >= 2 and ins.operands[1].type == X86_OP_MEM and ins.operands[1].mem.base == X86_REG_RDI:
                        value_offset = int(ins.operands[1].mem.disp)
            elif 0x2FEB0 in calls:
                field_kind = "i32"
                for ins in window:
                    if ins.mnemonic == "cmp" and ins.operands and ins.operands[0].type == X86_OP_MEM and ins.operands[0].mem.base == X86_REG_RDI:
                        presence_offset = int(ins.operands[0].mem.disp)
                    if ins.mnemonic == "mov" and len(ins.operands) >= 2 and ins.op_str.startswith("edx, dword ptr [rdi"):
                        value_offset = int(ins.operands[1].mem.disp)
            elif 0x30320 in calls:
                quotes = sum(1 for ins in window if ins.mnemonic == "mov" and ins.op_str == "dl, 0x22")
                field_kind = "quoted-i64" if quotes >= 2 else "i64"
                for ins in window:
                    if ins.mnemonic == "cmp" and ins.operands and ins.operands[0].type == X86_OP_MEM and ins.operands[0].mem.base == X86_REG_RDI:
                        presence_offset = int(ins.operands[0].mem.disp)
                    if ins.mnemonic == "mov" and len(ins.operands) >= 2 and ins.op_str.startswith("rdx, qword ptr [rdi"):
                        value_offset = int(ins.operands[1].mem.disp)
            else:
                cmp_offsets = []
                for ins in window:
                    if ins.mnemonic == "cmp" and ins.operands and ins.operands[0].type == X86_OP_MEM and ins.operands[0].mem.base == X86_REG_RDI:
                        cmp_offsets.append(int(ins.operands[0].mem.disp))
                if cmp_offsets:
                    field_kind = "bool"
                    if nullable and len(cmp_offsets) >= 2:
                        presence_offset, value_offset = cmp_offsets[0], cmp_offsets[1]
                    else:
                        value_offset = cmp_offsets[-1]

            fields.append({
                "key": key,
                "keyRva": f"0x{key_rva:X}",
                "kind": field_kind,
                "nullable": bool(nullable),
                "valueOffset": None if value_offset is None else f"0x{value_offset:X}",
                "presenceOffset": None if presence_offset is None else f"0x{presence_offset:X}",
            })
        return fields

    point = fields_for("point")
    march = fields_for("march")
    train = fields_for("train")
    point_keys = [f["key"] for f in point]
    march_keys = [f["key"] for f in march]
    train_keys = [f["key"] for f in train]
    require(point_keys == POINT_KEYS, f"{name}: point field order changed: {point_keys}")
    require(march_keys == MARCH_KEYS, f"{name}: march field order changed: {march_keys}")
    require(train_keys == TRAIN_KEYS, f"{name}: train field order changed: {train_keys}")

    capture_begin, capture_end = 0x39770, 0x398C0
    capture_off = int(pe.get_offset_from_rva(capture_begin))
    capture_rows = list(md.disasm(data[capture_off:capture_off + (capture_end - capture_begin)], base + capture_begin))
    capture = {int(ins.address - base): ins for ins in capture_rows}
    require(capture[0x39815].mnemonic == "imul" and "0x3a0" in capture[0x39815].op_str,
            f"{name}: point element stride changed")
    require(call_target(capture[0x39823]) == 0x30F70, f"{name}: point serializer call changed")
    require(capture[0x398A5].mnemonic == "imul" and "0x1b0" in capture[0x398A5].op_str,
            f"{name}: march element stride changed")
    require(call_target(capture[0x398B3]) == 0x30820, f"{name}: march serializer call changed")

    helper_begin, helper_end = 0x387C0, 0x388C7
    helper_off = int(pe.get_offset_from_rva(helper_begin))
    helper_rows = list(md.disasm(data[helper_off:helper_off + (helper_end - helper_begin)], base + helper_begin))
    helper = {int(ins.address - base): ins for ins in helper_rows}
    require(helper[0x3881B].mnemonic == "cmp" and "[rdi + 0x20]" in helper[0x3881B].op_str,
            f"{name}: nullable string presence test changed")
    require(call_target(helper[0x38837]) == 0x384A0, f"{name}: string escape helper changed")
    require(helper[0x38825].mnemonic == "mov" and helper[0x38825].op_str == "dl, 0x22",
            f"{name}: opening string quote changed")
    require(helper[0x3885B].mnemonic == "mov" and helper[0x3885B].op_str == "dl, 0x22",
            f"{name}: closing string quote changed")
    require(rip_text(helper[0x388A5]).startswith("null"), f"{name}: nullable string null literal changed")

    return {
        "proxy": name,
        "sha256": hashlib.sha256(data).hexdigest(),
        "serializerRvas": {k: f"0x{a:X}-0x{z:X}" for k, (a, z) in FUNCS.items()},
        "structureSizes": {"point": 0x3A0, "march": 0x1B0, "train": 0x38},
        "pointFields": point,
        "marchFields": march,
        "trainFields": train,
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
    return {
        "findingId": "LWB-R8-083",
        "date": "2026-09-27",
        "evidenceStatus": "RECOVERED CONTRACT",
        "sourceIdentity": {"path": str(binary), "sha256": digest},
        "recoveredResult": {
            "pointFieldCount": 57,
            "marchFieldCount": 25,
            "trainFieldCount": 6,
            "primitiveWriters": {
                "i32": "0x2FEB0",
                "f32": "0x300E0",
                "i64": "0x30320",
                "optionalString": "0x387C0",
                "train": "0x31FE0"
            },
            "pointSerializerRva": "0x30F70-0x31FD3",
            "marchSerializerRva": "0x30820-0x30F65",
            "trainSerializerRva": "0x31FE0-0x321FB",
            "pointElementBytes": 0x3A0,
            "marchElementBytes": 0x1B0,
            "pointFieldOrder": POINT_KEYS,
            "marchFieldOrder": MARCH_KEYS,
            "trainFieldOrder": TRAIN_KEYS,
            "nullableStringSemantics": (
                "string fields routed through the shared helper are always emitted; "
                "present values are JSON-escaped and quoted, absent values are literal null"
            ),
            "representation": (
                "per-field kind/nullability/native value and presence offsets are derived "
                "from each verified proxy under proxies[].pointFields/marchFields/trainFields"
            ),
        },
        "proxies": proxies,
        "limits": [
            "does not recover MapScanTick block traversal or coordinate order",
            "does not recover producer retry/backoff rules",
            "does not establish how often the serializer is called by the outer application beyond separately recovered pump behavior",
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
