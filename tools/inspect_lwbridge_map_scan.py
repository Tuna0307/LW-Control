#!/usr/bin/env python3
"""Read-only xref inspector for the supplied lwbridge map-scan binary.

The executable is a stripped Rust/Tauri PE, so useful source-level clues live
mostly in embedded strings and Rust ``&str`` descriptors.  This helper never
loads or executes the binary.  It locates selected map-scan strings, follows
one level of pointer indirection, maps code references to PE runtime-function
ranges, and annotates those functions with nearby printable/Rust strings.
"""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path
import struct
from typing import Iterable

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86_const import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP


EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
DEFAULT_TARGETS = (
    "enterWorldMap",
    "startMapScan",
    "stopMapScan",
    "XluaBridgeNativeStart",
    "__XLUA_BRIDGE_NATIVE_WORLD_CAPTURE_RUN",
    "__XluaBridgeNativeWorldCapture",
    "XluaBridgeMapScanTick",
    "XluaBridgeNativeUpdate",
    "XluaBridgePoll",
    "native world capture session stopped",
    "direct map scan completed",
)

EMBEDDED_ASSETS = (
    ("bridge-scripts.dat", 0x8699E0, 0x11E4F3),
    ("xlua-proxy-secure.dll", 0x987F74, 0x95800),
    ("xlua-proxy-plain.dll", 0xA1D774, 0x96000),
    ("xlua-proxy-bundle.json", 0xAB3774, 0x24E),
    ("xlua-legacy.dll", 0xAB39C2, 0xBF190),
    ("lwbridge-profile-launcher.exe", 0xB72B52, 0xA7200),
    ("lwbridge-multi-hook.dll", 0xC19D52, 0x65A00),
)


class InspectError(ValueError):
    pass


def _offset_to_rva(pe: pefile.PE, offset: int) -> int:
    for section in pe.sections:
        start = int(section.PointerToRawData)
        stop = start + int(section.SizeOfRawData)
        if start <= offset < stop:
            return int(section.VirtualAddress) + offset - start
    if offset < int(pe.OPTIONAL_HEADER.SizeOfHeaders):
        return offset
    raise InspectError(f"file offset 0x{offset:X} is outside PE sections")


def _rva_to_offset(pe: pefile.PE, rva: int) -> int | None:
    try:
        return int(pe.get_offset_from_rva(rva))
    except pefile.PEFormatError:
        return None


def _all_offsets(data: bytes, needle: bytes) -> list[int]:
    rows: list[int] = []
    cursor = 0
    while True:
        hit = data.find(needle, cursor)
        if hit < 0:
            return rows
        rows.append(hit)
        cursor = hit + 1


def _is_printable(raw: bytes) -> bool:
    return bool(raw) and all(byte in (9, 10, 13) or 0x20 <= byte <= 0x7E for byte in raw)


def _direct_ascii(pe: pefile.PE, data: bytes, image_base: int, va: int) -> str | None:
    rva = va - image_base
    offset = _rva_to_offset(pe, rva)
    if offset is None or offset < 0 or offset >= len(data):
        return None
    end = offset
    while end < min(len(data), offset + 220) and 0x20 <= data[end] <= 0x7E:
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
    if not 4 <= length <= 220:
        return None
    string_offset = _rva_to_offset(pe, ptr - image_base)
    if string_offset is None or string_offset + length > len(data):
        return None
    raw = data[string_offset : string_offset + length]
    if not _is_printable(raw):
        return None
    return raw.decode("ascii", "replace")


def _runtime_functions(pe: pefile.PE) -> list[tuple[int, int]]:
    pe.parse_data_directories(
        directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]]
    )
    rows: list[tuple[int, int]] = []
    for entry in getattr(pe, "DIRECTORY_ENTRY_EXCEPTION", []):
        begin = int(entry.struct.BeginAddress)
        end = int(entry.struct.EndAddress)
        if begin < end:
            rows.append((begin, end))
    rows.sort()
    return rows


def _containing_function(functions: list[tuple[int, int]], rva: int) -> tuple[int, int] | None:
    # Runtime-function tables are sorted; a binary search is unnecessary for
    # this small, targeted inspection pass.
    for begin, end in functions:
        if begin <= rva < end:
            return begin, end
        if begin > rva:
            break
    return None


def _code_sections(pe: pefile.PE, data: bytes) -> Iterable[tuple[str, int, bytes]]:
    for section in pe.sections:
        if not (int(section.Characteristics) & 0x20000000):
            continue
        start = int(section.PointerToRawData)
        size = int(section.SizeOfRawData)
        name = section.Name.rstrip(b"\0").decode("ascii", "replace")
        yield name, int(section.VirtualAddress), data[start : start + size]


