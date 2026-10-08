# LWB317-FUNCTION-HOME-PARITY-CLOSEOUT-009-R3 continuation — 2026-10-09

**Worker status: PARTIAL; NOT original 0.3.17 Home/Map A→A, NOT project-lead acceptance.**
This report continues, without rewriting, the previously pushed R3 review at
`docs/reviews/2026-10-09-LWB317-FUNCTION-HOME-PARITY-CLOSEOUT-009-R3-WORKER.md`.
Beginning SHA `b51124bf48d9d5d2d6199262c341a74c7a3288d9`; branch `research/offline-controller`.
Original fixed EXE SHA-256 `4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783`.

## Original source branches newly closed

**B launch-failure serialized value resolved:** `0x205412–0x20542A` copies the 80-byte launch Err into `[rsp+0x460]`. On the error path `0x2054CB`, `0x2055D0/0x2055DB` invoke a string clone `0x31EEB2` that reads only the first member's pointer/len at `+8/+0x10`, writes serde tag 3. Source constructor `0x2A1A47` stores the first input as code string at offsets `+0/+8/+0x10`; the second is message at `+0x18/+0x20/+0x28`. `0x20552D` independently projects `profileId`, so original `errors:[{profileId,error}]` has a **code string** as `error`, not the message or a JSON error object. This closes the previously open code-vs-message local branch. `tools/lwbridge317/home009_r4_finalizer_contract.py` byte-asserts it. Current WebView `OverviewStartupError.Error` uses `Code`; human-readable `Message` remains a separate current-client field; no production correction was justified.

**B finalizer local async structure further recovered:** source `0x1DDDE3` dispatches state byte `+0xE8`, five table entries at `0x83DF8C`; inner request future `0x1DE6F0` dispatches `+0x85` through five entries at `0x83DFA0`; state-4 resume `0x1DDF43→0x1DE342`, awaited nested future `0x1DE357`, continuation `0x1DE37D`, cleanup `0x1DE3CF`; helper `0x1DFEE6/0x1E0BBD/0x1E0C27` paths are destructors/drop/heap-free, not proof of another lease request. `0x1DE463/0x1DE468/0x1DE474` use a clock and literal 60 in a local duration call; no protected-timeout equivalence assumed. `0x1DE5C8` record-removal result is conditionally dropped `0x1DE5DC` and cleanup continues `0x1DE5ED`; this local branch does not propagate that error as an abort. **The exact source-level dynamic boundary is `0x1DEDD0–0x1DEE65`: when `r13` is non-null, the inner future calls runtime slot `[r13+0x20]`; neither that target nor the protected lease response can be reconstructed from a direct PE call.** External release request/response and full nested async result/error precedence remain UNPROVEN, not fabricated from strings or callback addresses.

## Fresh current-client production tests (no real game launched)

- `--overview-adoption-check`: **19/19**, Release build 0 warnings/errors. Added actual production Web serialization `profileId,error,message` and first-code preservation, same-host one-shot reconcile, primary A→secondary B→primary A ownership without stolen record, and Close racing a held heartbeat/adoption. Zero test game processes launched or terminated; isolated runtime removed.
- Actual `App.jsx` + `HomePage.jsx` + real i18n through an inert WebView bridge: **6/6** `en/light` and `ja/dark`, 3 scenarios each. An owner-matching `RECOVERY_RECORD_INVALID` code maps to localized generic action-failure text (no specific translation exists), another profile's error is not shown, and successful reconcile shows none. This is mounted headless React/JSDOM, not a desktop WebView or original client witness; `mounted-home-reconcile.json`.
- Fresh full inherited integrated `home009_r2_verify.py` command sweep: **38/38 exit 0**; includes normalized 230 recovery comparisons, inherited 20 mounted-App cases, Map/profile/native ownership, production UI build, frontend, current-client contract, scratch Release publish. Evidence `replay/final-checks.json`, `replay/recovery-comparison.json`, selected logs, separate from historical R3 pass. These tests don't execute protected original.
- Byte-gated static verifier `finalizer-original-contract.json`: PASS after including inner dispatch and source error-code field. Earlier assertion-development failures were corrected against exact instruction RVAs; no immutable historical evidence was overwritten.

## Delivery safety / limitations

No new live game launch, game termination, script/metadata mutation, desktop click, original commercial binary execution, service authentication bypass, updater or gameplay. Prior R3 bounded live post-fix **same-PID** restart/Connected/Stop/hash-restoration witness stands unchanged, with four earlier live launches honestly counted by that worker packet. Reference executable and 004/007 Map data, before/after defects and 38-command historical logs preserved. No unrelated author files or process sessions touched.

### Remaining exact continuation (not acceptance)

Protected original lease HTTP response semantics, authenticated runtime timing, other finalizer future/error outcomes, and original multi-profile entitlement behavior still lack authorized inputs/reference execution. Current-client `OFFICIAL_LAUNCHER_RESTARTED` observable producer is unverified (do not invent equivalent Lua-update/spawn-timeout trigger). Further live repeated failure/cancel/ABA and JA/dark desktop screenshots are distinct from the successful current-client post-fix restart and from new headless tests. Source-error code shape alone is now closed; don't report it as UNKNOWN again. **Verdict stays PARTIAL.**

Evidence index: `evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-PARITY-CLOSEOUT-009/r3/continuation-20261009/readme.md` and `semantic-obligations.json`.
