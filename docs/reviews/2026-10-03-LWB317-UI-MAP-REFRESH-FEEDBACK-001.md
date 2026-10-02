# LWB317-UI-MAP-REFRESH-FEEDBACK-001 — 2026-10-03

Status: COMPLETE implementation for the assigned source/local UI scope; independent returning-worker review follow-up. Project lead owns this checkpoint. Full Map, original pixels and native function parity are not accepted.

## Problem and resulting behavior

Before this change, ongoing scans never triggered a normal-table row refresh; completion refreshed summary/options but did not explicitly refresh visible rows. Export used fixed English labels and discarded successful result text, putting failures in the query-error channel. Local start errors were displayed raw and could be overwritten by scan-status/summary acknowledgements; matching stored-run errors were ignored.

The canonical page now coalesces reading/readBlocks changes into one 1000 ms trailing row revision. It does not create a repeating row poll: an unchanged readBlocks value does not rearm it after it fires. Completion advances rows immediately and clears a pending timeout; unmount disposes it. Scheduled Plunder remains a query boundary. Existing generation fencing, cache, server synchronization, provider contracts and native fences remain in place.

Export owns its own busy state, clears the shared action message before dispatch, passes the recovered localized headers/sheet/yes/no labels, shows count/path on non-canceled success, stays silent for cancellation and translates rejection into the shared result message. It preserves query errors and does not use row-action busy state. Start retires the stored scan result immediately. Local scan errors outrank matching stored/live errors, use the original translator when rendered, and survive successful status acknowledgements. Diagnostic offline text is suppressed when a recovered scan error is shown.

## Exact reference

Target EXE SHA-256: 4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.

MapDataPanel-B4GXEND2.js SHA-256: CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089. UTF-8 bytes: refresh policy _e 413; 1000 ms constant 5640; completion transition 34224; pending-timeout guard 34459; start $n 36787; export fr 41134; local/stored error rendering 49591. Full exact-anchor manifest: source-locators.json in the evidence packet. Main index summary producer and visible-Map five-second poll are also located there, at 363957 and 370275.

## Verification

- Executed original R and unmodified production page through inert callback/effect runtimes: 45 export outcome/locale cases, 19 scan error/start-retirement cases, 11 row revision/cleanup/boundary checkpoints; all pass. Immutable baseline 123459d differs in 15 recorded export/row checkpoints.
- The prior production harness advanced intervals but omitted timeout execution. This packet preserves it and copies its runtime with a chronological interval+timeout advance that commits each deadline. No historical scripts/results were edited to hide this omission.
- Timer comparisons assert exact row revisions and record actual query inventories. Original/current boot issue two/one search requests respectively. Completion ends at four/three additional requests because the original options acknowledgement replaces its object-valued name selection and triggers an additional query, whereas canonical scalar name states remain unchanged. This packet does NOT claim complete options-request/poll parity.
- Both explicit preview fixtures remain offline. Four native/native-unavailable fences and 14 rejected action calls pass.
- Maintained header (270 cases), picker, lifecycle/R1, request lifetime, navigation, interactions, integration, and historical table/filter/Checking/row/state replays pass. Historical outputs are preserved.
- Real browser: English/light seeded export message; actual Export click rejects and displays localized Action failed, returns busy to normal and creates no query alert. Japanese/dark displays one translated scan error. Start remains disabled; console errors/warnings empty. Saved JPEGs visually inspected. Success browser text is seeded only; controlled original/production callbacks prove successful acknowledgement. No file export was performed.
- check/build/check:production-build pass; nine catalogs still contain 1383 messages. Package fingerprints: 7887d40b642c7fbba9700c832c2e68eacd1a818729c03c4c6faddab63a264c23 / 7772b41abe185a67a6eaa12d15e0fdcab43d16ca2ca4992f02d64dd7fdadc839.
- Protected WIP hashes unchanged. Owned browser closed; owned Vite port 4331 stopped. Preview preferences restored to zh-CN/light as found.

## Remaining limits and next continuation

EXACT_BYTES and EXACT_CONTRACT apply to the original source recovery. Native providers/gameplay/export files, protected original post-auth pixels, full Auto UI/config behavior, transport error channels and all-page UI coverage remain unproved or separate. This is UI-only controlled evidence, not LIVE_PROVEN native functionality.

Next bounded UI task: reconcile summary/options refresh ownership (current summary polling reloads options; original separates five-second parent summaries from component options revisions), including bootstrap/completion query side effects and disposal. Avoid undoing accepted filter-lifecycle server redirect/Clear behavior. Returning worker can independently review header, both picker units and this feedback unit as a checkpoint.
