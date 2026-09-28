# R8-127 — device fingerprint derivation and child-launcher stdin contract

Date: 2026-09-28

## Device fingerprint derivation

Read-only inspection of the verified `lwbridge-0.3.1.exe` closes the previously unresolved device-binding transform.

The host executes `reg.exe query HKLM\\SOFTWARE\\Microsoft\\Cryptography /v MachineGuid`, trims CR/LF from command output, tokenizes each line, case-insensitively identifies the `MachineGuid` row, drops the value-name and registry-type tokens, joins the remaining value tokens with spaces, and trims Unicode whitespace from both ends.

The host then formats the exact UTF-8 input:

`lwbridge:` + trimmed MachineGuid value

That byte string is processed by the inlined SHA-256 implementation at the recovered auth path. The 32-byte digest is rendered through the immutable alphabet `0123456789abcdef`, producing exactly 64 lowercase hexadecimal characters.

Therefore the original auth/device fingerprint is:

`lowerhex(SHA256(UTF8("lwbridge:" + trim(machineGuidValue))))`

No raw MachineGuid or machine-specific derived fingerprint is committed in this checkpoint.

## Original profile-launcher extraction

The embedded `lwbridge-profile-launcher.exe` was extracted correctly by converting launcher RVA `0xB72B52` through the outer PE section table. The resulting 684,544-byte file reproduced the authoritative SHA-256:

`8f42adb9ed678445425e529cdee8f12a097c63053f9d4de314a758a8dfe362de`

A first literal-file-offset carve did not match and was not executed. It was replaced by the correct hash-locked RVA-backed extraction.

## Child input channel — LIVE-PROVEN

The verified extracted launcher was first started with no input. It remained alive without stdout, consistent with waiting for an input channel, and was terminated without starting Last War.

The launcher imports standard-handle/console read primitives and contains a `stdin` marker.

A second bounded black-box probe sent exactly one invalid JSON object (`{}`) over standard input and then EOF. The launcher exited with code 1 and emitted:

`{"ok":false,"error":"DESCRIPTOR_INVALID"}`

No game process was started.

This live-proves that the outer host feeds the child launcher a JSON `LaunchEnvelope` through stdin and that the child returns a JSON result through stdout.

The previously recovered envelope fields remain:

- `descriptorJson`
- `launchProof`
- `gameLaunchTicket`

## Signed launch-material search

Searches of the original LWBridge roaming state, embedded runtime, bridge-runtime, and temporary working directories found no reusable plaintext `launchProof` or `gameLaunchTicket` token.

Historical original logs record `profile launch ticket selected source=primary_official` and later `profile launch cached launch ticket format=LWLT2 reusable=false`, but they deliberately do not persist the signed token itself.

## Saved credentials boundary

`auth-credentials.v2.json` currently exists under the original LWBridge roaming directory. The frontend API exposes native commands `auth_credentials` and `auth_login`; `auth_login` accepts explicit `username` and `password` arguments.

Credential contents were not read or recorded. The earlier credential-bearing Login action remains a ChatGPT tool-layer restriction and was not rerouted through `auth_credentials`.

## Current boundary

R8-126's proxy-side device mismatch is no longer a derivation mystery: the exact 64-lowerhex host fingerprint algorithm is source-locked.

The independent child-launcher path is now also narrowed to a concrete stdin/stdout JSON contract.

The next unresolved standalone-launch blockers are the valid signed `launchProof` and `gameLaunchTicket`, plus completion of the descriptor path/isolation values needed to advance a synthetic envelope from `DESCRIPTOR_INVALID` to the proof/ticket validators.
