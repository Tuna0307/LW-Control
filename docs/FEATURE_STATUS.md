# Feature delivery status

This application candidate is an incremental delivery, not a completed 1-to-1 clone. Behavior reference: LWBridge 0.3.17, SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.

**UI main release candidate (2026-10-09):** imported production UI source/assets match the 88-file reviewed post-acceptance snapshot, except two intentional dev-only test-dependency manifests; no new proven local-presentation mismatch. The release sweep mounts the actual canonical production frontend in Edge: eight routes EN/light, eight routes JA/dark at 640px, 10 conditional fixture captures, reload/navigation/dialogs and zero page/console errors. A normally published executable with isolated disabled profile opened its real window without launching Last War, plus a packaged WebView2 fixture capture passed. The normal window's offscreen compositor could not be visually captured: do not interpret this as new original runtime pixel proof. See [release review](UI_MAIN_RELEASE_REVIEW.md) and [source manifest](product-source-manifest.json). Browser-only fixture states do not certify native functionality.

| Feature | Evidence in this delivery | Acceptance limit |
|---|---|---|
| Normal packaged Home | Mounted production WebView; stopped state, real language control and native preference saving | Complete original runtime pixels remain unproved |
| Auto Launch preference | Recovered local key/default/immediate-write contract; real mounted switch/native save and reload | Native mirror/rollback is an adaptation; complete launch behavior is separate |
| Automatic Reconnection setting | Actual native boolean save and mounted control/reload | Recovery execution and all original error paths are separate |
| Startup profile admission | Actual backend now invokes ordered registry reconciliation; disabled/locked profiles excluded, consumed once | Protected multi-profile lease/capacity behavior remains incomplete |
| Game-root selection/status | Actual backend tests: cancel, invalid no-save, valid save, normalization, unavailable state, ownership/root rebinding | Normal installed-game autodetection hides the picker; OS-dialog interaction not verified here |
| Game launch/connect/Stop | Prior research contains current-client live witnesses | Full original lifecycle A-to-A acceptance is incomplete; not certified by this candidate |
| City/Resource scanning | Existing implementations and prior current-client live witnesses retained | Original encrypted traversal/retry/result equivalence and four current lead findings remain open |
| Treasure/Ghost and other pages | Recovered UI plus partial native services retained | No complete functional acceptance |

Original Auto Launch source: recovered `index-BVfnK1wp.js` bytes 246750/246758, 247632, 248381, 338639. Original startup enabled/unlocked ordering is recorded in HOME009-R2 and the ordered reconciliation source. Original root handlers: `game_root_select` RVA `0x188F12–0x18A2AD`, `game_root_status` RVA `0x12816E–0x128A38`. Detailed authority and negative records are retained on the research branch.

Next release gate: complete one visible Home Launch -> Connected -> Close flow through canonical controls, preserve exact exit/restoration receipts and resolve its remaining original semantic differences. Then deliver City/Resource separately. Do not restart an all-pages campaign.
