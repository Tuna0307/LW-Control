# Independent project-lead review

Reviewed delivery: `967a92c3553e4bc81272741f333803ee54ae5e44`.
Verdict: **CHANGES_REQUIRED**, six finite local UI/state corrections.
See `docs/reviews/2026-10-05-LWB317-UI-VISUAL-REMAINING-002-LEAD.md`.

This fresh directory preserves the worker's historical evidence. No production
source or prior evidence result was modified by the review. Current master status
documents are deliberately advanced from worker AWAITING_REVIEW to lead
CHANGES_REQUIRED; old frozen document snapshots remain historical.

- `afk-automation.md`: exact recovered/current comparison and counterexamples.
- `automation-counterexample-results.txt`: lead's independent rerun of the
  embedded Automation original/current script: missing-field defaults, exact
  drag target, and measured generic invalid-card error geometry.
- `shell-home.md`: exact original/current source locators and embedded executable
  inert callback reproductions; independently rerun by the lead.
- `shell-callback-results.jsonl`: lead's fresh results for all five comparison
  outputs (three defects). Assertions pin the defective baseline, not acceptance.
- `map-integration.md`: independent bounded Map/E–H acceptance, current source
  identity and original pixel replay; no new product defect found.
- `run-full-app-independent.mjs`: copied worker integration runner with only
  dependency-import/output relocation; run on task-owned port 4410.
- `full-app-current-results.json` and `browser-current/`: fresh 213-assertion
  mounted current-App rerun, 11 images, zero console/page issues. This is not a
  new original/current whole-App pixel comparison.
- `check-train-browser.mjs`, `train-browser-results.json`,
  `train-drag-current.png`: separately authored mounted current Train drag-target
  counterexample using DOM DragEvents. Physical HTML5 drag is not claimed.

Useful rerun (start an owned Vite server first):

```
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/lead-review/run-full-app-independent.mjs <owned-port>
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/lead-review/check-train-browser.mjs <owned-port>
```

Those producers write only this directory; preserve committed review-baseline
results before running corrections. Source-contract reproduction code is embedded
in the two reviewer reports. All provider responses are inert. No Last War,
original-service, native scan/jump, or gameplay operation was invoked.

`validate-read-only.mjs` checks the unchanged 67 production inputs, reference
identity, 11 fresh screenshot hashes and reproduced defective baseline results.
It validates this review packet, not acceptance or correction. The task-owned
Vite listener on port 4410 was stopped after checking its process command line;
owner listeners were left untouched. AFK full-page and Automation conditional
composition proof also require the finite closeout described in the lead review.
