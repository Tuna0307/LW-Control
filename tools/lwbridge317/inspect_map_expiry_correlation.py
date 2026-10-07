#!/usr/bin/env python3
"""Strict read-only managed-correlation inspector for LWB317 ...-005.

This does NOT implement a general inverse for the current RG/RGMD modified
metadata-token encoding.  It names only operands independently validated by
metadata signatures, trivial accessors, or generated xLua wrappers. Unknown
operands remain raw.
"""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import struct
import sys
from pathlib import Path
from typing import Any

from dncil.cil.body.reader import read_method_body_from_bytes

ROOT = Path(__file__).resolve().parents[2]
RDL = Path(
    r"C:\Users\chimw\AppData\Local\FunFly\Last War-Survival Game\Game"
    r"\LastWar_Data\Assemblies\Assembly-CSharp.rdl"
)
RDL_SHA256 = "bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e"

_parser_path = ROOT / "tools/inspect_lastwar_rdl_metadata.py"
_spec = importlib.util.spec_from_file_location("expiry_corr_rdl", _parser_path)
if _spec is None or _spec.loader is None:
    raise RuntimeError("cannot load RDL parser")
_rdl = importlib.util.module_from_spec(_spec)
sys.modules[_spec.name] = _rdl
_spec.loader.exec_module(_rdl)

# Modified operands validated by independent artifact locators. These are
# deliberately local to this exact hash, not promoted into the generic parser.
KNOWN: dict[int, str] = {
    0x679E2902: "NetworkManager::_futureManager (FieldDef row 3769)",
    0x678A5222: "FutureManager::_sendInfos (FieldDef row 21014)",
    0x678A52C2: "FutureManager::_futureId (FieldDef row 21015)",
    0x678A5D02: "FutureManager::_totalCount (FieldDef row 21017)",
    0x678A5CA2: "FutureManager::_totalTime (FieldDef row 21018)",
    0x678A5D42: "FutureManager::_minTime (FieldDef row 21019)",
    0x678A5DE2: "FutureManager::_maxTime (FieldDef row 21020)",
    0x678A5242: "msgSendInfo::_futureId (FieldDef row 21011)",
    0x678A52E2: "msgSendInfo::_msgId (FieldDef row 21012)",
    0x678A5282: "msgSendInfo::_sendTime (FieldDef row 21013)",
    0xA7814D02: "msgSendInfo::.ctor(int,string) (MethodDef row 48025)",
    0xA7814CA2: "msgSendInfo::getSendTime() (MethodDef row 48026)",
    0xA7814DE2: "FutureManager::reset() (MethodDef row 48028)",
    0xA7814D82: "FutureManager::getFutureId() (MethodDef row 48029)",
    0xA7814DC2: "FutureManager::onSendRequest(int,string) (MethodDef row 48031)",
    0xA7814C62: "FutureManager::onServerMsgCome(int,int) (MethodDef row 48032)",
    0xA7814FA2: "FutureManager::.ctor() (MethodDef row 48034)",
    0xA79DD142: "NetworkManager::SyncPingPong(int) (MethodDef row 5755)",
    0xA79DD7E2: "NetworkManager::getFutureManager() (MethodDef row 5740)",
    0xA79C9CC2: "MessageFactory::get_Instance() (MethodDef row 6183)",
    0xA79C9F42: "MessageFactory::DispatchResponse(BaseEvent) (MethodDef row 6187)",
    0xA79C9E62: "MessageFactory::DispatchResponse(string,SFSObject) (MethodDef row 6192)",
    0xA79E23E2: "GameEntry::get_Network() (MethodDef row 3724)",
    0xA79E22E2: "GameEntry::get_Lua() (MethodDef row 3732)",
    0xA7947B02: "XLuaManager::get_Env() (MethodDef row 17353)",
    0xA7944082: "SFSObjectExtention::ToLuaTable(SFSObject,LuaEnv) (MethodDef row 17285)",
    0xA79444C2: "XLuaManager::DispatchResponse(string,object) (MethodDef row 17383)",
}

FUTURE_KEY_TOKEN = 0xE47CA5B0

METHODS = [
    ("NetworkManager", "Connect", ["string[]", "int32", "string", "int32"]),
    ("NetworkManager", "SendLuaMessage", ["string", "uint8[]"]),
    ("NetworkManager", "OnExtensionResponse", ["Sfs2X.Core.BaseEvent"]),
    ("Main.Scripts.Network.FutureManager", "reset", []),
    ("Main.Scripts.Network.FutureManager", "getFutureId", []),
    ("Main.Scripts.Network.FutureManager", "onSendRequest", ["int32", "string"]),
    ("Main.Scripts.Network.FutureManager", "onServerMsgCome", ["int32", "int32"]),
    ("MessageFactory", "DispatchResponse", ["string", "Sfs2X.Entities.Data.SFSObject"]),
    ("SFSObjectExtention", "ToLuaTable", ["Sfs2X.Entities.Data.SFSObject", "XLua.LuaEnv"]),
    ("XLuaManager", "DispatchResponse", ["string", "object"]),
    ("NetMessagePlaceholder", "__none__", []),  # sentinel, ignored
]

