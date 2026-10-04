# PROFILE-TABS project-lead acceptance — 2026-10-04

Decision: **COMPLETE / ACCEPTED for bounded source/local UI scope**. The
FINAL-INTEGRATION campaign and strict global UI parity remain PARTIAL.

Before this correction, changing the local profile reset Trade, Train and
Equipment to Daily, City and AFK. The original App owns those three selections
outside its profile-keyed child view. Current App now supplies separate
route-specific controlled selections while retaining the child remount/reset.
Single-profile pages retain their recovered local selection behavior.

Automation also used seven eagerly mounted CSS-hidden category grids. The exact
original has one grid with eight visited React Activity groups: resourceGather,
Trade, System, Chat, Resources, Daily-basic, Alliance, Daily-dispatch. Current
code preserves that order, mounts visited groups, suspends hidden effects and
retains local card state. Daily remains split around Alliance as in the source.

## Evidence and review

Reference EXE SHA-256 is `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Exact asset identities and slices are recorded in
`evidence/lwbridge-0.3.17/ui/LWB317-UI-FINAL-INTEGRATION-001/profiles/parent-tab-audit/`.
Main parent states: bytes363161/363192/363223; profile key372764; child bindings
373777/374455/374732. Automation default/effective/visited25370/25402/25549,
click42780 and grid42910. Squad local/visited/effective191517/191541/191602,
click192276. Map effective30426 and changeTab39238. Locators are UTF-8 byte offsets.

Independent review verified 77 exact source slices and full asset hashes,
reran the contract and mounted proofs, and found no additional product defect.
The reviewer confirms unchanged EquipmentContent, including motion/modal, and
preserved Map cache/generation/notification/reset ordering. I accept that finding
because it agrees with the actual diff and the distinguishing mounted tests;
no additional production change was made because of the review.

## Verification

- Current contract:22 cases, exact eight boundaries and unchanged leaf bodies.
- Actual mounted App/panels:five grouped proofs. Immutable307b13c reproduces
  three lost selections; current has zero across A→B→A and independent choices.
  Keyed child DOM/Map keyword still reset. Actual store subscriptions demonstrate
  hidden Daily effects stop, Trade alone subscribes, and Daily DOM/settings survive.
- Real offline browser:EN/light and JA/dark retain Trade/Train/Equipment across
  profile changes; Training settings survive Daily→Trade→Daily. Three settled
  JPEGs inspected. JA viewport740×600 CSS/client730; zero captured warnings/errors.
- Current Map regressions:ten suites PASS. Navigation baseline6/current0 across
  17 requests; request lifetime38/current0; interaction38/38; Scheduled10482 renders
  and51/51 mutations. Other suites cover header, Goods, Treasure, feedback, redirects
  and App/panel refresh ownership.
- Equipment105, Equipment R1 61 and AFK729 PASS; actual lazy mounted checks and
  six dynamic chunk/startup graph checks PASS. Current Map-entry/Cross-server/
  ownership and historical-preservation checks PASS.
- Canonical check/build/production-package, diff checks and protected-WIP7/7 PASS.

Three historical harness shape failures are preserved in v3. Separate adapters
resolve the renamed local tab binding, bind Goods to actual GameAssetImage with
an absent reader, and preserve Scheduled's historical normalized image markup
on both original/current sides. Behavioral assertions remain intact. Scheduled's
normalized image proof is not loaded-image/pixel proof; the dedicated image
component and caller packet supplies that separate source/local evidence.

Package fingerprints:
`3b893b91202017e5599f162e9ff122f8171f5045e393bd7a7b8b4543b391d976` /
`1b0627e7f6ca47edd72548eace2b33e873ef51930c3a1cdfa79e006cadd8522e`.

## Remaining boundary

Local profile capacity intentionally replaces the excluded commercial entitlement
gate. Inert transport/offline fixtures do not establish native profile/cache/config
producers, persistence, actual game actions/assets, physical HTML5 drag, or original
protected-runtime pixel equivalence. Seven protected paths remain untouched. Two
untracked vendor LICENSE/NOTICE documents were not authored by this checkpoint
and are excluded from it. Next lead work is a current finite inventory and offline
source-rendered visual differential, starting with Home/Map; no native phase starts.
