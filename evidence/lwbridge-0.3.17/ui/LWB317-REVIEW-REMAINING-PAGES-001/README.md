# Independent four-page review

Submitted revision: afd65b64808e0fd43175dbbaa5afc36e32def9ef.
Decision: CHANGES_REQUIRED; see dated REVIEW-REMAINING-PAGES-001 and R1 work item.

`check-review.mjs` intentionally reproduces twelve failing submitted cases: native
Mini/Settings false acknowledgement/state, initial updater data, City null-layout,
pointer offset/fallback and Apply preparation, plus event-time error locale lifetime.
It executes actual original/current components and callbacks in an inert hook
harness. No native/provider/network/browser action is invoked. Its successful exit
records reproduced defects, not corrected product acceptance.

Run from repository root:

```powershell
node evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-REMAINING-PAGES-001/check-review.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-REMAINING-PAGES-001/validate-evidence.mjs
```

Historical pins are expected to become stale after R1 production changes. Preserve
them; the new R1 packet must demonstrate corrected behavior against original and
submitted baseline. Passing worker helper/marker/hash checks did not cover these
boundaries. Worker screenshots/console records support disclosed preview proof;
no fresh lead browser or original-reference pixel comparison is claimed.