WRAPPER_PROOFS = [
    ("XLua.CSObjectWrap.NetworkManagerWrap", "_m_SyncPingPong", 0xA79DD142),
    ("XLua.CSObjectWrap.NetworkManagerWrap", "_m_getFutureManager", 0xA79DD7E2),
    ("XLua.CSObjectWrap.MessageFactoryWrap", "_g_get_Instance", 0xA79C9CC2),
    ("XLua.CSObjectWrap.MessageFactoryWrap", "_m_DispatchResponse", 0xA79C9F42),
    ("XLua.CSObjectWrap.GameEntryWrap", "_g_get_Network", 0xA79E23E2),
    ("XLua.CSObjectWrap.GameEntryWrap", "_g_get_Lua", 0xA79E22E2),
]


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def operand_value(operand: object) -> int | None:
    value = getattr(operand, "value", operand)
    return value if isinstance(value, int) else None


def find_method(image: Any, owner: str, name: str, args: list[str]):
    owners, _ = image.owner_maps()
    matches = []
    for index, row in enumerate(image.tables.MethodDef.rows, 1):
        if owners.get(index) != owner or str(row.Name) != name:
            continue
        _ret, actual = image.decode_method_signature(bytes(row.Signature.value))
        if actual == args:
            matches.append((index, row))
    if len(matches) != 1:
        raise AssertionError(f"method locator not unique: {owner}::{name}{args}: {len(matches)}")
    return matches[0]


def decode_body(image: Any, row: Any):
    offset = image.rva_to_offset(row.Rva)
    patched = bytearray(image.data[offset:])
    if patched and (patched[0] & 0x3) in (0, 1):
        patched[0] |= 0x2
    body = read_method_body_from_bytes(bytes(patched))
    return offset, body


def body_record(image: Any, index: int, row: Any) -> dict[str, Any]:
    offset, body = decode_body(image, row)
    original = image.data[offset : offset + body.size]
    instructions = []
    unresolved = []
    for ins in body.instructions:
        raw = operand_value(ins.operand)
        resolved = KNOWN.get(raw) if raw is not None else None
        if raw is not None and ins.opcode.name in {
            "call", "callvirt", "newobj", "ldfld", "ldflda", "stfld", "ldsfld",
            "stsfld", "ldstr", "ldtoken", "castclass", "isinst", "box",
            "constrained."
        } and resolved is None:
            unresolved.append({"il": ins.offset, "opcode": ins.opcode.name, "raw": f"0x{raw:08x}"})
        instructions.append({
            "il": ins.offset,
            "opcode": ins.opcode.name,
            "rawOperand": f"0x{raw:08x}" if raw is not None else (
                str(ins.operand) if ins.operand is not None else None
            ),
            "resolved": resolved,
        })
    ret, args = image.decode_method_signature(bytes(row.Signature.value))
    return {
        "row": index,
        "rva": f"0x{row.Rva:x}",
        "return": ret,
        "args": args,
        "fileOffset": f"0x{offset:x}",
        "originalRawBytesHex": original.hex(),
        "originalRawSha256": sha256(original),
        "decodedSize": body.size,
        "instructions": instructions,
        "unresolvedOperands": unresolved,
    }


def require_token(record: dict[str, Any], token: int, opcode: str | None = None) -> None:
    target = f"0x{token:08x}"
    hits = [
        x for x in record["instructions"]
        if x["rawOperand"] == target and (opcode is None or x["opcode"] == opcode)
    ]
    if not hits:
        raise AssertionError(f"expected token {target} missing from {record['rva']}")


