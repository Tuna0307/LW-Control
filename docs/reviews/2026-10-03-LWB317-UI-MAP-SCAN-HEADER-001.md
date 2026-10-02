# Scan timing and summary — project-lead implementation

Status: **COMPLETE implementation for focused source/local UI scope**;
independent returning-worker review follow-up. Not full Map/native/pixel parity.

Exact MapDataPanel-B4GXEND2.js, SHA-256
CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089,
provides time helpers at UTF-8 7885-8666, stored/progress expressions 32960,
timing caller 44467 and summary 48969. Production lacked timing, rounded
progress, used phase alone for completion, hid acknowledged zero progress and
added a counter row not present in that source. These focused defects are fixed.

Canonical header now renders start/end/duration, localized tooltips and ISO
attributes with source same-day formatting. Matching stored-run precedence,
publishing-first labels and unrounded clamped percentages are reproduced.
Optional-state presentation is mapped to actual provider acknowledgements;
the page clock runs while reading and is cleaned on stop/unmount. Source CSS
and catalogs are unchanged. Native action fences and accepted Map workflows
remain intact; no backend or gameplay provider was added.

270 actual original/current render comparisons pass across nine locales and
30 states; immutable baseline differs in 29 English cases. Six clock stages and
unmount pass. Four explicit offline preview states pass eight native-mode fences
and 28 rejected action calls. Fresh browser en/light and ja/dark timing/status/
progress/tab-navigation checks pass with zero captured errors/warnings. Saved
reading/completed screenshots were visually inspected.

Both prior picker checks, filter lifecycle/R1, request lifetime 38/0, navigation
17/0, interactions 38/38, integration 14/14 and maintained historical Map tests
pass. Historical records/assertions are preserved through documented adapters.
Canonical check/build/package and new evidence/WIP checks pass. Fingerprints:
b5ac5b5db4f33f3c890e6d8be6e164088c310fccfbca33cd62e7e1f08d62e88f /
3a10201330f71435d21077c57143ea780e32f4fafbb6a1fb5e58f82669530211.

Evidence: evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-SCAN-HEADER-001/.
Native MapScanState currently lacks UpdatedAt, whereas stored MapScanRun
provides timestamps; native delivery/timing has not been live validated. No
live scan/action or original protected runtime was used. Row-refresh/polling,
Auto configuration and start/export feedback remain separately assigned gaps.
Owned browser/Vite resources are closed; protected unrelated WIP is preserved.
