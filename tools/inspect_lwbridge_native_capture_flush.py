#!/usr/bin/env python3
"""Recover native-capture service/emission gates from LWBridge 0.3.1."""

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
    "secure": (0x987F74, 0x95800, "481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400"),
    "plain": (0xA1D774, 0x96000, "c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794"),
}
SERIALIZER = (0x38AB0, 0x39F15)

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
    require(digest == expected, f"{name} proxy SHA mismatch: {digest}")
    return data

def import_name_at(pe: pefile.PE, image_base: int, rva: int) -> tuple[str, str] | None:
    pe.parse_data_directories()
    for entry in getattr(pe, "DIRECTORY_ENTRY_IMPORT", []):
        dll = entry.dll.decode("ascii", "replace")
        for imp in entry.imports:
            if int(imp.address - image_base) == rva:
                return dll, (imp.name or b"").decode("ascii", "replace")
    return None


def inspect_proxy(name: str, data: bytes) -> dict[str, object]:
    pe = pefile.PE(data=data, fast_load=False)
    base = int(pe.OPTIONAL_HEADER.ImageBase)
    begin, end = SERIALIZER
    off = int(pe.get_offset_from_rva(begin))
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    rows = list(md.disasm(data[off:off + (end - begin)], base + begin))
    by_rva = {int(ins.address - base): ins for ins in rows}

    def ins(rva: int, mnemonic: str, op_text: str) -> None:
        item = by_rva.get(rva)
        require(item is not None, f"{name}: missing 0x{rva:X}")
        require((item.mnemonic, item.op_str) == (mnemonic, op_text),
                f"{name}: 0x{rva:X} got {item.mnemonic} {item.op_str}")

    def rip_target(rva: int) -> int:
        item = by_rva[rva]
        mem = next(
            (
                op for op in item.operands
                if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP
            ),
            None,
        )
        require(mem is not None, f"{name}: 0x{rva:X} is not RIP-relative")
        return int(item.address + item.size + mem.mem.disp - base)
    # GetTickCount64-backed minimum service gate.
    timer_call = by_rva[0x38AFB]
    require(timer_call.mnemonic == "call", f"{name}: timer call changed")
    timer_mem = next(
        (
            op for op in timer_call.operands
            if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP
        ),
        None,
    )
    require(timer_mem is not None, f"{name}: timer call is not RIP-relative")
    timer_iat_rva = int(
        timer_call.address + timer_call.size + timer_mem.mem.disp - base
    )
    require(timer_iat_rva == 0x6F180, f"{name}: timer IAT changed: 0x{timer_iat_rva:X}")
    require(
        import_name_at(pe, base, timer_iat_rva) == ("KERNEL32.dll", "GetTickCount64"),
        f"{name}: timer import is not GetTickCount64",
    )
    ins(0x38B05, "mov", "rcx, rax")
    require(by_rva[0x38B08].mnemonic == "sub", f"{name}: service clock subtraction changed")
    ins(0x38B12, "cmp", "rcx, 0x10")
    ins(0x38B16, "jae", "0x180038b30")
    require(by_rva[0x38B30].mnemonic == "mov", f"{name}: service clock update changed")
    require(
        rip_target(0x38B08) == rip_target(0x38B30),
        f"{name}: service timestamp read/write target differs",
    )

    # The stored run-id string is copied into local SSO storage at rbp+0xA8.
    run_id_object = rip_target(0x38BCE)
    require(rip_target(0x38BD8) == run_id_object + 0x18, f"{name}: run-id capacity target changed")
    require(rip_target(0x38BE0) == run_id_object, f"{name}: run-id data target changed")
    require(rip_target(0x38BF9) == run_id_object + 0x10, f"{name}: run-id length target changed")
    require(rip_target(0x39162) == run_id_object + 0x18, f"{name}: copied run-id capacity target changed")
    require(rip_target(0x3916A) == run_id_object, f"{name}: copied run-id data target changed")
    require(rip_target(0x39172) == run_id_object + 0x10, f"{name}: copied run-id length target changed")
    ins(0x39179, "mov", "rdx, r12")
    ins(0x3917C, "lea", "rcx, [rbp + 0xa8]")
    ins(0x3974E, "lea", "rdx, [rbp + 0xa8]")
    literal_rva = rip_target(0x3973B)
    literal_off = int(pe.get_offset_from_rva(literal_rva))
    require(data[literal_off:literal_off + 14] == b'{"scanRunId":"',
            f"{name}: scanRunId serializer literal changed")

    # Run-id change resets the active forced-emission clock and sets a one-shot flag.
    ins(0x38C00, "jne", "0x180038c10")
    ins(0x38C0E, "je", "0x180038c83")
    require(by_rva[0x38C70].mnemonic == "call", f"{name}: run-id copy call changed")
    one_shot_target = rip_target(0x38C7C)
    active_clock_target = rip_target(0x38C75)
    require(rip_target(0x391B3) == one_shot_target, f"{name}: one-shot load target changed")
    require(rip_target(0x391BA) == one_shot_target, f"{name}: one-shot clear target changed")

    # Active scanRunId selects 250 ms forced emission; idle selects 1000 ms.
    ins(0x391CD, "mov", "rcx, qword ptr [rbp + 0xb8]")
    ins(0x391D8, "test", "rcx, rcx")
    ins(0x391DB, "je", "0x1800391f3")
    ins(0x391E7, "cmp", "rax, 0xfa")
    ins(0x391ED, "jb", "0x1800391f3")
    ins(0x391EF, "mov", "dl, 1")
    ins(0x391F5, "test", "rcx, rcx")
    ins(0x391F8, "jne", "0x180039211")
    ins(0x39204, "cmp", "rax, 0x3e8")
    ins(0x3920A, "jb", "0x180039211")
    ins(0x3920C, "mov", "r8b, 1")
    require(rip_target(0x391E0) == active_clock_target, f"{name}: active clock read target changed")
    idle_clock_target = rip_target(0x391FD)

    # Active runs always reach final emission evaluation. Idle reaches it only
    # on run-id-change wake or the 1000 ms idle forced-emission gate.
    ins(0x39214, "test", "rcx, rcx")
    ins(0x39217, "jne", "0x180039458")
    ins(0x3921D, "movzx", "eax, bl")
    ins(0x39220, "or", "al, r8b")
    ins(0x39223, "jne", "0x180039458")

    # Remaining pending counts loaded after the shared R8-081 drain.
    counter_base = 0x91100 if name == "secure" else 0x92100
    pending_loads = {
        0x39193: counter_base,
        0x3919A: counter_base + 0x40,
        0x391A1: counter_base + 0x80,
        0x391A8: counter_base + 0xC0,
    }
    for rva, expected_target in pending_loads.items():
        require(
            rip_target(rva) == expected_target,
            f"{name}: pending-count load target changed at 0x{rva:X}",
        )

    for rva in (0x39462, 0x39472, 0x39482, 0x39492):
        ins(rva, "jne", "0x18003972c")

    ins(0x39498, "mov", "rcx, rdi")
    ins(0x3949B, "or", "rcx, r12")
    ins(0x3949E, "mov", "rax, rbx")
    ins(0x394A1, "or", "rax, rcx")
    ins(0x394A4, "jne", "0x18003972c")
    ins(0x394AA, "mov", "rcx, r13")
    ins(0x394AD, "or", "rcx, rsi")
    ins(0x394B0, "or", "dl, r8b")
    ins(0x394B3, "movzx", "eax, dl")
    ins(0x394B6, "or", "rcx, rax")
    ins(0x394B9, "jne", "0x18003972c")

    ins(0x39229, "xorps", "xmm0, xmm0")
    ins(0x3922C, "movups", "xmmword ptr [r15], xmm0")
    ins(0x39237, "mov", "qword ptr [r15 + 0x18], 0xf")
    ins(0x394BF, "xorps", "xmm0, xmm0")
    ins(0x394C2, "movups", "xmmword ptr [r15], xmm0")
    ins(0x394CD, "mov", "qword ptr [r15 + 0x18], 0xf")

    # After a real envelope is assembled, update the selected emission clock.
    ins(0x39B3F, "cmp", "qword ptr [rbp + 0xb8], 0")
    ins(0x39B47, "jne", "0x180039b59")
    require(by_rva[0x39B49].mnemonic == "mov", f"{name}: idle emission-clock write changed")
    require(by_rva[0x39B59].mnemonic == "mov", f"{name}: active emission-clock write changed")
    require(rip_target(0x39B49) == idle_clock_target, f"{name}: idle emission-clock target changed")
    require(rip_target(0x39B59) == active_clock_target, f"{name}: active emission-clock target changed")

    return {
        "proxy": name,
        "sha256": hashlib.sha256(data).hexdigest(),
        "serializerRva": "0x38AB0-0x39F15",
        "minimumServiceMs": 16,
        "forcedEmissionMs": {"activeRun": 250, "idle": 1000},
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
        "findingId": "LWB-R8-082",
        "date": "2026-09-27",
        "evidenceStatus": "RECOVERED CONTRACT",
        "sourceIdentity": {"path": str(binary), "sha256": digest},
        "recoveredResult": {
            "minimumServiceMs": 16,
            "activeRunForcedEmissionMs": 250,
            "idleForcedEmissionMs": 1000,
            "scanRunIdRole": "nonempty scanRunId selects active cadence; empty scanRunId selects idle cadence",
            "scanRunIdChange": (
                "when the incoming run id differs from the stored run id, the proxy updates it, "
                "resets the active forced-emission clock, and sets a one-shot wake flag"
            ),
            "evaluationPolicy": (
                "active runs always reach final emission evaluation after service/drain; idle state "
                "reaches it only when the one-shot wake flag is set or the 1000 ms idle gate is due"
            ),
            "finalEmissionTriggers": [
                "any drained point/march/removal vector is nonempty",
                "any remaining point/march/removal pending count is nonzero",
                "the captured run-id-change one-shot flag is nonzero",
                "the active 250 ms forced-emission gate is due",
                "the idle 1000 ms forced-emission gate is due",
            ],
            "emissionClockUpdate": (
                "after an envelope is assembled, the current service timestamp is written to the "
                "active or idle emission clock according to scanRunId emptiness"
            ),
            "suppression": "when the applicable evaluation/final-emission gates do not require an envelope, the serializer returns an empty result",
        },
        "proxies": proxies,
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
