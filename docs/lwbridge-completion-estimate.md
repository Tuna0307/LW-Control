# Two-page completion estimate — PM review 7

Date: 2026-09-09. Reviewed implementation: `0273569`. **Rough engineering assessment: about one quarter complete toward fully working Overview + Map Data.** Overview is approximately **20–25%**, Map Data **25–35%**, combined approximately **25–30%**. These are PM planning judgments, not recovered application constants, measured percentages of buttons, or elapsed-time forecasts.

Review 7 retains the same broad range: new source/response/runtime findings reduce uncertainty, but production launch, scan ingestion, options/summary/export and full acceptance have not been enabled. This is useful research progress without a justified increase in page-completion credit.

## What the estimate measures

It credits reusable UI, local configuration, recovered contracts and tested backend foundations, while weighting owned launch, connected scans/actions and integration heavily. A static finding and a test-only helper receive partial credit only. They do not count as a completed public command or a live outcome. The number of commits, findings, passing unit tests or checked subitems is not the denominator.

To make the judgment reviewable, this checkpoint uses the following **PM planning rubric**. Weights total 100 per page. Credits are rough assessments of completed engineering inside that work package; they are not measurements of original behavior. Evidence comes from the current feature ledger, code paths and offline tests. Round the resulting anchor to a broad range instead of reporting decimal precision.

| Overview package | Weight | Approximate package credit | Reason |
|---|---:|---:|---|
| Recovered UI/local interaction | 10 | 90% | Strong fixture/native-host foundation; exact authenticated reference parity still has limits. |
| Installation selection/local preferences | 15 | 80% | File/PE checks, persistence, rollback and picker work exist. |
| Owned launch/stop | 35 | 0% | Production start/stop still reject; recovered portions are not a functioning lifecycle. |
| Connected status/startup/reconnect/repair | 30 | 5% | Some contracts/preferences exist; authoritative workers/readiness are absent. |
| Integrated live/failure acceptance | 10 | 0% | No complete current-client lifecycle acceptance signed off. |

| Map Data package | Weight | Approximate package credit | Reason |
|---|---:|---:|---|
| Recovered UI/query consumers | 10 | 90% | Original UI and many payload/consumer contracts preserved. |
| Persistent index/search/filter/marks | 25 | 70% | Many offline predicates, snapshot consistency and marks pass; identity/normalization, sorts and production data still incomplete. |
| Public options/summary/export | 15 | 20% | Considerable static/test-only groundwork, but public commands remain unavailable. |
| Native ingestion/manual scan/completion | 25 | 5% | Schema, staging and transaction tests; no production capture/scheduler. |
| Auto scan/travel/conditional actions/jobs | 15 | 0% | Working end-to-end services/outcomes not established. |
| Integrated live/failure acceptance | 10 | 0% | No full scan/action acceptance signed off. |

The equal-page weighted anchor is roughly 27%, hence **about 25–30%**, with wider uncertainty than the arithmetic implies. This does **not** mean 70–75% of calendar time remains: unresolved launch/capture contracts can take unpredictable research effort. Estimates must be revised when real integration evidence changes the work remaining.

## Hard acceptance status

**0 of the 47 full acceptance cases is formally signed off in the current project audit.** This is an acceptance-record count, not a claim that no individual control or test works. Many local/offline subchecks pass. Real game launch, bridge readiness, scan ingestion/completion and action outcomes remain unproven.

The UI looking close to finished must not be reported as the whole project being close to finished. The largest remaining milestones are:

1. Supported owned launch/stop plus fresh bridge identity/heartbeat and recovery.
2. Native record identities/normalization, capture and bounded then full scan completion.
3. Public option-source/count/summary assembly and fully filtered, correctly typed Excel export.
4. Automatic scan/travel/jobs/actions and integrated current-client failure/restart acceptance.

Update this estimate only with evidence-backed changes in those packages. Do not increase it just because another test-only helper or restriction entry was added. See [review 7](lwbridge-project-status.md) for accepted changes and open requests.
