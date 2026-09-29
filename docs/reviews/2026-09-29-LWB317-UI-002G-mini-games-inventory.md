# LWB317-UI-002G — Mini Games UI inventory

Date: 2026-09-29

## Scope and evidence state

Runtime Mini Games observation is `BLOCKED` by the out-of-scope LWBridge
authorization boundary. This stage uses static `EXACT_BYTES` frontend evidence
only. No mini-game or gameplay action was invoked or behavior-traced.

Reference SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

Primary recovered component:

`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/HotkeyPanel-XA8idRHB.js`

- recovered size: 7,801 bytes;
- SHA-256: `9BD7C11768693705391060D4F79119B5EA4A5229E042758165D3AD751A961BE6`;
- Mini Games-specific hotkey-card definition begins near byte `0x457`;
- shared panel markup begins near byte `0xF77`.

The exact main bundle exposes the `mini-games` navigation entry near byte
`0x55602` and instantiates the shared panel with `category: miniGames` near byte
`0x5B9DD`. Exact English copy is in `en-BisSXcTB.js`; layout rules are in
`index-rIL9Fpht.css`.

Machine-readable evidence:

`evidence/lwbridge-0.3.17/ui/pages/mini-games/static-inventory.json`

## Page heading

Exact heading:

- title: `Mini Games`;
- description: `Optional helpers for in-game mini games.`

The page reuses the Hotkeys status strip, including the exact disconnected copy
`Game disconnected. Settings are saved and will apply after connection.`

## Exact static cards

The shared component produces four Mini Games cards/areas from exact static
evidence:

1. `G` — `Frontline reinforcement`, with the description `Press G after a
   Frontline Breakthrough battle starts to add 5 soldiers each time.` and an
   enabled/disabled toggle from loaded hotkey configuration.
2. `Black Market Chests` — description `Mark the grand prize chest in the game
   after the shuffle ends.` and switch label `Mark grand prize chest`.
3. `Unlock Land Cell` — description `Unlock the next unopened city land cell.
   Each click processes one cell.` plus primary action `Unlock One Cell` and
   busy label `Unlocking...`.
4. `Food House Auto Clear` — description `Open the in-game board and visibly
   clear the current S4 Food House level without items or entering the next
   level.` plus Start/Stop control and dynamic progress/status labels.

The exact land-cell result strings include `Unlock request sent for cell #{id}`
and `Failed to unlock the land cell.`. These are possible UI result states only;
the action was not triggered.

Food House static status copy includes current level, confirmed move progress,
elapsed time, solving/executing text, daily/all-complete states, activity-ended,
UI-open conflict, manual/state conflict, solve-failed, unsupported-client and
start-failed messages. None of the live values or task transitions were
fabricated.

The locale also contains the related Black Market marker text `Grand prize`.
Because this shared panel does not directly render that marker, it is preserved
as related exact locale evidence rather than claimed as a runtime-observed page
element.

## Exact styling

The page reuses the Hotkeys card system from the exact stylesheet:

- `.hotkey-grid`: two equal columns with `10px` gap, collapsing to one column
  under the responsive rule;
- `.hotkey-card`: card border/surface/shadow, `8px` radius, `12px 14px`
  padding and `8px` internal gap;
- headings: `14px`; descriptions: footnote size with `1.5` line-height;
- `.hotkey-state`: caption size and weight `650`; enabled state uses success
  text;
- `.mini-game-actions`: aligned flex row with `8px` gap;
- Mini Games action buttons: minimum width `120px`.

## Runtime gap

`BLOCKED` / not visually validated:

- rendered post-auth Mini Games geometry/theme;
- current Frontline/Black Market toggle values;
- current land-cell state/result;
- current Food House level/progress/elapsed/running state;
- live connected/action-disabled state;
- live hover/focus states;
- page screenshot and pixel comparison.

No gameplay/backend function recovery was performed.
