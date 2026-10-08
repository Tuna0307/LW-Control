"""Static decode of the original LWBridge 0.3.17 lease-release finalizer family.

Read-only: parses the SHA-gated PE, disassembles with capstone, asserts every
instruction / byte fact that finalizer-recovery.json relies on, then prints a
summary (and with --write regenerates finalizer-recovery.json next to this
script). Never executes the original program or any service.

usage: python decode_finalizer.py [--write] [path-to-lwbridge-0.3.17.exe]
needs: pefile, capstone (already required by tools/lwbridge317/home009_native.py)
"""
from __future__ import annotations

import hashlib
import json
import struct
import sys
from pathlib import Path

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86 import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

SHA = "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783"
CANDIDATES = [
    Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe"),
    Path(__file__).resolve().parents[5] / "reference" / "lwbridge-0.3.17.exe",
]
args = [a for a in sys.argv[1:] if not a.startswith("--")]
WRITE = "--write" in sys.argv
exe = Path(args[0]) if args else next((p for p in CANDIDATES if p.exists()), None)
if exe is None:
    raise SystemExit("reference executable not found")
raw = exe.read_bytes()
got = hashlib.sha256(raw).hexdigest().upper()
if got != SHA:
    raise SystemExit(f"ABORT: SHA-256 mismatch {got} != {SHA}")
pe = pefile.PE(data=raw)
BASE = pe.OPTIONAL_HEADER.ImageBase
md = Cs(CS_ARCH_X86, CS_MODE_64)
md.detail = True
_cache: dict[tuple[int, int], dict[int, object]] = {}


def ins(start: int, end: int) -> dict[int, object]:
    key = (start, end)
    if key not in _cache:
        _cache[key] = {i.address - BASE: i for i in md.disasm(pe.get_data(start, end - start), BASE + start)}
    return _cache[key]


def u64(rva: int) -> int:
    return struct.unpack("<Q", pe.get_data(rva, 8))[0]


def table(base: int, n: int) -> dict[int, int]:
    return {k: base + struct.unpack("<i", pe.get_data(base + 4 * k, 4))[0] for k in range(n)}


# Whole-window instruction maps (re-disassembling from the function starts keeps decoding aligned).
FIN = ins(0x1DDDE3, 0x1DE6F0)      # finalizer poll fn
HTTP = ins(0x1DE6F0, 0x1DF7A0)     # inner authenticated-API future
WRAP = ins(0x1D420D, 0x1D4468)     # launch-failure cleanup wrapper
STOP = ins(0x1D4468, 0x1D5009)     # profile_instance_stop body
REC = ins(0x203021, 0x2057DC)      # profile_instances_reconcile body
LAUNCH = ins(0x1D5009, 0x1DDBB2)   # shared launch body
CLOSEW = ins(0x1DDC84, 0x1DDDE3)   # wait-for-game-close
LEASEFILES = ins(0x2DD578, 0x2DD7B2)
UNBIND = ins(0x23E158, 0x23E6DB)
EMIT = ins(0x3A14EC, 0x3A15A4)
STRCLONE = ins(0x2A2C0, 0x2A32E)
DROPFIN = ins(0x1E1010, 0x1E1090)
DROPREQ = ins(0x1DFEE6, 0x1DFF73)
DROPBLD = ins(0x1E0BBD, 0x1E0C27)
DROPRES = ins(0x1E0C27, 0x1E0C6C)
UNREG = ins(0x2D1EB0, 0x2D1FB2)
REMOVE = ins(0x31020F, 0x31029F)
APIERR = ins(0x3A522B, 0x3A5309)


def chk(window, rva, mnem, ops=None):
    i = window[rva]
    assert i.mnemonic == mnem, (hex(rva), i.mnemonic, i.op_str)
    if ops is not None:
        assert i.op_str == ops, (hex(rva), i.mnemonic, i.op_str, ops)
    return i


def call_target(window, rva, target):
    i = window[rva]
    assert i.mnemonic in ("call", "jmp") and i.operands[0].type == X86_OP_IMM, (hex(rva), i.op_str)
    assert i.operands[0].imm - BASE == target, (hex(rva), hex(i.operands[0].imm - BASE), hex(target))


def jump_target(window, rva, mnem, target):
    i = window[rva]
    assert i.mnemonic == mnem, (hex(rva), i.mnemonic)
    assert i.operands[0].imm - BASE == target, (hex(rva), hex(i.operands[0].imm - BASE), hex(target))


def rip_target(window, rva):
    i = window[rva]
    for op in i.operands:
        if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
            return rva + i.size + op.mem.disp
    raise AssertionError(hex(rva))


