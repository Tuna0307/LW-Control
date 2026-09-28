# R8-106 — recover device-key mode selection and propagation

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Scope:** original auth-side device-key operation selection and its propagation into the Tokio blocking worker; no network/auth execution.

## Result

R8-106 closes the remaining ambiguity from R8-105/R8-106 scratch: the original normal login path selects **mode 0**, which reaches the worker unchanged and dispatches to the **open-or-create** persisted ECDH key helper.

The normal supervisor path selects **mode 1**, which dispatches to the **open-existing-only** helper. The cleanup path uses **mode 2**, which reaches the delete/already-missing cleanup helper.

## Login mode 0

Within `login_future` (`0xF1C9E-0xF2FBD`):

- `0xF2A9F`: source wrapper word initialized to `0x0100`;
- `0xF2AA8`: `xor ecx,ecx`, therefore `CL=0`;
- `0xF2AAA`: jumps into nested-wrapper initialization;
- `0xF1E14`: nested wrapper state byte = `0`;
- `0xF1E1B`: nested wrapper operation byte = `CL = 0`;
- `0xF1E32 -> 0xF3E63`: execute wrapper.

Therefore normal login explicitly selects operation **0**.

## Supervisor mode 1

In the supervisor future (`0xF6F41-0xF7F47`):

- `0xF7C3B`: wrapper word initialized to zero;
- `0xF7C4B`: `CL=1`;
- `0xF7C57`: jumps into nested-wrapper initialization;
- `0xF75EC`: nested wrapper state byte = `0`;
- `0xF75F3`: nested wrapper operation byte = `CL = 1`;
- `0xF760A -> 0xF3E63`: execute wrapper.

Therefore the normal supervisor selects operation **1**.

## Cleanup mode 2

The cleanup-pending path independently calibrates the same wrapper layout:

- wrapper base is `rbx+0x10`;
- `0xF48F0`: writes word `0x0200` at wrapper `+0x80`;
- therefore wrapper state = `0`, operation = `2`;
- `0xF47BC -> 0xF3E63` executes the cleanup operation.

## End-to-end propagation

`0xF3E63` reads wrapper state at `+0x80` and operation at `+0x81`. The operation then propagates through `0x467D95 -> 0x467FBB -> 0x468519`.

At `0x46857C`, the operation is moved into `ecx` before `0x468582 -> 0x1BDEE9`. `0x1BDEE9` saves it in `bl`, allocates the Tokio task, and writes it at `task_base+0xC0`; because the returned task pointer is `task_base+0x80`, the operation resides at exactly `task+0x40`.

The poll chain later passes that exact byte unchanged:

`task -> task+0x20 future -> 0x8927F -> 0x1C8C49 -> future+0x20 == task+0x40 -> 0x318D61`.

`0x318D61` reads the byte and dispatches:

- mode `0` -> `0x39ED6B`: open-or-create persisted key, then export;
- mode `1` -> `0x39ECBD`: open existing persisted key, then export;
- other active modes -> `0x39F03D`: delete/already-missing cleanup.

## Reachability impact

The current machine's missing persisted device key is **not a lasting local blocker**. The authentic original normal login path is explicitly designed to recreate the named `ECDH_P256` key when it is missing and then export the public key for `devicePublicKey`.

The supervisor subsequently uses open-only behavior and therefore expects provisioning to have already occurred.

`package-key.envelope` remains absent on the current machine and remains the missing package-decrypt material. This checkpoint does not contact auth services, use credentials, create a live replacement key, export private material, synthesize an envelope, or inspect the protected secure-proxy envelope-consumer body.

## R8-005 terminology correction

R8-106 corrects the earlier helper labels:

- `0x39ECBD-0x39ED6B` = **open-only + export**;
- `0x39ED6B-0x39EE94` = **open-or-create + export**.

The underlying R8-005 provider/key/public-export contract remains valid.

## Verification

Tool: `tools/inspect_lwbridge_device_key_mode_contract.py`

Evidence: `evidence/lwbridge-implementation/2026-09-27-r8-106-device-key-mode-contract.json`

Reference SHA-256: `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

## Status

**RECOVERED CONTRACT.** Map remains **NOT WORKING**; this checkpoint removes the local persisted-key ambiguity but does not recover the package-key envelope or assembled Lua source.