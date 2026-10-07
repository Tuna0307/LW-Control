# OFFLINE-HOST-CONTRACT-001 — independent lead review

Date: 2026-10-07. Reviewed worker `f970b6143cdf5cb3d186738a769c736901a3394d`
and checkpoints `9388e124`, `6e5617a7`, `f970b614`.

Disposition: **ACCEPTED for assigned offline/static/inert scope, with the exact
reader documentation erratum below**. No demonstrated production correction is
required by this review. Source/local UIUX and RECOVERY-003 acceptance remain;
Home/Map feature parity remains PARTIAL. Live pilot remains ON_HOLD_BY_OWNER.

## Independent verification

Fresh lead execution passed:

- Release `--profile-runtime-owner-check`: actual ownership/writer/packaged reader.
- Release `--map317-native-boundary-check`: capture/terminal/committed Stop seams.
- Release `--home-campaign-lifecycle-check`: inert lifecycle/profile/recovery/cleanup.
- Release `--overview-bridge-launch-binding-check`: clone host/listener binding;
  `productionCallLuaEnabled=false`. This is not original-service protocol proof.
- Python Windows ownership tests: 4/4.
- Complete-production-Lua ownership tests: 6/6, existing lupa 2.8/Lua 5.4 environment.
- New direct packaged-reader check: six distinguishing .NET cases; no Connect call,
  no game/xLua runtime and isolated root removed.
- Worker provenance: eight source hashes, four current installed static artifacts,
  reference EXE hash and exact 0.3.17 native evidence identity match. Worker commit
  range changes docs/evidence only, with no product or test-source modification.
- Read-only current runtime and Map compatibility revalidation.

Reader/provenance scripts and fresh artifacts are under the worker evidence root's
`lead-review-2026-10-07/`. Native/Python outcomes are recorded in `checks.json`.
Historical worker negative attempts, initial results and source pins remain intact.
No product changes mean no fresh frontend/package rebuild was necessary here.

## Authoritative reader erratum: LR-HOST-READER-UNITS-001

The worker review's "strict UTF-8 and a 4096-byte ceiling" is imprecise for the
**.NET adapter alone**. Actual `PipeClientAdapter.cs:34-51` uses
`File.ReadAllText(path, new UTF8Encoding(false, true))` followed by
`text.Length <= 4096`. `String.Length` counts UTF-16 code units. The packaged DLL
accepts 2,049 U+00E9 characters (4,098 UTF-8 bytes) and rejects 4,097 ASCII code
units. The `File.ReadAllText` reader also honors an encoding BOM: the packaged DLL
accepts a UTF-16LE-BOM file containing `schema=1`. Invalid BOM-less UTF-8 returns
`unavailable\n`. These are exact observed .NET results, not inferred xLua behavior.

The **Lua parsers** independently enforce `#text <= 4096`, a Lua string byte-length
bound: `current_overview_bridge.lua:413-433` and
`current_live_resource_probe.lua:549-575`. Separate their limits/encoding contracts
in future matrices; do not call both a .NET 4,096-byte UTF-8-only contract. Existing
owned writers and identity gates remain unchanged. This documentation correction
does not authorize redesigning the adapter or changing the bound/encoding policy.
The original worker record is preserved as history; this lead erratum supersedes
the imprecise description in current interpretations.

The lead reader harness initially resolved one parent too high, so no adapter was
loaded. A wrapper subsequently checked stale PowerShell LASTEXITCODE after a
successful script. The corrected path was rerun with `pwsh -File` and explicit
process exit checks; final six-case JSON and cleanup proof are authoritative.

## Evidence boundaries

The real packaged field is `Func<string,string>` and the production Lua consumer
uses reflected `Invoke` for a non-Lua-function value. Static current-client metadata
contains relevant generic delegate/reflection machinery. This does not execute
`FieldInfo.GetValue -> external delegate -> actual current-game xLua -> Invoke`.
That crossing, in-game same-owner scheduling/readiness, and positive Resource/City
acquisition remain UNKNOWN until a future authorized live witness.

The 0.3.17 inspectors establish exact bytes/source identity; branch annotation and
existing 0.3.1 findings do not alone prove every original serializer/traversal/wire
contract. Exact original protected traversal/wire parity remains unclaimed.
Current-native provider policies and adaptation choices must remain distinguishable
from original behavior. No owner desktop/game actions or protected access occurred.

## Next assignment

Owner requested a larger assignment without repeated relay between small units.
Assign `LWB317-FUNCTION-HOME-MAP-DEEP-RECOVERY-002`: a solo, offline campaign
through finite medium milestones covering exact 0.3.17/current-client provenance,
eight-category acquisition/projection, remaining Treasure/Ghost action contracts,
scheduled jobs, Home ownership integration and headless native validation.
Continue implementable branches automatically; no time minimum, no subagents,
GPT Work/Codex delegation, shared-desktop actions or live permission. Preserve all
accepted fixes. Unsupported branches require exact blockers, not invented success.
