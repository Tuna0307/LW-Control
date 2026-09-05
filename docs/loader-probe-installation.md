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

It returns an `InstallRefused` result explaining that the LENC contract must be
recovered first; it does not create a backup or modify a game file.

Restore a recorded backup while the game is closed:

```powershell
python tools/install_loader_probe.py --restore "<backup-directory>" --json
```

The repository now also contains a read-only LENC contract inspector:

```powershell
python tools/inspect_lenc_contract.py --json
```

It verifies the current LENC entry identity, resolves the encoded key operand
directly to FieldDef RID `1472` (`00..1F`) with the recovered native RG token
transform, confirms the zero 12-byte nonce and ChaCha8 round count, and records
the remaining `ChaCha20.Constants` discrepancy. The next loader milestone is to
recover that runtime constant-supply/patch path, then
decrypt a copy of the official entry and verify the Lua output without changing
the installed package. Daily-free-claim state parsing and claim execution remain
separate work.
