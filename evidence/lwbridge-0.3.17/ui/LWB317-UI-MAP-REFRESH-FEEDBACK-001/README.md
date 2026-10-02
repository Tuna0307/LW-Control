# MAP-REFRESH-FEEDBACK-001 evidence

Read docs/reviews/2026-10-03-LWB317-UI-MAP-REFRESH-FEEDBACK-001.md for recovery, implementation, checks and explicit limits.

- source-locators.json pins EXE/assets/exact UTF-8 anchors.
- check-feedback.mjs executes the actual original component and production callbacks/effects, reads immutable baseline 123459d and records results.json.
- harness.mjs preserves the historical hook adapter with only chronological interval/timeout advancement corrected; original oracle already executes both timer types. Production source is evaluated unchanged.
- check-fixtures.mjs records offline/native fences and rejected actions.
- run-regressions.mjs and replay-lifecycle.mjs redirect current results into this packet while preserving historical outputs. regression-results.json records all exits/results.
- browser-results.json distinguishes seeded success presentation from actual rejected-export callback proof. JPEG captures are preview evidence, not original pixels or successful file export.
- protected-wip.json pins existing AFK, scratch, parent screenshots and normalization-only JSON bytes.
- validate-evidence.mjs verifies result/source/image/WIP/check consistency.

Replay from repository root using node check-feedback.mjs --record and check-fixtures.mjs --record at this packet's full relative path; run-regressions.mjs replays all maintained suites. Use npm.cmd run check/build/check:production-build from src/LWBridge.UI-0.3.17.

No live operation or native provider was added. No full Map/original pixel parity is claimed. Row revision timings match, while bootstrap/options-ack query counts remain a separate ownership gap recorded in results and the review.
