# Strict one-to-one parity recovery directive

**Effective:** 2026-09-24
**Checkpoint:** `LWB-R8-001`
**Reference authority:** `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe`
**Verified SHA-256:** `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

This document supersedes every earlier project direction that allowed redesign, feature retirement, convenience behavior, performance-driven substitution, or owner-specific UI/product changes. Historical evidence remains valid at its recorded scope, but it is no longer product-design authority.

## End goal

The end product must reproduce LWBridge 0.3.1 one-for-one as a working program. The reference executable decides what features exist, what they are called, how they look, how they behave, what defaults they use, what errors they show, what requests they send, and what results they expose.

The implementation language, compatibility shims, internal process structure, or current-client adaptation may differ only when necessary to make the recovered original behavior work. Those internal differences must not intentionally change observable product behavior.

## Non-negotiable parity rules

1. Do not add a feature because it seems useful.
2. Do not remove a reference feature because it is inconvenient, slow, obsolete, gated, or difficult.
3. Do not redesign workflows, labels, tabs, defaults, timing, search semantics, scan semantics, navigation, storage behavior, or error handling without recovered reference evidence.
4. Do not optimize an original behavior by substituting a different algorithm unless the substitution is proven observationally equivalent to the reference and does not change any exposed contract.
5. Do not treat current Last War APIs, LW Atlas, community tools, or our previous implementation as product authority. They are research aids only.
6. When exact original bytes are recovered, preserve them byte-for-byte. Do not hand-edit extracted frontend chunks, icons, locale bundles, embedded assets, or other recovered payloads.
7. When original bytes are not yet recovered, recover the contract before implementing it. Unsupported behavior is a parity gap, not an invitation to invent.
8. A previous rebuild feature that is not demonstrated in LWBridge 0.3.1 is a deviation and must be removed or quarantined.
9. A previous owner-requested retirement or customization is historical only unless the same behavior exists in the reference.
10. Passing our own tests is not parity proof. The reference behavior remains the acceptance authority.

## What "not even a single byte" means here

The immutable reference executable and every extracted original asset are hash-locked and must never be modified. Any recovered payload that can be reused directly should remain byte-identical.

The rebuilt executable itself is not required to have the same PE hash as the Rust/Tauri reference because the reconstruction may use a different compiler/runtime and must remain compatible with the current Last War client. The required result is exact reference product behavior and byte-identical reuse of original recoverable assets, with no intentional user-visible deviation.

## Parity evidence classes

- **EXACT_BYTES** — bytes extracted from the verified reference and reused unchanged.
- **EXACT_CONTRACT** — behavior/fields/constants/control flow recovered from the verified reference with durable locators.
- **EQUIVALENT_REIMPLEMENTATION** — implementation differs internally but is proven to reproduce the recovered original contract.
- **DEVIATION** — rebuild behavior added, removed, altered, optimized, or customized without reference authority.
- **UNKNOWN** — original behavior is not yet recovered enough to reproduce safely.

A release is not one-to-one while any required reference feature remains `DEVIATION` or `UNKNOWN`.

## Current correction of direction

The R7 period produced useful evidence, but parts of the rebuild drifted into solving our own design. Examples include the wide-FOV Map scan optimization, the Secret Task Quick Find product addition, direct-list routing choices, removal of original product surfaces, and other rebuild policies that were not first established as original LWBridge behavior.

Those checkpoints remain evidence of experiments and current-client capabilities. They are not parity authority.

From R8 onward, research order is:

**reference LWBridge -> recover exact implementation/contract -> map to current Last War -> implement only what was recovered -> compare against reference -> live-prove functionality.**

## Protected bridge scripts are P0

`bridge-scripts.dat` is part of the original implementation and must be treated as a top-priority recovery target. The project already recovered substantial surrounding loader/crypto architecture, but not the complete plaintext package.

The goal is to recover the original script/package contents through permitted analysis methods, preserve the recovered bytes, identify every handler and contract they contain, and use those findings to replace guesses in the rebuild.

Historical environment denials such as SB-79 remain historical restriction records. Do not reroute an operation that an environment forbids. The underlying recovery question is still active and should be pursued through genuinely permitted methods, tools, artifacts, metadata, runtime observations, or other distinct analysis paths.

## Historical evidence policy

Do not rewrite or delete old reviews/evidence to make history look cleaner. Add superseding notes and current parity classifications. Historical R1-R7 acceptance matrices now prove only the reconstructed behavior they actually tested; they do not establish one-to-one completion unless separately tied back to the original reference.

## Delivery rule

Every future checkpoint must state:

- exact reference feature/function being recovered;
- original source locator and artifact identity;
- recovered contract or bytes;
- current implementation mapping;
- parity classification before and after;
- tests against the reference or extracted reference assets;
- current-client live proof where the feature requires it;
- remaining parity gaps.

Performance work is allowed only after parity is established, and only if it does not change observable behavior.
