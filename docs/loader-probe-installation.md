# Version-aware loader probe research

The current Last War build was tested with a small, independent Lua loader probe
while the game was closed. Those experiments established useful package and loader
facts, but the install strategy is rejected for the current build. The official
Lua entry is encrypted with the game's `LENC` format, so replacing that entry with
plaintext Lua breaks startup. `tools/install_loader_probe.py --apply` now fails
closed until the current encrypted loader contract is recovered and verified.

## Current build gates

The installer fails closed unless all of these recovered facts still match:

- `LWScripts.data` is an `LWLF` package at content version `12`;
- `version.txt` also reports `12`;
- `LWScripts.txt` exactly matches the package byte length and standard CRC-32;
- `BaseUtils.rdl` SHA-256 is
  `b20865f42c9272e0fca2b6deb2e9142576b12dcf42f11ad1a8816ddd59b952d6`;
- metadata uniquely resolves `CommonUtils.IsDebug` with signature `00 00 02` and
  the recovered tiny constant-Boolean body, and the method still returns false;
- Last War is not running;
- the official `DataCenter/Global/LuaEntry.luac` entry exists and no unrelated
  replacement is already installed.

The script directory is under `%LOCALAPPDATA%\..\LocalLow\FunFly\Last
War-Survival Game\lwScripts`; the earlier LocalAppData path was incorrect.

## Rejected plaintext-entry strategy

The historical research path created a full backup of `LWScripts.data`,
`LWScripts.txt`, `version.txt`, and `BaseUtils.rdl` below
`%LOCALAPPDATA%\LWControl\backups` before replacing anything. It then attempted to:

1. preserves the official Lua entry as
   `DataCenter/Global/LuaEntry_original.luac`;
2. installs a small wrapper as `DataCenter/Global/LuaEntry.luac`;
3. adds `LWControlProbe.luac`, which writes only
   `%LOCALAPPDATA%\LWControl\runtime\loader-probe.json`;
4. leaves `BaseUtils.rdl` unchanged after verifying its identity and method body;
5. rewrites `LWScripts.txt` using the resulting package length and CRC-32;
6. reparses the prepared package before replacement and verifies it again after
   installation.

That path is retained in source only as research history and is not reachable from
the command-line apply mode.

## LENC finding

Read-only extraction of the official `DataCenter/Global/LuaEntry.luac` from the
restored content-version-12 package produced:

- size: `1228` bytes;
- SHA-256: `50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137`;
- first four bytes: ASCII `LENC` (`4c 45 4e 43`);
- first 32 bytes:
  `4c454e4339a8e6d028e94750c40eeb92b24f6696453d12a4194ead8473560dce`.

No plaintext `DataCenter/Global/LuaEntry.lua` was found on disk. This explains why
the old bot technique of placing plaintext Lua bytes under a `.luac` name is no
longer sufficient on the current build.

### Rejected `IsDebug=true` experiment

On 2026-09-05 a first closed-game probe changed `CommonUtils.IsDebug` from false
to true, producing BaseUtils SHA-256
`72b813dd3e36be894b4dafe4f1b58e19e60956ba7b9d1c0b2786e48b1e995798`.
The official launcher accepted the modified script package and started Last War,
but `Player.log` showed that the game then searched for
`DataCenter/Global/LuaEntry.lua` and failed to find the official
`DataCenter/Global/LuaEntry.luac`. Lua startup consequently left the global
`LuaEntry` unset. No loader-probe heartbeat was produced and the pending command
queue remained empty.

The game was closed immediately and the installer backup was restored. Post-
restore verification reproduced the original package length/CRC, 18,686 entries,
the clean BaseUtils hash, and `IsDebug=false`. This proves the old hard-coded
`IsDebug=true` strategy is not portable to the current build. The installer now
refuses that patched BaseUtils state and keeps the method unchanged.

The default invocation remains a read-only dry run:

```powershell
python tools/install_loader_probe.py --json
```

`--apply` is intentionally refused:

```powershell
python tools/install_loader_probe.py --apply --json
```

It still returns an `InstallRefused` result and does not create a backup or modify
a game file. The LWLF-v3 encoder and offline candidate-package path have now been
recovered, but the prepared candidate has not yet been accepted by a bounded
current-game load, so the apply path remains disabled.

Build and verify a separate candidate without touching the installed package:

```powershell
python tools/install_loader_probe.py --prepare-dir "$env:TEMP\lwcontrol-lenc-candidate" --json
```

The preparation path derives the current key/nonce from the hash-gated native
`xlua.dll`, encrypts both the wrapper and probe sources, preserves the official
`LuaEntry.luac`, writes a new LWLF-v3 package in the selected output directory,
then re-reads that file and decrypts the serialized wrapper/probe back to their
exact input bytes. It refuses to overwrite an existing candidate directory's
package files.

The first verified content-version-12 candidate contained 18,688 entries versus
18,686 in the installed archive and had package SHA-256
`c10f132de46ac376d4bae74151c308b94fd2ea0f64aa3cc15afe64c55f95efaa`.
The installed archive, metadata, `BaseUtils.rdl`, and `IsDebug=false` state were
unchanged after this operation.

Restore a recorded backup while the game is closed:

```powershell
python tools/install_loader_probe.py --restore "<backup-directory>" --json
```

The repository now also contains a read-only LENC contract inspector:

```powershell
python tools/inspect_lenc_contract.py --json
```

That older inspector records the managed `BaseUtils.LencCodec` evidence. Subsequent
tracing proved that the installed LWLF file version `3` does not use that managed
path: `LWLuaFile.LoadFile` returns the raw `LENC` entry and native xLua performs
the live transform. The current read-only decoder is:

```powershell
python tools/extract_lenc_v3.py --json
```

It derives the key/nonce from the current hash-gated `xlua.dll`, reproduces the
native eight-round no-feed-forward ChaCha-family transform, observes the expected
`78 DA` zlib stream, and inflates the official entry to Lua bytecode. See
[Assembly-CSharp / xLua LENC runtime recovery](assembly-csharp-lwlua-lenc-runtime.md).
The same module now also provides the symmetric encoder used by the offline
candidate builder. Live installation remains deliberately gated on current-game
acceptance evidence.
