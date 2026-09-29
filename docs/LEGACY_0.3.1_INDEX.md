# Legacy LWBridge 0.3.1 research index

This document exists so worker AIs can use old knowledge without confusing it with the current 0.3.17 target.

## High-value historical areas

### Frontend/UI

- `docs/ui-reproduction/`
- `docs/lwbridge-ui.md` archived snapshot under
  `docs/archive/lwbridge-0.3.1-management/lwbridge-ui.md`
- recovered frontend/assets under `evidence/lwbridge-0.3.1/`

### Map

- `docs/lwbridge-map-scan.md`
- Map-related reviews under `docs/reviews/`
- Map tooling under `tools/`

### Overview/Home/lifecycle

- `docs/lwbridge-overview-recovery.md`
- Overview lifecycle reviews/evidence

### Host/proxy/bridge

- `docs/lwbridge-architecture.md`
- `docs/lwbridge-injection.md`
- transport/proxy reviews and inspectors under `tools/`

### Auth/session/entitlement

- R8-09x through R8-14x reviews
- corresponding evidence under `evidence/lwbridge-implementation/`

### Last War loader/current-client

- R9 reviews
- `evidence/lastwar-loader/`
- `evidence/official-runtime/`

## Reuse rule

When a 0.3.1 finding appears relevant to 0.3.17:

1. cite the old finding as a hypothesis;
2. check the 0.3.17 artifact/runtime;
3. record whether it is unchanged, changed or absent;
4. create a new `LWB317-*` finding;
5. only then update current parity status.

Do not edit old chronological reviews to make them look current.
