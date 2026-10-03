# Map-entry recovery checkpoint

Status: `EXACT_BYTES` / `EXACT_CONTRACT` for the source-backed navigation behavior below.

The reference is `C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe`, SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`. Its recovered frontend asset is `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js`, SHA-256 `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.

At UTF-8 byte 364377 the exact original `Tt` route selector checks `e!==i`. For a different `map-data` route it executes `_t().catch(...)`, then `Wi(e)`, then `c(() => { ... a(e) })`. `_t` is the summary refresh helper and `Wi` is the lazy route preloader. The exact function is pinned in `source-manifest.json` and executed by `run-baseline.mjs`.

Therefore the recovered entry contract is:

1. same-route selection is inert;
2. a different Map selection starts the summary request before route preload and before the React transition;
3. navigation does not await summary settlement;
4. summary rejection is handled independently and does not block the transition;
5. a different non-Map selection does not start a summary request, but still preloads then transitions.

The original parent also has profile/bootstrap summary loading and a connected five-second summary poll. Those owners are independent of this navigation request. The current canonical App has no equivalent lazy page-loader call because its route pages are statically imported through `Pages.jsx`; this task records the original `summary -> preload -> transition` relationship without introducing a bundler/page-loading rewrite.

The dispatch App blob is pinned from `74ceaf78dc24c08068320dfc9766cf349faa1b7e` as `baseline-74ceaf7-App.jsx`. Its actual callback/effect runtime shows the distinguishing mismatch: `selectRoute` transitions immediately, then the separate `[activeRoute, refreshMapSummary]` effect starts the entry summary only after Map is active. A direct initial `?view=map-data` route also gets that active-route request in addition to the profile/bootstrap request.

Preserved implementation guards are separate from exact original facts: current summary generation/profile fencing, profile/bootstrap ownership, connected polling, scan-completion refresh, retention, Cross-server ordering, Home acknowledgement and offline/native availability fences remain in place unless the focused correction itself requires otherwise. The reference does not establish a new cancellation policy for route changes; pending replies continue to use the existing generation/profile guards.

