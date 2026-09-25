# R8-032 — restore fixed primary-profile guard

**Date:** 2026-09-25
**Reference:** verified LWBridge 0.3.1
**Scope:** restore the host-local `profile_primary_set` observable contract without inventing a primary-profile mutation that the reference does not perform.

## Authority

Primary authority:

- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- public `profile_primary_set` handler at VA `0x140158026`;
- native primary guard helper at VA `0x1403d3f81`;
- shared profile-ID validator at VA `0x1403dc23e`;
- original controller schema containing the partial unique primary index.

No frontend asset changed.
## Recovered behavior

The command requires `profileId`. Missing or wrong-type input uses `INVALID_REQUEST`, and the shared native profile-ID rules apply before registry lookup.

The native helper first reads:

```sql
SELECT id FROM profiles WHERE is_primary = 1
```

If the requested profile is already the primary profile, the command succeeds without changing `is_primary`, `updated_at`, selection, or other registry fields.

If the requested profile is not the existing primary profile, the helper distinguishes:

- missing profile -> `PROFILE_NOT_FOUND`;
- existing non-primary profile -> `PROFILE_PRIMARY_FIXED`.

There is no recovered `UPDATE profiles SET is_primary ...` mutation in this command.
## Schema invariant

The original controller schema includes a partial unique index equivalent to:

```sql
CREATE UNIQUE INDEX IF NOT EXISTS idx_profiles_primary
ON profiles(is_primary) WHERE is_primary = 1
```

R8-032 restores that invariant in the rebuild controller database.

Success returns the refreshed normal profile-list state, preserving the selected profile.

## Regression coverage

The deterministic checks prove:

- native primary uniqueness index exists;
- asserting the current primary succeeds;
- successful assertion does not modify `updated_at`;
- primary/selection remain unchanged;
- another existing profile returns `PROFILE_PRIMARY_FIXED`;
- a missing profile returns `PROFILE_NOT_FOUND`;
- an invalid profile ID returns `INVALID_PROFILE_ID`;
- missing `profileId` returns `INVALID_REQUEST`;
- backend routing reaches `profile_primary_set`.
## Validation

- Release desktop build: 0 warnings / 0 errors;
- Release checks build: 0 warnings / 0 errors;
- full deterministic suite: `ok=true`, `failures=[]`, exit code 0;
- no frontend file changed.

Final generator, JSON, diff and staged-diff checks are recorded in checkpoint evidence.

## Still intentionally missing

R8-032 does not restore `profile_enable_set`, `profile_select` focus behavior, profile create/delete behavior, or launcher/game-instance ownership.

Trade Station was also re-audited while selecting the next target: `trade_station_configure` calls protected method `configureTradeStation` with a 5,000 ms timeout, so no fake local-only implementation was added.
