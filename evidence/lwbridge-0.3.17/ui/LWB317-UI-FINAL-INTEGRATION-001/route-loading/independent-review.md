# Independent route loading review

Recommendation: **ACCEPT for the assigned source/local loading scope**, subject to the project lead's final decision. No unresolved acceptance-blocking route defect was found.

I inspected the actual App/router/module and canonical-checker diff from checkpoint `07cb877`, the original shell asset and recovered navigation/preload/select/loaders/Suspense contract. I separately executed the original 15-case oracle, the seven grouped actual-mounted deferred/motion scenarios, and the current v2 callback/ownership replays. All passed. I reviewed the declaration migration proof and the later intentional Equipment-motion delta separately.

## Findings evaluated

| Finding | Assessment | Reason / change |
|---|---|---|
| Six module-level lazy types and eager Home might reset accepted state | Invalid against this implementation | Lazy identities are stable at module scope. Activity route keys and the selected-profile RetainedPages boundary are unchanged. Actual Mini Games DOM/state survives hide/return, intervals follow 1/0/1, and profile identity correctly remounts. |
| Preloading may await summary or block navigation | Invalid | Actual App starts summary, calls preload, then transitions without awaiting either. The mounted controlled-import case keeps Home visible while import is pending and shows Map before its summary acknowledgement. Active Map is inert. Original select locator: UTF-8 byte 364377. |
| Prefetch rejection can change the route or produce an unhandled rejection | Invalid in the recovered best-effort scope | Actual hover and focus invoke the dispatcher; rejected prefetch is handled and Home stays active. Original preload locator: byte 361263. Permanent chunk-load failure during lazy rendering remains ordinary React error behavior; no invented fallback/retry was added. |
| Shared Hotkey loader could merge Hotkeys/Mini Games component state | Invalid | The same stable lazy type intentionally matches original `zi`; separate keyed Activities/route wrappers own separate instances and Mini Games receives the exact category override. |
| One Suspense boundary could have the wrong fallback/hierarchy | Invalid | Actual App wraps the profile context/config errors/page content under one canonical panel + muted Processing fallback, matching original shell byte 372608. Initial deferred Hotkeys mount executes that fallback. |
| Canonical tests were weakened while moving page declarations | Invalid | Home integration changed its source path only; static required files were expanded; UI-complete scans all new owners instead of only the old monolith. Its assertions are retained. The independent delta check verifies 47 declaration bodies unchanged; the Equipment change is intentional and reviewed separately. |
| Historical 48-declaration migration checker now fails EquipmentContent | Valid historical-shape limitation | Later motion integration changes exactly that body. Its pre-motion baseline/result remain preserved. New `check-integration-review.mjs` proves the other 47 owners unchanged and all Equipment handlers/effects/state prefix unchanged except the recovered reduced-motion hook. No old proof was rewritten to produce a pass. |

## Evidence and limits

Original navigation/loader bytes are pinned in the separate source contract. `check-integration-review.mjs --record` passes **47 unchanged declarations**, **26 exact JSX event handlers**, unchanged Equipment producer/effect/handler prefix, source-shaped presence hierarchy and exact engine closure/import checks. Current callback replays pass Map-entry 10, Cross-server 12, App/Home/retention 3, current Map ownership, and affected aggregate 4. V2 preservation verifies 26 historical/previous-checkpoint files.

The controlled mounted loader wraps only import promises; resolved components and React Activity/Suspense remain actual canonical code. jsdom is used for DOM lifecycle. This does not prove original-runtime pixels, native WebView timing, current game compatibility, native functions or missing original state producers.
