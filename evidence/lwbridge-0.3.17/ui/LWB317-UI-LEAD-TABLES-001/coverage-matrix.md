# Map table correction coverage

All rows below are source/local checks, **AWAITING_REVIEW**, not original pixels
or new live proof. Exact source factories/helper locators are in map-table-results.json.

| Table | Source/local recovered coverage | Browser coverage |
|---|---|---|
| City | Eight column metadata/value/width cases, HP/level/expired shield/time/name fallbacks, translated mark/table labels | Positive 50-row page; headers/widths/native action fences |
| Resource | Five columns, text-key fallback, level/time; existing known/occupied adapter and unknown dash | Positive rows; known states and unknown first row |
| Monster | Five columns, name fallback, level/distance/time | Positive rows/headers/widths |
| Truck | Nine columns, cap/state/countdown, quality/Reindeer, eligibility, conditional reward sort/order/count/description | Protected/full/ready, eligible/ineligible, item sort then clear |
| Train | Seven columns, Live Target, alliance/quality/power/time, reward order/count/description | Positive rows/headers/widths/rewards |
| Secret Task | Nine columns, time/count-derived states, special quality, UUID/time/count eligibility, rewards/time | Pending/full/protected/expired/ready and eligibility |
| Ghost Ops | Same recovered shared nine-column table and task-status/selection helpers | Positive rows and eligibility; distinguishing states checked in actual JSX/source harness |
| Treasure | Twelve columns, type/supplies/name resolver, charging %, world/player/reason precedence, counts/time/owner/alliance | Charging/claimable/depleted; unclaimed/claimed/foreign; English light and Japanese dark |

All eight kinds have original/current factory comparisons in nine languages;
actual JSX assertions pin rendered headers, widths, table labels, reward counts/
descriptions and disabled native row controls. Missing assets stay placeholders.
The source clock is reproduced for these time-sensitive tables only; original
scanning/scheduling clocks are not claimed complete by this task.

Remaining source-recoverable gaps for later UI assignments:

- Original row action rendering: Follow/Jump text and keys, invalid-position dash,
  player mark SVG/tooltip/tracker state, row keys and selection identities.
- Original per-kind item/quality/completion/filter state and complete query UI
  interactions; current item state is still shared between Truck/Train.
- Scheduled Plunder groups/results/actions, task scheduling and claim controls.
- Other Map shell/loading/error/detail/keyboard/hover states not added by this unit.
- Native game-text/image integration; exact helper accepts supplied text, but the
  production bridge does not currently supply this dictionary.

Original post-auth runtime/pixel comparison stays BLOCKED. Map gameplay providers,
positive-row live-state blockers and dedicated Map campaign acceptance are not
changed. Home busy and Trade 003E were rechecked by their author and still require
independent review. No percentage or full UI completion claim is made.

Reproduce from repository root:

```powershell
node evidence/lwbridge-0.3.17/ui/LWB317-UI-LEAD-TABLES-001/check-map-tables.mjs --verify-record
node evidence/lwbridge-0.3.17/ui/LWB317-UI-LEAD-TABLES-001/validate-evidence.mjs
```
