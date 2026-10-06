# Independent recovery closeout review

Audited worker HEAD and remote: `99d8f7b513ae56a0aa3b4d92a202f81586a32924`.
Initial working tree clean. Lead disposition: **CHANGES_REQUIRED**.
No production/test files changed by this review.

Authority: dated `docs/reviews/2026-10-06-LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001-RECOVERY-001-LEAD.md`.
Continuation: `docs/work-items/LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001-RECOVERY-002.md`.

Files:

- `runner-results.json` and seven `.txt` outputs: actual zero-exit native runs.
  **Not owner-isolation acceptance:** source review afterward found shared runtime
  lease cleanup in supposedly isolated profile-owner composition. Further native/
  desktop execution stopped. There was no pre-run lease baseline. No actual owner
  deletion or unchanged owner storage is claimed. Fix isolation before rerunning.
- `home-review.md`, `map-review.md`, `frontend-evidence-review.md`: three bounded
  independent source reports evaluated by the lead; no reviewer ran native code.
- `check-counterexamples.mjs`, `counterexamples.json`: three negative witnesses using
  exact current initializer/callbacks/coordinator/helpers and controlled promises.
  Native commands/game actions zero; these are not mounted/native integration.
- `validate-closeout.mjs`: checks source pins, actual logs and saved package/capture
  identity. This validates the audit's evidence, not campaign acceptance.
- `observations.json`: read-only identity and post-run isolation observation limits.

The preserved v3 packet hashes match the current existing Release package. Both
screenshots were decoded: correct EN/light1120x720 and JA/dark900x720. The lead did
not independently drive/rebuild/re-execute that desktop proof after the safety
finding. The worker issue collector resets on document reload, so its final empty
array is not complete session issue coverage.

Reproduce **JS-only** negative cases:

```
node evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001/lead-closeout-2026-10-06/check-counterexamples.mjs
node evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001/lead-closeout-2026-10-06/validate-closeout.mjs
```

Success means current defects reproduce / audit evidence is consistent. It never
means the product passes those parity cases. Preserve these records and supply
inverse corrected proof in a new worker packet. Later intentional source/package
changes invalidate the audit's current-file pins; do not repin historical evidence.
