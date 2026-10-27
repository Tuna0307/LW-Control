# Inert handled-aware Truck snapshot candidate

Status: isolated Lua 5.3 candidate, not loaded into production. No app, game, installed script or network activity.

## Recovered local sources

The production pump at src/LWBridge.GameScripts/current_live_resource_probe.lua:7371-7412 enumerates the complete source-owned March dictionary on every Update frame, including frames with no new receive marker. Receipt changes in MarchDataManager._lastGetBlockMsgTime cause push ACKs without checking whether the queued March handler completed.

The existing camera-free gate at :3858-3901 documents the separate _lastGetBlockMsgTime and _lastHandleBlockMsgTime markers. The latter precedes synchronous ParseDataWorldMarchGet and the next Lua Update runs after the main-thread handler has returned. Use native DateTime.CompareTo; converted 64-bit ticks or string ordering cannot prove that the handler caught up.

The original typed TrainData/goods builder at :3644-3828 and exact owned Truck fence/repair/restoration at :7214-7497 remain the reference. Preserve 625 rectangles, 20 requests/s, 25-request fences, 2 bounded selective repair passes, source-owned UUID dedupe, full typed fields and exact restoration.

Retained benchmark-0 and benchmark-3 mobile receipts from the final October 24 all-ten ABBA reported Truck 56.536 / 57.001 seconds, 675 sends each (625 initial + 50 repairs), one repair pass, replyWaitSeconds 40.982 / 42.168 seconds, and captureSeconds 0.0. Capture time is measured using Unity realtimeSinceStartup within one Update frame and is too coarse here to prove the actual enumeration cost. The candidate has no live timing claim.

## Integration proposal

Inline the research helper fast_truck_handled_gate_candidate.lua in the same production Lua source only after a qualified owner-level trial. Initialize the gate with the native receive DateTime immediately before the first sent request. On each Update, read both receive and handle DateTimes and check that a receive is outstanding in the active fence. Idle frames skip full March enumeration. A newly received but unhandled response remains pending without ACK. Once the native handle marker is at or after that receive, invoke the ORIGINAL production train_march_aoi_records builder, validate and append all typed new rows, then increment both push and fence ACK. Never ACK before successful post-handle enumeration.

If a newer receive arrives before the older one is observed handled, the source dictionary may have overwritten intermediate rows. Return an ambiguous-overlap failure and use the existing exact owned cleanup and managed camera fallback rather than claiming coverage. Foreign/out-of-order stamps and missing/uncomparable native DateTime markers similarly fail closed.

## Checks and limitations

Run: python -m unittest tools.map_019_review.test_fast_truck_handled_gate_candidate -v

The 18 inert Lua 5.3 tests cover late rows added only after handler completion, 625 responses across 25 fences, five seeded 625-response delayed schedules, idle enumeration suppression, duplicate stamps, correct failure for unhandled overlapping replies, foreign responses, timestamp rollback, missing/uncomparable DateTimes, empty responses and no early ACK. They do not emulate every game main-thread scheduling or source dictionary mutation.

The candidate eliminates wasted enumerations in simulated frames, but it can add waiting or trigger fallback if replies arrive faster than native handling. It is NOT yet qualified for production because no genuine 20/s source-match shows how often the handle timestamp lags or whether repeated cache rows are available after that marker. A matched game-backed test must prove 625/625, zero missing/late typed rows, stable native field hashes, no additional fallback, exact Stop/restoration, and lower full-wall time. This remains a performance hypothesis, not an accepted optimization.

Another worker is exploring source-local UUID-before-type filtering independently; this candidate does not modify the shared production builder.
