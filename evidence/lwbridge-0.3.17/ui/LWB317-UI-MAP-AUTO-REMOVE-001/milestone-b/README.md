# LWB317-UI-MAP-AUTO-REMOVE-001 milestone B

Milestone B compares the actual recovered 0.3.17 Auto card with the current
`MapDataPage.jsx` Auto card across all six assigned state families in English and
Japanese. The fixed clock is `2026-10-04T09:15:00Z`, the timezone is
`Asia/Singapore`, the recovered CSS stays hash-exact, and current CSS remains in
the production `reference.css` then `styles.css` order.

The 12 source-render pairs are:

- recovered default;
- configured enabled/waiting with servers 8, 15 and 120;
- Auto running;
- Manual scan reading;
- offline;
- one remaining Auto scan type (`treasure`).

All 12 pairs have exact presentational views, exact scoped markup and zero raw
differences. The dedicated source pins cover the recovered type list, last-type
disabled predicate and Run-now disabled predicate, plus the current
`emitAutoConfig`, `addAutoServers`, `toggleAutoType` helpers and corresponding
predicates.

The Run-now predicate is recovered/current equivalent in the observed states:
configured waiting is enabled; default, Auto running, Manual reading and offline
are disabled. The one-type case remains otherwise runnable and protects its sole
selected type by disabling that checkbox. The disabled checkbox handler is not
artificially invoked.

`replay-controls.mjs` drives the actual rendered server input, Add button, Enter
handler and chip remove buttons through inert parent callbacks in EN and JA. Both
original and current produce the same order:

- Add `8, 15, 15, 120; 0 100000 abc` to initial server 8 -> `8,15,120`;
- Enter `120，7 15;9` -> `8,15,120,7,9`, with `preventDefault` and cleared draft;
- remove server 15 -> `8,120,7,9`.

No native/gameplay mutator is produced by this replay and Run now is never
clicked. No additional presentation defect was proven in the six state families,
so milestone B changes evidence only.

Replay:

```powershell
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-REMOVE-001/milestone-b/compare-states.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-REMOVE-001/milestone-b/replay-controls.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-REMOVE-001/milestone-b/validate-evidence.mjs
```

This remains source/local UI evidence. It does not invoke scan, Run now, server
jump, native provider or gameplay actions and does not claim broader Map parity.
