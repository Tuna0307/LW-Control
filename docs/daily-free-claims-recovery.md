# Daily Free Claims recovery notes

## What was analyzed

This pass continued static analysis of the supplied `LWControl.exe`. The executable
was not launched. The game was not opened or controlled, and no command was queued
to the Last War bridge.

The supplied executable is a .NET single-file bundle. Direct ILSpy decompilation of
the outer PE fails because the native host has no managed metadata. Parsing the .NET
bundle header recovered a version 6.0 bundle with 269 entries. The bundle header is
at byte 102,861,360 and the bundle ID is `HYOrSR4iwOur`.

The application-specific managed payloads include:

| Assembly | Uncompressed bytes |
| --- | ---: |
| `LastWarControl.Auth.dll` | 110,592 |
| `LastWarControl.Bridge.dll` | 24,576 |
| `LastWarControl.Core.dll` | 380,928 |
| `LastWarControl.Game.dll` | 4,894,720 |
| `LWControl.dll` | 43,364,352 |
| `LWControl.r2r.dll` | 72,130,560 |

Temporary extraction and decompilation copies were kept outside this repository.
The original binaries and recovered source are not committed here.
Exact source and extracted-artifact hashes are recorded in
[`daily-free-claims-evidence.json`](daily-free-claims-evidence.json). ILSpy
11.0.0.9375 was installed only into a temporary analysis directory for this pass;
no pre-existing ILSpy, dnSpy, or dotPeek command-line installation was found in
the common user/PATH locations checked.

## Recovered daily-claim architecture

`LastWarControl.Core.dll` contains a complete Daily Free Claims feature family:
options and validation, a planner, source ownership, an adapter registry, seven
source-specific adapters, eligibility checking, execution models, result
verification, and recovery policy.

The seven adapters and their recovered priorities are:

| Adapter ID | Priority | Resource/page locks |
| --- | ---: | --- |
| `daily_task_chest` | 900 | home + daily task page |
| `weekly_task_chest` | 890 | home + weekly task page |
| `vip_daily_reward` | 700 | home + VIP page |
| `store_daily_free_pack` | 690 | home + store page |
| `login_reward` | 650 | home + login event page |
| `tavern_free_recruit` | 600 | home + tavern page |
| `campaign_idle_reward` | 500 | home + campaign event page |

These seven sources are owned by `daily_free_claims`. Mail attachments, alliance
gifts, camp-armored rewards, and general world rewards are explicitly owned by
other feature modules, preventing two automation features from claiming the same
source.

## Recovered configuration contract

The original configuration is disabled by default, while all seven categories are
enabled inside the feature configuration. The maximum claims per run defaults to
20 and is constrained to 1 through 20.

The v1 contract is deliberately free-only. Premium currency, tickets, and
advertisement claims are all forced off. The desktop host serializes the feature
as a `run_once` command by default and explicitly sets `background=false`.

The embedded game module also rejects `start` mode; background Daily Free Claims
is not supported by this build. It accepts `state` and `probe` as read-only modes,
`stop` as a control operation, and `run_once` for execution.

## Runtime eligibility and ordering

The embedded `LWC2DailyFreeClaims` runtime is the closest recovered evidence to
the code that actually drives the game, so the new clean-room preview policy uses
its behavior as the parity target.

A source must be in the `Claimable` state. Its numeric currency cost must be zero
and the runtime must not mark it as having a currency cost. It is then considered
free when either a positive remaining-free-claims count is present, or the source
is explicitly marked free with a button semantic of `free` or `claim`.

The runtime candidate order is:

1. Task chests first when task-chest preference is enabled.
2. Adapter priority, descending.
3. Adapter ID, ordinal.
4. Source key, ordinal.

The managed C# planner in the reference application contains an additional expiry
ordering rule. The desktop host also forwards `prefer_expiring_rewards` to the
game. However, the embedded Daily Free Claims Lua planner in this supplied build
does not read expiry while sorting candidates. The two layers therefore disagree.
For runtime parity, this reconstruction currently follows the embedded runtime and
records the managed expiry option without using it to order preview candidates.

The local reconstruction still performs its own imported-observation identity,
freshness, duplicate, and expiry checks. Those are local fail-closed safeguards for
untrusted JSON observations, not claims that the original runtime uses the same
checks at that layer.

## Recovered execution and verification contract

