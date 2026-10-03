# LWB317-UI-EQUIPMENT-CLOSEOUT-001-R1 evidence

This packet is the bounded correction proof for the Equipment Schemes R1 work item.
It preserves the parent worker packet and the independent lead failure packet.

`check-r1.mjs` executes the corrected current callbacks/effects together with the
recovered original rename acknowledgement callback. `r1-results.json` is its recorded
61-assertion result. `browser-results.json` records the mounted React 19 browser
lifecycle and English/light plus Japanese/dark acknowledgement matrix. The two
screenshots expose the changed pending and failed rename states and were inspected
after capture.

`validate-evidence.mjs` pins the reference executable, recovered frontend assets and
exact Activity/Alt/rename/error byte slices, current production/evidence hashes,
browser screenshots, the immutable submitted lead failure packet, and the seven
protected-WIP paths. Run it from the repository root:

`node evidence/lwbridge-0.3.17/ui/LWB317-UI-EQUIPMENT-CLOSEOUT-001-R1/validate-evidence.mjs`

The parent `source-manifest.json` deliberately retains its historical pre-R1
`Pages.jsx` hash. This R1 validator pins the corrected current hash instead of
rewriting historical evidence.

Limits: this packet uses deterministic offline preview fixtures and real mounted
React browser behavior. It does not prove a native Equipment provider, native
persistence/gameplay, physical connector-driven HTML5 drag, or original post-auth
pixel equivalence.
