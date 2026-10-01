# LWB317-UI-HOME-ERROR-002-R1 evidence

This evidence covers only the root-status acknowledgement/polling correction.
`check-root-acknowledgement.mjs` parses the exact recovered frontend and current
`App.jsx`, then executes the extracted production root effect and callbacks through
the real `createBackendBridge` with synthetic local response envelopes.

The pinned original source is SHA-256
`44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.
The checker confirms `Jt` at UTF-8 byte 367489, the initial
`Ht().then(Jt).catch(...)` request at 369540, the selected-profile effect at
369239 with dependency `[r.selectedProfileId]`, and the periodic status/proxy
poll at 369979. Current production uses `backendBridge.profileId` as the existing
bootstrap-selected profile, requests root status on that profile effect, and no
longer includes `game_root_status` in `refreshStatus`.

Fourteen synthetic scenarios cover successful, failed and deferred initial root
acknowledgement; profile gating; timer inventory; cancellation surviving a later
periodic refresh; invalid/failed/valid picker outcomes; post-selection root-status
failure; reconnect profile injection/missing-profile rejection/deferred ack;
preference error isolation; and native-unavailable fencing. Successful initial and
post-selection acknowledgements both update root status and clear only the root
error. `gameActionError` remains independent.

`browser-results.json` records a fresh local recheck of `home-errors-both` and
`home-error-action-missing-root` on the pre-existing port 4319 preview server.
The rendering remains unchanged and no console errors were captured. No native
control was invoked.

Reproduce from the repository root:

```text
node evidence/lwbridge-0.3.17/ui/LWB317-UI-HOME-ERROR-002-R1/check-root-acknowledgement.mjs --verify-record
node evidence/lwbridge-0.3.17/ui/LWB317-UI-HOME-ERROR-002-R1/validate-evidence.mjs
```

The server-jump manual refresh still calls `refreshStatus`; because root status is
now source-shaped outside that refresh callback, a server-jump refresh does not
re-read `game_root_status`. Root status is refreshed on the selected-profile effect
and after a valid folder selection.

Limits remain synthetic/local for callback transport and browser-preview rendering.
No native picker, Last War process, live persistence, protected original runtime or
original pixel comparison was used.
