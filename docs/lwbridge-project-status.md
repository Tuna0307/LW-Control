# Project-manager checkpoint — review 11

Reviewed 2026-09-10 against `37a1dac724def9eb6635764cdda12573736abc65`, branch `research/offline-controller`. Three commits follow PM delivery `5e3af2a`; the working tree was clean and GitHub matched HEAD. [Review 10 with contributor follow-ups](reviews/2026-09-10-review-10-and-followups.md) is historical; the follow-up implementation claims were reviewed here.

## Result for the owner

**The AI finished useful supporting work, but the app still cannot fetch fresh game data.** Saved replay now has explicit labeling and stronger input/isolation tests. A helper can construct the original communication-pipe name. There is still no production connection service, accepted handshake or fresh resource acquisition, and no second fresh read. Normal Launch/Start Scan remain gated. No full acceptance case is newly signed off.

### Post-review implementation continuation — LWB-R5-007

After this PM audit, the regular implementation task recovered a further bounded connection slice from the same immutable reference. The original host is the named-pipe server; the secure xLua proxy is the client. The proxy uses a 4-byte little-endian payload-length prefix with recovered payload range `1..0x800000`, and its exact `hello` builder is version `1`/type `hello` with `profileId`, `instanceId`, empty `requestId`, numeric `timestamp`, and payload `token`, numeric `pid`, `buildId`. The rebuild now contains an offline frame codec and hello parser only. This continuation has not been PM-reviewed and does not supersede the audit's acceptance decisions.

The remaining PM10-01 blocker is now narrower: exact host `hello.ack` serialization, heartbeat freshness/readiness and host-to-proxy request/result correlation. No production `INativeAsyncCommandService`, fresh acquisition or second fresh read is claimed. ESC-005 has been completed for PM review around that specific grammar question; no specialist is assigned.

## Accepted work

| Commit / item | PM decision and evidence limit |
|---|---|
| `b102423` / R7-002 / PM10-01 recipe | Historical source commit, six source identities and run/restoration recipe are durably indexed. Accept preservation; not independently reproduced fresh acquisition. Candidate staging still includes a historical-session narrative, so do not promise that one command in today's checkout recreates the package. |
| `b102423` / PM10-02 | Replay banner, Resource selection, disabled acquisition controls, dedicated store and unavailable/replay status are implemented in reviewed code. Backend state/isolation pass; new banner/control behavior has no current runtime UI proof. Keep this visual validation pending rather than calling the old screenshot proof of the new UI. |
| `b102423` / PM10-03 | Accept focused importer and production-gate regressions after a fresh passing deterministic run. They exercise offline fixtures, not a connected session, fresh-response correlation or disconnection recovery. |
| `3208bf2` / R5-006 | Pipe-path helper agrees with the documented derivation and its deterministic check passes. The reported original-host pipe observation is historical evidence, not a game connection. PM did not repeat binary analysis or pipe interrogation. |
| `37a1dac` / session blocker | Correctly leaves session/request grammar unknown. Corrected the unsupported inference that a keyword history search proved no older implementation exists. |

## Findings and next work

- **PM11-01 — documentation corrected:** the history search establishes no reusable session implementation was identified by those particular searches. Candidate inspection was denied, so neither absence nor complete search coverage is proven. Corrections are in the live-result task and ESC-005. No new history inspection was performed.
- **PM11-02 — visual proof pending:** no post-R7-002 screenshot or interaction result demonstrates the new replay banner, initial Resource selection or disabled controls. The earlier app-capture operation was reported rejected by automatic review; PM did not retry or reroute it. Preserve this verification gap and only use a permitted validation method when available. It must not displace the connection task.
- **PM10-01 — active main task:** identify the minimum accepted connection handshake and request/result framing, then implement the supported production connection and acquire a real point from the app. The pipe-name helper has no production caller; current source has only the isolated host-probe command service. Finding the pipe cannot establish readiness.

Follow [the standard handoff](implementation-handoff.md) and [bounded live-result task](first-live-result.md). Do not reopen completed importer/replay work or defer to updater/export research. First state the exact missing connection fact and a permitted evidence-producing method. Distinguish endpoint roles, accepted framing and identity/readiness conditions from nearby string vocabulary. If the supported current-client route can deliver the bounded demonstration independently, document its evidence and deliberate design choices; do not assume full protected original-runtime recovery is automatically required. Never use an alternative route to repeat a denied operation.

The next checkpoint must return either an evidenced connection contract with the exact implementation it unlocks, working fresh app acquisition, or a completed bounded ESC-005 request explaining why the remaining permitted methods cannot answer that same question. A denial list, another pipe-name test or generic 'continue researching' is insufficient. No arbitrary attempt count is imposed.

## Specialist decision

No Daybreak task is approved. **At the time of review 11**, ESC-005 remained NEEDS_INFORMATION because it lacked a concrete permitted specialist method with tool/version/locator/results, relevant alternatives and precise return criteria. The post-review R5-007 continuation has since filled those fields and marks the narrow request/result-grammar packet `READY_FOR_PM_REVIEW`; that is not a PM approval or assignment. ESC-001 remains CLOSED; ESC-003 NOT_ASSIGNED; ESC-002/004 NEEDS_INFORMATION and deferred. Restrictions are not user refusals and cannot be bypassed by changing model.

## Fresh verification and limits

Release build: zero warnings/errors. Deterministic suite: `ok=true`, six groups true including `bridgeControlPipeContract`, no failures; run with `--verify-real-config-unchanged`. Frontend integrity, five preference scenarios and both transport checks passed. Read-only installed diagnostic was valid; game and launcher were stopped at test time. No game launch, fresh capture, current UI capture, historical source inspection or binary analysis was performed. Passing these checks does not close the first fresh-result goal.

[Review evidence](../evidence/lwbridge-implementation/2026-09-10-pm-review-11.json) records the audited source and checks. All 47 acceptance cases remain unchanged. This is a PM audit/instruction checkpoint; implementation credit belongs to the three reviewed commits.
