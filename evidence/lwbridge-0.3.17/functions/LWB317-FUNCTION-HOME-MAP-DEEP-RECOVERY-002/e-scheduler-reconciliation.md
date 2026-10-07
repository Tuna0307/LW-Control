# Milestone E — scheduled-job reconciliation

Exact recovered 0.3.17 scheduler/store behavior was compared against the current
`MapPlunderWorker` and `MapStore.Plunder` implementation and the existing inert
production provider boundary.

## Reconciled contract

- worker cadence: 100 ms;
- arm lead: 10,000 ms;
- protected provider arm timeout: 5,000 ms;
- Dispatch/Ghost terminal horizon: 15,000 ms;
- Truck terminal horizon: 30,000 ms;
- Dispatch/Ghost arm batch cap: 200;
- due jobs consume an attempt when moved to `running`;
- connection loss returns active/due work to `waiting_connection` with the
  recovered error family rather than fabricating success;
- restart converts previously running jobs to the recovered restart/wait state;
- connected result timeout becomes terminal failed;
- Dispatch/Ghost provider results are matched back to the durable server/task/kind
  identity; stale/nonmatching events do not complete another job;
- Ghost durable identity is namespaced with `ghost:` while the provider receives
  the underlying positive task UUID;
- daily-limit fanout applies to ordinary Dispatch work and excludes Ghost rows;
- Truck pending identity includes the generated `jobId`; stale result/job IDs are
  ignored;
- Truck timeout performs best-effort provider-pending cleanup after persisting the
  durable timeout result;
- replacing a terminal Truck job archives the previous attempt in
  `truck_plunder_history`; a running job is not overwritten;
- Cancel only changes `scheduled`/`waiting_connection` work;
- Retry only reopens recovered terminal states;
- Clear removes only terminal historical rows before the requested cutoff and can
  filter Dispatch vs Ghost history.

## Inert boundary proof

Existing Release
`--map317-plunder-worker-boundary-check`: **PASS**.

That check exercises controlled providers/clocks and persistent store transitions;
no live game/provider call is involved.

## Result

No scheduler/store discrepancy was demonstrated. No product change is justified by
E. Treasure/Ghost protected provider availability remains a separate D dependency
and does not alter the durable scheduler semantics.
