# City movement eligibility uses the current level

The owner reported that Drill Grounds, Hospitals and the Store could move in Last War but
could not be dragged in LW-Control. A read-only snapshot from the existing owner session
contained 132 buildings and marked only 61 movable; all four Drill Grounds and four Hospitals
were false. No requests were submitted to that session: the owner pressed Refresh and its
result was observed passively.

## Recovered caller contract

The installed Assembly-CSharp.rdl (SHA-256
`dec38a33a63ab2d29e02adbfa2032c4ab3f7f910443cba62a1f8895b809e80d9`)
contains CityBuilding.OnBeginLongTap, MethodDef 0x06001b95. Decoded IL at 0x000e loads
CSharpCallLuaInterface.CanMoveBuild; 0x0014 loads build_Id, 0x001a loads _level and
0x001f adds them before the call at 0x0020. The repository map_014_il_decode tools recover
the encrypted operands; the simpler metadata inspector alone does not resolve them.

Current Lua forwards that argument to BuildingUtils.CanMoveBuild, which checks the gate
and selected template's CanMove rule. LW-Control passed only building.itemId. That selected
the base/level-zero template rather than the same current-level row used by the game.

The correction passes integer itemId + level, failing closed on missing or invalid levels.
It preserves the native eligibility function, footprint, zone, gate, ownership and placement
validation. It does not mark every building movable or invent an override list.

## Qualification

Controlled regression uses the real producer with distinct base and current-level eligibility,
checks fixed buildings and gate restrictions, rejects invalid levels and sends no moves.
Existing controlled City producer and candidate checks also pass when run separately from
their known failing old native source-identity qualification. Four package tests pass; the
full Python suite reports 46 pass, nine existing source-identity failures and zero skips.
Those source qualification checks are retained.

With explicit owner approval, the owner app closed normally and stopped/restored its game.
A fresh isolated lifecycle with no gameplay controllers then queried only city_layout_snapshot_get
on the exact identified account. The genuine corrected snapshot reports 119 movable out of 132,
including all four Drill Grounds and all four Hospitals. The 58 newly movable buildings use
the native current-level rule. No actual relocation was sent or qualified.

The task console expected a wrapped response although the exchange returns confirmed data
directly, so its final consumer assertion exited nonzero. The original outcome is preserved.
The captured snapshot and successful exact Stop receipts were independently audited, including
original-file hashes and absence of recovery journals/game processes. Raw evidence stays in
ignored artifacts/city-movement-20261108. This is a read-only eligibility qualification, not
a live relocation or complete gameplay qualification for runtime 1106.
