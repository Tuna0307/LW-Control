"""Static decoder for encrypted IL token operands in Last War RGMD (.rdl) assemblies.

Recovered by known-plaintext analysis on Assembly-CSharp.rdl
(sha256 bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e):
  * 936 auto-property getter/setter -> <X>k__BackingField pairs (Field table),
  * two MethodDef calls with known targets (UpdateLWAoi -> UpdateLWAoi_Normal etc),
  * every ldstr operand must land on a #US heap entry start.
Constants solved bit-by-bit (solve_full.py); verified on all ~600k token operands
(verify_decoder.py).

    x     = ror32(encoded, 5)
    token = ((x ^ C) - B) ^ A        (mod 2^32)   -> ordinary ECMA-335 token (table<<24 | row)

encoder:  encoded = rol32(((token ^ A) + B) ^ C, 5)

The same transform also applies to the method-header LocalVarSig token.
Separately, #US heap entry length prefixes are stored doubled (stored = 2 * ECMA length).
"""
from __future__ import annotations

A, B, C = 0x02090CBA, 0x17E52197, 0x0ED2D2D2
MASK = 0xFFFFFFFF


def ror32(v: int, n: int) -> int:
    return ((v >> n) | (v << (32 - n))) & MASK


def rol32(v: int, n: int) -> int:
    return ((v << n) | (v >> (32 - n))) & MASK


# The arithmetic yields a scrambled table byte; it is remapped by a fixed permutation.
# Observed on all operands (100% row-range/heap-start valid, MemberRef field/method
# signature kinds consistent with the opcode):
TABLE_MAP = {
    0x01: 0x01,  # TypeRef
    0x04: 0x04,  # Field
    0x06: 0x06,  # MethodDef
    0x70: 0x70,  # UserString
    0x02: 0x0A,  # MemberRef
    0x0A: 0x02,  # TypeDef
    0x4B: 0x1B,  # TypeSpec
    0x5B: 0x2B,  # MethodSpec
    0x51: 0x11,  # StandAloneSig (method-header LocalVarSig)
}
TABLE_UNMAP = {v: k for k, v in TABLE_MAP.items()}


def decode_raw(enc: int) -> int:
    x = ror32(enc & MASK, 5)
    return (((x ^ C) - B) & MASK) ^ A


def decode_token(enc: int) -> int:
    raw = decode_raw(enc)
    table = TABLE_MAP.get(raw >> 24)
    if table is None:
        raise ValueError(f"unknown table code 0x{raw >> 24:02x} for operand 0x{enc:08x}")
    return (table << 24) | (raw & 0xFFFFFF)


def encode_token(token: int) -> int:
    raw = (TABLE_UNMAP[token >> 24] << 24) | (token & 0xFFFFFF)
    return rol32((((raw ^ A) + B) & MASK) ^ C, 5)


# Long-form branch (br/brtrue/beq/.../leave) int32 relative offsets use the same
# rol5/xor/add/xor scheme; bits 0..19 share A/B/C, the high bits were fit separately
# (every decoded target lands on an instruction boundary).  Tokens and branch values
# therefore disagree only in bits >= 20 (the table byte, handled by TABLE_MAP above).
BA, BB, BC = 0x02090CBA, 0x10452197, 0x0572D2D2


def decode_branch_offset(enc: int) -> int:
    d = (((ror32(enc & MASK, 5) ^ BC) - BB) & MASK) ^ BA
    return d - (1 << 32) if d >> 31 else d