def lit(window, rva, text: bytes):
    t = rip_target(window, rva)
    assert pe.get_data(t, len(text)) == text, (hex(rva), hex(t), pe.get_data(t, len(text)))
    return t


facts: list[dict] = []


def fact(fid, addr, evidence, meaning, conf):
    facts.append({"id": fid, "addr": addr, "evidence": evidence, "interpretation": meaning, "confidence": conf})


# ---------------------------------------------------------------- finalizer 0x1DDDE3
chk(FIN, 0x1DDE18, "movzx", "eax, byte ptr [rdx + 0xe8]")
states = table(0x83DF8C, 5)
assert states == {0: 0x1DDE2F, 1: 0x1DE6E2, 2: 0x1DE6EE, 3: 0x1DE07B, 4: 0x1DDF43}, states
chk(FIN, 0x1DE6EE, "ud2")
call_target(FIN, 0x1DE6E9, 0x7A5150)  # resumed-after-completion panic helper
fact("F-STATE", "0x1ddde3/0x83df8c",
     "state byte [frame+0xe8]; jump table 0:0x1dde2f 1:0x1de6e2(panic helper 0x7a5150) 2:0x1de6ee(ud2) 3:0x1de07b 4:0x1ddf43",
     "Rust async-fn poll: 0 unresumed, 1 returned (re-poll panics), 2 poisoned, 3 awaiting the mutex Acquire future (0x1f5f9a), "
     "4 awaiting the inner authenticated-API future (0x1de6f0). Output at [rcx]: 0x8000000000000001 = Pending, 0x8000000000000000 = Ok(()), anything else = Err(80-byte error).",
     "high")

# state 0: lookup of the in-memory lease record
chk(FIN, 0x1DDE46, "cmp", "qword ptr [r15 + 0x170], 0")
call_target(FIN, 0x1DDE69, 0x381DF)   # hasher
call_target(FIN, 0x1DDEFC, 0x7A3E30)  # memcmp of key
call_target(FIN, 0x1DDFB9, 0x46D046)  # Option<&Record>::cloned
chk(FIN, 0x1DDFC5, "movabs", "rbx, 0x8000000000000000")
jump_target(FIN, 0x1DDFCF, "jmp", 0x1DE64B)
chk(FIN, 0x1DE64B, "mov", "qword ptr [rbp], rbx")
chk(FIN, 0x1DE663, "mov", "al, 1")
chk(FIN, 0x1DE665, "mov", "byte ptr [r14 + 0xe8], al")
fact("F-NOREC", "0x1dde46..0x1ddfcf,0x1de64b",
     "map count [state+0x170]==0 or key miss -> Option<Record>::cloned()==None -> rbx=0x8000000000000000; jmp 0x1de64b stores tag, state=1",
     "If no lease record exists in the in-memory map (state+0x158, key = instanceId) the finalizer returns Ok(()) immediately: no HTTP, no file removal, no event, no retry deadline.",
     "high")

# record is flagged busy, request path formatted, mutex acquired
chk(FIN, 0x1DDFE9, "call")
chk(FIN, 0x1DDFEE, "mov", "ecx, 0x158")
call_target(FIN, 0x1DDFFF, 0x310A4C)
chk(FIN, 0x1DE009, "mov", "byte ptr [rax + 0x90], 1")
t = lit(FIN, 0x1DE026, b"\x12/api/multi/leases/\xc0\x08/release\x00")
assert t == 0x83D27E
call_target(FIN, 0x1DE038, 0x276B0)
call_target(FIN, 0x1DE08B, 0x1F5F9A)
chk(FIN, 0x1DE093, "je", "0x1401de430")
chk(FIN, 0x1DE430, "movabs", "rax, 0x8000000000000001")
chk(FIN, 0x1DE43E, "mov", "al, 3")
fact("F-PATH", "0x1dde009,0x1de026,0x83d27e",
     "mov byte [rec+0x90],1 ; fmt template bytes 12 '/api/multi/leases/' c0 08 '/release' ; 0x276b0 = alloc::fmt::format",
     "Before awaiting anything the record is marked busy ([rec+0x90]=1, set via re-lookup 0x310a4c). Request target = format!(\"/api/multi/leases/{}/release\", leaseId) where leaseId is the first String of the cloned 0x98-byte record. Then await the tokio-style Mutex Acquire on [[state+0x150]+0x10]. Pending => state 3.",
     "high")

