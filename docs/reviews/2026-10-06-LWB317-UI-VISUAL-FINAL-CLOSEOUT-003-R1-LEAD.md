# R1 project-lead independent review

Date: 2026-10-06. Reviewed delivery: `0a6e0c35065f44ce5c8151a3a0e37ee9989e4638`.
Decision: **CHANGES_REQUIRED for one Equipment save-mode mismatch.**
The reported AFK cross-profile leak is corrected and accepted for source/local
scope. Global recoverable UIUX remains PARTIAL solely pending this new focused
finding and final acceptance. No native-function phase is started.

## Accepted R1 corrections and verification

The actual App supplies selected-profile identity to Squads and its AFK/Equipment
children. All four recovered AFK config lifecycles now have independent retained
profile owners. The original registry uses `[profile, scope]`; the actual current
App correctly follows A-edit → B-clean → A-retained → B-clean.

Equipment profile ownership is also justified by exact recovered Squad `fd`,
UTF-8 byte 176937, whose `equipment` config hook receives selected profile `l`.
The source audit satisfies the assignment's requirement not to change Equipment
ownership by assumption. Its draft/confirmation/saving/error lifetime is shared
per profile; selection/dialog/drag/toast/action state remains component-local.
The populated fixture's initial confirmed presets equal its draft presets, so
the new initializer does not introduce an initial dirty-state difference.

Independently executed by the lead:

- Canonical frontend check: PASS, all nine catalogs at 1,383 keys.
- Fresh build/package verification: PASS. Build fingerprint
  `3687f1b12facf940af3419c6a70cd7f9630eb831c754d543e4350755e0f3e12c`;
  package `0f74ce69349b51d9a8f5747f28fd209b92554916669518e382576413a8b73400`.
- Submitted rewrite-free final validator: PASS.
- Actual served host reconciliation `--verify --port=4441`: 257 assertions PASS.
- Fresh complete-App replay in `r1-lead-review/`: 275 assertions, 67 served source
  dependencies, 16 decoded captures, nine catalogs, 11 semantic mutations and
  zero console/page issues. Fresh readonly validator: PASS.
- All sixteen fresh screenshots are byte-identical to the R1 delivery; the lead
  manually inspected four representative profile/retention/narrow/locale captures.

The bounded independent reviewer reran exact-original profile contract and actual
browser ownership checks, plus 17 Equipment assertions. Those pass. Its report
and additional distinguishing case are under `r1-lead-review/profile/`. The
unfinished optional worker-2 audit is not counted as completed validation.
Fresh root runners are unchanged copies of the audited R1 runner; this does not
claim a new protected original complete-App pixel oracle.

## LR-EQUIPMENT-FLUSH-001 — later edits saved without an explicit Save

Recovered Squad `fd` helper `P`, UTF-8 byte **180274**, is exactly:

```js
async function P(e){u.state.edit(e,!1);try{return await u.state.flush(!1)}catch{return null}}
```

Current `SquadsPage.jsx` `flushPreviewConfig` invokes
`equipmentConfig.store.flush()` with its default argument `true`. The exact
recovered config store `T` distinguishes this argument: `false` acknowledges
the dispatched draft (and explicitly queued saves), while `true` continues
draining later dirty drafts even when no second Save was requested.

The reviewer and lead each executed the counterexample against an isolated
mounted **actual current SquadsPage**, current config hook and rendered rename/
HTML5 drop callbacks with a controlled deferred local adapter:

1. Profile A starts a rename Save; its acknowledgement stays pending.
2. Visit B, then return A. The profile-owned store remains pending; transient
   component action-busy state resets, as in the recovered component lifecycle.
3. Invoke the rendered drop handler to make a subsequent equipment move, without
   pressing Save again. Release the first rename acknowledgement.

| Settled state | Executed exact original T/P | Current mounted component |
| --- | --- | --- |
| Confirmed config | First rename draft | **Later moved draft** |
| Later move remains dirty | true | **false** |

Both browser runs recorded zero issues. This is an actual mounted current
consumer plus exact-original helper/store comparison, using inert rendered
callbacks and a controlled local acknowledgement. It makes no native/gameplay
or physical connector-driven HTML5-drag claim. It is a UI save lifecycle defect,
not a provider implementation dependency.

Lead reproduction: `r1-lead-review/lead/equipment-owner-adversarial.mjs` and its
result JSON. It is a byte-identical copy of the reviewer reproduction independently
executed by the lead; historical worker and earlier baseline records are unchanged.

Correct the Equipment flush mode to the recovered `false` semantics. Preserve
explicitly queued second Saves, error/Retry/Discard and retained profile ownership.
Do not suppress all later edits or clear profile stores to hide the difference.

## Continuation

Complete `docs/work-items/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003-R2.md`: this single
save-mode correction, the distinguishing inverse case and affected final checks.
The earlier six fixes, composition gaps and now-corrected profile ownership remain
accepted for their bounded source/local scope. No broad visual campaign is reopened.
Native/provider-positive execution, loaded assets, updater/OS behavior and protected
original complete-App runtime pixels remain separately unverified. This lead review
changes documentation/evidence only and preserves the reviewed production source.