def inspect(rdl_path: Path = RDL) -> dict[str, Any]:
    data = rdl_path.read_bytes()
    if sha256(data) != RDL_SHA256:
        raise AssertionError("current Assembly-CSharp.rdl hash changed")
    image = _rdl.MetadataImage.load(rdl_path)

    records: dict[str, Any] = {}
    for owner, name, args in METHODS:
        if owner == "NetMessagePlaceholder":
            continue
        index, row = find_method(image, owner, name, args)
        records[f"{owner}::{name}({','.join(args)})"] = body_record(image, index, row)

    # Independent generated-wrapper validation for modified MethodDef operands.
    # Wrapper signatures vary; locate by exact owner/name, then require a unique method.
    wrapper_results = []
    owners, _ = image.owner_maps()
    for owner, name, token in WRAPPER_PROOFS:
        matches = [(i, r) for i, r in enumerate(image.tables.MethodDef.rows, 1)
                   if owners.get(i) == owner and str(r.Name) == name]
        if len(matches) != 1:
            raise AssertionError(f"wrapper locator not unique: {owner}::{name}")
        i, row = matches[0]
        rec = body_record(image, i, row)
        require_token(rec, token)
        wrapper_results.append({
            "owner": owner, "method": name, "methodRow": i,
            "rva": f"0x{row.Rva:x}", "rawToken": f"0x{token:08x}",
            "resolved": KNOWN[token],
        })

    send = records["NetworkManager::SendLuaMessage(string,uint8[])"]
    connect = records["NetworkManager::Connect(string[],int32,string,int32)"]
    incoming = records["MessageFactory::DispatchResponse(string,Sfs2X.Entities.Data.SFSObject)"]
    reset = records["Main.Scripts.Network.FutureManager::reset()"]
    get_id = records["Main.Scripts.Network.FutureManager::getFutureId()"]
    on_send = records["Main.Scripts.Network.FutureManager::onSendRequest(int32,string)"]
    on_server = records["Main.Scripts.Network.FutureManager::onServerMsgCome(int32,int32)"]
    to_lua = records["SFSObjectExtention::ToLuaTable(Sfs2X.Entities.Data.SFSObject,XLua.LuaEnv)"]

    for rec, token in (
        (send, 0x679E2902), (send, 0xA7814D82), (send, 0xA7814DC2),
        (connect, 0xA7814DE2), (reset, 0x678A52C2), (reset, 0x678A5222),
        (on_send, 0x678A5222), (on_server, 0x678A5222),
        (incoming, 0xA79E23E2), (incoming, 0xA79DD7E2),
        (incoming, 0xA7814C62), (incoming, 0xA79E22E2),
        (incoming, 0xA7944082), (incoming, 0xA79444C2),
    ):
        require_token(rec, token)

    send_key_hits = sum(x["rawOperand"] == f"0x{FUTURE_KEY_TOKEN:08x}" for x in send["instructions"])
    recv_key_hits = sum(x["rawOperand"] == f"0x{FUTURE_KEY_TOKEN:08x}" for x in incoming["instructions"])
    if send_key_hits != 1 or recv_key_hits != 2:
        raise AssertionError("future-key raw string token shape changed")

    # Unique literal presence is supporting evidence only: the modified ldstr token
    # is NOT globally decoded to this heap offset.
    fuid_utf16 = "fuid".encode("utf-16-le")
    fuid_offsets = []
    pos = 0
    while True:
        pos = data.find(fuid_utf16, pos)
        if pos < 0:
            break
        fuid_offsets.append(pos)
        pos += 1

    # The response future-call token occurs once in the exact artifact.
    unique_server_call = data.count(struct.pack("<I", 0xA7814C62))
    if unique_server_call != 1:
        raise AssertionError(f"onServerMsgCome raw token occurrence changed: {unique_server_call}")

    return {
        "ok": True,
        "rdl": {"path": str(rdl_path), "sha256": RDL_SHA256,
                "rgmdOffset": f"0x{image.root_offset:x}"},
        "resolutionPolicy": {
            "generalModifiedTokenInverseRecovered": False,
            "validatedLocalOperandResolution": True,
            "unknownOperandsRemainRaw": True,
            "futureKeyLdstrRawToken": f"0x{FUTURE_KEY_TOKEN:08x}",
            "uniqueUtf16FuidOffsets": [f"0x{x:x}" for x in fuid_offsets],
            "note": "literal heap occurrence is supporting evidence; no unproven ldstr inverse is claimed",
        },
        "wrapperProofs": wrapper_results,
        "methods": records,
        "correlation": {
            "send": "allocates fuid, writes one future-key field, associates fuid with msgId",
            "pendingStore": "Dictionary<int,msgSendInfo> keyed by fuid; duplicate key is not replaced",
            "receive": "when the same future-key field exists, reads it and calls onServerMsgCome(fuid,serverTime)",
            "consume": "onServerMsgCome checks keyed entry, accounts timing, then removes that fuid entry",
            "luaBoundary": "same SFSObject is passed through ToLuaTable, which enumerates its data-holder entries, then XLuaManager.DispatchResponse",
            "reuse": "NetworkManager.Connect calls FutureManager.reset; reset sets _futureId=0 and clears _sendInfos",
            "ordering": "managed timing association is key-based, not FIFO; out-of-order fuid responses can target their own pending entry",
            "directGhostFutureKeyEmission": "UNKNOWN: MessageFactory conditionally checks the key; no static direct ghost.recon.steal response producer/schema proves it is emitted",
            "disposition": "PARTIAL_SOURCE_PROVEN_MANAGED_CORRELATION",
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--rdl", type=Path, default=RDL)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    result = inspect(args.rdl)
    text = json.dumps(result, indent=2)
    if args.out:
        args.out.write_text(text + "\n", encoding="utf-8")
    print(text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