# JSON body
chk(FIN, 0x1DE10A, "movabs", "rcx, 0x65636e6174736e69")
chk(FIN, 0x1DE117, "mov", "word ptr [rax + 8], 0x6449")
chk(FIN, 0x1DE12C, "lea", "rdx, [r14 + 0x78]")
call_target(FIN, 0x1DE13B, 0x31EEB2)
call_target(FIN, 0x1DE17B, 0x5A9712)
chk(FIN, 0x1DE1AD, "movabs", "rcx, 0x6b6f54657361656c")
chk(FIN, 0x1DE1BA, "mov", "word ptr [rax + 8], 0x6e65")
chk(FIN, 0x1DE1CF, "lea", "rdx, [r14 + 0x48]")
call_target(FIN, 0x1DE1DE, 0x31EEB2)
call_target(FIN, 0x1DE220, 0x5A9712)
chk(FIN, 0x1DE257, "mov", "byte ptr [r14 + 0xf0], 5")
fact("F-BODY", "0x1de10a-0x1de257",
     "key literals 'instance'+'Id' and 'leaseTok'+'en'; values = String->serde Value (0x31eeb2) of frame+0x78 / frame+0x48 (record+0x48 / +0x18); inserted via 0x5a9712; Value tag byte 5 (Object)",
     "Request body is the JSON object {\"instanceId\": record.instanceId, \"leaseToken\": record.leaseToken}. Key order is insertion order instanceId, leaseToken (map impl unknown, so wire order UNKNOWN).",
     "high")

# state 4: awaiting the inner request
chk(FIN, 0x1DDF5D, "cmp", "eax, 3")
jump_target(FIN, 0x1DDF60, "je", 0x1DE342)
call_target(FIN, 0x1DE357, 0x1DE6F0)
chk(FIN, 0x1DE369, "jne", "0x1401de37a")
chk(FIN, 0x1DE373, "mov", "al, 4")
call_target(FIN, 0x1DE37D, 0x1DFEE6)
chk(FIN, 0x1DE382, "mov", "byte ptr [r14 + 0x490], 1")
call_target(FIN, 0x1DE3CF, 0x1E0BBD)
call_target(FIN, 0x1DE3DB, 0x47D60B)
chk(FIN, 0x1DE3E2, "cmp", "rax, qword ptr [rsp + 0x160]")
chk(FIN, 0x1DE3EA, "jno", "0x1401de445")
fact("F-AWAIT", "0x1de342-0x1de3ea",
     "poll 0x1de6f0; ==0x8000000000000001 -> sub-state [r15]=3, return Pending(al=4); else drop request future 0x1dfee6, builder 0x1e0bbd, release mutex permit 0x47d60b, then branch on first word of the 64-byte result",
     "The inner future's Ready value is Result<serde Value, ApiError>: first word 0x8000000000000000 = Ok(data), else Err. The mutex permit is held across the whole HTTP round trip and released before any local mutation.",
     "high")

# Ok branch
call_target(FIN, 0x1DE408, 0x31020F)
chk(FIN, 0x1DE412, "jno", "0x1401de4ec")
call_target(FIN, 0x1DE5C8, 0x2DD578)
chk(FIN, 0x1DE5D2, "jo", "0x1401de5e1")
call_target(FIN, 0x1DE5DC, 0x1E16A0)
call_target(FIN, 0x1DE5ED, 0x3A0510)
call_target(FIN, 0x1DE5F6, 0x3A14EC)
chk(FIN, 0x1DE614, "movabs", "rbx, 0x8000000000000000")
call_target(FIN, 0x1DE626, 0x1E0C27)
# 0x2dd578 : lease file removal
lit(LEASEFILES, 0x2DD5CB, b"remove lease proofremove lease recordremove lease directory")
lit(LEASEFILES, 0x2DD6DA, b"remove lease record")
lit(LEASEFILES, 0x2DD784, b"remove lease directory")
assert pe.get_data(0x8BC795, 9) == b"instances"
fact("F-OK", "0x1de408-0x1de626",
     "Ok => 0x31020f remove_entry(state+0x158,id) ; 0x2dd578 (remove lease proof/record/directory, result dropped at 0x1de5d2/0x1de5dc) ; 0x3a0510 (remove id from map state+0x38) ; 0x3a14ec emit ; Ok(())",
     "On a successful release (HTTP 2xx and body.ok==true; response data is NEVER read and is dropped by 0x1e0c27) the in-memory record is removed, then the on-disk lease artifacts "
     "(optional proof file = record+0x60 Option<String>, the lease record file and the instance directory under <dir>/instances/<instanceId>) are deleted with all I/O errors ignored, "
     "then the id is removed from a second map (state+0x38), then the 'bridge://multi-entitlement' event is emitted, then Ok(()).",
     "high (flow); medium (identity of map +0x38)")

