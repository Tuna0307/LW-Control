# Progress — LWB317-FUNCTION-ORIGINAL-LUA-RECOVERY-006

Starting HEAD/remote: `692fd40de1639261ca2c7dda7c7884c527786133`, clean.

## A — COMPLETE

Exact 0.3.17 encrypted LWBP v2 package/resource layout is hash-gated. Secure and
plain loader proxies plus bundle are identified. No standalone
package-key.envelope/bridge-scripts.dat/authorization.ticket is in the bounded
supplied roots.

Commit:
`a9196752ac4148c7b76b04d3af6e17df4da96277`.

## B — COMPLETE

0.3.17 loader/crypto/integrity/input contract is source-recovered, including
signed envelope, device-bound P-256 agreement, HKDF-SHA256, AES-256-GCM,
package SHA/build binding and direct module table with no compression.

Commit:
`65bde8312dae7ef96d5db0d86d7c73df8b4f4d18`.

## C — COMPLETE WITH EXACT INPUT BLOCKER

Exact package + proxies + bundle were carved into task-owned
`c-extracted/` and hash/PE/bundle verified. Original decrypt was not attempted
because no signed package-key.envelope is supplied and the matching persisted
CNG private key is owner state that this audit does not access.

Inert recovered-format tests: 8/8 PASS.

## D — ACTIVE

Continue static original-controller recovery for Treasure status/claim and Ghost
preparation. Determine whether any body copy exists outside encrypted module
plaintext; recover every host-visible lifecycle/order/result contract available;
prove the exact dependency for any remaining body semantics; report clone
differences only. No product code changes.
