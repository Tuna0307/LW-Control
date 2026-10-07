# Checkpoint A — exact-original payload inventory

State: **COMPLETE**.

Starting checkout was clean at
`692fd40de1639261ca2c7dda7c7884c527786133`, equal to
`origin/research/offline-controller`.

## Exact 0.3.17 package

Reference EXE:

- path: `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`
- SHA-256:
  `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`
- file size: 15,866,880 bytes
- PE overlay: 0 bytes

One valid embedded LWBP v2 package is present at raw `0x8EED88`
(outer-host RVA `0x8EFF88`):

- size: 1,418,098 bytes
- SHA-256:
  `C215B5AA87619F547F2D999E58D1A1CB4780B49FEC7040362732257D86D4FA82`
- version: 2
- build ID: `yoWZOvy8GWpsTNLrcZGcYQ`
- nonce: 12 bytes at package offset 34
- ciphertext length: 1,418,032 bytes
- ciphertext begins at package offset 50
- final tag: 16 bytes
- exact size relation:
  `12 + buildIdLength + 12 + 4 + ciphertextLength + 16`.

This is the actual encrypted `bridge-scripts.dat` payload. It is not frontend
JavaScript, current-game Lua, or recovered controller plaintext.

The 0.3.1 package-layout *shape* remains a useful hypothesis and matches this
outer binary shape, but the 0.3.17 package identity/build/ciphertext are different.
The old package hash/build cannot be reused.

## Exact embedded resource layout

The package ends exactly at raw `0xA490FA`, where the outer host contains this
161-byte resource-name sequence:

`bridge-scripts.dat`
`xlua-proxy-secure.dll`
`xlua-proxy-plain.dll`
`xlua-proxy-bundle.json`
`xlua-legacy.dll`
`lwbridge-profile-launcher.exe`
`lwbridge-multi-hook.dll`
`lastwar-texts`

Immediately after the name block:

- secure proxy raw `0xA4919B`, 619,008 bytes,
  SHA `AE8BBA866DF80E9C924923D1F59D49E824C784C8BA9B47780FE2F58305E00A86`;
- plain proxy raw `0xAE039B`, 620,544 bytes,
  SHA `266129C6C92F3AE89001493D61CCFDF4F5DAC1FE66CFF4FBB4799E6C01A6C902`;
- proxy bundle JSON raw `0xB77B9B`, 590 bytes,
  SHA `D6B3C71208A658D3D4FF05AB1AF2E5ABEE3F5BAB05839E15D6C97F6D58CFFD1C`.

The bundle JSON independently names both proxies and its recorded proxy SHA-256
values exactly match the embedded files. This source-backs the secure/plain
classification rather than inferring it from resource order alone.

The remaining adjacent embedded PE assets are also inventoried in
`a-payload-inventory.json`, including the signed legacy xLua DLL, profile
launcher and multi-hook DLL. Authenticode certificate bytes are included in the
legacy DLL's file boundary rather than misclassified as a separate resource.

## Marker ownership

The outer-host ASCII `bridge-scripts.dat` marker at `0xA490FA` is the embedded
resource name. The secure/plain proxies separately contain
`@bridge-scripts.dat`, the Lua compiler chunk label.

The secure/plain proxies contain UTF-16 runtime
`\bridge-runtime\package-key.envelope` path strings at outer raw
`0xABB32B` / `0xB5252B`. The outer host contains the independent auth/runtime
material string cluster `package-key.envelope` at raw `0xD545E8` and
`LWKE1` at `0xD54528`.

The controller names are outer-host provider call names:

- `claimTreasures`: raw `0x829D1F`;
- `getTreasureClaimStatus`: raw `0x82A048`;
- `prepareGhostPlunderTasks`: raw `0x82D2B0`.

Those names are host-side call boundaries, not the missing implementation bodies.

## Supplied-input inventory

A bounded recursive search of only:

- this repository; and
- the sibling supplied reference-artifact folder containing the EXE

found no standalone:

- `package-key.envelope`;
- `bridge-scripts.dat`;
- `authorization.ticket`.

No owner runtime/config/credential location was searched.

Therefore the encrypted original payload is definitely **present**, but a
standalone legitimate package-key envelope is not among the supplied artifacts.
That is an input-availability fact, not a decryption failure.

## Reproduction

`python tools\lwbridge317\inspect_original_lua_payload.py --output evidence\lwbridge-0.3.17\functions\LWB317-FUNCTION-ORIGINAL-LUA-RECOVERY-006\a-payload-inventory.json`

The tool hash-gates the 0.3.17 EXE, parses the package, validates adjacency,
calculates certificate-aware embedded PE lengths and verifies the bundle-declared
secure/plain hashes.