# Err branch
call_target(FIN, 0x1DE456, 0x310A4C)
call_target(FIN, 0x1DE463, 0x5CFC70)
chk(FIN, 0x1DE468, "mov", "r8d, 0x3c")
chk(FIN, 0x1DE471, "xor", "r9d, r9d")
call_target(FIN, 0x1DE474, 0x5DC950)
chk(FIN, 0x1DE479, "mov", "qword ptr [rsi + 0x80], rax")
chk(FIN, 0x1DE480, "mov", "dword ptr [rsi + 0x88], edx")
call_target(FIN, 0x1DE48A, 0x3A14EC)
call_target(FIN, 0x1DE4A5, 0x2A2C0)
jump_target(FIN, 0x1DE4E7, "jmp", 0x1DE62B)
chk(STRCLONE, 0x2A2D2, "mov", "rbx, qword ptr [rdx + 8]")
chk(STRCLONE, 0x2A2D6, "mov", "rsi, qword ptr [rdx + 0x10]")
fact("F-ERR", "0x1de445-0x1de4e7",
     "Err => rec=0x310a4c lookup; [rec+0x80]=Instant::now()+Duration(60s,0) (0x5cfc70/0x5dc950, r8d=0x3c); 0x3a14ec emit; 0x2a2c0 = String::clone(code); NO call to 0x31020f/0x2dd578/0x3a0510",
     "On any release failure the record, its files and the second map entry are KEPT; only a retry-after deadline ([rec+0x80]/[rec+0x88]) = now+60 s is written if the record still exists, the entitlement event is emitted, and Err is returned. "
     "The busy flag [rec+0x90] set before the call is not cleared here. Returned error = {code = api.code, message = clone(api.code), details = api.detail (Value at +0x18..0x38)}; ApiError.retryable (+0x38) is discarded.",
     "high")

# drop glue (cancellation)
chk(DROPFIN, 0x1E1018, "movzx", "eax, byte ptr [rcx + 0xe8]")
call_target(DROPFIN, 0x1E1030, 0x1E0BBD)
call_target(DROPFIN, 0x1E103C, 0x47D60B)
call_target(DROPFIN, 0x1E105C, 0xFD161)
jump_target(DROPFIN, 0x1E1084, "jmp", 0x82658)
fact("F-DROP", "0x1e1010",
     "drop glue by state: 3 -> drop Acquire (0xfd161) ; 4 -> drop request builder (0x1e0bbd) + release permit (0x47d60b) ; both free path String [frame+0xc8] and drop record clone [frame+0x30] (0x82658); other states nothing",
     "Cancelling/dropping the finalizer future (e.g. app shutdown mid-await) performs ONLY destructors: the HTTP request is abandoned, the permit is released, the record stays in the map with busy flag=1 and WITHOUT a retry deadline, files are not removed, no event, nothing is retried by the drop.",
     "high")

# ---------------------------------------------------------------- inner future 0x1DE6F0
chk(HTTP, 0x1DE714, "movzx", "eax, byte ptr [rdx + 0x85]")
inner = table(0x83DFA0, 5)
assert inner == {0: 0x1DE72B, 1: 0x1DF79B, 2: 0x1DF7C9, 3: 0x1DE8BF, 4: 0x1DE85D}, inner
call_target(HTTP, 0x1DE7B0, 0x3A530F)   # auth config / token
call_target(HTTP, 0x1DE827, 0x3A9637)   # device fingerprint
call_target(HTTP, 0x1DF3F5, 0x3A8C1F)   # client build id / integrity
call_target(HTTP, 0x1DEC0E, 0x311B48)   # send / poll
lit(HTTP, 0x1DE9C1, b"REQUEST_TIMEOUT")
lit(HTTP, 0x1DECD3, b"NETWORK_ERROR")
lit(HTTP, 0x1DF741, b"SESSION_INVALID")
lit(HTTP, 0x1DEEE5, b"NETWORK_ERROR")   # body read / decode failure branch (0x1deeda: cmp bpl,6)
chk(HTTP, 0x1DEED8, "jne", "0x1401def23")
assert pe.get_data(0xD55402, 16) == b"AUTH_URL_INVALID"
assert pe.get_data(0xD55412, 19) == b"AUTH_HTTPS_REQUIRED"
assert pe.get_data(0xD55425, 19) == b"AUTH_NOT_CONFIGURED"
AUTHCFG = ins(0x3A530F, 0x3A55A0)
chk(AUTHCFG, 0x3A5371, "mov", "r8d, 0x10")
call_target(AUTHCFG, 0x3A5377, 0x3A522B)
call_target(HTTP, 0x1DE9B4, 0x50BD14)       # reqwest::Error::is_timeout-like probe
chk(HTTP, 0x1DE9B9, "test", "al, al")
chk(HTTP, 0x1DE9BB, "je", "0x1401decd3")   # false -> NETWORK_ERROR ; true -> REQUEST_TIMEOUT

