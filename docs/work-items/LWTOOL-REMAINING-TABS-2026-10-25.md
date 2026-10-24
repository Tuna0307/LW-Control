# Equipment, City Layout, Hotkeys, Mini Games and Settings

Owner assignment, 2026-10-25: compare these five areas side by side with LwTool,
recover their active methods, implement useful improvements and finish their
qualification autonomously. The owner reports being logged into the sub-account
and permits task app/game operation and bounded feature actions there.

Baseline: main `122d1989`, after merged Home/Map PR #16 and v1.1 release.
Work on `codex/lwtool-remaining-tabs`, with a new draft PR. Do not modify main,
merge, publish a release, discard `.claude/` or overwrite private research.

## Scope

- Equipment: snapshot, four presets, capture/name/save/apply/restore, equipment
  shortcuts, ownership and uncertain-send handling.
- City Layout: snapshot, move plans, draft save/reopen, validation, execution,
  cancellation, partial failure and restoration.
- Hotkeys: Q/W/E/R, recall, item/gem speed-ups and every retained shortcut;
  key-repeat, focus, edit controls, conflicts and exact target admission.
  Owner clarification, 2026-10-25: speed-ups are comparison only; keep them removed.
- Mini Games: Food House, Black Market and chest hint, including current event
  availability. Compare any related retained solver but do not resurrect removed
  Unlock Land Cell or Space-countdown features.
- Settings: actual game FPS/ping display, application language/theme and their
  persistence, reconnect, profile ownership and disabled-feature idle cost.

For every feature record our active path, LwTool's active path, evidence,
availability and disposition: implement, retain, reject or explicitly defer.
Recovered code is not runtime proof; feature gates and dead paths stay visible.
Use existing authenticated recovery in artifacts/lwtool-connection-20261023;
verify its hashes and reuse focused evidence instead of repeating unpacking.
Never bypass licensing or integrate the abandoned remote Map service.

## Live authority and preservation

Before mutations, freshly read the authenticated game UID/server and match an
owner-approved sub-account pair. Server alone cannot authorize actions. Keep
other identities read-only and record private admission locally. The current
assignment supersedes old deferrals for Equipment and available Mini Games.
Preserve the explicit Unlock Land Cell prohibition, no real-money purchases,
guildless-only player targets, no alliance-target attacks, no external messages,
and no anti-cheat interference. Prefer reversible free actions; capture native
before/after state and restore equipment/buildings and display preferences.

One live controller, background/programmatic own-app operation, Chinese game
language, exact process/session ownership, supported Stop and original-script
restoration. Do not act on unrelated windows or bypass a real tool rejection.
Preserve accepted Home/Map, bounded queues, command authority and no-replay rules.

## Finish contract

Create a sanitized comparison matrix and durable progress ledger in
docs/research/lwtool/2026-10-25/remaining-tabs. Trace the current game interfaces
before implementing recovered ideas. Add distinguishing checks for actual
changes, qualify accessible features through the genuine native/package path,
and state locked/event-dependent gaps without fabricating positives. Compare
equivalent workloads before claiming performance superiority.

Deliver pushed coherent commits, a new draft PR, a source-pinned clean test ZIP,
its integrity receipt, integrated regression evidence, known limits and cleanup.
Keep recovered code, credentials and raw account/runtime evidence ignored locally.
Independent acceptance follows implementation/testing; a report alone is not the
completion of this assignment.

## Worker handback, 2026-10-25

Product source `4565147a9adf38ae0e21e535c584c2efaaf4aceb`, draft PR #17.
Read `docs/research/lwtool/2026-10-25/remaining-tabs/FINAL_QUALIFICATION.md`
and its EVIDENCE.json. Equipment, City and Settings passed the final package's
bounded native checks and exact restoration. Hotkey attack/recall lacks a fresh
positive because no staffed squad/three eligible targets were available; Mini
Games lack active event/view/battle state. Their controlled checks and genuine
native availability refusals are retained separately. Speed-ups stay removed.
Independent review and owner acceptance are pending; no merge/release.
