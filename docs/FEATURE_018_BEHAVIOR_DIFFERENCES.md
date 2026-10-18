# FEATURE-RECOVERY-018 owner direction and behavior differences

This ledger records deliberate owner-directed differences from the recovered 0.3.17 Hotkeys and Mini Games behavior. It does not by itself establish live feature acceptance.

| ID | Classification | Owner-facing behavior | Evidence and qualification |
| --- | --- | --- | --- |
| FEATURE-018-D01 | OWNER_DIRECTION | Remove march speed-ups from Hotkeys. The Attack card has no item or diamond speed-up controls, the separate current-march speed-up panel is removed, Q/W/E/R never consumes speed-up items or diamonds, and retired rapid commands/providers are unavailable. Existing saved settings that still contain `attackMarchSpeedupItem` or `attackMarchSpeedupDiamond` remain loadable; those properties are ignored. | R16 compatibility/UI/Desktop/Lua checks verify the retired settings and command paths cannot re-enable spending. The separate R16 live attack regression records one normal Q → A cycle and checks that item/diamond state does not change. |
