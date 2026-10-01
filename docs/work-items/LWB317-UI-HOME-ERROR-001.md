# LWB317-UI-HOME-ERROR-001 — Home error translation only

Owner: project lead. State: COMPLETE / ACCEPTED for focused source/local scope
after independent worker review and PM-017 integration on 2026-10-02. Assigned after owner said
continue while the other AI is not ready. Queue entry: HOME-ERROR-001.
Baseline: 80e7a752d22d1d6d474dc56fe0b827a68ae062e6.
Implementation/source differential/render/browser/canonical checks completed.
Delivery: docs/reviews/2026-10-02-LWB317-UI-HOME-ERROR-001.md and focused evidence.
Independent review baf5473 is integrated by PM-017. Error channels/busy remain
AWAITING_REVIEW. Acceptance: docs/reviews/2026-10-02-LWB317-PM-017-home-translation-acceptance.md.

Goal/scope: match actual Home translatedError helper to original Ir/Lr token
extraction, reversed unique-code priority, error/auth.error/update.error lookup and
localized generic message. Add explicit browser-only Home QA states as needed and
source differential/helper/render/browser evidence. Preserve error visibility,
Home state channels, callback/ack/profile scoping and native lifecycle fences.

Inputs: AGENTS.md, AI_WORK_PROTOCOL, task.md, Home queue, PM-015 audit;
index-BVfnK1wp.js SHA-256 44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6,
Ir byte 328453, Lr byte 328684, qr byte 336694. Target exe SHA-256
4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.

Non-goals: error-channel refactor, Home busy/localized-switch correction, native
providers/game launch/control, original auth bypass/UI, other feature families,
legacy fallback or subagents. Preserve unrelated AFK/scratch/parent screenshots.

Outputs: dated review and evidence/lwbridge-0.3.17/ui/LWB317-UI-HOME-ERROR-001/;
actual helper and render comparisons, reference hashes/byte locators, browser
observations/screenshot hashes, current conservative UI/status/handoff updates.

Acceptance: string/object/Error/empty/unknown/embedded/duplicate/namespace cases,
nine locale checks, Home error/recovery-detail render checks and representative
local browser QA. Home integration, canonical check/build/package, evidence and
git diff --check must pass. Commit/push explicit owned paths and verify remote;
return AWAITING_REVIEW for independent review. Stop at this unit, no fixed timeout.
