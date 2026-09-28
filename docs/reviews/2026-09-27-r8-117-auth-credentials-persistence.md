# R8-117 — recover original auth-credentials persistence and restore ownership

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Status:** RECOVERED CONTRACT / CURRENT-MACHINE NEGATIVE
**Scope:** original host credential-file ownership, v2-first restore, legacy migration, DPAPI boundary, and current-machine filename reachability. No credential values, password values, private keys, browser password stores, or live auth requests are read.

## Result

R8-116 proved the signed-out frontend calls `auth_credentials` to prefill empty username/password fields. R8-117 recovers the host source behind that command and closes the possibility of an undiscovered alternate credential store.

The auth-service constructor owns four credential/session paths under its persistent auth-data root:

- `auth-session.json`;
- `auth-credentials.json`;
- `auth-session.v2.json`;
- `auth-credentials.v2.json`.

The same constructor separately owns the runtime-material root used for `authorization.challenge`, `authorization.ticket`, `package-key.envelope`, and `build.manifest`.

## Credential restore

Credential restore is function `0x14023A24C-0x14023A60A`.

It is v2-first:

1. reads the active v2 credential path through service offsets `+0x120/+0x128`;
2. deserializes the typed v2 credential object;
3. requires exact version byte `2`;
4. decrypts the persisted protected password through secure-storage helper `0x14039F5CE`;
5. on a successful v2 decode returns username + plaintext password to the command result object.

If the v2 path is unavailable or unusable, restore falls back to the legacy credentials path at service offsets `+0xE0/+0xE8`.

The legacy JSON object is source-locked to exact fields:

- `version`;
- `username`;
- `encryptedPassword`.

The legacy version check is exact version `1`. A valid legacy object decrypts `encryptedPassword` through the original secure-storage compatibility helper and then invokes the v2 save/migration path.

Missing/invalid credential data resolves to the empty credential result; it does not synthesize credentials and it does not query a second Windows credential store.

## Credential save / migration

Credential save/migration is function `0x140239D61-0x14023A111`.

The original path:

1. validates the incoming username/password request material;
2. protects the password through host helper `0x14039F4C5` (`CryptProtectData` + Base64, already source-locked by earlier R7/R8 evidence);
3. builds credential version `2`;
4. serializes exact fields through serializer `0x14023E7EE-0x14023E8DA`:
   - `version`;
   - `username`;
   - `encryptedPassword`;
5. writes only the active v2 credential path from service offsets `+0x120/+0x128` through shared persistence helper `0x14023D8FC`.

Failure to protect password material is normalized to exact public code `SECURE_STORAGE_UNAVAILABLE`.

The legacy credential file is therefore a migration input, not a separate live secure-store backend.

## Current-machine result

A complete filename search under `C:\Users\chimw` for the original persistence names found zero accessible matches for:

- `auth-session.json`;
- `auth-credentials.json`;
- `auth-session.v2.json`;
- `auth-credentials.v2.json`;
- `authorization.ticket`;
- `package-key.envelope`;
- `device-key-cleanup.pending`.

The live reference frontend was also observed in signed-out state with browser validation focusing the empty username field after Login submit. No edit-control value was read. The persisted CNG device key remained absent, proving native login had not executed.

Because the original `auth_credentials` source has no alternate credential backend, there is no legitimate stored-login route currently available on this machine.

## Impact on Map recovery

This closes the repeated owner-login branch as a research dependency. Do not keep asking the owner to type credentials and do not read browser/password-manager data, fabricate auth state, synthesize credentials, or bypass original admission checks.

The authentic source blocker remains unchanged:

`valid original authorization -> package-key.envelope -> authentic package decrypt -> assembled Lua source -> XluaBridgeMapScanTick`.

Without legitimate persisted credentials/session state, further independent work should return to static/offline recovery of that source/package boundary or genuinely new surviving artifacts, not UI login repetition.

## Status

**MAP: NOT WORKING. HOME: NOT WORKING. WHOLE LWBRIDGE: NOT READY.**