def _annotated_function(pe: pefile.PE, data: bytes, image_base: int,
                        functions: list[tuple[int, int]], rva: int) -> list[str]:
    function_range = _containing_function(functions, rva)
    if function_range is None:
        raise InspectError(f"RVA 0x{rva:X} is not inside a PE runtime-function range")
    begin, end = function_range
    offset = _rva_to_offset(pe, begin)
    if offset is None:
        raise InspectError(f"runtime function 0x{begin:X}-0x{end:X} is not file-backed")
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    rows = [f"function 0x{begin:X}-0x{end:X} ({end - begin} bytes)"]
    for instruction in md.disasm(data[offset : offset + (end - begin)], image_base + begin):
        annotations: list[str] = []
        for operand in instruction.operands:
            referenced_va: int | None = None
            if operand.type == X86_OP_MEM and operand.mem.base == X86_REG_RIP:
                referenced_va = instruction.address + instruction.size + operand.mem.disp
            if referenced_va is None:
                continue
            direct = _direct_ascii(pe, data, image_base, referenced_va)
            rust = _rust_str_descriptor(pe, data, image_base, referenced_va)
            text = rust or direct
            if text is not None:
                annotations.append(repr(text[:180]))
        suffix = f" ; {' | '.join(annotations)}" if annotations else ""
        rows.append(
            f"  0x{instruction.address - image_base:X}: {instruction.mnemonic:<7} "
            f"{instruction.op_str}{suffix}"
        )
    return rows


def _embedded_asset_report(pe: pefile.PE, data: bytes, digest: str) -> str:
    lines = [f"sha256={digest}", "embedded_assets:"]
    for name, rva, size in EMBEDDED_ASSETS:
        offset = _rva_to_offset(pe, rva)
        if offset is None or offset + size > len(data):
            raise InspectError(f"embedded asset {name!r} is not fully file-backed")
        payload = data[offset : offset + size]
        lines.append(
            f"  {name}: rva=0x{rva:X} offset=0x{offset:X} size={size} "
            f"sha256={hashlib.sha256(payload).hexdigest()} head={payload[:8].hex()}"
        )
        if name == "bridge-scripts.dat":
            if len(payload) < 12 or payload[:4] != b"LWBP":
                raise InspectError("embedded bridge-scripts.dat does not have the expected LWBP header")
            version, build_id_length = struct.unpack_from("<II", payload, 4)
            build_id_start = 12
            build_id_end = build_id_start + build_id_length
            if build_id_end > len(payload):
                raise InspectError("embedded bridge-scripts.dat build ID exceeds the package length")
            build_id = payload[build_id_start:build_id_end].decode("ascii", "replace")
            lines.append(
                f"    package_magic=LWBP package_version={version} "
                f"build_id_length={build_id_length} build_id={build_id!r}"
            )
    return "\n".join(lines)


def _rust_str_table_report(
    pe: pefile.PE,
    data: bytes,
    image_base: int,
    digest: str,
    table_rva: int,
    count: int,
) -> str:
    if count <= 0:
        raise InspectError("Rust string table count must be positive")
    offset = _rva_to_offset(pe, table_rva)
    if offset is None or offset + (count * 16) > len(data):
        raise InspectError(
            f"Rust string table 0x{table_rva:X} with {count} entries is not fully file-backed"
        )

    lines = [
        f"sha256={digest}",
        f"image_base=0x{image_base:X}",
        f"rust_str_table rva=0x{table_rva:X} offset=0x{offset:X} count={count}",
    ]
    for index in range(count):
        descriptor_offset = offset + (index * 16)
        ptr, length = struct.unpack_from("<QQ", data, descriptor_offset)
        if length > 4096:
            raise InspectError(
                f"Rust string table entry {index} has implausible length {length}"
            )
        string_offset = _rva_to_offset(pe, ptr - image_base)
        if string_offset is None or string_offset + length > len(data):
            raise InspectError(f"Rust string table entry {index} is not file-backed")
        raw = data[string_offset : string_offset + length]
        if not _is_printable(raw):
            raise InspectError(f"Rust string table entry {index} is not printable")
        text = raw.decode("ascii", "replace")
        lines.append(
            f"  [{index}] descriptor_rva=0x{table_rva + index * 16:X} "
            f"string_rva=0x{ptr - image_base:X} length={length} text={text!r}"
        )
    return "\n".join(lines)


