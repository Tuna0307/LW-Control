# LWB317-UI-HOME-PREFERENCE-LIFETIME-001

Status: COMPLETE / ACCEPTED for both assigned source/local preference units,
2026-10-04. A Auto Launch remains accepted at c0d9082. B Automatic Reconnection
was finished by the lead after worker interruption, source-backed event correction,
mounted profile proof and independent follow-up review. See the dated A/B reviews
and unit B packet. Native persistence and protected-original pixels remain separate;
global UI remains PARTIAL. Next: broader offline source-rendered visual comparison.

Historical dispatch2026-10-04:001A was assigned as a medium worker unit covering only
Open games at startup / Auto Launch. Automatic Reconnection remains subsequent;
the parent is PARTIAL. The lead must not concurrently edit the assigned
App.jsx/HomePage.jsx preference scope while that worker is active.

Goal: reproduce Home preference editability and visible value lifetime from
LWBridge0.3.17, including edits while previous work is unfinished. Recover the
producer/consumer chain before changing callbacks. This is phase2 local UI,
not permission to start native/gameplay implementation.

Inputs:AGENTS.md, AI_WORK_PROTOCOL, current App.jsx/HomePage.jsx,
previewConfig.js, existing Home error/root-ack/busy packets, and
LWB317-UI-OFFLINE-VISUAL-001/home/render-results.json plus
preference-counter-evidence.json. Reference main SHA44C4E4…524C6.
qr336694 passes no disabled prop to Bn213332; local setter248381 writes through
Sr246842 then updates state; reconnect It366485 edits and flushes a selected-profile
flag draft. Current busy clauses and acknowledgement-before-state differ.

Required recovery:exact local preference storage key/default/change ordering;
flag draft initial/edit/flush/confirmation/error/Retry/Discard state; selected-profile
ownership; concurrent edit/poll/ack ordering. Do not infer that all Home preferences
share one storage or transport contract. Native adapter differences must be explicit.

Allowed changes:canonical Home/local frontend preference state and existing
callback adapters necessary for the recovered UI. Existing command names and
native result contracts must be retained or changes separately justified; no new
provider/native command/gameplay, auth/account/login or fake success. Preserve
root/action error independence, root picker/cancellation/ack lifetime, profile
injection and missing-profile pre-dispatch rejection, source/local profile selection,
poll ownership, unavailable-provider fences and seven protected paths.

Acceptance:immutable actual baseline distinguishing save-disabled and value
timing; exact original/current callback/store proof; actual mounted render/control
tests across pending first write, second edit, success/error, stale/obsolete ack,
profile replacement and polling; affected Home/App/shared draft checks; real
offline UI with inert controlled deferred responses; canonical package, diff,
protected-WIP and independent review. Changing only disabled predicates is not
a complete fix. Record exact limits and commit/push a coherent reviewed checkpoint.
