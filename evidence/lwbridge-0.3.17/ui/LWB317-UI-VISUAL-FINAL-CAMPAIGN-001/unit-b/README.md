# Unit B — remaining Map and Scheduled Plunder visuals

Status: worker-complete for Unit B; campaign continues to Unit C.

No production correction was required in this unit. Two initial integrated mismatches were evidence-harness defects: the current `MapTable` SSR had escaped the fixed clock, and the recovered page's internal `lastwar_localize` state had been incorrectly treated as a prop. Both harness defects are corrected and the resulting browser packet is regenerated from source.

The Scheduled Plunder source differential executes the exact recovered Dispatch/Ghost/Truck group functions and current canonical groups for 10,482 renders, including timing boundaries, result/error mappings, action predicates and 61 explicit fixture branches. It detects all 51 plausible source mutations. The integrated Map replay executes 75 full-page cases across eight data tabs plus Scheduled, Manual/Auto and loading/empty/populated/error/availability overlaps.

Browser evidence contains 16 recovered/current pairs and 32 screenshots. Thirteen pairs are decoded-pixel exact. The three non-exact pairs are the already accepted current-only query-error banner and Start Scan native-availability fence in two offline Scheduled fixtures; `check-browser-pixels.py` proves zero changed pixels outside their narrow source-classified masks. Mounted canonical QA adds 25 local interaction assertions in EN/light and JA/dark with zero console/page errors and no native/gameplay action execution.

Validation commands:

```text
python unit-b/check-browser-pixels.py
LWB317_VISUAL_FINAL_UNIT_B_PIXELS_OK

node unit-b/validate-unit-b.mjs
LWB317_VISUAL_FINAL_UNIT_B_VALIDATED
```

The original Scheduled native job-producer schema is not present in the recovered frontend assets. Source-read row fields and all recovered render branches are validated with disclosed synthetic rows; native job production/gameplay remains UNKNOWN and outside this campaign.
