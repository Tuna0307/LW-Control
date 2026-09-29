# LWB317-UI-002F — Hotkeys UI inventory

Date: 2026-09-29

## Scope and evidence state

Runtime Hotkeys observation is `BLOCKED` by the out-of-scope LWBridge
authorization boundary. This stage is a static `EXACT_BYTES` frontend inventory
only. No shortcut was executed and no gameplay/backend behavior was traced.

Reference SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

Primary recovered component:

`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/HotkeyPanel-XA8idRHB.js`

- recovered size: 7,801 bytes;
- SHA-256: `9BD7C11768693705391060D4F79119B5EA4A5229E042758165D3AD751A961BE6`;
- normal Hotkeys card definition begins near byte `0x89`;
- panel markup begins near byte `0xF77`;
- export is near byte `0x1E34`.

Navigation evidence is in the exact main bundle at byte `0x555DE` as the
`hotkeys` entry. Exact English copy comes from `en-BisSXcTB.js`; layout rules
come from `index-rIL9Fpht.css`.

Machine-readable evidence:

`evidence/lwbridge-0.3.17/ui/pages/hotkeys/static-inventory.json`

## Page heading and status

Exact heading:

- title: `Game Hotkeys`;
- description: `Fixed shortcuts that respond while the Last War game window is active.`

The component renders a status strip. Its exact disconnected copy is:

`Game disconnected. Settings are saved and will apply after connection.`

Static load/save errors are `Failed to load hotkey settings.` and `Failed to
save hotkey settings.`. Configuration is loaded rather than hard-coded by this
panel, so current switch values are runtime/persisted state and remain
`UNKNOWN`.

## Exact shortcut cards

The recovered normal Hotkeys card array is ordered as follows:

1. `Q / W / E / R` — `Attack target`
2. `A / S / D / F` — `Recall squad`
3. `Space` — `Shield countdown`
4. `F6 / F7 / F8` — `Use Shield`
5. `Alt + 1～4` — `Equipment preset`
6. `F9` — `Random relocation`
7. `F10` — `Alliance relocation`

Every card contains a toggle and `Enabled`/`Disabled` state label sourced from
the loaded configuration. The Attack target card additionally contains the two
checkbox labels `Use speedup items from the inventory` and `Buy speedups with
diamonds when items run out`, plus the exact warning that items are spent first
and diamond purchases spend real diamonds.

The Use Shield card has the exact warning `This shortcut consumes the matching
Shield item immediately.` Both relocation cards show `Relocation may consume an
item. This shortcut is disabled by default.` The last sentence is direct locale
evidence for that documented default; other effective toggle defaults are not
inferred.

The descriptive shortcut copy is recorded because it is visible UI. No listed
game action was triggered in this campaign.

## Exact styling

Recovered CSS establishes:

- `.hotkey-grid`: two equal columns with `10px` gap, collapsing to one column
  under the responsive rule;
- `.hotkey-card`: `1px` card border, card surface/shadow, `8px` radius,
  `12px 14px` padding and `8px` internal gap;
- card hover state changes card surface, border and shadow through shared theme
  variables;
- card headings use `14px`; descriptions use footnote size with `1.5`
  line-height;
- `.hotkey-binding`: blue/info treatment, `6px` radius, `6px 8px` padding and
  `600 12px/1 Consolas, monospace`;
- `.hotkey-card-danger`: danger-line border;
- `.hotkey-danger`: danger text/background, caption size, `6px` radius and
  `7px 8px` padding;
- `.hotkey-status`: muted footnote text, soft surface, `1px` line border,
  `7px` radius and `8px 10px` padding.

## Runtime gap

`BLOCKED` / not visually validated:

- rendered page geometry/theme after authentication;
- current hotkey enabled/disabled values;
- current Attack speedup options;
- live connected state and save-in-progress disabled states;
- live hover/focus behavior;
- page screenshot and pixel comparison.

No gameplay/backend function recovery was performed.