lit(HTTP, 0x1DF084, b"SERVICE_UNAVAILABLE")
lit(HTTP, 0x1DEFA9, b"ok")
lit(HTTP, 0x1DEFCE, b"data")
lit(HTTP, 0x1DF019, b"/error/code")
lit(HTTP, 0x1DF09C, b"/error/detail")
chk(HTTP, 0x1DEF87, "mov", "ecx, 0xffffff38")
chk(HTTP, 0x1DEF93, "cmp", "cx, 0x63")
chk(HTTP, 0x1DEF9C, "cmp", "eax, 5")
chk(HTTP, 0x1DEFC3, "cmp", "byte ptr [rax], 1")
chk(HTTP, 0x1DEFC8, "cmp", "byte ptr [rax + 1], 0")
lit(HTTP, 0x1DF4A0, b"application/json")
lit(HTTP, 0x1DF4B5, b"Accept")
lit(HTTP, 0x1DF4FD, b"X-Device-Fingerprint")
lit(HTTP, 0x1DF52F, b"X-Client-Build-Id")
lit(HTTP, 0x1DF569, b"X-Client-Auth-Policy")
assert pe.get_data(0x83D3EA, 1) == b"2"
chk(HTTP, 0x1DF607, "cmp", "qword ptr [r13 + 0x3a8], 0")
chk(HTTP, 0x1DF60F, "je", "0x1401df741")
# A-Z0-9_ validation loop for the server error code
chk(HTTP, 0x1DF35B, "mov", "r11b, byte ptr [r9 + r10]")
chk(HTTP, 0x1DF363, "cmp", "dil, 0x1a")
chk(HTTP, 0x1DF36D, "cmp", "dil, 0xf6")
chk(HTTP, 0x1DF374, "cmp", "r11b, 0x5f")
fact("F-HTTP", "0x1de6f0 (+drop 0x1dfee6)",
     "state byte [+0x85] table 0x83dfa0 {0:0x1de72b 1:0x1df79b 2:0x1df7c9 3:0x1de8bf 4:0x1de85d}; callees 0x3a530f (auth base-URL/config: AUTH_URL_INVALID, AUTH_HTTPS_REQUIRED, AUTH_NOT_CONFIGURED), 0x3a9637 (MachineGuid via reg.exe query), 0x3a8c1f (CLIENT_INTEGRITY_FAILED), 0x311b48 (send/body poll); headers Accept: application/json, X-Device-Fingerprint, X-Client-Build-Id, X-Client-Auth-Policy: 2; source file src\\commands\\auth.rs",
     "Generic authenticated JSON POST helper shared by lease acquire/activate/release. Order: auth base-URL/config -> device fingerprint -> client build/integrity -> (if auth required and [state+0x3a8]==0) SESSION_INVALID without sending -> send. "
     "Transport errors: is_timeout (0x50bd14) -> REQUEST_TIMEOUT else NETWORK_ERROR (both retryable=true via 0x3a529d); SESSION_INVALID/AUTH_* /CLIENT_INTEGRITY_FAILED are retryable=false (0x3a522b). A failure while reading/decoding the response body also yields NETWORK_ERROR (0x1deeda..0x1deefd). "
     "Success = HTTP status 200..=299 AND body is a JSON object AND body.ok == true -> Ok(body.data clone). Otherwise ApiError{code = /error/code if it is a non-empty String of only [A-Z0-9_] else 'SERVICE_UNAVAILABLE', detail = /error/detail clone or None}. No retry loop inside.",
     "high (flow); medium (header/value roles); no live bytes")

# Bytes vtable resolution of the indirect call
chk(HTTP, 0x1DEDD0, "test", "r13, r13")
chk(HTTP, 0x1DEE65, "call", "qword ptr [r13 + 0x20]")
call_target(HTTP, 0x1DECA9, 0x2E4902)
static_vt = [0x351BF, 0x35225, 0x35209, 0x2A661, 0x2A664]
for vt in (0x7FDFA8, 0x7FEC68, 0x7FFD10, 0x801BE8, 0xD4A150, 0xD51828, 0xDF8500):
    assert [u64(vt + 8 * k) - BASE for k in range(5)] == static_vt, hex(vt)
