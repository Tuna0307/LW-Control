# R8-044 — restore game_asset_image public contract

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** restore the native read-only game asset image request/validation/result contract and native-style persistent PNG caching without changing map acquisition or other game actions.

## Authority

Primary native evidence:

- `game_asset_image` handler `0x14017A65C-0x14017C064`;
- JSON string-field helper `0x1401D2EE7`;
- whitespace/source normalizer `0x1400385A4`;
- exactly-one-source validator `0x1403BBE95-0x1403BC1FD`;
- game-disconnected helper `0x1403AD367-0x1403AD3B8`;
- game request setup around `0x14017B331` (`getAssetImage`, timeout `0x3A98` = 15,000 ms);
- PNG/data-URL result path around `0x14017B3F6-0x14017B5C9`;
- cache maintenance cadence around `0x14017BF4E-0x14017BF76` (`0xEA60` = 60,000 ms);
- cache helper `0x1403B9690` and SHA-256 hex helper `0x1401DE702`;
- retained frontend `GameAssetImage-DEJtwPJL.js` for public `dataUrl` consumption.

This checkpoint stays outside the protected package-key RVA lane and does not perform a game mutation.

## Public request admission

Native reads `assetPath` and `spriteName` as optional JSON strings. Missing fields, null values and non-string JSON values are treated as absent. String values are trimmed before admission.

Exactly one trimmed source must remain:

- asset path only -> source kind `assetPath`;
- sprite name only -> source kind `spriteName`;
- neither or both -> `INVALID_REQUEST` / `exactly one image source is required`.

The old rebuild used `INVALID_ASSET`, rejected non-string fields immediately, and added a 1,024-character/newline/NUL rule that is not present in the recovered native admission path. R8-044 removes those observable deviations.
## Cache / game-call ordering

Native resolves `<runtimeRoot>\asset-cache` and attempts the persistent PNG cache before requiring a live game route. A valid cached PNG can therefore satisfy `game_asset_image` without a current game connection.

On a cache miss, the handler requires the game route. Missing connectivity returns exact `GAME_DISCONNECTED` / `game disconnected`.

The game-side method is `getAssetImage` with an exact 15,000 ms timeout. The native handler issues one game request; it does not contain the rebuild's previous three-attempt/150-ms retry loop.

R8-044 removes that retry loop. The retained current-client probe remains equivalent transport plumbing for reaching the game-side operation; it is not claimed byte-identical to the original named-pipe implementation.

## PNG/result contract

Native accepts a returned byte vector only when its first eight bytes equal the PNG signature `89 50 4E 47 0D 0A 1A 0A`. Failure is exact `INVALID_ASSET` / `invalid PNG asset`.

The old rebuild additionally required an IHDR chunk, matching width/height metadata and dimensions <= 4096. Those checks are not part of the recovered public native contract and are removed as admission gates.

Success returns exactly one public field:

`{ "dataUrl": "data:image/png;base64,<PNG bytes>" }`

Width/height remain internal rebuild diagnostics only and are opportunistically derived when an IHDR is present. They are not exposed by the public command.
## Persistent cache behavior

Native cache structure recovered from the handler/helper:

- cache directory name: `asset-cache` under the selected runtime root;
- cache files: `.png`;
- filename uses a SHA-256 digest rendered as all 32 digest bytes -> 64 lowercase hexadecimal characters;
- cache maintenance is gated to at most once every 60,000 ms;
- cleanup considers PNG entries and begins eviction once aggregate cache size exceeds the 256 MiB boundary.

The exact byte preimage fed into native SHA-256 is not yet byte-closed. R8-044 therefore uses a collision-safe canonical source identity (`sourceMode + ':' + sourceValue`) as **EQUIVALENT_REIMPLEMENTATION** for the cache filename while preserving the observable disk-reuse behavior. The rebuild also uses best-effort atomic file replacement and oldest-file eviction; those I/O details are equivalent plumbing, not claimed exact native bytes/order.

Production cache root is the retained selected-profile runtime directory (`%LOCALAPPDATA%\LWBridgeRebuild\profiles\<profileId>\asset-cache`), matching the rebuild's existing per-profile runtime ownership used by `map-data.db` and runtime config.

## Deterministic coverage

R8-044 checks prove:

- non-string image fields are treated as absent;
- selected string source is trimmed;
- neither/both sources fail with exact `INVALID_REQUEST` message;
- public success contains only `dataUrl`;
- disconnected acquisition maps to exact `GAME_DISCONNECTED` and performs one request only;
- malformed PNG signature fails with exact `INVALID_ASSET` message;
- a successful PNG is persisted and reused by a second source instance without another game request;
- the unrelated R8-040 proxy-status deterministic test no longer samples a real running game process; production proxy behavior is unchanged.

## Remaining boundary

The native cache SHA-256 preimage is still `UNKNOWN`. Live current-client transport remains an equivalent adapter rather than the original transport implementation. Exact native timeout-error wording after a connected 15-second `getAssetImage` timeout is not separately claimed by this checkpoint.