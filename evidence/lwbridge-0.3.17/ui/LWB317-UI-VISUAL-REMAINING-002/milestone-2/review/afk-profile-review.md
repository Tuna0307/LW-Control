# AFK/profile coordinator correction review

Review target: current uncommitted coordinator edits in
`src/LWBridge.UI-0.3.17/src/SquadsPage.jsx` SHA-256
`26A896E92667EE12719F3AD1CB907AADF99D957819771465023D9396333AF544`
and `previewAfkCloseoutFixtures.js` SHA-256
`B1FB835809F847FAC51C1A79A8DDB8584AFACA15B9143DC01CCC7C5393856E68`.
Oracle remains recovered `SquadPanel-HC3-DJei.js` SHA-256
`ED466E353AA896D8C3FC783FF1802930156FB9E9EB54327AEF73F2FF86FA06C7`,
`I` UTF-8 byte 28070 / length 22554 / SHA-256
`0A4895C21AD443AA7A1C05B54CFF2E8DD89F9038BED10DBFFFB9001A4128EE2E`,
plus exact `Ce` and parent `pd` locators frozen in `recovery/afk-profile-worker.md`.

Only concrete remaining fixes are listed below.

1. **Restore the recovered Monster-AFK/Potion store boundary.** Current
   `SquadsPage.jsx:254-255` still keeps profiles in `config` while Master, Potion and
   Drill live together in `toolbar`. Recovered `I` has one `y` store containing
   `{ enabled, strategies, allianceDrill }` and a separate `b` Stamina-Potion store.
   This is still behaviorally different even though the new error labels look
   correct: profile and Master/Drill writes can currently save/fail independently,
   while source serializes them through the same `y` store; conversely an invalid
   Potion draft is currently part of `validAfkToolbarConfig` and can affect the
   Master/Drill store, which source cannot do. Refactor the source-local ownership so
   Master + profiles + Drill share one config/error/Retry/Discard/saving queue and
   Potion owns its own independent config/error queue. Do not solve this only by
   changing `toolbarErrorOwner` labels.

2. **Add the missing Master-disable action-busy (`B="stop"`) lifetime, and start
   reorder busy before mutating the draft.** Recovered `$e(false)` sets `B="stop"`
   before awaiting the stop action and clears it in `finally`; while set, Master,
   Potion, Drill, Add, profile-enable, Delete and profile-drag/reorder are gated.
   Current `SquadsPage.jsx:336` sends both Master enable and disable directly through
   `updateToolbar`, so the recovered stop-busy presentation is still unreachable.
   Model the false transition with a disclosed inert acknowledgement only; no native
   action is required. Also move `setProfileAction("reorder")` ahead of the
   `setProfiles(..., false)` edit at lines 282-291. Source sets `B="reorder"` before
   `Ze` edits/flushes, while current external-store listeners can observe the
   reordered draft before the React busy state is set.

3. **Feed actual/source-valid available squad indexes through the page instead of
   leaving `[1,2,3,4]` as the effective production value.** The corrected
   `addProfile` now uses `availableSquads.slice(0,1)` and Drill uses the same prop,
   but `SquadsPage.jsx:41` calls `AfkContent` without that prop and
   `previewAfkCloseoutFixtures.js` exposes no alternate available-squad set. The page
   therefore still always uses the default `[1,2,3,4]`. Recovered `I` obtains `Ne`
   from discovered squads, recovered `Ce(target, Ne, kind)` uses `Ne.slice(0,1)`,
   and Drill appends only `Ne` entries not already selected. Add a disclosed
   source-local squad-index fixture/provider and pass it into `AfkContent`, including
   at least a non-1-first case such as `[3,4]` and an empty-squad case. This is needed
   to make the new-profile and Drill fixes executable through the real Squads page.

4. **Finish recovered runtime-name/error translation.** Source builds `Ae` from
   each worker's `joinTargetNameKey` and renders
   `Ae[key] || joinTargetName || ""` for both profile runtime rows and Drill waiting
   detail. Current `AllianceDrillPreviewSettings` line 117 and profile runtime line
   382 use only `joinTargetName`; `previewAfkProfileRuntime` supplies no name-key
   branch. Add a disclosed translated-name mapping/name-key fixture and prefer it as
   source does. Profile runtime errors also still use direct `t(row.lastError)` at
   line 384, whereas recovered `I` calls its imported error translator `_` before
   rendering. Use the recovered error-translation contract so source-valid error
   codes/messages do not depend on already being locale keys.

5. **Match the recovered shared composition-error (`Ie`) lifetime.** Current
   `compositionError` is initialized from `previewState` once at line 265 and its
   clear sites do not match recovered `V`:
   - recovered target discovery sets the error on failure and clears it on a later
     successful discovery; current has no corresponding update after initialization;
   - recovered successful profile selection `st` and successful profile enable `W`
     clear `Ie`; current profile-select (line 373) and profile-enable (line 369) do
     not clear `compositionError`;
   - recovered successful Drill toggle `nt/tt` does **not** clear an existing `Ie`,
     while current line 338 calls `setCompositionError("")` before every successful
     Drill toggle.
   Keep the now-correct zero-squad enable-attempt placement, but make the error
   producer/clear lifetime match these exact source transitions instead of treating
   it as a generic panel error.

No additional production fix was found in this review for toolbar-settings ancestry,
editor sibling ancestry, profile runtime-row placement/order, Add outside-pointer
dismissal, profile drag-over/DataTransfer/glyph behavior, Potion edit debounce,
Drill Join debounce, or the `pd` visited-tab React `Activity` retention boundary.
