# Independent Home error translation review evidence

LWB317-REVIEW-HOME-ERROR-001, 2026-10-02. Recommendation: **ACCEPT for focused source/local scope**.

`check-review.mjs` independently parses and executes the actual recovered 0.3.17
`Ir`, `Lr`, and `qr` functions and the current production `translatedError` and
Home render functions. Its 13 cases distinguish reversed/deduplicated code
priority, namespace order, plain-object versus `Error` message handling, custom
stringification, lowercase object codes versus lowercase message tokens, localized
generic fallback, and Japanese recovery-detail composition. `independent-cases.json`
is the saved record; `--verify-record` compares it with a fresh execution.

`browser-results.json` records bounded local-preview observations for English
`home-error-unknown` and Japanese `home-recovery-error-unknown`. The latter is
captured in `recovery-error-ja.jpg`. The reviewer-owned browser tab was closed;
the pre-existing port 4319/PID 62280 preview server was left running.

Reproduce the owned evidence from repository root:

```text
node evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-HOME-ERROR-001/check-review.mjs --verify-record
node evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-HOME-ERROR-001/validate-evidence.mjs
```

This evidence establishes focused source/local translation behavior only. It does
not establish original post-auth pixel parity, native error-object transport,
native/gameplay lifecycle behavior, or acceptance of the separately reviewed Home
channel/busy units.
