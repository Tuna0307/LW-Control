# Assembly-CSharp / xLua LENC runtime recovery

This note records the read-only recovery of the current LWLF version-3 Lua-entry
loader. It corrects an earlier assumption that `BaseUtils.LencCodec` or the
managed `EncryptUtils.SuperDecrypt` path handled the installed `LENC` entry.
No installed game file or script package was changed during this milestone.

## Current build identities

Read-only inspection on 2026-09-06 used these current files:

| File | SHA-256 |
| --- | --- |
| `Assembly-CSharp.rdl` | `871efe06819fbac438413eb96b7df8193d0be56094f3a44d5ff141e6219adcbd` |
| `XLuaRuntime.rdl` | `5d1aa6536a4feec7467225a119ea2f0b7451e9d0b51220d902e5f0d8bbc081b2` |
| `xlua.dll` | `21eb704afdb7e528f4b90fa1b90bf414c221b06ba990d625aaaaed31b292740f` |
| `BaseUtils.rdl` | `b20865f42c9272e0fca2b6deb2e9142576b12dcf42f11ad1a8816ddd59b952d6` |

The installed `LWScripts.data` is LWLF file version `3`, content version `12`.
Its `DataCenter/Global/LuaEntry.luac` entry is 1,228 bytes, begins with ASCII
`LENC`, and has SHA-256
`50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137`.

## Managed archive-version split

`LWLuaFile.LoadFile` is MethodDef RID 5155 (`0x06001423`, RVA `0xA0A90`) in
the current `Assembly-CSharp.rdl`. Its managed `GameKit.Base.EncryptUtils.SuperDecrypt`
call is real, but it is conditional on `get_fileVersion() == EncryptedFileVersion`,
where `EncryptedFileVersion` is `2`.

The installed archive is file version `3`. On that path `LoadFile` returns the
entry bytes unchanged. `XLuaManager.CustomLoaderImpl` (MethodDef RID 17429,
token `0x06004415`) calls `LWLuaFile.LoadFile` and returns that byte array directly
to xLua. There is no managed version-3 decrypt after `LoadFile`.

This separates two contracts that were previously mixed together:

- archive version 2 uses the managed `EncryptUtils.SuperDecrypt` path;
- installed archive version 3 hands the raw `LENC` bytes to the native xLua
  loader.

The version-2 path remains useful historical evidence, but it is not the live
decoder for the installed package.

## Managed/native xLua boundary

In `XLuaRuntime.rdl`, `XLua.LuaDLL.Lua.luaL_loadbuffer` is MethodDef RID 138
(`0x0600008A`, RVA `0x29B0`). It calls P/Invoke MethodDef RID 137
(`0x06000089`). ImplMap RID 55 maps that method to module `xlua`, import name
`x348237B0`.

The current `xlua.dll` exports `x348237B0` at ordinal 71, RVA `0x25DA0`. The
export is a thin wrapper around the native load-buffer core at RVA `0x35B8`.

The DLL contains exactly one ASCII `LENC` constant at RVA `0x7F138` and exactly
one executable RIP-relative reference to it, at RVA `0x15EB`. The surrounding
loader flow is:

1. ensure at least four bytes are buffered;
2. compare the first four bytes with `LENC`;
3. set payload length to total length minus four;
4. derive a 32-byte key and 12-byte nonce with helper RVA `0x27918`;
5. transform the bytes after `LENC` with helper RVA `0x27564`;
6. zero the temporary key/nonce buffers with helper RVA `0x279BC`;
7. if the transformed payload begins `78 DA`, inflate it with zlib;
8. continue into the normal Lua parser.

## Exact version-3 transform

RVA `0x27564` contains the standard ChaCha state constant
`"expand 32-byte k"`, the standard ChaCha quarter-round rotate counts
`16, 12, 8, 7`, and four complete column-plus-diagonal double rounds. The stream
therefore uses eight ChaCha rounds.

There is one non-standard detail that matters: the native helper does **not** add
the original state words back to the working state after the rounds. It XORs the
post-round working state directly into the payload. A normal ChaCha8 block
function therefore cannot decode this format even with the correct key and nonce.

The state layout is otherwise the familiar IETF form:

`constant[4] | key[8] | counter[1] | nonce[3]`

The counter starts at zero and increments once per 64-byte payload block.

## Native key and nonce derivation

Helper RVA `0x27918` generates 44 bytes. For each index `0..43`, it calls two
byte-selector functions (RVAs `0x27A78` and `0x27B24`) and XORs their returned
bytes. The first 32 result bytes are the key and the final 12 are the nonce.

The two recovered selector tables are:

`bfc69b79c50ce3a34a0724392e62928f2837cd899bfce35b11cbcbaecb6beafe2ccf031ad27d7ff09f8fb111`

and:

`56d02627c4091c751b4b82e93f1571120edd07ffb66058d28aa1d7716f9fd8abaf912230d1384fc927b1534b`

Their XOR gives:

- key: `e916bd5e0105ffd6514ca6d01177e39d26eaca762d9cbb899b6a1cdfa4f43255`;
- nonce: `835e212a03453039b83ee25a`.

