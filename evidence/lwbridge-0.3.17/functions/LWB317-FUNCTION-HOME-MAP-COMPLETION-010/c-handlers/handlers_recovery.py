#!/usr/bin/env python3
"""Static, hash-gated recovery of original LWBridge 0.3.17 Home handlers (HOME-MAP-COMPLETION-010 / c-handlers).

Reads (never executes) the reference PE. Every asserted fact is an instruction/byte check;
a failed assertion aborts with a non-zero exit code. Output: handlers-recovery.facts.json
(machine-checked evidence block; the interpretive record is handlers-recovery.json/.md).

usage: python handlers_recovery.py [path-to-lwbridge-0.3.17.exe]
needs: pefile, capstone (same as tools/lwbridge317/home009_native.py)
"""
from __future__ import annotations

import hashlib
import json
import os
import re
import struct
import sys
from pathlib import Path

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86 import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

SHA = "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
HERE = Path(__file__).resolve().parent
REPO = HERE.parents[4]
CANDIDATES = [
    Path(sys.argv[1]) if len(sys.argv) > 1 else None,
    Path(os.environ.get("LWB317_EXE", "")) if os.environ.get("LWB317_EXE") else None,
    Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe"),
    REPO / "reference" / "lwbridge-0.3.17.exe",
]
EXE = next(p for p in CANDIDATES if p is not None and p.exists())
RAW = EXE.read_bytes()
assert hashlib.sha256(RAW).hexdigest() == SHA, "reference executable identity mismatch (abort)"

