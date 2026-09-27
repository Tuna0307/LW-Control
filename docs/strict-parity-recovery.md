# Strict one-to-one parity recovery directive

**Effective:** 2026-09-24
**Checkpoint:** `LWB-R8-097`
**Reference authority:** `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe`
**Verified SHA-256:** `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

This document supersedes every earlier project direction that allowed redesign, feature retirement, convenience behavior, performance-driven substitution, or owner-specific UI/product changes. Historical evidence remains valid at its recorded scope, but it is no longer product-design authority.

## End goal

The end product must reproduce LWBridge 0.3.1 one-for-one as a working program for every retained product feature, subject only to the explicit owner exception below. The reference executable decides what retained features are called, how they look, how they behave, what defaults they use, what errors they show, what requests they send, and what results they expose.

The implementation language, compatibility shims, internal process structure, or current-client adaptation may differ only when necessary to make the recovered original behavior work. Those internal differences must not intentionally change observable product behavior.


### Owner working/acceptance reset — 2026-09-27

For owner-facing status, **WORKING is binary**. A feature is WORKING only when the recovered original LWBridge 0.3.1 logic for that feature is the production path and that path has succeeded against the real current Last War client. A live-successful substitute, compatibility reconstruction, `EQUIVALENT_REIMPLEMENTATION`, old custom scanner, fallback, test harness, or output-equivalent path is **NOT WORKING** for this acceptance label.

For Home and Map there is **no production fallback acceptance**. Equivalent implementations may remain only as historical evidence, comparison/oracle tooling, or isolated research/test harnesses. They must not silently activate, mask failure of the recovered path, or be used to report the feature as working.

Immediate priority is **original Map acquisition engine first**, then any remaining original Home lifecycle gaps. Freeze unrelated auth/entitlement/secondary-feature recovery unless it is a demonstrated direct dependency of Home/Map recovery. Server season progression is not evidence that the installed client implementation is incompatible; compatibility changes require concrete current-client evidence.


## Owner direction — authentication dependencies may be recovered

The owner originally excluded the account/login/authentication feature family. On 2026-09-26 the owner explicitly superseded that restriction: authentication, authorization, entitlement, account/session and related state **may be researched, restored or implemented when doing so helps recover or make retained LWBridge functions work correctly against the live game**.

The priority remains retained product behavior, especially Home and Map. Do not invent credentials, hard-code roles/capacity, or substitute synthetic premium/admin state merely to make a button appear functional. Recover the original derivation and admission behavior first.

Standalone account-product work such as redesigning Login/Register/account management remains non-priority unless it is required by a retained feature or by the original state pipeline being reproduced. Historical reviews that called authorization owner-excluded remain valid descriptions of the scope at the time; new checkpoints may supersede those fences with stronger recovered evidence.

## Non-negotiable parity rules

1. Do not add a feature because it seems useful.
2. Do not remove a retained reference feature because it is inconvenient, slow, obsolete, gated, or difficult. Authentication/authorization/entitlement dependencies may be recovered when retained behavior requires them; standalone account-product work remains non-priority unless that retained-state pipeline requires it.
3. Do not redesign workflows, labels, tabs, defaults, timing, search semantics, scan semantics, navigation, storage behavior, or error handling without recovered reference evidence.
4. Do not substitute a different algorithm as the production acceptance path for a retained feature. An equivalent implementation may support research/comparison, but it does not make the feature WORKING; the recovered original logic must be the live production path.
5. Do not treat current Last War APIs, LW Atlas, community tools, or our previous implementation as product authority. They are research aids only.
6. When exact original bytes are recovered, preserve them byte-for-byte. Do not hand-edit extracted frontend chunks, icons, locale bundles, embedded assets, or other recovered payloads.
7. When original bytes are not yet recovered, recover the contract before implementing it. Unsupported behavior is a parity gap, not an invitation to invent.
8. A previous rebuild feature that is not demonstrated in LWBridge 0.3.1 is a deviation and must be removed or quarantined.
9. A previous owner-requested retirement or customization is historical only unless the same behavior exists in the reference. The 2026-09-26 auth direction supersedes the earlier blanket exclusion only for dependency recovery required by retained behavior.
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

A release is not one-to-one for the retained product scope while any required retained reference feature remains `DEVIATION` or `UNKNOWN`.

## Current correction of direction

The R7 period produced useful evidence, but parts of the rebuild drifted into solving our own design. Examples include the wide-FOV Map scan optimization, the Secret Task Quick Find product addition, direct-list routing choices, removal of original product surfaces, and other rebuild policies that were not first established as original LWBridge behavior.

Those checkpoints remain evidence of experiments and current-client capabilities. They are not parity authority.

From R8 onward, research order is:

**reference LWBridge -> recover exact implementation/contract -> map to current Last War -> implement only what was recovered -> compare against reference -> live-prove functionality.**

## Protected bridge scripts remain a retained recovery target

`bridge-scripts.dat` is part of the original implementation and remains important to exact parity. Under R8-097, however, immediate work stays on the original Map acquisition engine and then Home lifecycle. General package/auth recovery resumes when it directly answers a blocker for those surfaces or after they meet owner acceptance.

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
