# LWBridge 0.3.17 project status

**Date:** 2026-09-29  
**Branch:** `research/offline-controller`  
**Phase:** 1 — UI parity / 8-hour Loop campaign active

## Current state

The abandoned bot rebuild has been deleted. The repository is back on the research branch and the 0.3.17 preparation baseline is complete.

The prior 0.3.1 reverse-engineering history remains intact and is treated as legacy evidence.

The active target is LWBridge 0.3.17.

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
- conditional Advanced entry exists statically but runtime visibility remains unknown.

Project-lead acceptance review:

`docs/reviews/2026-09-29-LWB317-PM-002-review-ui-001a.md`

## Active campaign

The project lead has authorized:

`docs/LOOP_CAMPAIGN_8H.md`

This campaign may proceed continuously through pre-authorized **UI-only** stages:

- runtime/static shell and page inventory;
- common visual-system consolidation;
- clean separate 0.3.17 UI scaffold;
- shell/navigation reproduction;
- accessible page reproduction;
- visual comparison/fix pass.

It must stop before gameplay/backend function reverse engineering.

Queue authority:

`docs/LOOP_QUEUE.md`

Loop rules:

`docs/LOOP_WORKER_PROTOCOL.md`

## Desktop state

The owner has re-enabled desktop-control tooling for the UI campaign.

This does not authorize:

- Last War/gameplay control;
- auth/licensing bypass;
- backend/gameplay function reverse engineering.

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

Review the completed 8-hour UI campaign, inspect every checkpoint/evidence set, accept or correct the UI baseline, and only then decide whether Phase 2 function recovery may begin.
