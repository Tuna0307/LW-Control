# MAP-009 lead offline review

Source `56e472c8e94137ea47a4844c5bb9828d6df969b8`, same branch/PR #9.
**ACCEPT for the delivered offline source inventory, selected-body tests and
limited CPU grouping change. Not acceptance of a new native acquisition route,
live speedup, complete Map, or release candidate.**

Independent checks executed:

- `python -m unittest discover -s tools/map_009_offline -p 'test_*.py' -v`:
  11/11 PASS. Four groups execute selected decoded v24 instruction bodies through
  a bounded Python interpreter with explicit inert callbacks; seven are adversarial
  acquisition/restoration model groups. These are not native response witnesses.
- Imported `build_source_inventory.manifest()` read only and compared against the
  committed JSON: exact match, 26 Lua modules and 12 managed method entries.
- Desktop default Release suite: PASS, Home/Map/Auto/installed-name checks, no game
  launches. Source/remote verified at the submitted checkpoint; initial tree clean.
- Read the actual production delta: pending band blocks are grouped once rather
  than repeatedly filtered. Group boundaries/order are preserved for an unchanged
  pending set during the provider call; no protocol or camera restore change.
  No measured whole-scan seconds attributed to this CPU improvement.

The continuation correctly distinguishes known-point/UUID lookup from full unknown
population discovery. Some serializers carry serverId; that alone does not prove
remote service acceptance, correct network route, full scope or camera independence.
Native SendViewRequest/SendAoiRequest and response/cache paths are now concrete
research leads. Consecutive-region requestId/generation fields in the model are
adapter abstractions, not proven native server fields; they must not be fabricated
in a future native response.

The owner now permits background native testing while using Chrome, with no mouse
control or minimization. This supersedes the MAP-009 offline-only execution limit
for the new scoped task, not the prohibition on live claims/plunder/sharing/spending.
Follow `work-items/LWB317-MAP-BACKGROUND-ACQUISITION-010.md` on the same branch/PR.
First small native region/known-object trials, then qualified full-world comparisons.
Respect exact task ownership, script backup/restoration and background-only operation.
No merge/publication was performed. Main/v0.6.0 and MAP-008 final release findings
remain unchanged; offline review is not final functional approval.
