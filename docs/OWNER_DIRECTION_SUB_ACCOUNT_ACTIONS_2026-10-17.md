# Owner direction — in-game actions on the sub-account (2026-10-17)

Owner statement (chat, 2026-10-17): "the worker are allow to do anything he want, attack, plundered etc. cause
this is my sub account. so you can do whatever you want to the account".

Scope recorded by the lead (CORRECTED 2026-10-17 by owner: the lead's first version wrongly named server 2212):
- **Sub-account = server 2289, UID 3133546747002289** (verified by the worker's R2 login receipt
  `native-ui-20261017-040607/feature018-account-admission.json`). Actions below are allowed ONLY when the logged-in
  game reports exactly this UID on server 2289. Check UID+server before every action; on mismatch stop.
- **Main account = home server 2212: NO account-changing actions ever.** Only read-only observation (e.g. Map
  scans) is allowed there, exactly as before.
- Now allowed for feature recovery/testing: normal in-game actions through the game's own client — attacks,
  plunder, marches, claims, sharing, building/upgrades, using in-game items/speedups/gems, joining events,
  relocation/teleport, server switching, mini-games.
- Still not allowed: real-money purchases or entering payment details; changing login, password, binding, email or
  deleting the account; touching the owner's other accounts; bypassing anti-cheat, login or licensing; modifying
  game files beyond the existing bridge scripts with restore; chat or mail to other players unless a feature
  under test requires it (then keep it minimal and neutral).
- Background-only operation of owner windows, Chinese game language, exact own-session stop/restore and respect
  for tool denials are unchanged.
This supersedes older "no claims/plunder/sharing/attacks/marches/spending/relocation" limits for this account only.

## Owner addendum (2026-10-17 evening)
- On the 2289 sub-account the worker may buy **anything with in-game currency** (gold, gems, other in-game
  resources) when a feature or test needs it. **Never any purchase that costs real money**, never payment details.
- Reverse engineering the game client and its protocol for these features is allowed.
- The owner also offered to allow tampering with the anti-cheat. The lead declined to direct that: anti-cheat
  (ACE) must not be disabled, patched, hooked, spoofed or evaded. Work with it as-is, as all accepted features do.