The selector tables and leaf functions are hash-gated in the new read-only tool,
so build drift fails closed instead of silently reusing these values.

## Decoding proof on the installed Lua entry

The first 64 encrypted payload bytes after `LENC` are:

`39a8e6d028e94750c40eeb92b24f6696453d12a4194ead8473560dcec610d7f3a9f59bc6f12b7719c3b62b9b1d18e22855de2aed522a48f9466e89f2806782e4`

Applying the recovered native transform produces:

`78da8556dd4e1b47149ef18eb15903a1ada2368902558aa2364a4da5b68a9256ea6e08b85486fe9090aab2b45adb03322cbb66775db0daa6639c402e72d737a8`

The `78 DA` header matches the native loader's inflate branch. zlib decompression
then succeeds and produces 2,873 bytes with SHA-256
`f3893d273e5560e2085eca8354d4133058401a26619c132cb8c286f72e795740`.
The output begins:

`1b4c7561530119930d0a1a0a0404080878560000000000000000000000287740`

That is the Lua bytecode signature (`1B 4C 75 61`) followed by version byte
`0x53`, providing an independent structural check that the live transform is
correct.

`tools/extract_lenc_v3.py` reproduces this proof without writing to the game or
the package:

```powershell
python tools/extract_lenc_v3.py --json
```

It reads the selected archive entry into memory, derives the current key/nonce
from the hash-gated `xlua.dll`, reproduces the native transform, inflates `78 DA`
payloads, and reports hashes and prefixes only.

## Producer-side encoding recovered

The transform at RVA `0x27564` is an XOR stream transform, so the same recovered
block function is used in both directions. `tools/extract_lenc_v3.py` now exposes
`encode_lenc_bytes()`: it zlib-compresses source bytes at level 9, verifies the
compressed representation starts with `78 DA`, applies the native transform, and
prefixes `LENC`.

Exact reproduction of the official compressed byte stream is **not** a loader
requirement. The native loader checks only for the `78 DA` header after the
transform and then invokes its inflate path. Python zlib level 9 satisfies that
header contract. Synthetic plaintext Lua and binary-Lua fixtures both round-trip
through `encode -> native transform inverse -> inflate` in the offline tests.

The exported native load-buffer wrapper at RVA `0x25DA0` also clears the mode
argument before entering the normal Lua load core. That path therefore does not
force binary-only mode after the LENC/inflate stage. This is static evidence that
an encrypted plaintext Lua wrapper is compatible with the loader's parser mode;
current-game acceptance of the prepared wrapper remains a separate live proof.

`tools/install_loader_probe.py --prepare-dir <directory>` now uses that encoder to
build a completely separate LWLF-v3 candidate package. It leaves the installed
archive untouched, preserves the exact official `LuaEntry.luac` as
`LuaEntry_original.luac`, adds an encrypted wrapper and encrypted probe entry,
re-reads the serialized package, and decodes both new entries back to their exact
source bytes.

A 2026-09-06 offline preparation against content version 12 produced:

- input entry count: `18,686`;
- candidate entry count: `18,688`;
- candidate package length: `41,177,738` bytes;
- candidate SHA-256:
  `c10f132de46ac376d4bae74151c308b94fd2ea0f64aa3cc15afe64c55f95efaa`;
- encrypted wrapper SHA-256:
  `f1d714c64dc572f038757172f1faea3245e4108aedff8aa140bfe92ba004ea3a`;
- encrypted probe SHA-256:
  `0dd8d216cd94863530233ff9dd2d2c2c57c6573b74f6969b537d88da8497f5`;
- preserved official entry SHA-256:
  `50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137`.

The installed package was re-checked after preparation and remained at 18,686
entries with the original package CRC, clean `BaseUtils.rdl` hash, and
`IsDebug=false`.

## PROVEN / UNKNOWN boundary

| Status | Finding |
| --- | --- |
| PROVEN | LWLF v3 bypasses the managed version-2 `SuperDecrypt` branch. |
| PROVEN | Raw `LENC` bytes are handed from `CustomLoaderImpl` to native xLua. |
| PROVEN | Native `xlua.dll` recognizes `LENC` at the sole code reference. |
| PROVEN | RVA `0x27564` is an eight-round ChaCha-family core with no feed-forward. |
| PROVEN | Current-build key and nonce derivation and exact values above. |
| PROVEN | Official `LuaEntry.luac` decrypts to `78 DA`, inflates, and yields Lua bytecode. |
| PROVEN | The XOR transform is symmetric and level-9 zlib produces the native loader's required `78 DA` input. |
| PROVEN | A separate LWLF-v3 candidate containing encrypted wrapper/probe entries serializes and decodes back to the exact source bytes. |
| UNKNOWN | Whether the current game will accept and execute that prepared candidate package; no live installation was performed in this pass. |
| UNKNOWN | Whether every `LENC` entry uses this same contract across future builds; hashes and tables must be revalidated after updates. |

Python zlib does not reproduce the official compressed stream byte-for-byte, but
that distinction is now classified as producer implementation detail rather than
a loader blocker. The remaining gate is a bounded current-game load of the
already prepared and offline-verified candidate.
