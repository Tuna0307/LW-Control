# BLOCKER-BOUNDARIES-004 — independent lead review

Date: 2026-10-07
Worker checkpoint: `d900d8a548c072485cabc9b55d3dc5b07c4e6085`.

## Disposition

**ACCEPTED for the assigned offline/read-only boundary audit.**
This accepts BT-01 as partial evidence with the old missing-UUID rationale
rejected, BT-02 as valid preparation/execution separation, and BT-03 as partial
evidence with terminal correlation still UNKNOWN. It does not accept live behavior,
the protected Treasure controller, exact Ghost preparation transformations or
working Ghost execution. Source/local UIUX and accepted R3 behavior remain intact.

## Fresh checks

- A, B and C of `tools/lwbridge317/audit_map_blocker_boundaries.py`: `ok=true`.
- `tests/map_blocker_boundaries_lua_checks.py`: 4/4 PASS.
- `tests/map_provider_decoded_body_oracles.py`: 4/4 PASS.
- `tests/map_provider_semantics_lua_checks.py`: 3/3 PASS.
- Reference/current identities verified by the hash-gated audits. Starting HEAD
  and direct remote branch matched d900d8a5; starting checkout was clean.
- Diff from `8ee3b83f4812d017f813b6ac5fe6a4f12aec7332` contains audit tools/tests,
  evidence/review and work-item status only, with no production-source change.

Lua execution used the existing
`C:\Users\chimw\.codex\tmp\lwb317-recovery003-lua\Scripts\python.exe` environment.
No live/game/desktop action was taken.

## Independently reproduced correction: LR-GHOST-EXPIRY-005

The accepted host normalization checks positive expiry conditionally:
`expire > 0 && expire <= plunder` rejects a row; non-positive/missing expiry does
not. The exact-original audit B verifies the positive-expiry condition at
`0x134295`, `0x134298`, `0x13429B`. The current task constructor initializes zero.

The newly added Desktop helper instead rejects `taskExpireTime <= 0` and then
unconditionally compares plunder time against expiry. This is an over-strict
current helper, not an unrecovered original feature rule.

Fresh actual-packaged-method execution:

```text
pwsh -NoProfile -File evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-MAP-BLOCKER-BOUNDARIES-004/lead-review-2026-10-07/check-expiry.ps1
```

The script loads the existing Release Map/Desktop assemblies and invokes only
their pure static row-validation methods. It constructs no provider or store and
does no transport/game/runtime work. Assembly hashes and results are recorded in
`expiry-results.json` next to the script.

| Expiry | Packaged host | Packaged helper |
|---|---|---|
| Positive, after plunder | accept | accept |
| Zero | accept | reject |
| Negative | accept | reject |
| Missing | accept | reject |
| Positive, equal to plunder | reject | reject |
| Positive, before plunder | reject | reject |

The public preparer is still unavailable; this correction affects its internal
source-backed helper and is not an assertion that currently enabled live actions
are failing. Preserve this negative record when adding corrected proof.

SEMANTICS-003 is therefore **CHANGES_REQUIRED for LR-GHOST-EXPIRY-005**. The earlier
Ghost-before-Dispatch safety fix is credited by the focused passing tests, while
whole-delivery acceptance and unresolved provider claims remain separate.

## Which follow-ups to execute

Assign `LWB317-FUNCTION-MAP-EXPIRY-CORRELATION-005`: fix the proved expiry condition
and continue static managed FutureManager/send/receive tracing. Fixing parser
limitations to recover real method bodies may provide new correlation evidence.
Do not treat metadata names or injected raw fields as an end-to-end proof.

Do not create a production Treasure controller shell or a correlator which accepts
unproven UUID/future/point fields merely to show implementation progress. Those
may become justified after their semantic/identity producers are established.
The current helper remains an adaptation rather than exact private preparation.
All public action fences, owner live hold, historical evidence and acceptance
limits stay unchanged. An absent provider name is not an impossibility claim.
