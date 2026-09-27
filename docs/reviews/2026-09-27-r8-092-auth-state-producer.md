# R8-092 — recover the original public AuthState producer

> **Follow-up (R8-093, 2026-09-27):** the persisted SessionV2 source is now substantially recovered: exact seven-field schema including `version`, required `version=2`, v2-first restore, centralized supervisor ownership, restored token/metadata decode boundary and metadata-to-role projection. The remaining source gap is below/around that persisted session: device-key acquisition, ticket/envelope lifecycle, service mutations and entitlement/capacity refresh.

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Status:** RECOVERED CONTRACT
**Scope:** host-side authorization-state object, phase emitters, publication, refresh ownership and grace timing. No credentials, password material, private keys or live auth requests are used.

## Result

R8-091 recovered the command-side authorization admission and proved that retained provider requests must not invent `premium/admin` fields. R8-092 closes the next dependency: the original host's public authorization-state producer itself.

The original keeps one shared 0xC0-byte AuthState object. State-mutating paths replace fields in that shared object and publish the refreshed object through:

`bridge://auth-state`

The public `auth_state` command is a snapshot read of this shared state. It does not directly call any of the phase emitters. If the shared state cannot be obtained, its exact public error is:

- code `STATE_UNAVAILABLE`;
- message `authorization state is unavailable`.

This means retained commands should consume one shared produced state; they should not independently manufacture authorization flags.

## Exact public AuthState schema

The host serializer at `0x14023E99B` writes exactly eight fields in this order:

| # | Field | Internal offset |
|---:|---|---:|
| 1 | `phase` | `+0x00` |
| 2 | `username` | `+0x18` |
| 3 | `accessRole` | `+0x30` |
| 4 | `watermarkTraceCode` | `+0x48` |
| 5 | `expiresAt` | `+0x60` |
| 6 | `lastHeartbeatAt` | `+0x78` |
| 7 | `lockedUntil` | `+0x90` |
| 8 | `errorCode` | `+0xA8` |

The exact phase vocabulary recovered from the producer family is:

- `checking`;
- `authorized`;
- `grace`;
- `locked`;
- `signedOut`.

Do not mix these with the separate multi-entitlement phase vocabulary recovered elsewhere (`initializing/authorized/grace/restricted`).

## Phase constructors

### authorized

The `authorized` emitter at `0x1402391D4` copies the successful identity projection into the shared AuthState:

- username;
- accessRole;
- watermarkTraceCode;
- expiresAt.

It refreshes `lastHeartbeatAt` through the original formatter/helper at `0x14023F244`.

It explicitly clears:

- `lockedUntil`;
- `errorCode`.

R8-092 does not guess the textual timestamp format produced by `0x14023F244`.

### grace

The `grace` emitter at `0x140239756` changes the phase to `grace` and installs the current error code. It does not rebuild the identity projection. Existing username/role/watermark/expiry/heartbeat values therefore remain owned by the prior successful authorization state.

This is why grace must be represented as a transition of the shared state rather than as a synthetic new login result.

### checking

The `checking` emitter at `0x140238D8B` changes the phase to `checking` and clears the current public `errorCode`. It otherwise preserves the shared AuthState while the supervisor performs revalidation.

### signedOut

The normal `signedOut` emitter at `0x14023855D`:

- sets phase `signedOut`;
- clears the username slot;
- resets `accessRole` to exact string `normal`;
- clears watermarkTraceCode;
- clears expiresAt;
- clears lastHeartbeatAt;
- clears lockedUntil;
- clears errorCode.

`auth_logout` calls this exact emitter.

There is also a session-error transformer at `0x140238289`: when the incoming error code is exactly `SESSION_INVALID`, it chooses phase `signedOut`; for other errors in that path it chooses `locked`. That transformer is not identical to the normal logout reset, so the two paths must not be conflated.

### locked

The locked-state emitter family uses exact phase `locked` and exact default role `normal`.

Recovered dedicated terminal errors include:

- `NETWORK_GRACE_EXPIRED`;
- `ACCOUNT_EXPIRED`;
- `SESSION_INVALID` through the session-error transformer.

Other locked/error paths pass through their current error value; R8-092 does not invent an exhaustive service-error enumeration.

## Producer ownership and transitions

Direct call mapping gives the following state ownership:

- login response ingestion reaches the common login/auth projection at `0x140239937`;
- renew can publish `authorized` or a locked/error state;
- heartbeat can publish `authorized`, `grace`, or a locked/error state;
- the heartbeat/session supervisor publishes `checking`, routes session errors through the `SESSION_INVALID` transformer, and can publish `signedOut`;
- explicit `auth_logout` publishes the normal `signedOut` reset.

The public `auth_state` command does not perform any of those mutations itself.

## Exact grace clock

The heartbeat transition owns a separate numeric grace clock at internal state offset `+0x420`. This is not the public `lastHeartbeatAt` field.

On the recovered grace-eligible error path:

1. if the internal grace clock is zero, the host stores the current numeric time from `0x14023F1C0`;
2. the separate eligibility predicate at `0x14023CB51` must succeed;
3. current time is sampled again;
4. the host computes `elapsed = now - graceStartedAt`;
5. grace is admitted while `elapsed <= 900000` ms;
6. otherwise execution leaves the grace branch and proceeds to the locked/error path.

This is exactly equivalent to the R8-091 admission-side comparison:

`now - graceStartedAt < 900001`

The two-deadline inputs consumed inside `0x14023CB51` are source-locked, but R8-092 does not assign unsupported field names to them.

## Relationship to retained feature fences

R8-092 removes the previous “unknown AuthState shape/phase producer” blocker.

It does **not** yet justify enabling retained auth-dependent actions live. The remaining implementation dependency is the state source beneath this producer:

- persisted `SessionV2`;
- token/metadata recovery through the original secure-storage/device-key path;
- service renew/heartbeat results;
- entitlement/accessRole projection into the successful authorization result;
- startup scheduling and persistence semantics around those sources.

Those pieces must be recovered without inventing credentials, role, capacity or entitlement.

## Verification

Hash-gated verifier:

`tools/inspect_lwbridge_auth_state_producer.py`

Machine-readable evidence:

`evidence/lwbridge-implementation/2026-09-27-r8-092-auth-state-producer.json`

Reference SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

No production behavior changes in this checkpoint.