The recovered runtime does not blindly click a list of rewards. It executes one
`claim_once` step, associates it with a claim-attempt ID, then re-reads state before
continuing. A live recreation should therefore re-detect after each claim instead
of executing a stale multi-item preview as a batch.

The recovered verification path correlates command ID, feature ID, adapter ID,
source key, reward ID, claim-attempt ID, capture ID, capture time, and server time.
It only accepts a claim effect when fresh post-state proves at least one matching
transition, such as a Claimable-to-AlreadyClaimed state change, a decrease in free
claim count, disappearance from the refreshed list, or a matching server reward
receipt.

The managed verifier additionally requires high-confidence authoritative evidence
from the server state manager or server reward receipt. A positive action return by
itself is not treated as successful completion.

The runtime recovery policy retries a page/route problem at most once. Paid/free
condition failures are blocked. Correlation, replay, stale-state, or other
unconfirmed outcomes become Unknown State instead of being retried as a new claim.

## Recovered local bridge transport

`LastWarControl.Bridge.dll` shows a file-command transport rooted at
`%LOCALAPPDATA%\LastWarControl` with these subdirectories:

- `commands\pending`
- `commands\processing`
- `commands\results`
- `commands\ledger`
- `commands\cancelled`
- `runtime`

Command envelopes use schema version 1 and contain command ID, feature ID,
request time, and string arguments. The pending queue has 64 named slots. The host
writes a temporary file and atomically moves it into a free slot. Command IDs are
single-use across pending, processing, result, cancelled, and ledger state.

Bridge health also checks for a running `LastWar` process and a fresh
`runtime\lua-heartbeat.json`. The recovered host uses a 15-second heartbeat
freshness window with a five-second future-clock tolerance.

This is enough to describe the transport format and build an offline adapter, but
it is not evidence that the supplied game-side bridge is currently installed,
compatible with the user's Last War build, or safe to invoke. No queue files were
created during this analysis.

## Current-machine bridge compatibility observation

A read-only inspection on 2026-09-05 found Last War running and an existing
`%LOCALAPPDATA%\LastWarControl` tree. The stored heartbeat identifies Daily Free
Claims as `lwc2-daily-free-claims-20`, exactly matching the embedded module recovered
from the supplied executable. However, the heartbeat file was last written on
2026-09-02 and predates the currently running Last War process, so it fails the
recovered 15-second heartbeat/correlation gate and must be treated as stale.

The current `LWScripts.data` is a valid LWLF container at content version 12 with
18,686 entries. Read-only parsing found the official `DataCenter/Global/LuaEntry.luac`
but did not find `LWC2DailyFreeClaims` or the bridge's preserved
`LuaEntry_original` entry. The current script container is therefore no longer the
previously injected bridge container.

The current `BaseUtils.rdl` has now been resolved further without modifying it. Its
`RGMD` metadata identifies `CommonUtils.IsDebug` as MethodDef RID 133 at RVA
`0x4BD0`, which maps to file offset `0x2DD0`. The body begins `08 16 2A`, matching
the supplied installer's accepted constant-Boolean method contract and currently
returning false. The previous hard-coded RVA `0x333C` / file offset `0x153C` simply
points elsewhere in the updated file. See [BaseUtils.rdl loader recovery](baseutils-rdl-recovery.md)
for the metadata evidence and read-only inspector.

The reconstruction does not attempt a game-file mutation from this evidence. The
C# app now includes a read-only bridge inspector that checks game
presence, heartbeat age, process-start correlation, pending queue count, and Daily
Free Claims version without creating a command file.

## Reconstruction changes from this pass

The C# preview planner now mirrors the recovered runtime in the areas proven above:

- default run limit is 20 and validation rejects larger values;
- all seven categories are selected by default while the master feature remains off;
- imported observations model the recovered source status and free-condition fields;
- runtime free eligibility uses Claimable status, zero-cost evidence, remaining-free
  count or the exact runtime free semantics;
- fixed adapter priorities and runtime ordering replace the earlier invented expiry
  ordering;
- preview output is labeled `preview-only/recovered-runtime-policy`;
- a read-only local bridge inspector reproduces the recovered 15-second heartbeat,
  five-second future tolerance, and process-start correlation checks.

The application still sends zero game actions. Live work should begin with a
read-only `state`/`probe` adapter and current bridge-heartbeat compatibility check,
then add one bounded claim only after the read/result correlation path is verified
against the current game installation.