sv = ins(0x2A661, 0x2A665)
chk(sv, 0x2A661, "xor", "eax, eax")
chk(sv, 0x2A663, "ret")
chk(sv, 0x2A664, "ret")
assert [u64(0x7E9CD0 + 8 * k) - BASE for k in range(5)] == [0x35C65, 0x35D62, 0x35C97, 0x35E20, 0x35C1C]
assert [u64(0x7E9A98 + 8 * k) - BASE for k in range(5)] == [0x3518D, 0x351F3, 0x351DD, 0x35314, 0x35188]
shared = ins(0x35C1C, 0x35C65)
chk(shared, 0x35C26, "lock dec", "qword ptr [rcx + 0x20]")
chk(shared, 0x35C57, "jmp", "qword ptr [rip + 0x78ba72]")  # HeapFree
sh = ins(0x2E4902, 0x2E49A6)
chk(sh, 0x2E4955, "shl", "rax, 5")   # 32-byte elements = bytes::Bytes
fact("F-CALLBACK", "0x1dedd0/0x1dee65 ; 0x2e4902 ; vtables 0x7fdfa8.. 0x7e9a98 0x7e9cd0",
     "call [r13+0x20] with rcx=data field, rdx=ptr, r8=len ; r13 = vtable pointer of a 32-byte element popped from a ring buffer (0x2e4902: shl rax,5) ; .rdata has 5-slot tables [clone,to_vec,to_mut,is_unique,drop]: static (7 copies, drop=0x2a664=ret), 0x7e9a98 (drop 0x35188->0x35287), 0x7e9cd0 (drop 0x35c1c: lock dec [rcx+0x20], HeapFree)",
     "The 'runtime callback' is NOT a protocol callback: it is bytes::Bytes::drop dispatched through the Bytes vtable (slot +0x20 in this bytes version) while the HTTP client discards buffered body chunks on the transport-error path. Targets are enumerable (static no-op, promotable, shared) and have no lease/ticket semantics.",
     "medium-high (signature + vtable shape; producer set enumerated by pattern, not by RTTI)")

# ---------------------------------------------------------------- callers / precedence
for site in (0x1D8AE2,):
    call_target(LAUNCH, site, 0x1DDDE3)
call_target(WRAP, 0x1D4371, 0x1DDDE3)
call_target(STOP, 0x1D4AC8, 0x1DDDE3)
call_target(REC, 0x2046EE, 0x1DDDE3)
# every caller discards a non-Ok finalizer result with 0x1e16a0
call_target(LAUNCH, 0x1D8B3C, 0x1E16A0)
chk(LAUNCH, 0x1D8B32, "jo", "0x1401d8b41")
chk(WRAP, 0x1D43A9, "cmp", "qword ptr [r15], r12")
call_target(WRAP, 0x1D43B6, 0x1E16A0)
chk(STOP, 0x1D4B0C, "jo", "0x1401d4b1b")
call_target(STOP, 0x1D4B16, 0x1E16A0)
chk(REC, 0x20472F, "jo", "0x14020473e")
call_target(REC, 0x204739, 0x1E16A0)
fact("F-PRECEDENCE-FIN", "0x1d8b27-0x1d8b3c ; 0x1d43a4-0x1d43b6 ; 0x1d4b01-0x1d4b16 ; 0x204724-0x204739",
     "all four call sites: poll finalizer, Pending(0x8000000000000001)->propagate Pending, else drop future (0x1e1010) and conditionally drop result (0x1e16a0 unless tag==0x8000000000000000)",
     "The finalizer's Err is discarded at every call site (launch lease-persist failure, launch-failure wrapper, Stop, reconcile). No path surfaces a release/cleanup error code to the UI.",
     "high")

