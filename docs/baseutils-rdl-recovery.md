# BaseUtils.rdl loader recovery

This note records read-only recovery of the current Last War `BaseUtils.rdl` used
by the bridge loader. No game file was changed and no bridge command was queued.

## Current file

Read-only inspection on 2026-09-05 used:

`%LOCALAPPDATA%\FunFly\Last War-Survival Game\Game\LastWar_Data\Assemblies\BaseUtils.rdl`

Observed file evidence:

- size: `565248` bytes;
- SHA-256: `b20865f42c9272e0fca2b6deb2e9142576b12dcf42f11ad1a8816ddd59b952d6`;
- first four bytes: `52 47 01 00` (`RG` plus format/version bytes);
- the old installer target at file offset `0x153C` now contains
  `06 7B E2 3A 9F 67 28 22`, so the previous hard-coded location is not valid for
  the current file.

## Metadata format recovered

The file is not metadata-free. It contains an ECMA-335-style metadata root at file
offset `0x33F9C`. Its signature is `RGMD` rather than the normal CLR `BSJB`
signature, but the rest of the root uses the familiar metadata layout and reports
runtime version `v4.0.30319`.

Five normal metadata streams were recovered:

| Stream | Absolute file offset | Size |
| --- | ---: | ---: |
| `#~` | `0x34008` | `0x29124` |
| `#Strings` | `0x5D12C` | `0xFD1D` |
| `#US` | `0x6CE4C` | `0x12BCD` |
| `#GUID` | `0x7FA1C` | `0x10` |
| `#Blob` | `0x7FA2C` | `0x9E44` |

The `#~` tables stream contains 454 TypeDefs, 4,080 MethodDefs, and 4,080
MethodPtr rows. This is sufficient to resolve the loader method by metadata rather
than searching for a guessed byte offset.

## Current `CommonUtils.IsDebug`

Resolving the only MethodDef named `IsDebug` produces:

- declaring type: `CommonUtils` (TypeDef RID 9);
- method: `IsDebug` (MethodDef RID 133);
- method flags: `0x0096` (public/static/hide-by-signature in normal CLR terms);
- signature blob: `00 00 02`, i.e. default calling convention, zero parameters,
  Boolean return type;
- RVA: `0x4BD0`;
- `.text` mapping: virtual address `0x2000`, raw pointer `0x200`;
- current file offset: `0x2DD0`;
- body prefix: `08 16 2A 08 16 2A 00 00`.

The first three bytes match the supplied installer's accepted compact method
contract: header byte `0x08`/`0x0A`, Boolean constant opcode `0x16`/`0x17`, then
`ret` (`0x2A`). The current second byte is `0x16`, which is the false constant.

The same method-header transformation appears on larger methods. Current RDL tiny
headers such as `0x08` parse as normal tiny CIL headers after setting bit `0x02`
in memory (`0x0A`), while observed fat headers such as `0x11` parse after the same
in-memory repair (`0x13`). `tools/rdl_il.py` now implements this as a read-only
copy operation and feeds only the repaired copy to `dncil`; it never writes the
header back to the RDL file.

Using that repair, `LencCodec.Decrypt` (TypeDef RID 13, MethodDef RID 156, RVA
`0x542C`, file offset `0x362C`) parses as structured CIL. The recovered control
flow checks for null/short input, validates a four-byte LENC magic header, copies
the bytes after that header into a new buffer, and passes the payload plus two
static fields to a lower-level routine before returning the result. Metadata-token
operands are transformed independently. The exact 32-bit operand transform was
recovered later from the native RG loader and is documented below.

This proves the previous failure was caused by the method moving, not by the
`IsDebug` method disappearing or changing to an unknown implementation. The old
installer hard-coded RVA `0x333C` (file offset `0x153C`); the current metadata puts
the same method contract at RVA `0x4BD0` (file offset `0x2DD0`).

## Reproducible read-only inspector

`tools/inspect_baseutils_rdl.py` now parses the `RGMD` metadata streams, locates a
MethodDef by exact name, resolves its declaring TypeDef, maps its RVA through the
embedded `.text` section header, and checks the recovered loader signature. It does
not contain a write/patch path.

Example:

```powershell
python tools/inspect_baseutils_rdl.py "$env:LOCALAPPDATA\FunFly\Last War-Survival Game\Game\LastWar_Data\Assemblies\BaseUtils.rdl"
```

Use `--json` to capture machine-readable evidence.

## Consequence for reconstruction

The loader no longer needs a fixed build-specific `IsDebug` RVA. The repository
now contains `tools/install_loader_probe.py`, which resolves `CommonUtils.IsDebug`
from metadata and verifies the exact signature/body contract plus the current
`BaseUtils.rdl` hash and script content version. A live closed-game experiment on
2026-09-05 proved that changing this build's `IsDebug` result to true makes the Lua
loader seek `.lua` resources instead of the official `.luac` entry and breaks Lua
startup. A second package-only experiment also failed because the official entry
uses the current `LENC` encrypted representation. The tool therefore treats the
method as an identity gate, leaves it unchanged, and now refuses `--apply` until a
verified LENC-compatible strategy exists. See
[Version-aware loader probe](loader-probe-installation.md).


