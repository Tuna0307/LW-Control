# Independent Equipment review

Submitted implementation: `4bafcd437929f2f905b557ca700657467afbdcbc`.
Decision: **CHANGES_REQUIRED**. See the dated lead review and R1 work item.

Run from repository root:

```powershell
node evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-EQUIPMENT-CLOSEOUT-001/check-review.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-EQUIPMENT-CLOSEOUT-001/validate-evidence.mjs
```

The runner deliberately reproduces hidden Equipment Alt effects and compares
recovered rename pending/failure behavior against the synchronous preview callback.
It asserts the submitted failing baseline, not future correction acceptance.
No native/browser/gameplay actions are run. The validator pins current reviewed
production and the preserved worker packet, verifies exact original bytes and EXE
identity, and hashes all three screenshots. Preserve these historical pins when
R1 changes production; its new packet must validate its new implementation.

`verification-results.json` qualifies existing passing tests and image evidence.
No independent browser input, physical HTML5 drag, native persistence or original
pixel comparison is claimed. Recovered hidden-tab effects and rename acknowledgement
states are frontend work; native integration remains separate.
