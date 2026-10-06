# Interrupted worker checkpoint audit

Lead decision: **PARTIAL / CHANGES_REQUIRED**, 2026-10-06.
Audited pushed HEAD: `540bc53d73053bb1eb70d6a09fea851a9e96d625`, plus 29 frozen WIP files.
No production/worker files changed by this audit.

Authority: `docs/reviews/2026-10-06-LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001-LEAD.md`.
Continuation: `docs/work-items/LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001-RECOVERY-001.md`.

- `worker-checkpoint.json`: byte/hash preservation baseline, initial HEAD/remote.
- `runner-results.json` and six logs: independently executed focused native checks.
- `home-review.md`, `map-review.md`, `frontend-auto-review.md`: bounded independent
  source reviews; main lead evaluated findings and dispositions in the dated review.
- `check-auto-callback-races.mjs`, `auto-callback-races.json`: exact App callback
  extraction and real helpers, controlled promises. Two same-profile stale-intent
  failures; one conditional profile-replacement recovery case. Not a mounted App.
- `auto-owner-repro/`: links actual native Auto core to a tiny isolated console
  project with inert execution boundary; no Desktop/game provider instantiated.
  `auto-owner-repro-result.json` proves disable can stop Manual M after Auto A
  externally finishes before its next poll. Native lease interleaving is source
  proof, not execution of full Map317 host.
- `validate-audit.mjs`: verifies preservation, proof structure and focused results.
  It validates the interrupted baseline, so future intentional worker corrections
  will invalidate the corresponding current-file checks; preserve this evidence.

Native compilation (skipped UI target), canonical Map317 checks and frontend
check/build/package also passed. Fresh source/package fingerprints are in the
dated review. Saved native captures were decoded and inspected only. Both render
Chinese/light despite EN/JA-dark filenames; prior timeout evidence is preserved.
No live Last War, updater, owner installation mutation or protected service used.

Reproduce bounded negative cases from repo root:

```
node evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001/lead-audit-2026-10-06/check-auto-callback-races.mjs
dotnet run --project evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001/lead-audit-2026-10-06/auto-owner-repro/AutoOwnerRepro.csproj
node evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001/lead-audit-2026-10-06/validate-audit.mjs
```

The negative repro programs report success when the audited defect is reproduced.
Do not mislabel those outputs as production passing parity. Corrected worker proof
must assert the inverse behavior and keep these immutable audit records.
