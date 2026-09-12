#!/usr/bin/env python3
"""Read-only xref inspector for LWBridge's embedded xLua proxy PE files.

The tool hash-gates the supplied LWBridge 0.3.1 executable, reads a verified
proxy payload directly from the embedded bytes, and locates code references to
native Map Scan strings.  It never loads, writes, or executes the proxy DLL.
"""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path
import struct

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP


EXPECTED_LWBRIDGE_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
PROXIES = {
    "secure": (
        0x987F74,
        0x95800,
        "481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400",
    ),
    "plain": (
        0xA1D774,
        0x96000,
        "c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794",
    ),
}
DEFAULT_TARGETS = (
    "__XLUA_BRIDGE_NATIVE_WORLD_CAPTURE_RUN",
    "__XluaBridgeNativeWorldCapture",
    "XluaBridgeMapScanTick",
    "XluaBridgeNativeUpdate",
    "XluaBridgePoll",
    "pendingPoints",
    "pendingMarches",
    "pendingAcks",
    "dropped",
)


class InspectError(ValueError):
    pass


def _all_offsets(data: bytes, needle: bytes) -> list[int]:
    rows: list[int] = []
    cursor = 0
    while True:
        hit = data.find(needle, cursor)
        if hit < 0:
            return rows
        rows.append(hit)
        cursor = hit + 1


def _rva_to_offset(pe: pefile.PE, rva: int) -> int | None:
    try:
        return int(pe.get_offset_from_rva(rva))
    except pefile.PEFormatError:
        return None


def _offset_to_rva(pe: pefile.PE, offset: int) -> int:
    for section in pe.sections:
        start = int(section.PointerToRawData)
        stop = start + int(section.SizeOfRawData)
        if start <= offset < stop:
            return int(section.VirtualAddress) + offset - start
    if offset < int(pe.OPTIONAL_HEADER.SizeOfHeaders):
        return offset
    raise InspectError(f"proxy offset 0x{offset:X} is outside PE sections")


def _runtime_functions(pe: pefile.PE) -> list[tuple[int, int]]:
    pe.parse_data_directories(
        directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]]
    )
    rows = [
        (int(entry.struct.BeginAddress), int(entry.struct.EndAddress))
        for entry in getattr(pe, "DIRECTORY_ENTRY_EXCEPTION", [])
        if int(entry.struct.BeginAddress) < int(entry.struct.EndAddress)
    ]
    rows.sort()
    return rows


def _containing_function(functions: list[tuple[int, int]], rva: int) -> tuple[int, int] | None:
    for begin, end in functions:
        if begin <= rva < end:
            return begin, end
        if begin > rva:
            break
    return None


def _direct_ascii(pe: pefile.PE, data: bytes, image_base: int, va: int) -> str | None:
    offset = _rva_to_offset(pe, va - image_base)
    if offset is None or offset >= len(data):
        return None
    end = offset
    while end < min(len(data), offset + 180) and 0x20 <= data[end] <= 0x7E:
        end += 1
    raw = data[offset:end]
    if len(raw) < 4:
        return None
    return raw.decode("ascii", "replace")


def _rust_str_descriptor(pe: pefile.PE, data: bytes, image_base: int, va: int) -> str | None:
    offset = _rva_to_offset(pe, va - image_base)
    if offset is None or offset + 16 > len(data):
        return None
    ptr, length = struct.unpack_from("<QQ", data, offset)
    if not 4 <= length <= 180:
        return None
    string_offset = _rva_to_offset(pe, ptr - image_base)
    if string_offset is None or string_offset + length > len(data):
        return None
    raw = data[string_offset : string_offset + length]
    if not all(0x20 <= byte <= 0x7E for byte in raw):
        return None
    return raw.decode("ascii", "replace")


def _annotated_function(
    pe: pefile.PE,
    data: bytes,
    image_base: int,
    begin: int,
    end: int,
) -> list[str]:
    offset = _rva_to_offset(pe, begin)
    if offset is None:
        raise InspectError(f"proxy function 0x{begin:X}-0x{end:X} is not file-backed")
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    rows = [f"function 0x{begin:X}-0x{end:X} ({end - begin} bytes)"]
    for ins in md.disasm(data[offset : offset + (end - begin)], image_base + begin):
        annotations: list[str] = []
        for operand in ins.operands:
            if operand.type != X86_OP_MEM or operand.mem.base != X86_REG_RIP:
                continue
            va = ins.address + ins.size + operand.mem.disp
            text = _rust_str_descriptor(pe, data, image_base, va) or _direct_ascii(
                pe, data, image_base, va
            )
            if text:
                annotations.append(repr(text))
        suffix = f" ; {' | '.join(annotations)}" if annotations else ""
        rows.append(f"  0x{ins.address - image_base:X}: {ins.mnemonic:<7} {ins.op_str}{suffix}")
    return rows


