# LWBridge 0.3.17 project status

**Date:** 2026-09-30
**Branch:** `research/offline-controller`
**Phase:** 2 — function recovery / Map Goal final static checkpoint

## Current state

Phase 1 static UI recovery has been accepted. The active target remains exact
LWBridge 0.3.17, and the only active Phase 2 function family is
`LWB317-RE-MAP-001` (Map Data).

The Map frontend/host surface, scan lifecycle, schema-v4 per-profile storage,
query/export/marks/history, navigation/action contracts, and scheduled
Dispatch/Ghost/Truck worker semantics have been recovered from exact 0.3.17
evidence and implemented in the versioned `LWBridge.Map317` plane. Normal
Desktop production uses that plane as the sole persistent Map authority at
`profiles/<profileId>/map-data/map-data.db`; legacy Map persistence is retained
only for explicitly isolated historical proof/replay modes.

Current Last War content-version 22 has passed the Map-specific static/source
compatibility revalidation in `LWB317-COMPAT-MAP-V22-001`. Source-backed adapters are wired for acquisition,
navigation, Treasure inspection, server-day, Dispatch alliance share, Dispatch
scheduled execution and Truck scheduled execution. Treasure claim/status and
Ghost preparation remain explicit fail-closed current-client provider gaps
rather than approximations. No production Map change was required for v22.
Fresh bounded Map-only live validation is next.

The prior 0.3.1 reverse-engineering history remains intact as legacy evidence
and is reused only where exact 0.3.17/current-client revalidation exists.

## Target identity

- Version: 0.3.17
- Size: 15,866,880 bytes
- SHA-256: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Explicit auth scope exception

The new reconstruction does **not** recreate LWBridge's original login/account/licensing/entitlement system.

If login/locked state is encountered:

- document the access boundary only;
- do not bypass it;
- do not reverse engineer credential/token/license/purchase protocols;
- do not implement the original login/account/licensing UI.

If a later in-scope feature proves it consumes auth-produced state, recover only the minimum downstream dependency contract that feature requires.

## Accepted 0.3.17 work

### LWB317-PM-001

Project-management/documentation reset for the 0.3.17 program.

### LWB317-UI-001A

Accepted static frontend-package baseline.

Established from the exact reference:

- Rust/Tauri 2 + Wry/WebView2 desktop host;
- Vite/React frontend;
- exact 24-record Brotli-compressed frontend asset table;
- all 24 frontend assets recovered and hash-locked;
- exact static top-level navigation keys/order and English labels;
- an Advanced navigation helper exists statically, but this exact 0.3.17 build
  passes `false` into its premium/admin visibility gate, so the effective
  visible navigation has eight entries and Advanced is statically forced off.

Project-lead acceptance review:

`docs/reviews/2026-09-29-LWB317-PM-002-review-ui-001a.md`

## UI campaign result

The project-lead-authorized UI campaign is `ACCEPTED`:

`docs/LOOP_CAMPAIGN_8H.md`

It completed or legitimately blocked each authorized **UI-only** stage:

- runtime/static shell and page inventory;
- common visual-system consolidation;
- clean separate 0.3.17 UI scaffold;
- shell/navigation reproduction;
- accessible page reproduction;
- visual comparison/fix pass (static contract fixes complete; direct post-auth
  reference/pixel comparison blocked by the auth boundary).

The separate static UI reconstruction remains at `src/LWBridge.UI-0.3.17/`.
Phase 2 Map function recovery is now active under the separate Map Goal; all
other function families remain blocked.

Queue authority:

`docs/LOOP_QUEUE.md`

Loop rules:

`docs/LOOP_WORKER_PROTOCOL.md`

## Desktop / live-validation state

Desktop-control tooling is authorized for the active Map Goal. The Goal also
authorizes bounded Map-only interaction with an **assistant-owned** Last War
session after a fresh process/session ownership check. It does not authorize
non-Map gameplay or auth/licensing/credential work.

At the v22 compatibility checkpoint, diagnostics report no running Last War or
LWBridge process. This is not reused as live-session ownership proof;
ownership/process state must be checked again immediately before live work.

## Historical research value

The 0.3.1 branch history contains deep work on:

- frontend recovery;
- host command inventory;
- Map acquisition;
- Overview lifecycle;
- auth/session/entitlement boundaries;
- bridge/proxy/native transport;
- Last War loader/runtime identity;
- current-client live testing.

Those findings may substantially accelerate future 0.3.17 work, but each reused claim must be revalidated against 0.3.17.

Historical auth research remains archived evidence, not a current reconstruction target.

## Next project-lead milestone

Finish the coherent Map action/current-client static checkpoint, then perform
the authorized bounded assistant-owned Map live validation. Record each fresh
successful path and every genuine state/provider blocker, finish the Map Goal
handoff/master-document closure, rerun repository verification, commit/push the
final Map checkpoint, and stop before another subsystem.
