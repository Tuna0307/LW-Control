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

The loader no longer needs a fixed build-specific `IsDebug` RVA. A safer future
installer can resolve `CommonUtils.IsDebug` from metadata, verify its exact
signature/body contract, verify the expected game/content version, and fail closed
when any identity check is ambiguous. That installer has not been implemented or
run in this recovery pass because Last War is currently running and the user asked
for analysis/documentation while continuing to play.