# wrapper (launch-failure cleanup)
chk(WRAP, 0x1D426C, "lock cmpxchg", "dword ptr [r13 + 0x210], ecx")
chk(WRAP, 0x1D42D2, "mov", "qword ptr [r13 + 0x220], r12")
call_target(WRAP, 0x1D42F1, 0x2D1EB0)
call_target(WRAP, 0x1D4317, 0x1F5F9A)
call_target(WRAP, 0x1D43BF, 0x47D60B)
call_target(WRAP, 0x1D43EC, 0x23E158)
chk(WRAP, 0x1D4427, "mov", "cl, 1")
chk(WRAP, 0x1D4429, "xor", "eax, eax")
wrap_states = table(0x83DE94, 5)
assert wrap_states == {0: 0x1D4243, 1: 0x1D445A, 2: 0x1D4466, 3: 0x1D4310, 4: 0x1D435F}, wrap_states
chk(WRAP, 0x1D4417, "mov", "al, 1")
# unregister helper details
call_target(UNREG, 0x2D1ECF, 0x2C6AE1)
chk(UNREG, 0x2D1EE2, "lock cmpxchg", "dword ptr [rsi + 0xd8], ecx")
# profile unbind helper: INSTANCE_MISMATCH, recovery.json removal
t = pe.get_data(0x84185F, 17)
assert t == b"INSTANCE_MISMATCH", t
lit(UNBIND, 0x23E1CD, b"STATE_UNAVAILABLE")
assert pe.get_data(0xD47FEC, 13) == b"recovery.json"
assert b"remove recovery record" in pe.get_data(0xD4806D, 0x60)
call_target(UNBIND, 0x23E404, 0x2DBDE4)
call_target(UNBIND, 0x23E509, 0x310176)
launch_calls = sorted(
    r for r, i in LAUNCH.items()
    if i.mnemonic == "call" and i.operands[0].type == X86_OP_IMM and i.operands[0].imm - BASE == 0x1D420D
)
assert len(launch_calls) == 27, len(launch_calls)
fact("F-WRAPPER", "0x1d420d (27 call sites inside 0x1d5009)",
     "async fn(state A, state B, profileId, instanceId): (1) RwLock write [A+0x210], A.Option@+0x220 := None ; (2) 0x2d1eb0 unregister instance ; (3) await Mutex [A+0x108] ; (4) 0x1ddde3 finalizer, result dropped ; (5) unlock ; (6) 0x23e158 profile unbind, result dropped ; Ready(()) (cl=1, al=0)",
     "Every launch failure branch after lease acquisition runs this wrapper: local instance/pipe registration removal FIRST (synchronous, before any network), then lease release (HTTP, may take up to the client timeout), then removal of the profile's recovery.json / profile binding. The wrapper cannot fail, so the launch error is always the one returned. "
     "If the wrapper future is dropped while awaiting (state 3/4: drop glue 0x1dff73) steps (4)-(6) are skipped but step (1)-(2) already happened.",
     "high (order/ignored errors); medium (what A+0x220 holds)")
fact("F-LAUNCH-DOUBLE", "0x1d8ae2 then 0x1d8f8a",
     "launch lease-grant apply fails (0x3a0a95 non-Ok at 0x1d88cf/0x1d88d6) -> finalizer directly (0x1d8ae2), result dropped; then error flows to 0x1d8d30 jno 0x1d8f22 -> wrapper 0x1d420d at 0x1d8f8a",
     "On this one path the release is attempted twice: once directly and once inside the wrapper. If the first removed the record, the second finalizer is the no-record Ok no-op; if the first failed the second is a second HTTP attempt (no delay between). The returned error is the original apply error.",
     "medium-high (inferred from straight-line flow, not executed)")

# Stop / reconcile
call_target(STOP, 0x1D4971, 0x1DDC84)
call_target(STOP, 0x1D4A2A, 0x2D1EB0)
call_target(STOP, 0x1D4B50, 0x23E158)
chk(STOP, 0x1D4B90, "cmp", "rax, r13")
jump_target(STOP, 0x1D4BC9, "jmp", 0x1D4ECF)
chk(CLOSEW, 0x1DDCEE, "mov", "ecx, 0x64")
assert CLOSEW[0x1DDD2A].op_str == "r8d, 0x5f5e100"
t = pe.get_data(0x83C6D8, 18)
assert t == b"GAME_CLOSE_TIMEOUT", t
call_target(REC, 0x204593, 0x1DDC84)
call_target(REC, 0x204649, 0x2D1EB0)
call_target(REC, 0x204786, 0x23E158)
chk(REC, 0x2047C7, "cmp", "rax, rcx")
jump_target(REC, 0x2047CA, "je", 0x204B33)
fact("F-STOP-RECON", "0x1d4468 ; 0x203021",
     "Stop: wait-for-close 0x1ddc84 (100 iterations x 100 ms, then GAME_CLOSE_TIMEOUT 'The game did not close in time.') -> 0x2d1eb0 unregister -> lock -> finalizer (error dropped) -> unlock -> 0x23e158 (its Err, tag 0x8000000000000001, IS propagated at 0x1d4b90/0x1d4bc9 -> 0x1d4ecf). Reconcile: same sequence per element (0x204593/0x204649/0x2046ee/0x204786), 0x23e158 Err handled at 0x2047ca->0x204b33.",
     "Stop and reconcile run the release only after the game is confirmed closed; a close timeout aborts before any release/unregister. Release errors never reach the caller; unbind errors (INSTANCE_MISMATCH 0x84185f, STATE_UNAVAILABLE, PROFILE_NOT_FOUND) can.",
     "high (Stop); medium (reconcile error handling at 0x204b33 not fully followed)")

# event emission
lit(EMIT, 0x3A1562, b"bridge://multi-entitlement")
chk(EMIT, 0x3A156E, "mov", "r9d, 0x1a")
fact("F-EVENT", "0x3a14ec / 0xd54d60",
     "string 'bridge://multi-entitlement' (0x1a bytes) passed to the emit helper 0x28830e with a snapshot struct (LeaseResponse leaseId/leaseToken/instanceId/expiresAt/serverTime ; EntitlementResponse planCode/maxProfiles/accountExpiresAt)",
     "Both the success and failure branches of the finalizer emit the 'bridge://multi-entitlement' UI event (after local state mutation). The no-record branch does not.",
     "medium-high")

