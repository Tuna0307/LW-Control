# Competitor research

What we learned from **LwTool**, a competing Last War helper, and which of its ideas we adopted.
The reports are dated snapshots. They describe LW-Control as it was when each was written (v1.0
`main@45e151c8` up to the v1.2 work branch). For current behavior, read
[ARCHITECTURE.md](../ARCHITECTURE.md) and [STATUS.md](../STATUS.md).

Files the reports cite under `docs/research/`, `docs/proof/` or `tools/lwtool_improvement_bench/`
were removed from `main` and are kept in the `archive/pre-cleanup-2026-10-27` tag. Recovered
competitor code and raw captures were never committed.

An earlier LastWarPlugin report was removed. LastWarPlugin turned out to be a launcher wrapper around
LwTool (its native wrapper creates `Local\LwTool_MasterKey`), and the LwTool connection report
replaces its conclusions. It is in the archive tag as
`docs/research/lastwarplugin/2026-10-23/CONNECTION_METHOD_REPORT.md`.

| Report | Date | Main conclusion |
|---|---|---|
| [lwtool/CONNECTION_METHOD_REPORT.md](lwtool/CONNECTION_METHOD_REPORT.md) | 2026-10-23 | Neither connection design is simply better. LwTool has a leaner command path (shared memory) and late-connect recovery; LW-Control has the stronger exact-owner Stop/cleanup and Start-completion contract. Keep our architecture. |
| [lwtool/FULL_PRODUCT_METHOD_MATRIX.md](lwtool/FULL_PRODUCT_METHOD_MATRIX.md) | 2026-10-23 | Tab-by-tab method comparison with IMPLEMENT / DEFER / REJECT decisions. Three low-risk improvements were taken (lease parse cache, less redundant UI polling, AFK snapshot reuse). |
| [lwtool/IMPROVEMENT_BENCHMARK_REPORT.md](lwtool/IMPROVEMENT_BENCHMARK_REPORT.md) | 2026-10-23 | Before/after measurements for those improvements. |
| [lwtool/HOME_CONNECTION_STABILITY_REPORT.md](lwtool/HOME_CONNECTION_STABILITY_REPORT.md) | 2026-10-23 | The owner's visible disconnect was not reproduced; two local failure classes were found and fixed (heartbeat publication stall, local pipe loss). Final acceptance: [design note](../design-notes/2026-10-23-home-final-acceptance.md). |
| [lwtool/MAP_SCAN_METHOD_REPORT.md](lwtool/MAP_SCAN_METHOD_REPORT.md) | 2026-10-24 | LwTool's "quick Map" uses a remote scanner service, not the local game. Our local direct-AOI scanner was kept. Closeout: [design note](../design-notes/2026-10-25-map-final-qualification.md). |
| [lwtool/REMAINING_TABS_COMPARISON.md](lwtool/REMAINING_TABS_COMPARISON.md) | 2026-10-25 | Equipment, City, Settings, Hotkeys and Mini Games compared; adopted changes shipped in v1.2. Qualification: [design note](../design-notes/2026-10-25-remaining-tabs-qualification.md). |
| [lwtool/CHAT_REWARDS_COMPARISON.md](lwtool/CHAT_REWARDS_COMPARISON.md) | 2026-10-25 | LwTool had separate Gold Egg and cross-server controls. The recommended changes were implemented in v1.2 ([design note](../design-notes/2026-10-25-chat-rewards.md)). |

Ideas that were deferred, not rejected, are listed under "Ideas not done" in
[STATUS.md](../STATUS.md).
