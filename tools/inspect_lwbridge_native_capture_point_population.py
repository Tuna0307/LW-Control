#!/usr/bin/env python3
"""Recover safe native-capture point population rules from LWBridge 0.3.1."""

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

class InspectError(ValueError):
    pass

def require(ok: bool, message: str) -> None:
    if not ok:
        raise InspectError(message)
POINT_BUNDLE_NAMES = [
    (0x32B49, "ownerUid"), (0x32B97, "uuid"),
    (0x32BE7, "serverId"), (0x32C37, "srcServerId"),
    (0x32C87, "worldId"), (0x32CD7, "itemId"),
    (0x32D27, "id"), (0x32D77, "level"),
    (0x32DC7, "quality"), (0x32E17, "state"),
    (0x32E67, "curHp"), (0x32EB7, "curMaxHp"),
    (0x32F07, "allianceId"), (0x32F57, "playerName"),
    (0x32FA7, "alAbbr"), (0x32FF7, "name"),
    (0x33047, "power"), (0x33097, "specialType"),
    (0x330E7, "protectEndTime"), (0x33137, "shieldEndTime"),
    (0x33187, "completionTime"), (0x331D7, "cfgId"),
    (0x33227, "stealList"), (0x33277, "expiredTime"),
    (0x332C7, "taskExpireTime"),
]
POINT_BUNDLE_NAMES += [
    (0x33317, "actEndTime"), (0x33367, "gatherMarchUuid"),
    (0x333B7, "gatherUid"), (0x33407, "gatherAllianceId"),
    (0x33457, "lastHpTime"), (0x334A7, "unavailableTime"),
    (0x334F7, "recoverSpeed"), (0x33547, "fireSpeed"),
    (0x33597, "heroList"), (0x335E7, "rewarded"),
    (0x33637, "memberList"), (0x33687, "ownerServer"),
    (0x336D7, "eventId"), (0x33727, "allianceAbbr"),
    (0x33777, "rewardUserList"), (0x337C7, "diggingUserList"),
    (0x33817, "complete"), (0x33867, "ownerName"),
    (0x338B7, "startTime"), (0x33907, "expireTime"),
    (0x33957, "createTime"), (0x339A7, "type"),
    (0x339F7, "fromPoint"), (0x33A47, "multiple"),
    (0x33A97, "killerId"),
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

    def expect_rip(rva: int, mnemonic: str, target: int):
        ins = expect(rva, mnemonic)
        actual = rip_target(ins)
        require(actual == target + delta,
                f"{name}: 0x{rva:X} RIP target 0x{actual:X} != 0x{target + delta:X}")
        return ins

    for rva, field in POINT_BUNDLE_NAMES:
        ins = expect(rva, "lea")
        target = rip_target(ins)
        require(target is not None and text_at(target) == field,
                f"{name}: point bundle name mismatch at 0x{rva:X}")
    require(len(POINT_BUNDLE_NAMES) == 50, "point bundle field count changed")

    # Core point identity fields from R8-084.
    expect_rip(0x34E87, "mov", 0x91FC0)
    expect_rip(0x34E97, "mov", 0x91FC8)
    expect_rip(0x34EA5, "mov", 0x91FD0)
    expect(0x34EBE, "cmovle", "edx, r8d")
    expect(0x34EC6, "cmp", "r8d, edx")
    expect(0x34EC9, "sete", "byte ptr [rsp + 0x6c]")
    expect(0x34ECE, "jne", "0x18003604d")

    # Direct nullable int32 routes.
    direct_i32 = [
        (0x350A0, "qword ptr [rbp + 0x320]", 0x350B8, "dword ptr [rsp + 0x70]"),
        (0x350D8, "qword ptr [rbp + 0x328]", 0x350F0, "dword ptr [rsp + 0x78]"),
        (0x35110, "qword ptr [rbp + 0x330]", 0x35128, "dword ptr [rbp - 0x80]"),
        (0x35144, "qword ptr [rbp + 0x358]", 0x3515C, "dword ptr [rbp - 0x78]"),
        (0x35178, "qword ptr [rbp + 0x360]", 0x35190, "dword ptr [rbp - 0x70]"),
        (0x351AC, "qword ptr [rbp + 0x368]", 0x351C4, "dword ptr [rbp - 0x68]"),
    ]
    for load_rva, load_op, store_rva, store_op in direct_i32:
        expect(load_rva, "mov", f"rax, {load_op}")
        expect(store_rva, "mov", f"{store_op}, ecx")
    for load_rva, load_op, store_rva, store_op in [
        (0x351E0, "qword ptr [rbp + 0x3b8]", 0x351F8, "dword ptr [rbp - 0x60]"),
        (0x35214, "qword ptr [rbp + 0x340]", 0x3522C, "dword ptr [rbp - 0x58]"),
        (0x35248, "qword ptr [rbp + 0x338]", 0x35260, "dword ptr [rbp - 0x50]"),
        (0x3527C, "qword ptr [rbp + 0x348]", 0x35294, "dword ptr [rbp - 0x48]"),
        (0x352B0, "qword ptr [rbp + 0x350]", 0x352C8, "dword ptr [rbp - 0x40]"),
        (0x3536F, "qword ptr [rbp + 0x430]", 0x35387, "dword ptr [rbp - 8]"),
        (0x35411, "qword ptr [rbp + 0x490]", 0x35429, "dword ptr [rbp + 0x20]"),
    ]:
        expect(load_rva, "mov", f"rax, {load_op}")
        expect(store_rva, "mov", f"{store_op}, ecx")

    expect(0x3531E, "mov", "rcx, qword ptr [rbp + 0x420]")
    expect(0x3532B, "mov", "eax, dword ptr [rcx + rbx]")
    expect(0x35336, "mov", "dword ptr [rbp - 0x18], eax")
    expect(0x353DD, "mov", "rcx, qword ptr [rbp + 0x488]")
    expect(0x353EA, "mov", "eax, dword ptr [rcx + rbx]")
    expect(0x353F5, "mov", "dword ptr [rbp + 0x18], eax")

    # Five list-backed count fields use the same _size helper.
    size_name = expect(0x3A0C7, "lea")
    size_target = rip_target(size_name)
    require(size_target is not None and text_at(size_target) == "_size",
            f"{name}: list count helper no longer resolves _size")
    for load_rva, load_op, call_rva, dest_rva, dest_op in [
        (0x352E4, "r8, qword ptr [rbp + 0x3c0]", 0x352F5, 0x352FD, "qword ptr [rbp - 0x28]"),
        (0x35301, "r8, qword ptr [rbp + 0x418]", 0x35312, 0x3531A, "qword ptr [rbp - 0x20]"),
        (0x35352, "r8, qword ptr [rbp + 0x428]", 0x35363, 0x3536B, "qword ptr [rbp - 0x10]"),
        (0x353A3, "r8, qword ptr [rbp + 0x448]", 0x353B4, 0x353BC, "qword ptr [rbp + 8]"),
        (0x353C0, "r8, qword ptr [rbp + 0x450]", 0x353D1, 0x353D9, "qword ptr [rbp + 0x10]"),
    ]:
        expect(load_rva, "mov", load_op)
        expect(call_rva, "call", "0x18003a080")
        expect(dest_rva, "mov", f"{dest_op}, rcx")
    # qword/time routes; three int32 source times are sign-extended into i64 record fields.
    for load_rva, load_op, read_rva, read_mnemonic, store_rva, store_op in [
        (0x35445, "rax, qword ptr [rbp + 0x318]", 0x35452, "mov", 0x3545F, "qword ptr [rbp + 0x38]"),
        (0x3547F, "rax, qword ptr [rbp + 0x390]", 0x3548C, "mov", 0x35499, "qword ptr [rbp + 0x28]"),
        (0x354B9, "rax, qword ptr [rbp + 0x3a0]", 0x354D6, "movsxd", 0x354E5, "qword ptr [rbp + 0x48]"),
        (0x354F8, "rax, qword ptr [rbp + 0x3a8]", 0x35515, "movsxd", 0x35524, "qword ptr [rbp + 0x58]"),
        (0x35537, "rax, qword ptr [rbp + 0x3b0]", 0x35544, "mov", 0x35551, "qword ptr [rbp + 0x68]"),
        (0x35571, "rax, qword ptr [rbp + 0x3c8]", 0x3557E, "mov", 0x3558B, "qword ptr [rbp + 0x78]"),
        (0x355B7, "rax, qword ptr [rbp + 0x3d0]", 0x355C4, "mov", 0x355D1, "qword ptr [rbp + 0x88]"),
        (0x35600, "rax, qword ptr [rbp + 0x3d8]", 0x3560D, "mov", 0x3561A, "qword ptr [rbp + 0x98]"),
        (0x35649, "rax, qword ptr [rbp + 0x3e0]", 0x35656, "mov", 0x35663, "qword ptr [rbp + 0xa8]"),
        (0x35692, "rax, qword ptr [rbp + 0x3f8]", 0x356AF, "movsxd", 0x356BE, "qword ptr [rbp + 0xb8]"),
        (0x356E0, "rax, qword ptr [rbp + 0x400]", 0x356ED, "mov", 0x356FA, "qword ptr [rbp + 0xc8]"),
    ]:
        expect(load_rva, "mov", load_op)
        expect(read_rva, read_mnemonic)
        store_reg = "rax" if store_rva == 0x35524 else "rcx"
        expect(store_rva, "mov", f"{store_op}, {store_reg}")

    # Float32 fields.
    expect(0x35729, "mov", "rax, qword ptr [rbp + 0x408]")
    expect(0x35736, "movss", "xmm0, dword ptr [rax + rbx]")
    expect(0x35757, "mov", "dword ptr [rbp + 0x108], eax")
    expect(0x3577E, "mov", "rax, qword ptr [rbp + 0x410]")
    expect(0x3578B, "movss", "xmm0, dword ptr [rax + rbx]")
    expect(0x357AC, "mov", "dword ptr [rbp + 0x110], eax")
    # Eleven raw IL2CPP string fields feed exact native string slots through 0x3A140.
    for load_rva, load_op, call_rva, dest_rva, dest_op in [
        (0x357D3, "r8, qword ptr [rbp + 0x310]", 0x357E2, 0x357EA, "rcx, [rbp + 0x120]"),
        (0x35807, "r8, qword ptr [rbp + 0x370]", 0x35816, 0x3581E, "rcx, [rbp + 0x148]"),
        (0x3583B, "r8, qword ptr [rbp + 0x378]", 0x3584A, 0x35852, "rcx, [rbp + 0x170]"),
        (0x3586F, "r8, qword ptr [rbp + 0x380]", 0x3587E, 0x35886, "rcx, [rbp + 0x198]"),
        (0x358A3, "r8, qword ptr [rbp + 0x388]", 0x358B2, 0x358BA, "rcx, [rbp + 0x1c0]"),
        (0x358D7, "r8, qword ptr [rbp + 0x3e8]", 0x358E6, 0x358EE, "rcx, [rbp + 0x1e8]"),
        (0x3590B, "r8, qword ptr [rbp + 0x3f0]", 0x3591A, 0x35922, "rcx, [rbp + 0x210]"),
        (0x3593F, "r8, qword ptr [rbp + 0x438]", 0x3594E, 0x35956, "rcx, [rbp + 0x260]"),
        (0x35973, "r8, qword ptr [rbp + 0x440]", 0x35982, 0x3598A, "rcx, [rbp + 0x288]"),
        (0x359A7, "r8, qword ptr [rbp + 0x460]", 0x359B6, 0x359BE, "rcx, [rbp + 0x2b0]"),
        (0x359DB, "r8, qword ptr [rbp + 0x498]", 0x359EA, 0x359F2, "rcx, [rbp + 0x2d8]"),
    ]:
        expect(load_rva, "mov", load_op)
        expect(call_rva, "call", "0x18003a140")
        expect(dest_rva, "lea", dest_op)

    # runtimeClass is the actual IL2CPP runtime class name.
    expect_rip(0x34ED7, "call", 0x91F40)
    expect_rip(0x35A0F, "mov", 0x91F38)
    expect(0x35A16, "mov", "rcx, qword ptr [rbp + 0x688]")
    expect(0x35A1D, "call", "rax")
    expect(0x35A3E, "lea", "rcx, [rbp + 0x238]")
    # Derived booleans from raw fields.
    expect(0x35A62, "mov", "rax, qword ptr [rbp + 0x398]")
    expect(0x35A6F, "cmp", "dword ptr [rax + rbx], 0")
    expect(0x35A73, "setne", "byte ptr [rbp + 0x118]")
    expect(0x35A7A, "mov", "byte ptr [rbp + 0x119], 1")
    expect(0x35C39, "mov", "rax, qword ptr [rbp + 0x458]")
    expect(0x35C46, "movzx", "eax, byte ptr [rax + rbx]")
    expect(0x35C67, "mov", "byte ptr [rbp + 0x11a], al")
    expect(0x35C6D, "mov", "byte ptr [rbp + 0x11b], cl")

    # Resource pointType 7 getters override/populate resType and resLevel.
    expect(0x35A81, "cmp", "dword ptr [rsp + 0x68], 7")
    expect_rip(0x35A88, "mov", 0x92240)
    expect(0x35A9B, "mov", "dword ptr [rbp - 0x38], eax")
    expect(0x35A9E, "mov", "byte ptr [rbp - 0x34], 1")
    expect_rip(0x35AB7, "mov", 0x92248)
    expect(0x35ACA, "mov", "dword ptr [rbp - 0x30], eax")
    expect(0x35ACD, "mov", "byte ptr [rbp - 0x2c], 1")

    # TreasurePointInfo class-specific times and treasureType getter/raw-type fallback.
    expect(0x35B17, "cmp", "r11, 0x11")
    expect(0x35B5E, "mov", "rax, qword ptr [rbp + 0x468]")
    expect(0x35B78, "mov", "qword ptr [rbp + 0xd8], rcx")
    expect(0x35BA7, "mov", "rax, qword ptr [rbp + 0x470]")
    expect(0x35BC1, "mov", "qword ptr [rbp + 0xe8], rcx")
    expect(0x35BF0, "mov", "rax, qword ptr [rbp + 0x478]")
    expect(0x35C0A, "mov", "qword ptr [rbp + 0xf8], rcx")
    expect_rip(0x35C73, "mov", 0x92250)
    expect(0x35CA1, "mov", "rax, qword ptr [rbp + 0x480]")
    expect(0x35CCB, "mov", "dword ptr [rbp], ecx")
    expect(0x35CCE, "mov", "byte ptr [rbp + 4], al")
    # WorldSuppliesPoint get_configId overrides the raw cfgId when the getter exists.
    expect(0x35CF1, "cmp", "r11, 0x12")
    expect_rip(0x35D30, "mov", 0x92258)
    expect(0x35D41, "call", "rax")
    expect(0x35D43, "mov", "dword ptr [rbp - 0x60], eax")
    expect(0x35D46, "mov", "byte ptr [rbp - 0x5c], 1")

    field_sources = [
        ("pointIndex", "PointInfo.pointIndex", "direct positive canonical identity"),
        ("mainIndex", "PointInfo.mainIndex", "non-positive falls back to pointIndex"),
        ("isMainPoint", "pointIndex/mainIndex", "equality after normalization"),
        ("pointType", "PointInfo.pointType", "direct"),
        ("serverId", "serverId", "direct nullable i32"),
        ("srcServerId", "srcServerId", "direct nullable i32"),
        ("worldId", "worldId", "direct nullable i32"),
        ("uuid", "uuid", "direct nullable quoted-i64"),
        ("ownerUid", "ownerUid", "IL2CPP string conversion"),
        ("allianceId", "allianceId", "IL2CPP string conversion"),
        ("playerName", "playerName", "IL2CPP string conversion"),
        ("alAbbr", "alAbbr", "IL2CPP string conversion"),
        ("name", "name", "IL2CPP string conversion"),
        ("power", "power", "direct nullable i64"),
        ("id", "id", "direct nullable i32"),
        ("itemId", "itemId", "direct nullable i32"),
        ("cfgId", "cfgId/get_configId", "raw nullable i32; WorldSuppliesPoint getter overrides when available"),
        ("level", "level", "direct nullable i32"),
        ("quality", "quality", "direct nullable i32"),
        ("isSpecial", "specialType", "present raw field -> specialType != 0"),
    ]
    field_sources += [
        ("resType", "GetResType", "pointType == 7 getter"),
        ("resLevel", "GetResLevel", "pointType == 7 getter"),
        ("state", "state", "direct nullable i32"),
        ("curHp", "curHp", "direct nullable i32"),
        ("curMaxHp", "curMaxHp", "direct nullable i32"),
        ("shieldEndTime", "shieldEndTime", "int32 sign-extended to nullable i64"),
        ("protectEndTime", "protectEndTime", "int32 sign-extended to nullable i64"),
        ("completionTime", "completionTime", "direct nullable i64"),
        ("expiredTime", "expiredTime", "direct nullable i64"),
        ("taskExpireTime", "taskExpireTime", "direct nullable i64"),
        ("actEndTime", "actEndTime", "direct nullable i64"),
        ("gatherMarchUuid", "gatherMarchUuid", "direct nullable quoted-i64"),
        ("gatherUid", "gatherUid", "IL2CPP string conversion"),
        ("gatherAllianceId", "gatherAllianceId", "IL2CPP string conversion"),
        ("stolenCount", "stealList._size", "runtime list _size"),
        ("heroCount", "heroList._size", "runtime list _size"),
        ("rewarded", "rewarded", "direct nullable i32"),
        ("memberCount", "memberList._size", "runtime list _size"),
        ("ownerServer", "ownerServer", "direct nullable i32"),
        ("treasureType", "GetWorldTreasureType/type", "TreasurePointInfo getter, raw type fallback"),
        ("rewardedCount", "rewardUserList._size", "runtime list _size"),
        ("diggingCount", "diggingUserList._size", "runtime list _size"),
        ("fromPoint", "fromPoint", "direct nullable i32"),
        ("multiple", "multiple", "direct nullable i32"),
        ("lastHpTime", "lastHpTime", "int32 sign-extended to nullable i64"),
        ("unavailableTime", "unavailableTime", "direct nullable i64"),
        ("startTime", "startTime", "TreasurePointInfo-only nullable i64"),
        ("expireTime", "expireTime", "TreasurePointInfo-only nullable i64"),
        ("createTime", "createTime", "TreasurePointInfo-only nullable i64"),
        ("recoverSpeed", "recoverSpeed", "direct nullable f32"),
        ("fireSpeed", "fireSpeed", "direct nullable f32"),
        ("complete", "complete", "raw byte -> nullable bool"),
        ("runtimeClass", "IL2CPP runtime class name", "object_get_class + class_get_name"),
        ("eventId", "eventId", "IL2CPP string conversion"),
        ("allianceAbbr", "allianceAbbr", "IL2CPP string conversion"),
        ("ownerName", "ownerName", "IL2CPP string conversion"),
        ("killerId", "killerId", "IL2CPP string conversion"),
    ]
    require(len(field_sources) == 57, f"{name}: point field coverage count {len(field_sources)}")
    return {
        "proxy": name,
        "sha256": hashlib.sha256(data).hexdigest(),
        "pointBundleFields": [field for _, field in POINT_BUNDLE_NAMES],
        "pointSerializerFieldCoverage": [
            {"field": field, "source": source, "transform": transform}
            for field, source, transform in field_sources
        ],
        "listCountField": "_size",
        "runtimeClassRule": "il2cpp_object_get_class -> il2cpp_class_get_name",
        "classSpecificRules": {
            "TreasurePointInfo": ["startTime", "expireTime", "createTime", "treasureType"],
            "WorldSuppliesPoint": ["cfgId getter override"],
        },
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
    for key in ("pointBundleFields", "pointSerializerFieldCoverage",
                "listCountField", "runtimeClassRule", "classSpecificRules"):
        require(proxies[0][key] == proxies[1][key], f"secure/plain differ for {key}")
    return {
        "findingId": "LWB-R8-088",
        "date": "2026-09-27",
        "evidenceStatus": "RECOVERED CONTRACT",
        "sourceIdentity": {"path": str(binary), "sha256": digest},
        "recoveredResult": {
            "pointBundleFields": proxies[0]["pointBundleFields"],
            "pointSerializerFieldCoverage": proxies[0]["pointSerializerFieldCoverage"],
            "listCountField": proxies[0]["listCountField"],
            "runtimeClassRule": proxies[0]["runtimeClassRule"],
            "classSpecificRules": proxies[0]["classSpecificRules"],
        },
        "proxies": proxies,
        "limits": [
            "covers safe-region point producer population; protected MapScanTick remains outside scope",
            "does not inspect or cross protected native region 0x3F8E0-0x40A6D",
            "does not recover block traversal/order/coordinates, per-tick work/request pacing, retry/backoff, or protected acknowledgement behavior",
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
