# Remaining tabs qualification

Product source: `4565147a9adf38ae0e21e535c584c2efaaf4aceb`, branch
`codex/lwtool-remaining-tabs`, draft PR #17. Ready for independent review for the
bounded results below. No main merge, release or whole-product superiority claim.

## Changes and comparison

The [comparison matrix](COMPARISON.md) covers Equipment, City Layout, Hotkeys,
Mini Games and Settings against the freshly recovered authenticated LwTool
client. LwTool's separate game engine remains unrecovered; host RPCs are not
treated as proof of native final state or performance.

Useful improvements implemented independently:

- Equipment preset Rename changes only the name under the native config lock,
  with a saved-preset identity check. It neither recaptures nor equips gear.
- Hotkeys/Mini Games queued edits retain the original profile generation.
- FPS/ping changes capture their profile before the first async yield; a retired
  generation cannot apply to, save into or roll back its successor.
- Equipment read-only snapshots tolerate a bounded freshness gap with exact
  owner/account checks. Mutation admission and no-replay remain unchanged.
- Equipment confirmation tolerates failed observations by querying only its
  original operation. Confirmed changes with a failed subsequent read display
  refresh-pending feedback instead of a false rejection.

City Layout and the existing Mini Game engines were retained. No matching City
Layout, Food House or chest-hint flow was found in the recovered LwTool UI/API.
That is a scoped source finding, not proof that no other LwTool module has one.
March speed-ups remain removed by the owner's explicit decision. Existing keys
remain unchanged; Space countdown and Unlock Land Cell stay removed.

## Final package native results

The isolated final-package run freshly verified the approved sub-account before
actions. Raw identifiers, native rows and screenshots remain ignored locally.

| Area | Fresh result | Limit |
|---|---|---|
| Equipment | Native attach, drag between heroes/back, four preset captures/applications, name-only Rename and exact restoration passed | Not a LwTool live timing comparison |
| City Layout | Single move, buffered swap, real in-flight cancellation and restoration passed; all 111 original building anchors matched | No new City engine algorithm was introduced |
| Settings | Four production toggle clicks, native FPS/ping confirmation, English/Chinese UI at 900/1600px, theme/sidebar persistence and original live/persisted settings restoration passed | Native game language remained Chinese; nine catalogs have controlled checks |
| Hotkeys | Native target/squad eligibility was observed; no attack or recall packet sent | Fewer than three eligible low-level monsters in the bounded area and no staffed attack squad; this is not a live attack/recall positive |
| Mini Games | Native Food House, chest and frontline availability checks returned explicit unavailable results | No active Food House event, chest view or compatible frontline battle; no solver/gameplay positive claimed |

The Hotkey/Mini availability cases completed their diagnostic workflow; their
outer harness label `pass` must not be read as a positive gameplay result.
Current Hotkey recognizer, focus/repeat/owner fences and Mini engines retain
their source/controlled regressions and earlier qualified evidence, with these
fresh availability limits kept separate.

Final aggregate comparisons matched every initial equipment slot and all 25
inventory wearers, plus the complete original City layout. Normal owned Stop
proved game exit and restored all three original script files, with no recovery
journal. Prior unsuccessful candidates and their exact restoration are retained
in [EVIDENCE.json](EVIDENCE.json); they are not erased by this successful run.

## Checks and delivery

Full UI checks and the Desktop delivery suite passed, including the new
generation races, atomic rename inverses, eight snapshot-authority cases,
query-only transient/persistent refusal and confirmed-refresh feedback cases.
Home/Map controlled regression suites passed within those checks; their accepted
production behavior is preserved. This was not a fresh all-ten Map stress run.
All 31 package-auditor tests passed. A second exact-account read-only session
confirmed shield inventory and both relocation preflights without dispatch;
all four unsaved equipment ordinals correctly refused in its fresh profile.
It also closed normally with exact original-script restoration. Final process
inventory found no LastWar, launcher, LWBridge or LwTool process.

ZIP: `LW-Control-lwtool-improvement-4565147a9adf.zip`, 10,379,480 bytes,
SHA-256 `eff0de40a1af994173f3a4608859ac4c9551ca0a6bf26b6e1c17d739118aaa2d`.
Its 105 entries, all payload hashes/CRC, 29 bridge files, privacy boundary and
exact extraction were verified. Product input fingerprint:
`73383f0bfe7220c7fe7e0be9230819260698703f962bc5893bd82d63a12e6a17`
(742 inputs). Documentation-only handback commits do not change this package.

Private evidence root: `artifacts/lwtool-remaining-tabs-20261025/`.
The package is under `package-final3/`; native final receipts are indexed by
hash in EVIDENCE.json. No competitor code, credentials or runtime data ships.

Independent review and owner acceptance remain separate. Live attack/recall and
event-dependent Mini Game positives require eligible game state. No measured
cross-product speed, CPU or memory winner is established for these five tabs.