def inspect(
    path: Path,
    targets: tuple[str, ...],
    dump_rva: int | None = None,
    embedded_assets: bool = False,
    rust_str_table_rva: int | None = None,
    rust_str_table_count: int | None = None,
) -> str:
    data = path.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if digest != EXPECTED_SHA256:
        raise InspectError(f"unsupported lwbridge SHA-256 {digest}; expected {EXPECTED_SHA256}")

    pe = pefile.PE(data=data, fast_load=False)
    image_base = int(pe.OPTIONAL_HEADER.ImageBase)
    functions = _runtime_functions(pe)

    if embedded_assets:
        return _embedded_asset_report(pe, data, digest)

    if rust_str_table_rva is not None:
        if rust_str_table_count is None:
            raise InspectError("--rust-str-table-count is required with --rust-str-table-rva")
        return _rust_str_table_report(
            pe,
            data,
            image_base,
            digest,
            rust_str_table_rva,
            rust_str_table_count,
        )

    if dump_rva is not None:
        return "\n".join([
            f"sha256={digest}",
            f"image_base=0x{image_base:X}",
            *_annotated_function(pe, data, image_base, functions, dump_rva),
        ])

    target_vas: dict[int, str] = {}
    pointer_vas: dict[int, str] = {}
    lines = [f"sha256={digest}", f"image_base=0x{image_base:X}"]
    for target in targets:
        hits = _all_offsets(data, target.encode("ascii"))
        lines.append(f"target {target!r}: {len(hits)} occurrence(s)")
        for offset in hits:
            rva = _offset_to_rva(pe, offset)
            va = image_base + rva
            target_vas[va] = target
            lines.append(f"  string offset=0x{offset:X} rva=0x{rva:X} va=0x{va:X}")
            encoded = struct.pack("<Q", va)
            for pointer_offset in _all_offsets(data, encoded):
                pointer_rva = _offset_to_rva(pe, pointer_offset)
                pointer_va = image_base + pointer_rva
                pointer_vas[pointer_va] = target
                lines.append(
                    f"    pointer offset=0x{pointer_offset:X} rva=0x{pointer_rva:X} va=0x{pointer_va:X}"
                )

    interesting_vas = {**target_vas, **pointer_vas}
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    xrefs: list[tuple[int, int, str, str, str]] = []
    for section_name, section_rva, code in _code_sections(pe, data):
        section_va = image_base + section_rva
        for instruction in md.disasm(code, section_va):
            seen: set[tuple[int, str]] = set()
            for operand in instruction.operands:
                target_va: int | None = None
                if operand.type == X86_OP_MEM and operand.mem.base == X86_REG_RIP:
                    target_va = instruction.address + instruction.size + operand.mem.disp
                elif operand.type == X86_OP_IMM:
                    target_va = int(operand.imm) & 0xFFFFFFFFFFFFFFFF
                if target_va in interesting_vas:
                    seen.add((target_va, interesting_vas[target_va]))
            for _, label in sorted(seen):
                xrefs.append(
                    (
                        instruction.address,
                        instruction.address - image_base,
                        label,
                        section_name,
                        f"{instruction.mnemonic} {instruction.op_str}".rstrip(),
                    )
                )

    lines.append(f"code_xrefs={len(xrefs)}")
    function_ranges: set[tuple[int, int]] = set()
    for va, rva, label, section_name, text in xrefs:
        function_range = _containing_function(functions, rva)
        if function_range is not None:
            function_ranges.add(function_range)
            fn = f"0x{function_range[0]:X}-0x{function_range[1]:X}"
        else:
            fn = "unknown"
        lines.append(
            f"xref {label!r} section={section_name} rva=0x{rva:X} function={fn}: {text}"
        )

    lines.append(f"referencing_functions={len(function_ranges)}")
    for begin, end in sorted(function_ranges):
        lines.extend(_annotated_function(pe, data, image_base, functions, begin))

    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=Path)
    parser.add_argument("--target", action="append", dest="targets")
    parser.add_argument(
        "--dump-rva",
        type=lambda value: int(value, 0),
        help="dump the PE runtime function containing this RVA and skip xref scanning",
    )
    parser.add_argument(
        "--embedded-assets",
        action="store_true",
        help="report the verified binary's embedded runtime assets without extracting them",
    )
    parser.add_argument(
        "--rust-str-table-rva",
        type=lambda value: int(value, 0),
        help="decode a file-backed table of Rust &str descriptors at this RVA",
    )
    parser.add_argument(
        "--rust-str-table-count",
        type=int,
        help="number of 16-byte Rust &str descriptors to decode",
    )
    args = parser.parse_args()
    targets = tuple(args.targets) if args.targets else DEFAULT_TARGETS
    try:
        print(
            inspect(
                args.binary,
                targets,
                dump_rva=args.dump_rva,
                embedded_assets=args.embedded_assets,
                rust_str_table_rva=args.rust_str_table_rva,
                rust_str_table_count=args.rust_str_table_count,
            )
        )
    except (InspectError, OSError, pefile.PEFormatError) as exc:
        parser.error(str(exc))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