def inspect(path: Path, proxy_name: str, targets: tuple[str, ...]) -> str:
    outer = path.read_bytes()
    outer_digest = hashlib.sha256(outer).hexdigest()
    if outer_digest != EXPECTED_LWBRIDGE_SHA256:
        raise InspectError(
            f"unsupported lwbridge SHA-256 {outer_digest}; expected {EXPECTED_LWBRIDGE_SHA256}"
        )

    outer_pe = pefile.PE(data=outer, fast_load=False)
    proxy_rva, proxy_size, expected_proxy_digest = PROXIES[proxy_name]
    proxy_offset = int(outer_pe.get_offset_from_rva(proxy_rva))
    data = outer[proxy_offset : proxy_offset + proxy_size]
    proxy_digest = hashlib.sha256(data).hexdigest()
    if proxy_digest != expected_proxy_digest:
        raise InspectError(
            f"embedded {proxy_name} proxy SHA-256 {proxy_digest}; expected {expected_proxy_digest}"
        )

    pe = pefile.PE(data=data, fast_load=False)
    image_base = int(pe.OPTIONAL_HEADER.ImageBase)
    functions = _runtime_functions(pe)
    interesting: dict[int, str] = {}
    lines = [
        f"lwbridge_sha256={outer_digest}",
        f"proxy={proxy_name}",
        f"proxy_sha256={proxy_digest}",
        f"proxy_image_base=0x{image_base:X}",
    ]
    for target in targets:
        hits = _all_offsets(data, target.encode("ascii"))
        lines.append(f"target {target!r}: {len(hits)} occurrence(s)")
        for offset in hits:
            rva = _offset_to_rva(pe, offset)
            va = image_base + rva
            interesting[va] = target
            lines.append(f"  string offset=0x{offset:X} rva=0x{rva:X} va=0x{va:X}")
            pointer_encodings = (
                ("va64", struct.pack("<Q", va)),
                ("rva32", struct.pack("<I", rva & 0xFFFFFFFF)),
            )
            for pointer_kind, encoded in pointer_encodings:
                for pointer_offset in _all_offsets(data, encoded):
                    if pointer_offset == offset:
                        continue
                    pointer_rva = _offset_to_rva(pe, pointer_offset)
                    pointer_va = image_base + pointer_rva
                    interesting[pointer_va] = target
                    lines.append(
                        f"    {pointer_kind} pointer offset=0x{pointer_offset:X} "
                        f"rva=0x{pointer_rva:X}"
                    )

    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    xrefs: list[tuple[int, str, str]] = []
    function_ranges: set[tuple[int, int]] = set()
    for section in pe.sections:
        if not (int(section.Characteristics) & 0x20000000):
            continue
        section_rva = int(section.VirtualAddress)
        for ins in md.disasm(section.get_data(), image_base + section_rva):
            for operand in ins.operands:
                target_va: int | None = None
                if operand.type == X86_OP_MEM and operand.mem.base == X86_REG_RIP:
                    target_va = ins.address + ins.size + operand.mem.disp
                elif operand.type == X86_OP_IMM:
                    target_va = int(operand.imm) & 0xFFFFFFFFFFFFFFFF
                if target_va not in interesting:
                    continue
                rva = ins.address - image_base
                label = interesting[target_va]
                xrefs.append((rva, label, f"{ins.mnemonic} {ins.op_str}".rstrip()))
                function_range = _containing_function(functions, rva)
                if function_range:
                    function_ranges.add(function_range)

    lines.append(f"code_xrefs={len(xrefs)}")
    for rva, label, text in xrefs:
        function_range = _containing_function(functions, rva)
        fn = f"0x{function_range[0]:X}-0x{function_range[1]:X}" if function_range else "unknown"
        lines.append(f"xref {label!r} rva=0x{rva:X} function={fn}: {text}")
    lines.append(f"referencing_functions={len(function_ranges)}")
    for begin, end in sorted(function_ranges):
        lines.extend(_annotated_function(pe, data, image_base, begin, end))
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("--proxy", choices=tuple(PROXIES), default="secure")
    parser.add_argument("--target", action="append", dest="targets")
    args = parser.parse_args()
    targets = tuple(args.targets) if args.targets else DEFAULT_TARGETS
    try:
        print(inspect(args.binary, args.proxy, targets))
    except (InspectError, OSError, pefile.PEFormatError) as exc:
        parser.error(str(exc))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