pe = pefile.PE(data=RAW)
pe.parse_data_directories(
    directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"],
                 pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_IMPORT"]])
BASE = pe.OPTIONAL_HEADER.ImageBase
md = Cs(CS_ARCH_X86, CS_MODE_64)
md.detail = True
FUNCS = sorted((x.struct.BeginAddress, x.struct.EndAddress) for x in pe.DIRECTORY_ENTRY_EXCEPTION)
IMPORTS = {}
for ent in pe.DIRECTORY_ENTRY_IMPORT:
    for imp in ent.imports:
        IMPORTS[imp.address - BASE] = f"{ent.dll.decode()}!{(imp.name or b'').decode()}"


def func_of(rva):
    import bisect
    i = bisect.bisect_right([f[0] for f in FUNCS], rva) - 1
    return FUNCS[i] if i >= 0 and rva < FUNCS[i][1] else None


def rd(rva, n):
    return pe.get_data(rva, n)


def text(rva, n):
    return rd(rva, n).decode("latin-1")


def dis(a, b):
    return {i.address - BASE: i for i in md.disasm(rd(a, b - a), BASE + a)}


def rip_target(i):
    for op in i.operands:
        if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
            return i.address - BASE + i.size + op.mem.disp
    return None


def imm_target(i):
    return i.operands[0].imm - BASE if i.operands and i.operands[0].type == X86_OP_IMM else None


_text_cache = {}


def call_sites(target):
    """direct E8/E9 sites whose rel32 resolves to target (scan of every executable section)."""
    out = []
    for s in pe.sections:
        if not (s.Characteristics & 0x20000000):
            continue
        data = s.get_data()
        va = s.VirtualAddress
        pos = data.find(b"\xe8")
        while pos != -1:
            if pos + 5 <= len(data):
                rel = struct.unpack_from("<i", data, pos + 1)[0]
                if va + pos + 5 + rel == target:
                    out.append(va + pos)
            pos = data.find(b"\xe8", pos + 1)
    return out


DEV = os.environ.get("LWB_DEV") == "1"
FAILS = []


def check(cond, msg):
    if not cond:
        if DEV:
            FAILS.append(msg)
            print("FAIL:", msg)
            return
        raise AssertionError(msg)


FACTS = []


def fact(item, fid, locator, evidence, interpretation, confidence, state="EXACT_BYTES"):
    FACTS.append({"item": item, "id": fid, "locator": locator, "evidence": evidence,
                  "interpretation": interpretation, "confidence": confidence, "state": state})


def hx(v):
    return hex(v)


# ---------------------------------------------------------------------------------------------
# generic: the error constructor 0x2a1a47(out, code_ptr, code_len, msg_ptr, msg_len[stack]).
# code string = rdx/r8d, message = r9 / [rsp+0x20]. Helper reads both.
def err_pair(code_rva, code_len, msg_rva, msg_len):
    return rd(code_rva, code_len).decode(), rd(msg_rva, msg_len).decode()


# =============================================================================================
# ITEM 0: presence of the clone's error codes in the original host image
# =============================================================================================
CLONE_CODES = ["AUTOMATION_NOT_IMPLEMENTED", "BRIDGE_DISCONNECTED", "BRIDGE_HOST_BUSY", "GAME_CLOSE_FAILED",
               "GAME_OPERATION_CANCELLED", "GAME_REPAIR_REQUIRED", "INSTANCE_NOT_OWNED", "INVALID_PAYLOAD",
               "GAME_RUNNING", "PROFILE_ALREADY_RUNNING", "PROFILE_RUNNING", "PROFILE_NOT_FOUND",
               "PROFILE_LOCKED", "INVALID_REQUEST", "INVALID_PROFILE_ID", "INVALID_PROFILE_NOTE",
               "INVALID_PROFILE_ORDER", "PROFILE_ID_REQUIRED", "PROFILE_RUNTIME_UNAVAILABLE",
               "STATE_UNAVAILABLE", "GAME_OPERATION_IN_PROGRESS", "GAME_ROOT_NOT_FOUND", "INVALID_GAME_ROOT",
               "UNMANAGED_GAME_RUNNING", "GAME_CLOSE_TIMEOUT", "LAUNCH_FAILED", "RECOVERY_RECORD_INVALID",
               "RECOVERY_PROCESS_MISMATCH", "RECOVERY_RECORD_NOT_FOUND", "GAME_DISCONNECTED",
               "SERVER_MAINTENANCE", "COMMAND_NOT_IMPLEMENTED", "PROFILE_LIMIT_REACHED", "INSTANCE_MISMATCH",
               "INSTANCE_NOT_FOUND", "BRIDGE_START_TIMEOUT", "PROFILE_ALREADY_BOUND"]
code_counts = {c: RAW.count(c.encode()) for c in CLONE_CODES}  # plain substring: Rust literals are packed without separators
for c in ("AUTOMATION_NOT_IMPLEMENTED", "BRIDGE_DISCONNECTED", "BRIDGE_HOST_BUSY", "GAME_CLOSE_FAILED",
          "GAME_OPERATION_CANCELLED", "GAME_REPAIR_REQUIRED", "INSTANCE_NOT_OWNED", "INVALID_PAYLOAD",
          "COMMAND_NOT_IMPLEMENTED", "SERVER_MAINTENANCE"):
    check(code_counts[c] == 0, f"{c} unexpectedly present in the original image")
for c in ("PROFILE_NOT_FOUND", "PROFILE_LOCKED", "INVALID_REQUEST", "INVALID_PROFILE_ID", "INVALID_PROFILE_NOTE",
          "INVALID_PROFILE_ORDER", "PROFILE_ID_REQUIRED", "PROFILE_RUNTIME_UNAVAILABLE", "STATE_UNAVAILABLE",
          "GAME_OPERATION_IN_PROGRESS", "GAME_ROOT_NOT_FOUND", "INVALID_GAME_ROOT", "UNMANAGED_GAME_RUNNING",
          "GAME_CLOSE_TIMEOUT", "LAUNCH_FAILED", "RECOVERY_RECORD_INVALID", "RECOVERY_PROCESS_MISMATCH",
          "RECOVERY_RECORD_NOT_FOUND", "GAME_DISCONNECTED", "PROFILE_ALREADY_RUNNING", "PROFILE_RUNNING",
          "PROFILE_LIMIT_REACHED", "INSTANCE_MISMATCH", "INSTANCE_NOT_FOUND", "BRIDGE_START_TIMEOUT"):
    check(code_counts[c] >= 1, f"{c} missing from the original image")
fact("0", "code-presence", "whole image byte scan (plain substring; literals are packed)",
     {"absentInOriginalHostImage": sorted(c for c, n in code_counts.items() if n == 0),
      "presentCounts": {c: n for c, n in code_counts.items() if n}},
     "Codes with count 0 are not produced by the original native host as string literals "
     "(game/Lua-produced codes are not covered by this scan).", "HIGH")

# =============================================================================================
# ITEM 1: set_automation  0x12c34b-0x12da36
# =============================================================================================
SA = (0x12c34b, 0x12da36)
sa = dis(*SA)
check(func_of(0x12c34b) == SA, "set_automation function range")
check(text(0x829d7e, 14) == "set_automation", "command name literal")
check(rip_target(sa[0x12c3ef]) == 0x829d7e, "name literal reference at 0x12c3ef")
# payload parse: name via get("name") -> str or "" ; enabled via get("enabled") bool default false
check(rd(0x39f35e, 0x41)[:3] == b"\x48\x83\xec" and text(0x829d7e + 0, 0) == "", "helper present")
g = dis(0x39f35e, 0x39f39f)
check(g[0x39f362].op_str == "byte ptr [rcx], 5", "get_str: payload must be Object (serde tag 5)")
check(g[0x39f375].op_str == "byte ptr [rax], 3", "get_str: value tag 3 = String")
check(g[0x39f38a].mnemonic == "test" and g[0x39f396].mnemonic == "cmove", "missing/non-string -> ('' empty) via cmove")
check(sa[0x12c8b0].op_str == "byte ptr [rbp], 5", "payload Object check before reading enabled")
check(rd(rip_target(sa[0x12c895]), 11) == b"nameenabled", "keys name/enabled")
check(sa[0x12c8df].op_str == "byte ptr [rax], 1" and sa[0x12c8e2].op_str == "eax, byte ptr [rax + 1]",
      "enabled: only JSON bool (serde tag 1) is honoured, otherwise false")
check(sa[0x12c8d8].mnemonic == "xor" and sa[0x12c93f].mnemonic == "xor", "enabled default false")
# profile resolution first
check(imm_target(sa[0x12c7e9]) == 0x23b9e5, "profile-runtime resolver 0x23b9e5 called before name handling")
# name dispatch
check(sa[0x12c98c].op_str == "rdi, 0xe" and sa[0x12c992].op_str == "rdi, 0x15", "name length dispatch 14 / 21")
check(sa[0x12c9c5].op_str == "rax, 0x736f6c436f747561" and sa[0x12c9cf].op_str.endswith("[rsi]"), "'autoClos' compare")
check(sa[0x12c9d2].op_str == "rcx, 0x7075706f5065736f" and sa[0x12c9dc].op_str.endswith("[rsi + 6]"), "'osePopup' compare constant at +6")
check(struct.pack("<Q", 0x736f6c436f747561) + struct.pack("<Q", 0x7075706f5065736f)[2:] ==
      b"autoClosePopup"[:8] + b"autoClosePopup"[8:], "ClosePopup constants spell autoClosePopup")
check(sa[0x12c9e3].mnemonic == "je" and imm_target(sa[0x12c9e3]) == 0x12d080, "autoClosePopup -> 0x12d080")
check(sa[0x12d080].mnemonic == "xor" and sa[0x12d080].op_str == "ecx, ecx", "autoClosePopup forces enabled=false (xor ecx,ecx)")
check(sa[0x12d082].op_str == "rsi, [rbx + 0x1020]" or sa[0x12d082].op_str.startswith("rsi, [rbx + 0x1020"), "0x12d082 entry")
check(sa[0x12d089].op_str == "byte ptr [rbx + 0x1020], cl", "effective enabled stored at frame+0x1020")
check(sa[0x12c9c0].mnemonic == "jmp" and imm_target(sa[0x12c9c0]) == 0x12d082,
      "autoForceUpdateReload jumps to 0x12d082 with ecx=requested enabled (skips the xor)")
check(rd(0xd45b28, 21) == b"autoForceUpdateReload" and rd(0xd44fab, 24) == b"auto_force_update_reload"
      and rd(0xd44fc3, 16) == b"auto_close_popup", "config key names")
# local persistence helper 0x2ce276
check(imm_target(sa[0x12d0ac]) == 0x2ce276, "local persist via 0x2ce276")
lp = dis(0x2ce276, 0x2ce676)
check(lp[0x2ce366].op_str == "qword ptr [r14 + 8], 0x15", "persist: 21-byte name compare")
check(rip_target(lp[0x2ce374]) == 0xd45b28, "persist: compare against autoForceUpdateReload")
check(lp[0x2ce38d].op_str == "r14, [rcx*8 + 0x10]", "key length 0x18 vs 0x10")
check(rip_target(lp[0x2ce2cb]) is not None and text(rip_target(lp[0x2ce2cb]), 17) == "STATE_UNAVAILABLE",
      "persist: STATE_UNAVAILABLE on poisoned lock")
check(rd(0xd476b8, 27) == b"config state is unavailable", "STATE_UNAVAILABLE message")
check(text(rip_target(lp[0x2ce450]), 14) == "INVALID_CONFIG" and text(rip_target(lp[0x2ce457]), 15) == "object required",
      "persist: INVALID_CONFIG / 'object required' when config.json root is not an object")
check(text(rip_target(lp[0x2ce538]), 11) == "config.json", "persist target file config.json")
# bridge-online gate and forward
check(imm_target(sa[0x12d170]) == 0x2d47a6, "bridge_online predicate after local persist")
check(imm_target(sa[0x12d15d]) == 0x41a681 and sa[0x12d112].op_str == "qword ptr [rbx + 0xff0], 0x15",
      "autoForceUpdateReload additionally applies to the recovery supervisor (0x41a681)")
check(text(rip_target(sa[0x12d303]), 13) == "setAutomation" and sa[0x12d31c].op_str.endswith("0x1388"),
      "local path forwards 'setAutomation' with 5000 ms timeout (frame+0x1060)")
check(sa[0x12cbb7].op_str.endswith("0x1388") and text(rip_target(sa[0x12cb9e]), 13) == "setAutomation",
      "bridge-name path forwards 'setAutomation' with 5000 ms timeout")
chk = dis(0x2ae893, 0x2ae8e4)
check(imm_target(chk[0x2ae89e]) == 0x2d47a6, "0x2ae893 = require_game_connected")
check(rd(0xd44e27, 17) == b"GAME_DISCONNECTED" and rd(0xd44e38, 17) == b"game disconnected",
      "GAME_DISCONNECTED / 'game disconnected'")
check(imm_target(sa[0x12c9fb]) == 0x2ae893, "bridge-name path requires a connected game first")
check(imm_target(sa[0x12cd01]) == 0x2cd431, "bridge-name path persists via 0x2cd431 only after the game accepted")
# 0x2cd431 key map
cs = dis(0x2cd431, 0x2cdcab)
keys = {}
for a_, k_, ln in ((0xd44e67, "auto_red_packet_treasure", 24), (0xd44f86, "auto_weekend_shield", 19),
                   (0xd44f99, "auto_attack_shield", 18), (0xd44fc3, "auto_close_popup", 16)):
    check(rd(a_, ln) == k_.encode(), "key literal " + k_)
    keys[k_] = hx(a_)
check(struct.pack("<Q", 0x6968735f6f747561) + b"eld"[:0] + struct.pack("<I", 0x646c6569)[:0] != b"",
      "auto_shield bytes present")
check(cs[0x2cd9c6].op_str == "rcx, 0x6968735f6f747561" and cs[0x2cd9d3].op_str.endswith("0x646c6569"),
      "auto_shield key built inline ('auto_shi' + 'ield')")
check(text(rip_target(cs[0x2cd5ba]), 14) == "INVALID_CONFIG", "0x2cd431 INVALID_CONFIG when config root not an object")
# 0x41a681 semantics
ra = dis(0x41a681, 0x41a850)
check(ra[0x41a6a4].op_str == "dl, dl" and ra[0x41a6a6].mnemonic == "je", "0x41a681(ctx, enabled): enabled -> tail-call 0x41a8a0")
check(imm_target(ra[0x41a6cb]) == 0x41a8a0, "enabled=true runs the recovery monitor tick immediately")
check(rd(0xd67e28, 0x34) == b"game recovery stopped because automation is disabled", "disable log message")
check(rd(0xd67f2a, 24) == b"auto_force_update_reload", "monitor re-reads auto_force_update_reload (0x41b7ef)")
fact("1", "set_automation-parse", "0x12c34b (poll fn), 0x39f35e, 0x12c88d-0x12c941",
     {"payload": "serde_json::Value; name = payload.get('name') as string else '' ; enabled = payload.get('enabled') "
                 "if JSON bool else false; profile resolved first via 0x23b9e5 (profileId required)",
      "noPayloadTypeErrorCodes": "there is no INVALID_PAYLOAD/AUTOMATION_* code in the image"},
     "Missing/non-string name is treated as the empty string and falls into the forward-to-game branch; missing or "
     "non-boolean enabled is false. The only payload error is the shared profile resolver: PROFILE_ID_REQUIRED "
     "(missing/non-string/empty profileId), STATE_UNAVAILABLE, PROFILE_RUNTIME_UNAVAILABLE.", "HIGH")
fact("1", "set_automation-local-names", "0x12c98c-0x12d3a7",
     {"names": ["autoClosePopup (len 14)", "autoForceUpdateReload (len 21)"],
      "configKeys": {"autoClosePopup": "auto_close_popup", "autoForceUpdateReload": "auto_force_update_reload"},
      "effectiveEnabled": {"autoForceUpdateReload": "requested enabled", "autoClosePopup": "always false (xor ecx,ecx at 0x12d080)"},
      "order": ["persist bool into <profile>/config.json via 0x2ce276 (errors propagate)",
                "autoForceUpdateReload only: 0x41a681(runtime, enabled) -> enabled: run recovery tick now; "
                "disabled: clear recovery state + log 'game recovery stopped because automation is disabled'",
                "if game connected (0x2d47a6): send 'setAutomation' {name, enabled=effective} to the game, timeout 5000 ms; "
                "failure is only logged (warn) and ignored",
                "return Object{ok:true,name,enabled=effective}"]},
     "Local names are persisted first and the response always succeeds even when the game is offline or rejects the "
     "forward. The UI reads response.enabled as the new switch value, so autoClosePopup can never be switched on.",
     "HIGH (mechanics) / MEDIUM (that forcing false is intended product behaviour)")
fact("1", "set_automation-forwarded-names", "0x12c9e9-0x12cd01, 0x2cd431, 0x2ae893",
     {"nameValidation": "none in the host: any other name (including '') is forwarded to the game",
      "gate": "require_game_connected -> else GAME_DISCONNECTED ('game disconnected')",
      "request": "setAutomation {name, enabled}, timeout 5000 ms",
      "afterGameOk": "persist to config.json: autoShield->auto_shield, autoAttackShield->auto_attack_shield, "
                     "autoWeekendShield->auto_weekend_shield, autoRedPacketTreasure->auto_red_packet_treasure; every "
                     "other name only recomputes legacy auto_shield = auto_weekend_shield || auto_attack_shield",
      "gameRejects": "error object from the game is returned unchanged (code/message produced game-side; Lua not decodable here)",
      "returnValue": {"ok": True, "name": "<requested>", "enabled": "<requested>"}},
     "The host does not own the 'allowed names' list for these; the game-side setAutomation does (UNKNOWN: encrypted Lua).",
     "MEDIUM", "EXACT_BYTES (host side) / UNKNOWN (game side)")
fact("1", "set_automation-ui", "frontend index-BVfnK1wp.js ht()/automation flags",
     {"uiNames": ["autoForceUpdateReload", "autoClosePopup", "autoWeekendShield", "autoAttackShield"],
      "uiRead": "automation_status.config.auto_force_update_reload / auto_close_popup / auto_weekend_shield / auto_attack_shield",
      "uiWrite": "(await set_automation(name, enabled, profileId)).enabled"},
     "Home flags are read from automation_status.config keys and written through set_automation; response.enabled is authoritative.",
     "HIGH", "EXACT_CONTRACT")

# =============================================================================================
# ITEM 2: profile_select / reorder / note
# =============================================================================================
ps = dis(0x10b70b, 0x10c27f)
check(text(0x8299d9, 14) == "profile_select", "name literal")
check(rip_target(ps[0x10b7af]) == 0x8299d9, "ref")
check(imm_target(ps[0x10ba79]) == 0x2ac656 and text(rip_target(ps[0x10ba5e]), 9) == "profileId",
      "profileId extracted with the INVALID_REQUEST extractor")
ex = dis(0x2ac656, 0x2ac6c0)
check(ex[0x2ac65e].op_str == "byte ptr [rdx], 5" and ex[0x2ac67a].op_str == "byte ptr [rax], 3", "Object + String required")
check(text(rip_target(ex[0x2ac69f]), 15) == "INVALID_REQUEST" and ex[0x2ac696].mnemonic == "mov", "INVALID_REQUEST (code==message)")
check(imm_target(ps[0x10bab8]) == 0x326f21, "select_profile")
check(imm_target(ps[0x10bad0]) == 0x2abf1d and text(rip_target(dis(0x2abf1d, 0x2abf66)[0x2abf2e]), 9) == "focusGame",
      "focusGame optional bool")
fg = dis(0x2abf1d, 0x2abf66)
check(fg[0x2abf22].op_str == "sil, 2" and fg[0x2abf4c].op_str == "esi, 2", "focusGame default (tri-state 2 -> true)")
check(imm_target(ps[0x10bafa]) == 0x2424d7, "post-select instance lookup (STATE_UNAVAILABLE on poisoned lock)")
sel = dis(0x326f21, 0x327460)
check(imm_target(sel[0x326f51]) == 0x3288c1, "select validates profile id first")
vid = dis(0x3288c1, 0x328924)
check(text(rip_target(vid[0x3288f4]) if rip_target(vid[0x3288f4]) else 0xd4ea14, 18) == "INVALID_PROFILE_ID", "INVALID_PROFILE_ID")
check(vid[0x3288c9].op_str == "rax, [r8 - 0x41]" and vid[0x3288cd].op_str == "rax, -0x41" and vid[0x3288d1].mnemonic == "jbe",
      "profile id length 1..64")
cc = dis(0x398035, 0x398085)
check(cc[0x398049].op_str == "r9b, 0xa" and cc[0x398060].op_str == "eax, 0x5f" and cc[0x398065].op_str == "eax, 0x2d",
      "profile id charset [0-9A-Za-z_-]")
check(rd(0xd4e7b1, 0x47) == b"SELECT enabled = 1 AND locked_reason IS NULL FROM profiles WHERE id = ?", "select SQL")
check(b"PROFILE_LOCKEDUPDATE controller_state SET value = ? WHERE key = 'selected_profile_id'" in RAW,
      "PROFILE_LOCKED then UPDATE controller_state")
check(sel[0x327189].op_str == "sil, 2" and rd(0xd4de0d, 17) == b"PROFILE_NOT_FOUND" and sel[0x32719f].op_str == "r8d, 0x11",
      "no row -> PROFILE_NOT_FOUND")
check(sel[0x32722b - 0 if 0x32722b in sel else 0x32722b].mnemonic == "mov" and sel[0x327215].mnemonic == "mov" and sel[0x327215].op_str.endswith("0xe"),
      "disabled/locked -> PROFILE_LOCKED (len 14)")
ro = dis(0x327460, 0x327c9b)
check(imm_target(ro[0x3274a3]) == 0x3288c1, "reorder validates every id")
check(imm_target(ro[0x3274d3]) == 0x325abe, "reorder reads current list")
check(rd(0xd4e8e2, 21) == b"INVALID_PROFILE_ORDER" and ro[0x3275e1].mnemonic == "mov", "INVALID_PROFILE_ORDER")
check(ro[0x327580].op_str == "qword ptr [rsp + 0x248], r14" and ro[0x327588].mnemonic == "jne"
      and ro[0x32758a].op_str == "r14, rdi", "count equality with current profile count")
check(ro[0x327832].op_str == "edi, edi" and ro[0x327b27].op_str == "rdi" and ro[0x327b27].mnemonic == "inc",
      "display_order = 0-based index")
check(rd(0xd4e8a0, 0x42) == b"UPDATE profiles SET display_order = ?, updated_at = ? WHERE id = ?", "reorder SQL")
check(rd(0xd4e90d, 21) == b"begin profile reorder", "single transaction")
nt = dis(0x327c9b, 0x32830a)
check(imm_target(nt[0x327ccf]) == 0x3288c1, "note: id validated first")
check(nt[0x327d47].mnemonic == "cmp" and nt[0x327d47].op_str == "rax, 0x50", "note length <= 80 characters")
check(rd(0xd4ea00, 20) == b"INVALID_PROFILE_NOTE", "INVALID_PROFILE_NOTE")
check(nt[0x327e87].op_str == "edx, 0x20" and nt[0x327e90].op_str == "edx, -0x7f" and nt[0x327e93].op_str == "edx, 0x21",
      "note control chars: <0x20 and 0x7f..0x9f rejected")
check(rd(0xd4e934, 0x39) == b"UPDATE profiles SET note = ?, updated_at = ? WHERE id = ?", "note SQL")
fact("2", "profile_select", "0x10b70b, 0x326f21, 0x3288c1, 0x2abf1d, 0x2424d7",
     {"order": ["payload Object & profileId String else INVALID_REQUEST (code==message)",
                "profile id valid ([A-Za-z0-9_-], 1..64 bytes) else INVALID_PROFILE_ID",
                "SELECT enabled=1 AND locked_reason IS NULL: no row -> PROFILE_NOT_FOUND; 0 -> PROFILE_LOCKED",
                "UPDATE controller_state SET value=? WHERE key='selected_profile_id'",
                "focusGame (JSON bool; missing/non-bool = true): focus the profile's game window if it has an instance"],
      "noRunningCheck": "selecting never checks running state",
      "codesNotInOriginal": ["BRIDGE_HOST_BUSY"]},
     "PROFILE_NOT_FOUND and PROFILE_LOCKED are original. BRIDGE_HOST_BUSY is not present anywhere in the original image: "
     "the original has no 'cannot change profile while shared bridge host is busy' rejection.", "HIGH")
fact("2", "profile_reorder", "0x12709b handler, 0x327460",
     {"order": ["profileIds must be array of strings (INVALID_REQUEST otherwise, see clone parity)",
                "each id validated (INVALID_PROFILE_ID)", "list must have exactly the current profile count and contain "
                "every existing profile id, else INVALID_PROFILE_ORDER",
                "one transaction; display_order = 0-based position; updated_at = now(ms)"]},
     "Full permutation required; ordering semantics = display_order ascending (list query ORDER BY display_order, created_at, id).",
     "HIGH (SQL/limits) / MEDIUM (handler-level array extraction)")
fact("2", "profile_note_set", "0x1a4d26 handler, 0x327c9b",
     {"order": ["profileId + note strings", "profile id validated FIRST (INVALID_PROFILE_ID)",
                "note <= 80 chars and no control chars -> else INVALID_PROFILE_NOTE",
                "UPDATE profiles SET note=?, updated_at=? WHERE id=?; 0 rows -> PROFILE_NOT_FOUND"]},
     "Id validation precedes note validation.", "HIGH")

# =============================================================================================
# ITEM 3: game root change
# =============================================================================================
gr = dis(0x188f12, 0x18a2ad)
check(text(0x829d6e, 16) == "game_root_select", "name literal")
check(rip_target(gr[0x188fb3]) == 0x829d6e, "ref")
check(gr[0x1893da].op_str == "rcx, 0x64656c65636e6163", "'canceled' key built inline")
check(struct.pack("<Q", 0x64656c65636e6163) == b"canceled", "key spelling is canceled (single l)")
check(gr[0x1893ad].mnemonic == "jne" and gr[0x1893a0].op_str == "rsi, 0x8000000000000001", "dialog-cancelled branch")
check(text(rip_target(gr[0x18972e]), 17) == "INVALID_GAME_ROOT", "INVALID_GAME_ROOT")
check(imm_target(gr[0x189939]) == 0x413185, "set_game_root(state, path)")
check(rd(0xd6639f, 17) == b"INVALID_GAME_ROOT" and rd(0xd663b0, 0x2d) == b"select the folder containing Game\\LastWar.exe",
      "INVALID_GAME_ROOT message")
sg = dis(0x413185, 0x41353a)
check(imm_target(sg[0x4131a9]) == 0x41281d and imm_target(sg[0x4131c3]) == 0x412d56, "normalize then validate")
check(imm_target(sg[0x41327a]) == 0x41253e, "persist")
check(rd(0xd65f22, 4) == b"Game", "Game dir literal")
PROC = {"CreateToolhelp32Snapshot", "Process32FirstW", "Process32NextW", "OpenProcess", "TerminateProcess",
        "EnumWindows", "QueryFullProcessImageNameW", "NtQuerySystemInformation", "K32EnumProcesses"}


def reach_proc(start_fn, depth=4, maxsize=6000):
    seen = {start_fn}
    frontier = [start_fn]
    hits = {}
    cache = {}

    def info(fn):
        if fn in cache:
            return cache[fn]
        calls, iats = set(), set()
        for i in md.disasm(rd(fn[0], fn[1] - fn[0]), BASE + fn[0]):
            if i.mnemonic in ("call", "jmp") and i.operands and i.operands[0].type == X86_OP_IMM:
                f = func_of(i.operands[0].imm - BASE)
                if f and f != fn:
                    calls.add(f)
            t = rip_target(i)
            if t in IMPORTS:
                iats.add(IMPORTS[t].split("!")[1])
        cache[fn] = (calls, iats)
        return cache[fn]

    for d in range(depth):
        nxt = []
        for f in frontier:
            calls, iats = info(f)
            for x in iats & PROC:
                hits.setdefault(x, []).append((hx(f[0]), d))
            for g in calls:
                if g not in seen and g[1] - g[0] < maxsize:
                    seen.add(g)
                    nxt.append(g)
        frontier = nxt
    return hits, len(seen)


hits, nfn = reach_proc((0x188f12, 0x18a2ad))
check(hits == {}, f"game_root_select unexpectedly reaches process APIs: {hits}")
fact("3", "game_root_select", "0x188f12-0x18a2ad, 0x413185, 0x41281d, 0x412d56, 0x41253e",
     {"payload": "none (frontend calls game_root_select() without arguments; folder chosen with a native dialog)",
      "cancel": "result carries cancelled:true (key built inline at 0x1893da); nothing persisted",
      "validate": "normalized path must contain Game\\LastWar.exe layout else INVALID_GAME_ROOT "
                  "'select the folder containing Game\\LastWar.exe'",
      "persist": "state write under a write-lock (poisoned -> STATE_UNAVAILABLE 'authorization state is unavailable')",
      "processApisReachable": hits, "functionsVisited": nfn,
      "noRunningCheck": "no GAME_RUNNING/GAME_OPERATION_IN_PROGRESS/stop/rebind inside the handler or its direct-call closure (depth 4, indirect calls not followed)"},
     "The original does not reject, stop or re-bind a running game when the root changes. It persists the new root; "
     "later consumers (monitor 0x41a8a0 uses find_game_pid(root) 0x41b451, stop 0x128a38, launch 0x1d5009 GAME_ROOT_NOT_FOUND) "
     "read the stored root. DEDUCED (not executed): a running game under the old root is no longer found under the new root by the recovery monitor.",
     "HIGH (no rejection) / LOW-MEDIUM (downstream deduction)")

# =============================================================================================
# ITEM 4: game_recovery_status
# =============================================================================================
gs = dis(0x154905, 0x155144)
check(text(0x82982f, 20) == "game_recovery_status", "name literal")
check(rip_target(gs[0x1549ab]) == 0x82982f, "ref")
check(imm_target(gs[0x154c65]) == 0x23b9e5, "profile runtime resolver")
check(imm_target(gs[0x154c91]) == 0x41c8dd, "recovery snapshot reader")
rs = dis(0x23b9e5, 0x23ba8f)
check(text(rip_target(rs[0x23b9fe]) if rip_target(rs[0x23b9fe]) else 0x841585, 9) == "profileId", "profileId key")
check(rs[0x23b9f1].op_str == "byte ptr [r8], 5" and rs[0x23ba18].op_str == "byte ptr [rax], 3", "Object/String check")
check(rs[0x23ba2f].mnemonic == "test" and rs[0x23ba47].mnemonic == "je", "empty string (len 0) -> PROFILE_ID_REQUIRED (no trim)")
check(rd(0x84158e, 19) == b"PROFILE_ID_REQUIRED", "PROFILE_ID_REQUIRED")
rr = dis(0x24156e, 0x241840)
check(imm_target(rr[0x2417a0]) == 0x2a1a47 and rd(0x841a25, 27) == b"PROFILE_RUNTIME_UNAVAILABLE", "PROFILE_RUNTIME_UNAVAILABLE (0x1b)")
check(text(rip_target(rr[0x241606]), 17) == "STATE_UNAVAILABLE", "STATE_UNAVAILABLE (registry lock poisoned)")
rc = dis(0x41c8dd, 0x41ca70)
check(rd(0xd67eb0, 17) == b"STATE_UNAVAILABLE" and rd(0xd67ec1, 0x22) == b"game recovery state is unavailable",
      "recovery state lock poisoned")
ser = dis(0x41d3fc, 0x41d5e7)
order = []
for a_ in sorted(ser):
    t = rip_target(ser[a_])
    if ser[a_].mnemonic == "lea" and t is not None and 0xd67700 <= t < 0xd68200:
        order.append(t)
KEYS = {0xd68060: "updateDetected", 0xd6806e: "restarted", 0xd68077: "startedAt", 0xd68080: "completedAt",
        0xd6808b: "attempts", 0xd68093: "nextRetryAt", 0xd67743: "error", 0xd6809e: "noticeId", 0xd680a6: "noticeVisible"}
check(rd(0xd68060, 14) == b"updateDetected" and rd(0xd6806e, 9) == b"restarted" and rd(0xd68077, 9) == b"startedAt"
      and rd(0xd68080, 11) == b"completedAt" and rd(0xd6808b, 8) == b"attempts" and rd(0xd68093, 11) == b"nextRetryAt"
      and rd(0xd67743, 5) == b"error" and rd(0xd6809e, 8) == b"noticeId" and rd(0xd680a6, 13) == b"noticeVisible",
      "record key literals")
fact("4", "game_recovery_status", "0x154905, 0x23b9e5, 0x24156e, 0x41c8dd, 0x41d3fc",
     {"errors": [
         {"code": "PROFILE_ID_REQUIRED", "message": "PROFILE_ID_REQUIRED",
          "when": "payload not Object, profileId missing, non-string, or empty string (no trimming)"},
         {"code": "STATE_UNAVAILABLE", "message": "(registry lock poisoned; message equals code)", "when": "profile registry lock poisoned"},
         {"code": "PROFILE_RUNTIME_UNAVAILABLE", "message": "PROFILE_RUNTIME_UNAVAILABLE", "when": "no runtime for that profileId"},
         {"code": "STATE_UNAVAILABLE", "message": "game recovery state is unavailable", "when": "recovery-state mutex poisoned (never for 'no recovery yet')"}],
      "result": "always an Object snapshot: state, reason, updateDetected, restarted, startedAt, completedAt, attempts, "
                "nextRetryAt, error, noticeId, noticeVisible (serializer key order 0x41d3fc)",
      "noRecoveryYet": "snapshot is still returned (idle record); there is no error for 'nothing recorded'"},
     "Errors only arise from profile resolution or a poisoned lock; the idle state is a normal result, not STATE_UNAVAILABLE.", "HIGH")

# =============================================================================================
# ITEM 5: Start in-flight / precondition order
# =============================================================================================
ob = dis(0x41b3bc, 0x41b451)
check(ob[0x41b3d9].mnemonic == "lock cmpxchg" or "cmpxchg" in ob[0x41b3d9].mnemonic or ob[0x41b3d9].op_str.endswith("[rdx + 0x115], cl"),
      "op-flag try-acquire is a CAS on ctx+0x115")
check(rd(0xd67ee3, 26) == b"GAME_OPERATION_IN_PROGRESS" and rd(0xd67efd, 0x2d) == b"another game operation is already in progress",
      "GAME_OPERATION_IN_PROGRESS code/message")
callers_41b3bc = sorted(call_sites(0x41b3bc))
check(callers_41b3bc == [0xe5921, 0x111bee, 0x1291e6], f"callers of try-acquire: {[hx(c) for c in callers_41b3bc]}")
check(func_of(0x111bee) == (0x11157c, 0x112b45) and text(0x829a50, 10) == "game_start", "0x11157c = game_start")
check(func_of(0x1291e6) == (0x128a38, 0x129fa7) and text(0x8297aa, 9) == "game_stop", "0x128a38 = game_stop")
check(func_of(0xe5921) == (0xe5884, 0xe6bf7), "0xe5884 = recovery run")
# profile_instance_start does NOT use the flag
pis = (0x20715f, 0x207ca9)
d_pis = dis(*pis)
check(text(0x83c347, 22) == "profile_instance_start", "profile_instance_start literal")
check(imm_target(d_pis[0x207808]) == 0x1d5009, "profile_instance_start -> launch 0x1d5009")
check(text(rip_target(d_pis[0x207784]), 14) == "closeUnmanaged", "closeUnmanaged key")
check(imm_target(d_pis[0x207684]) == 0x2ac656 and text(rip_target(d_pis[0x207669]), 9) == "profileId", "profileId via INVALID_REQUEST extractor")
launch = dis(0x1d5009, 0x1ddbb2)
refs = {}
for a_, i in launch.items():
    t = rip_target(i)
    if i.mnemonic == "lea" and t in (0x83c51a, 0x83c68d, 0x83c910, 0x83c3a0, 0x83c6d8, 0x841870):
        refs.setdefault(t, []).append(a_)
check(0x1d5be1 in refs[0x83c51a], "GAME_ROOT_NOT_FOUND at 0x1d5be1")
check(refs[0x83c910] == [0x1d7690], "PROFILE_LIMIT_REACHED at 0x1d7690")
check(imm_target(launch[0x1d6240]) == 0x23e9dc, "reserve call 0x23e9dc inside launch at 0x1d6240")
res = dis(0x23e9dc, 0x23efdb)
check(rd(0x841870, 23) == b"PROFILE_ALREADY_RUNNING" and res[0x23ec6c].op_str == "r8d, 0x17", "PROFILE_ALREADY_RUNNING")
check(res[0x23ebff].op_str == "ecx, 0x6f727265" and res[0x23ec0a].op_str == "eax, 0x72", "stale 'error'-phase record check ('erro'+'r')")
check(res[0x23ec11].op_str == "byte ptr [r8 - 0x18], 0", "error-phase record replaced only if flag byte == 0")
unm = [a_ for a_, i in launch.items() if i.mnemonic == "lea" and rip_target(i) is not None and 0x83c650 <= rip_target(i) < 0x83c6e0]
check(min(unm) > 0x1d6240 and max(unm) < 0x1d7690, "unmanaged-game gate lies between reserve (0x1d6240) and PROFILE_LIMIT_REACHED (0x1d7690)")
check(not any(0x20715f <= c < 0x207ca9 for c in call_sites(0x41b3bc)), "profile_instance_start never takes the op flag")
check(0x1d6240 < 0x1d6548 < 0x1d7690, "precondition order")
gsd = dis(0x11157c, 0x112b45)
check(imm_target(gsd[0x111bee]) == 0x41b3bc and imm_target(gsd[0x111c31]) == 0x41bcd5 and imm_target(gsd[0x111d23]) == 0x41b451
      and imm_target(gsd[0x111e4e]) == 0x41e613, "game_start call order: op-flag, persist desired, find tracked game, kill launcher/updater/sync")
check(gsd[0x111c28].op_str == "bpl, 1" and gsd[0x111c45].op_str == "byte ptr [r14 + 0x124], bpl", "game_start sets desired-running (persist true, then memory)")
check(text(rip_target(gsd[0x111c83]), 30) == "game lifecycle start requested", "log")
fact("5", "profile_instance_start-order", "0x20715f, 0x1d5009 (0x1d5411.. 0x1d7690), 0x23e9dc, 0x2ac656",
     {"order": [
         "payload: Object with String profileId, else INVALID_REQUEST (closeUnmanaged bool, default false)",
         "STATE_UNAVAILABLE (authorization state lock) 0x1d5411",
         "GAME_ROOT_NOT_FOUND 0x1d5be1",
         "reserve instance 0x23e9dc @0x1d6240: STATE_UNAVAILABLE (registry lock) | PROFILE_ALREADY_RUNNING when the profile already has an instance record (a stale phase=='error' record with flag byte 0 is silently replaced)",
         "capacity log, unmanaged gate 0x1d6548-0x1d74e5: UNMANAGED_GAME_RUNNING{pids} unless closeUnmanaged, terminate errors, GAME_CLOSE_TIMEOUT",
         "PROFILE_LIMIT_REACHED 0x1d7690", "ticket/lease/launch/bridge-wait stages"],
      "notInThisPath": ["GAME_OPERATION_IN_PROGRESS", "GAME_RUNNING", "GAME_OPERATION_CANCELLED"],
      "opFlagUsers": "ctx+0x115 try-acquire (0x41b3bc) is used only by game_start 0x11157c, game_stop 0x128a38 and the recovery run 0xe5884"},
     "For the UI-used profile_instance_start the single in-flight/already-running rejection is PROFILE_ALREADY_RUNNING "
     "(any existing instance record, including starting), evaluated after GAME_ROOT_NOT_FOUND and BEFORE the unmanaged-game check. "
     "GAME_RUNNING exists in the image only in the proxy-repair path (0xd47442).", "HIGH (order) / MEDIUM (reserve predicate meaning of flag byte)")
fact("5", "game_start-legacy", "0x11157c-0x112b45",
     {"order": ["profile resolver 0x23b9e5 (PROFILE_ID_REQUIRED/STATE_UNAVAILABLE/PROFILE_RUNTIME_UNAVAILABLE)",
                "try-acquire op flag else GAME_OPERATION_IN_PROGRESS 'another game operation is already in progress'",
                "persist game_desired_running=true (0x41bcd5), memory flag ctx+0x124=1, reset recovery observation 0x41ad16, log 'game lifecycle start requested'",
                "find tracked game process 0x41b451: present -> success path without launching",
                "kill LastWarLauncher/Updater/Sync 0x41e613; re-find; terminate stray game 0x41e543; launch",
                "on any error: release flag, memory flag=0, persist game_desired_running=false (0x1123b4)"]},
     "Legacy single-game command not used by the Home frontend (frontend uses profile_instance_*).", "MEDIUM", "EXACT_CONTRACT_RECONSTRUCTED")

# =============================================================================================
# ITEM 6: profile_instance_status
# =============================================================================================
pist = dis(0x1a0da4, 0x1a213c)
check(text(0x829e4c, 23) == "profile_instance_status", "name literal")
check(imm_target(pist[0x1a1436]) == 0x2ac656 and text(rip_target(pist[0x1a141e]), 9) == "profileId", "profileId via INVALID_REQUEST extractor")
check(imm_target(pist[0x1a151a]) == 0x241836 and imm_target(pist[0x1a1672]) == 0x24156e, "instance lookup then runtime lookup")
for a_, s_ in ((0x1a17cf, b"ionState"), (0x1a17dd, b"connecti"), (0x1a188b, b"onnected"), (0x1a1899, b"bridgeCo"),
               (0x1a190f, b"rtbeatAt"), (0x1a191d, b"lastHear"), (0x1a1b96, b"leaseAct")):
    imm = pist[a_].operands[1].imm
    check(struct.pack("<Q", imm) == s_, f"inline key bytes at {hx(a_)}")
cl = dis(0x2d7ff3, 0x2d8131)
check(cl[0x2d7ffe].op_str == "r10, qword ptr [rcx + 0x48]" and cl[0x2d8002].mnemonic == "jno" and imm_target(cl[0x2d8002]) == 0x2d802e,
      "lastError Some -> 0x2d802e")
check(rd(0x8bcc4e, 5) == b"error" and cl[0x2d7ff6].op_str == "edx, 5", "-> 'error'")
check(cl[0x2d8008].op_str == "r10, 8" and cl[0x2d8012].op_str == "r11, 0x676e697472617473", "phase 'starting' -> 'starting'")
check(cl[0x2d8036].op_str == "r10, 0xa" and cl[0x2d8040].op_str == "r11, 0x697265766f636572" and rd(0x84b684, 10) == b"recovering",
      "phase 'recovering' -> 'recovering'")
check(cl[0x2d806b].op_str == "r10, 0x10" and cl[0x2d8124].mnemonic == "lea" and rd(0xd47bf0, 13) == b"awaitingLogin",
      "phase 'awaitingIdentity' (16) -> 'awaitingLogin'")
check(cl[0x2d811c].mnemonic == "lea" and rd(0xd47bdf, 5) == b"grace", "'grace' result exists")
check(rd(0xd47be4, 12) == b"reconnecting" and rd(0xd47bce, 9) == b"connected" and rd(0xd47bd7, 8) == b"starting", "'reconnecting'/'connected' results")
check(cl[0x2d80cf].op_str == "edx, 0xc" and cl[0x2d8116].op_str == "edx, 9", "len 12 'reconnecting' / len 9 'connected'")
fact("6", "profile_instance_status-shape", "0x1a0da4, inline key bytes 0x1a17cf-0x1a1ba3",
     {"errors": ["INVALID_REQUEST (profileId missing/non-string; NOT PROFILE_ID_REQUIRED in 0.3.17)",
                 "STATE_UNAVAILABLE", "PROFILE_RUNTIME_UNAVAILABLE"],
      "result": "Option<instance record>; none -> JSON null (UI treats stopped as null); fields profileId, instanceId, phase, pid, "
                "startedAt, lastError, identityConfirmed, leaseRequired, connectionState, bridgeConnected, lastHeartbeatAt, leaseActive"},
     "0.3.17 still builds connectionState/bridgeConnected/lastHeartbeatAt/leaseActive (keys assembled from immediates, so "
     "string search misses them).", "HIGH (keys) / MEDIUM (null on absence: carried from 0.3.1 + structure)")
fact("6", "connectionState-classifier", "0x2d7ff3-0x2d8130",
     {"inputs": "record(lastError,phase,identityConfirmed[+0x70],leaseRequired[+0x71]), leaseStatus str, leaseActive, bridgeConnected, heartbeatFresh",
      "table": [
          "lastError != null -> 'error'",
          "phase 'starting' -> 'starting'", "phase 'recovering' -> 'recovering'", "phase 'awaitingIdentity' -> 'awaitingLogin'",
          "else leaseRequired && leaseStatus=='grace' && !leaseActive -> 'grace'",
          "else connected iff bridgeConnected && heartbeatFresh && identityConfirmed (&& leaseActive when leaseRequired) ; otherwise 'reconnecting'"],
      "uiMap": {"offline": "profile.connection.offline", "starting": "..starting", "recovering": "..recovering",
                "awaitingLogin": "..awaitingLogin", "connected": "..connected", "reconnecting": "..reconnecting",
                "grace": "..grace", "locked": "..locked", "error": "..error"},
      "noStopped": "no 'stopped' phase or 'maintenance'/'offline' connectionState is produced by this classifier"},
     "The clone's CreateProfileInstanceStatus matches this table for single-profile; the clone-only 5-field CreateInstanceStatus "
     "(phase 'stopped', 'offline', 'maintenance', error text codes) has no counterpart in the original handler.", "HIGH (table)", "EXACT_BYTES")

# =============================================================================================
# ITEM 7: game_desired_running persistence
# =============================================================================================
check(rd(0xd47c80, 20) == b"game_desired_running", "config key literal (writer)")
check(rd(0xd68197, 20) == b"game_desired_running", "config key literal (reader)")
wr = dis(0x2ceb43, 0x2ceef2)
check(rip_target(wr[0x2ced1a]) == 0xd47c80, "writer references the key")
check(text(rip_target(wr[0x2cedb6]), 11) == "config.json", "writer persists into config.json")
rdr = dis(0x41be3c, 0x41c0ab)
check(rip_target(rdr[0x41bf3a]) == 0xd68197 and rdr[0x41bf41].op_str == "r8d, 0x14", "reader gets 'game_desired_running'")
check(rdr[0x41bf51].op_str == "byte ptr [rax], 1" and rdr[0x41bf56].op_str == "dil, byte ptr [rax + 1]", "bool only; default false")
check(rdr[0x41c065].op_str == "byte ptr [r15 + 0x124], dil" and rdr[0x41c06c].op_str == "word ptr [r15 + 0x125], 0x100",
      "context init: desired(+0x124) <- persisted value; busy(+0x125)=0; monitor-armed(+0x126)=1")
check(sorted(call_sites(0x41be3c)) == [0x21d66b, 0x29f4b4], "context constructor callers")
sd = dis(0x41bd6f, 0x41be3c)
check(sd[0x41bd8c].op_str == "bpl, 1" and imm_target(sd[0x41bd95]) == 0x41bcd5, "0x41bd6f(ctx,pid): persist true")
check(imm_target(dis(0x41bcd5, 0x41bd6f)[0x41bcfd]) == 0x2ceb43, "0x41bcd5 persists via 0x2ceb43")
cl2 = dis(0x41bbff, 0x41bcd5)
check(cl2[0x41bc26].op_str == "r8d, r8d" and imm_target(cl2[0x41bc29]) == 0x41bcd5, "0x41bbff: persist false")
check(sorted(call_sites(0x41bbff)) == [0xe9030, 0x16e74d, 0x199e30, 0x1d46f4, 0x23cb7e], "clear-desired callers (incl. profile_instance_stop 0x199e30)")
check(sorted(call_sites(0x41bd6f)) == [0x23fe5c, 0x2a1196, 0x2a12fe], "set-desired callers (launch finalize 0x23fac5, reconcile 0x2a0cb7)")
check(func_of(0x23fe5c) == (0x23fac5, 0x23fef2), "launch finalize")
fact("7", "game_desired_running", "0x41be3c, 0x2ceb43, 0x41bcd5, 0x41bd6f, 0x41bbff, 0x41ca7d",
     {"persisted": "yes: config.json key game_desired_running (bool) written on every transition",
      "readAtStartup": "yes: runtime-context constructor 0x41be3c reads it (missing/non-bool = false) into ctx+0x124",
      "set": "game_start (before launch, reset to false on failure), launch finalize 0x23fac5 and reconcile adoption (0x41bd6f with pid)",
      "cleared": "profile_instance_stop (0x199e30 -> 0x41bbff), rollback helper 0x1d4468, 0xe6f71, 0x16e139, 0x23c486 and 0x41ca7d",
      "separateGate": "auto_force_update_reload is a separate persisted flag; the monitor tick needs auto_force_update_reload && desired && armed AND a tracked pid (ctx+0x120 != 0)",
      "restartBehaviour": "a persisted true with no tracked pid does not relaunch by itself (tick returns when ctx+0x120==0)"},
     "Clone persisting GameDesiredRunning matches the original; do not make it memory-only.", "HIGH")

# =============================================================================================
with open(HERE / "handlers-recovery.facts.json", "w", encoding="utf-8") as fh:
    json.dump({"referenceSha256": SHA.upper(), "mode": "static PE read/disassembly only", "facts": FACTS,
               "codePresenceInOriginalImage": code_counts}, fh, indent=2, ensure_ascii=False)
print(f"OK: {len(FACTS)} fact blocks, all assertions passed ({EXE})" if not FAILS else f"DEV: {len(FAILS)} failed assertions")
