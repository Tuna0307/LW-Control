# Old pending work closeout — 2026-10-04

The owner requested review of the long-standing pending files before dispatching
the large UI finish campaign. All ten historically protected file bodies are
preserved byte for byte under archive/, checked against the original ten-file
baseline. Archive .gitattributes disables line-ending conversion for these bodies.
Preservation is not acceptance of old scripts or generated results.

Disposition:

- previewAfkFixtures.js pending enable flags are superseded by accepted
  previewAfkCloseoutFixtures.js, which explicitly overrides all four flags.
  Executing the actual adapter with HEAD and pending base modules produced equal
  complete configurations across39 discovered/boundary preview states. Restore
  the tracked base fixture; keep the pending source in this archive.
- FILTER-LIFECYCLE-001-R1/independent-results.json changes only a historical
  currentSha256 to another now-stale value. Archive that generated variant and
  restore the committed historical evidence; it is not a new implementation fix.
- AUTO-CONFIG-001/regression-results.json is already byte-identical to HEAD;
  refresh Git's index metadata without rewriting the file. No content correction.
- The two Equipment motion LICENSE/NOTICE files accompany the already tracked
  recovered dependency and its attribution comment; retain and commit them.
  Original dependency package version remains UNKNOWN as their notice states.
- .scratch-lwb317 contains two formatted recovered assets and an early baseline
  script. Archive their bytes and keep the original local copies as ignored cache.
  The baseline script is historical and is not promoted to a current acceptance gate.
- CORRECT-003/screenshots/weekly-save-error-light-en.png is0 bytes, an invalid
  empty placeholder. Preserve its body in the archive and ignore the original
  exact file. It supplies no usable screenshot evidence.

No research file was deleted. Current source/evidence restores affect only the
two reviewed superseded/generated changes, after snapshot/equivalence verification.
The old HOME-PREFERENCE-LIFETIME-001B guard tests the earlier live-file WIP layout
and is now historical. Do not rewrite that guard/baseline. Future work verifies
this archive using check-archive.mjs, and captures its own starting dirty files.
Actual cleanup/tests/commit results are recorded in the dated closeout review.