## Recovered token transform and current LENC crypto contract

Read-only native recovery on 2026-09-06 found the exact 32-bit transform used by
the current RG loader for encoded metadata operands. For a stored 32-bit value
`encoded`, the normal ECMA-335 token is:

`ror32(((encoded ^ 0xA5A5A5A5) - 0x075BCD16) ^ 0x3ADE68B1, 5)`

The subtraction wraps to 32 bits. `tools.rdl_il.decode_metadata_token()` and the
inverse `encode_metadata_token()` implement this mapping. It is validated across
multiple metadata kinds, not just fields: `0x679F3862 -> 0x040005C0` (FieldDef
RID 1472), `0x679F9902 -> 0x04000039` (FieldDef RID 57), `0xA79F9182 ->
0x0600007D` (MethodDef RID 125), `0x279FBD03 -> 0x0A000119` (MemberRef RID 281,
`RuntimeHelpers.InitializeArray`), and `0x879F9A82 -> 0x010000D5` (TypeRef).
The same transform also maps observed user-string operands back to `0x70......`
tokens.

`LencCodec..cctor` contains two `ldtoken` operands. The second one,
`0x679F3862`, decodes directly to FieldDef token `0x040005C0`, i.e. RID `1472`.
That `FieldRVA` initializer is at file offset `0x33EE7`. Its exact bytes are:

`00 01 02 03 04 05 06 07 08 09 0a 0b 0c 0d 0e 0f 10 11 12 13 14 15 16 17 18 19 1a 1b 1c 1d 1e 1f`

The same static constructor allocates `Nonce` as a 12-byte array with no
initializer, so its managed initial value is twelve zero bytes. The first
`ldtoken`, `0x679F0E22`, initializes the four-byte `MAGIC` field from the embedded
`LENC` bytes at `0x33A1D`.

The lower-level call used by `LencCodec.Decrypt` is also much narrower now. Across
all 4,080 MethodDefs there is exactly one method with the stack-compatible
signature `void(byte[], byte[], byte[])`: `ChaCha20.Xor` MethodDef RID `125`.
That wrapper forwards to the five-argument `Xor(byte[], int, int, byte[], byte[])`
implementation with offset zero and the full buffer length. Metadata constants
for `ChaCha20` recover `BLOCK_SIZE = 64` and `ROUNDS = 8`, so this is a ChaCha8
variant rather than the usual 20-round configuration.

The current managed
`ChaCha20..cctor` creates `Constants` as an empty `uint[]`, while the `Xor` body
immediately indexes four words from that field. A trial using the standard ChaCha
constant `"expand 32-byte k"`, the recovered key `00..1F`, zero nonce, counter
zero, and eight rounds does **not** reproduce the expected Lua 5.3 header from the
official LENC payload. Subsequent Assembly-CSharp/xLua tracing resolved the scope
of this discrepancy: this managed path is not the live decoder for the installed
LWLF version `3`. Version `3` returns the raw `LENC` bytes to native xLua, whose
separate transform has now been recovered. No game file was modified during these
checks.

The managed-field question is now narrower. The encoded field token used for
`ChaCha20.Constants` is `0x679F9902`, which decodes directly to FieldDef token
`0x04000039` (RID 57). An exact `.text` scan for CIL static-field
access encodings finds four `ldsfld` sites (`0x27C1`, `0x27CC`, `0x27DA`, and
`0x27E8`) and exactly one `stsfld` site (`0x2B42`). Parsing `ChaCha20..cctor`
shows that sole store is the sequence `ldc.i4.0`, `newarr`, `stsfld`, `ret`.
There is no second managed store and no managed address-take (`ldsflda`) for the
field. Therefore ordinary managed code does not populate the four constants after
the static constructor. This remains a property of the managed path; it is no
longer a blocker for decoding the installed version-3 entry.

`LencCodec.SmokeTest` does not provide an embedded known-answer vector. Its
recovered flow accepts caller-supplied encrypted bytes plus an expected string,
checks the `LENC` magic, decrypts to text, and compares the result with that
caller-supplied expectation. It therefore confirms the intended verification
path but does not reveal the missing `ChaCha20.Constants` value.

`tools/inspect_lenc_contract.py` reproduces the managed-path key/nonce/round
evidence, checks the official `LuaEntry.luac` hash, and records the failed
standard-constant trial read-only:

```powershell
python tools/inspect_lenc_contract.py --json
```

Native runtime tracing is documented separately in
[GameAssembly RGMD runtime recovery](gameassembly-rgmd-runtime.md). The live
LWLF-v3 decoder and the reason this managed trial does not apply are documented in
[Assembly-CSharp / xLua LENC runtime recovery](assembly-csharp-lwlua-lenc-runtime.md).
