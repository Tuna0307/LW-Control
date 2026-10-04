# Independent toolbar review evidence

Decision: CHANGES_REQUIRED. See the dated lead review for R1/R2 and proof limits.
`pixel-results.json` is an independent pixel-level counterexample to the worker's
availability-only claim. It decodes pinned submitted PNGs; no image is edited.

```powershell
python evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-MAP-TOOLBAR-VISUAL-001/check-pixel-fences.py --expect-submitted-defect
```

Without that explicit historical-defect flag, the checker requires zero changed
pixels outside newly disabled action buttons and correctly fails the submitted
Treasure pair. `--measurements <new packet measurements.json>` permits the same
independent check on a future correction; do not use the historical expectation
as its acceptance criterion. `--record` updates only this lead packet.

Fresh mounted replay: start an assistant-owned canonical Vite server on4346 with
strictPort, then run `node .../replay-mounted.mjs`. The wrapper uses the existing
actual browser interaction suite, changing only owned output paths and4346.
It records source hash/adaptations/stdout/exit status, two fresh screenshots and
34 case results. Clean only that owned service. Owner4335 is outside this replay.

The source/evidence subreviews were read-only. Source AST checks14/14 passed;
fresh46-case source comparison and six browser pairs reproduced submitted results
with file writes intercepted in memory. Their conclusions and exact locators are
recorded in the dated review. This packet is not original-runtime or native proof.
