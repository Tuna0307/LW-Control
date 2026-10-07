#!/usr/bin/env python3
"""Distinguishing parser/correlation proofs for ...-005."""
from __future__ import annotations

import importlib.util
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TOOL = ROOT / "tools/lwbridge317/inspect_map_expiry_correlation.py"

spec = importlib.util.spec_from_file_location("expiry_corr_check", TOOL)
if spec is None or spec.loader is None:
    raise RuntimeError("cannot load expiry/correlation inspector")
mod = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = mod
spec.loader.exec_module(mod)


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def token_hits(record: dict, raw: int) -> list[dict]:
    text = f"0x{raw:08x}"
    return [x for x in record["instructions"] if x["rawOperand"] == text]


def main() -> int:
    # Independent synthetic resolver fixture: exact validated values resolve,
    # one-bit mutations and unrelated raw values do not.
    fixture = {
        0xA7814D82: "FutureManager::getFutureId() (MethodDef row 48029)",
        0xA7814C62: "FutureManager::onServerMsgCome(int,int) (MethodDef row 48032)",
        0xA7944082: "SFSObjectExtention::ToLuaTable(SFSObject,LuaEnv) (MethodDef row 17285)",
    }
    for raw, fragment in fixture.items():
        require(raw in mod.KNOWN, f"fixture mapping missing: {raw:#x}")
        require(fragment in mod.KNOWN[raw], f"fixture mapping changed: {raw:#x}")
        require((raw ^ 0x20) not in mod.KNOWN, f"mutated token unexpectedly resolved: {raw:#x}")
    require(0xDEADBEEF not in mod.KNOWN, "unrelated token unexpectedly resolved")

    result = mod.inspect()
    require(result["ok"] is True, "inspector did not pass")
    policy = result["resolutionPolicy"]
    require(policy["generalModifiedTokenInverseRecovered"] is False,
            "tool promoted an unproven global token inverse")
    require(policy["validatedLocalOperandResolution"] is True,
            "validated local operand resolution missing")
    require(policy["unknownOperandsRemainRaw"] is True,
            "unknown operand fail-closed policy changed")
    require(policy["uniqueUtf16FuidOffsets"] == ["0xd7fafc"],
            "unique current-RDL fuid literal locator changed")

    methods = result["methods"]
    send = methods["NetworkManager::SendLuaMessage(string,uint8[])"]
    connect = methods["NetworkManager::Connect(string[],int32,string,int32)"]
    reset = methods["Main.Scripts.Network.FutureManager::reset()"]
    get_id = methods["Main.Scripts.Network.FutureManager::getFutureId()"]
    on_send = methods["Main.Scripts.Network.FutureManager::onSendRequest(int32,string)"]
    on_server = methods["Main.Scripts.Network.FutureManager::onServerMsgCome(int32,int32)"]
    recv = methods["MessageFactory::DispatchResponse(string,Sfs2X.Entities.Data.SFSObject)"]
    to_lua = methods["SFSObjectExtention::ToLuaTable(Sfs2X.Entities.Data.SFSObject,XLua.LuaEnv)"]

    require((send["row"], send["rva"]) == (5752, "0xad890"), "SendLuaMessage locator changed")
    require((recv["row"], recv["rva"]) == (6192, "0xb592c"), "MessageFactory locator changed")
    require((to_lua["row"], to_lua["rva"]) == (17285, "0x1a8414"), "ToLuaTable locator changed")

    require(token_hits(send, 0xA7814D82), "send no longer allocates future id")
    require(token_hits(send, 0xA7814DC2), "send no longer associates future id/msg id")
    require(len(token_hits(send, mod.FUTURE_KEY_TOKEN)) == 1,
            "send future-key token count changed")
    require(token_hits(connect, 0xA7814DE2), "Connect no longer resets FutureManager")
    require(token_hits(reset, 0x678A52C2), "reset no longer zeroes future id field")
    require(token_hits(reset, 0x678A5222), "reset no longer reaches pending dictionary")
    require(token_hits(get_id, 0x678A52C2), "getFutureId field access changed")
    require(token_hits(on_send, 0x678A5222), "onSendRequest dictionary field changed")
    require(token_hits(on_server, 0x678A5222), "onServerMsgCome dictionary field changed")

    require(len(token_hits(recv, mod.FUTURE_KEY_TOKEN)) == 2,
            "receive future-key Contains/Get shape changed")
    for raw in (0xA79E23E2, 0xA79DD7E2, 0xA7814C62):
        require(token_hits(recv, raw), f"managed future consume chain changed: {raw:#x}")
    for raw in (0xA79E22E2, 0xA7944082, 0xA79444C2):
        require(token_hits(recv, raw), f"managed-to-Lua chain changed: {raw:#x}")

    # Unknown operands are deliberately retained; the test fails if the proof
    # silently turns into an over-broad resolver.
    require(send["unresolvedOperands"], "send unexpectedly has no raw unresolved operands")
    require(recv["unresolvedOperands"], "receive unexpectedly has no raw unresolved operands")
    require(to_lua["unresolvedOperands"], "ToLuaTable unexpectedly has no raw unresolved operands")
    require(send["originalRawBytesHex"] and recv["originalRawBytesHex"],
            "raw method bytes were not retained")

    corr = result["correlation"]
    require(corr["disposition"] == "PARTIAL_SOURCE_PROVEN_MANAGED_CORRELATION",
            "correlation disposition changed")
    require(corr["directGhostFutureKeyEmission"].startswith("UNKNOWN:"),
            "direct Ghost producer emission was improperly promoted")
    require("key-based" in corr["ordering"], "managed ordering conclusion changed")
    require("reset" in corr["reuse"], "connection reuse conclusion changed")

    print("LWB317_EXPIRY_CORRELATION_PARSER_CHECKS_OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