summary_unknowns = [
    {"id": "U-1", "what": "Real HTTP/timeout values and server error bodies for /api/multi/leases/{id}/release",
     "reason": "protected service; no request/response bytes available. Client timeout is configured outside the future (reqwest builder not located)."},
    {"id": "U-2", "what": "Exact retry/consumer semantics of record fields +0x80/+0x88 (60 s deadline) and +0x90 (busy flag)",
     "reason": "writers found in four sibling lease ops (0xe6f71, 0xe9115, 0x2d8131, finalizer); the periodic renew/heartbeat reader and the code that clears +0x90 were not decoded."},
    {"id": "U-3", "what": "Identity of AppState.Option@+0x220 cleared by the wrapper and of the second map at lease-service state+0x38",
     "reason": "only mutation sites decoded; no naming string tied to them."},
    {"id": "U-4", "what": "Exact on-disk file names removed by 0x2dd578 (lease record file name, proof file naming)",
     "reason": "path built by 0x2dcd09 (not decoded); only the error-context strings and the 'instances' directory literal were confirmed. recovery.json belongs to 0x2dbde4, not to 0x2dd578."},
    {"id": "U-5", "what": "Whether every one of the 27 launch-failure edges has a distinct error code and which ones occur for the current client",
     "reason": "only call-site count and shared cleanup semantics established; producer-by-producer code mapping not done here."},
    {"id": "U-6", "what": "Reconcile handling of the 0x23e158 error at 0x204b33 (does it become an errors[] entry?)",
     "reason": "branch not followed to the JSON projection."},
    {"id": "U-7", "what": "JSON key emission order for {instanceId, leaseToken}",
     "reason": "depends on serde_json map feature (BTreeMap vs preserve_order); not determined."},
]

clone_notes = [
    {"orig": "launch failure => wrapper 0x1d420d: unregister instance, release remote lease (errors ignored), remove recovery.json/profile binding (errors ignored); launch error returned",
     "clone": "OverviewLifecycleService.StartAsync catch (lines ~877-915) sets phase/connectionState=error and lastError=ex.Code only when gamePid is null, rethrows; finally block cancels the control-pipe launch binding (pipe registration). Lease/adoption/runtime-session cleanup (StopLeaseTimer(deleteLease:true), ClearRuntimeSessionFiles, RemoveAdoptionRecord) exists in ResetCancelledStartState (~969-1002) which runs for cancel paths only. The remote /api/multi/leases/{id}/release call has no clone counterpart (protected service; clone uses a local lease timer).",
     "status": "difference to review: non-cancel launch failures in the clone do not run the lease/record cleanup the original runs at every failure edge (verify whether the python helper does it)"},
    {"orig": "cleanup errors never replace the launch error; release Err is dropped everywhere",
     "clone": "StopCancelledSuccessfulStartAsync maps a restore failure to GAME_CLOSE_FAILED / RECOVERY_RECORD_COMMIT_FAILED (new codes that replace the triggering cancel/timeout in some branches)",
     "status": "adapted behaviour; not source-equivalent"},
    {"orig": "Stop: wait up to 100 x 100 ms for close (GAME_CLOSE_TIMEOUT, 'The game did not close in time.') BEFORE unregister/release; unbind Err (INSTANCE_MISMATCH etc.) is propagated",
     "clone": "StopAsync (~1099-1170): see file; not compared line by line here",
     "status": "UNKNOWN comparison"},
]

if __name__ == "__main__":
    out = {
        "referenceSha256": SHA,
        "mode": "static disassembly only; original program and services never executed",
        "tool": "python capstone/pefile (same as tools/lwbridge317/home009_native.py)",
        "facts": facts,
        "launchFailureCallSitesOfWrapper": [hex(x) for x in launch_calls],
        "cloneComparison": clone_notes,
        "unknowns": summary_unknowns,
    }
    print(f"SHA-256 OK ({exe}); {len(facts)} fact groups asserted; 27 wrapper call sites in launch body")
    for f in facts:
        print(f"[{f['id']}] {f['addr']}  ({f['confidence']})\n   {f['interpretation']}")
    print("UNKNOWNS:")
    for u in summary_unknowns:
        print(f"  {u['id']}: {u['what']} -- {u['reason']}")
    if WRITE:
        dest = Path(__file__).resolve().parent / "finalizer-recovery.json"
        dest.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
        print("wrote", dest)
