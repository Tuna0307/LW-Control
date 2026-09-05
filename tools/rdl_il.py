#!/usr/bin/env python3
"""Read-only helpers for parsing transformed Last War RDL method bodies.

Current RDL files keep ordinary CIL method bodies but clear the method-header
format bit used by normal CLR readers. These helpers restore that single bit in
memory, feed the repaired copy to dncil, and expose the parsed instructions.

The source RDL bytes are never modified.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any

from dncil.cil.body.reader import read_method_body_from_bytes


HEADER_FORMAT_BIT = 0x02
TOKEN_XOR_A = 0xA5A5A5A5
TOKEN_SUB = 0x075BCD16
TOKEN_XOR_B = 0x3ADE68B1
TOKEN_ROR = 5


class RdlMethodBodyError(ValueError):
    pass


@dataclass(frozen=True)
class ParsedRdlMethodBody:
    file_offset: int
    original_header_byte: int
    repaired_header_byte: int
    header_size: int
    code_size: int
    total_size: int
    instructions: tuple[Any, ...]


def repair_method_header_copy(method_bytes: bytes) -> bytes:
    """Return a copy with the normal CLR method-header format bit restored."""
    if not method_bytes:
        raise RdlMethodBodyError("method body is empty")
    repaired = bytearray(method_bytes)
    repaired[0] |= HEADER_FORMAT_BIT
    return bytes(repaired)


def parse_method_body_bytes(method_bytes: bytes, *, file_offset: int = 0) -> ParsedRdlMethodBody:
    """Parse one transformed RDL method body without mutating its source bytes."""
    repaired = repair_method_header_copy(method_bytes)
    try:
        body = read_method_body_from_bytes(repaired)
    except Exception as exc:
        raise RdlMethodBodyError(
            f"could not parse repaired method body at file offset 0x{file_offset:X}: {exc}"
        ) from exc
    return ParsedRdlMethodBody(
        file_offset=file_offset,
        original_header_byte=method_bytes[0],
        repaired_header_byte=repaired[0],
        header_size=body.header_size,
        code_size=body.code_size,
        total_size=body.size,
        instructions=tuple(body.instructions),
    )


def parse_method_body_file(path: Path, file_offset: int, *, window: int = 0x10000) -> ParsedRdlMethodBody:
    """Read and parse a method body from an RDL file using an in-memory copy."""
    if file_offset < 0:
        raise RdlMethodBodyError("file offset must be non-negative")
    if window < 16:
        raise RdlMethodBodyError("parse window is too small")
    data = path.read_bytes()
    if file_offset >= len(data):
        raise RdlMethodBodyError("file offset is beyond end of file")
    return parse_method_body_bytes(
        data[file_offset : min(len(data), file_offset + window)],
        file_offset=file_offset,
    )


def operand_value(operand: Any) -> Any:
    """Return the numeric value carried by dncil token operands when present."""
    return getattr(operand, "value", operand)


def instruction_rows(parsed: ParsedRdlMethodBody) -> list[dict[str, Any]]:
    """Convert dncil instructions into stable JSON-friendly rows."""
    rows: list[dict[str, Any]] = []
    for instruction in parsed.instructions:
        operand = operand_value(instruction.operand)
        rows.append(
            {
                "offset": instruction.offset,
                "opcode": instruction.opcode.name,
                "operand": operand,
                "operand_hex": f"0x{operand:08X}" if isinstance(operand, int) and operand >= 0 else None,
            }
        )
    return rows


def decode_metadata_token(token_value: int) -> int:
    """Decode one current RDL 32-bit metadata-token operand."""
    if not isinstance(token_value, int) or not 0 <= token_value <= 0xFFFFFFFF:
        raise RdlMethodBodyError("encoded token must be a 32-bit unsigned integer")
    value = token_value ^ TOKEN_XOR_A
    value = (value - TOKEN_SUB) & 0xFFFFFFFF
    value ^= TOKEN_XOR_B
    return ((value >> TOKEN_ROR) | (value << (32 - TOKEN_ROR))) & 0xFFFFFFFF


def encode_metadata_token(token_value: int) -> int:
    """Encode one normal 32-bit metadata token into the current RDL representation."""
    if not isinstance(token_value, int) or not 0 <= token_value <= 0xFFFFFFFF:
        raise RdlMethodBodyError("metadata token must be a 32-bit unsigned integer")
    value = ((token_value << TOKEN_ROR) | (token_value >> (32 - TOKEN_ROR))) & 0xFFFFFFFF
    value ^= TOKEN_XOR_B
    value = (value + TOKEN_SUB) & 0xFFFFFFFF
    return value ^ TOKEN_XOR_A


def encoded_field_token_rid_mod8(token_value: int) -> int:
    """Compatibility helper returning decoded FieldDef RID modulo eight."""
    decoded = decode_metadata_token(token_value)
    if decoded >> 24 != 0x04:
        raise RdlMethodBodyError(
            f"encoded token decodes to 0x{decoded:08X}, which is not a FieldDef token"
        )
    return (decoded & 0x00FFFFFF) % 8
