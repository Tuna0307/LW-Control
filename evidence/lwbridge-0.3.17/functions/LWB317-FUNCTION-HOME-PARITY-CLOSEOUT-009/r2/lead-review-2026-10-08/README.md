# R2 project-lead evidence

Reviewed `79617a18d493fbb722f4cc2179279bff954472be`. Decision:
PARTIALLY_ACCEPTED / CHANGES_REQUIRED, full original Home/Map PARTIAL.

- `final-checks.json` and log files: independent rerun, 38/38 exit zero.
- `production-trace.json` / `recovery-comparison.json`: actual current 230-case
  recovery trace versus preserved normalized original-contract oracle. Unchanged
  normalization limits; not protected-original execution.
- `mounted-app-profiles.json`: actual canonical App, 20 headless cases.
- `reconcile-current.json`: independent actual production admission, 4/4 match.
- `refresh-error-negative.json`: actual production helper/observer + host/registry
  inverse, two cases/one mismatch; no process/transport/game/desktop. Helper
  fulfillment suppresses a completed refresh failure. Immutable negative.
- `original-refresh-error.json`: fresh hash-gated original instruction slice.
- `review-integrity.json`: reference and historical evidence preservation,
  temporary verification root cleanup and bounded proof classification.

Reproduce to a **fresh output path** after a fresh Release build:

```
dotnet run --project evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-PARITY-CLOSEOUT-009/r2/lead-review-2026-10-08/refresh-probe/RefreshProbe.csproj -c Release -- <fresh-result.json>
```

The probe records actual outcomes without changing production. After correction,
both helper outcomes must respect the source-backed refresh error. Do not replace
the historical negative or rewrite its expected contract. Original addresses and
finding details: `docs/reviews/2026-10-08-LWB317-FUNCTION-HOME-PARITY-CLOSEOUT-009-R2-LEAD.md`.

No lead product changes, new game launch/Stop, desktop capture/input/focus or
protected-service requests. The owner's running game was not operated or closed.
