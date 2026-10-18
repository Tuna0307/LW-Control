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

## Owner update (2026-10-17 night) — sub-account now on server 2288
- The owner moved the sub-account to **server 2288**. Gameplay/in-game spending is allowed only when the logged-in
  game reports **server 2288**. The previous UID was 3133546747002289; if the UID on 2288 differs, record it in the
  first session's admission receipt and lock all later actions to that UID + 2288. Server 2289 is no longer
  approved. Main account **2212 stays read-only**.
- Shield items need alliance coins the sub-account lacks: skip F6/F7/F8 shield testing for now.
- Black Market chest marker and Food House auto-clear: event not running — set aside (no work now).

## Owner update (2026-10-18) — sub-account now on server 2296
- The approved sub-account is now **UID 1183884595002296 on server 2296**. All worker gameplay, marches and
  in-game spending (gems allowed, never real money) only when the game reports exactly this UID + server.
  2288 and 2289 are no longer approved. Main account **2212 stays read-only**.
- **Attacks:** the worker may attack any target (monsters, resource nodes, other players' cities/troops) **except
  members of the sub-account's own alliance**. Check the target's alliance before every attack; if same alliance
  or unknown, do not send.
- **March speed-ups:** item and gem (diamond) speed-ups may be used on 2296 through the game's own confirm.
- **F9/F10 relocation** may be tried on 2296.
- **Never press/test Unlock Land Cell again.** The owner confirmed it works; before the tutorial is finished it
  breaks the game. No live Land Cell requests at all.
- Set aside until the owner says so: Space shield countdown, F6/F7/F8 shields, Alt+1–4 equipment presets.
- Mini Games tab is accepted as done for now (G works; Food House/Black Market wait for their events).

## Owner update (2026-10-18, later) — new sub-account UID 4799432499002212 on server 2212
- The approved test sub-account is now **UID 4799432499002212**, which is on **server 2212** — the same server as the
  owner's MAIN account. Lock every action to this exact **UID** (server alone is NOT enough). Any other UID on
  2212, including the owner's main account, stays read-only. 1183884595002296/2296 is no longer approved.
- **Player attacks: only players with no alliance (no guild).** Never attack a player who is in any alliance, and
  never target the owner's main account. Monsters and resource fields are fine. If a target's alliance cannot be
  read, do not send. The Map scan may be used to find guildless player cities.
- All other 2026-10-18 rules stay: in-game currency/gems allowed, never real money; never press Unlock Land Cell.
