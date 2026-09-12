# LWBridge 0.3.1 architecture recovery

## Artifact authority

`lwbridge-0.3.1.exe` is a Windows x64 Rust/Tauri application. Verified SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

Recovered Rust source-path markers include command modules for auth, automation, bridge, city layout, feedback, map, multi-license, platform, profile, and update. Service markers include automation, bridge pipe/store, dispatch assist/plunder, feedback, game recovery, map index, map scan, multi-license, player tracker, profile manager/store, proxy, sheep solver, truck plunder, and update.

## Embedded runtime assets

The verified EXE embeds these runtime components:

| Asset | Size | SHA-256 |
|---|---:|---|
| `bridge-scripts.dat` | 1,172,723 | `a1e23419c25c76bee16942922a643381e0d38a857902569d927f22ef02c99c8d` |
| `xlua-proxy-secure.dll` | 612,352 | `481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400` |
| `xlua-proxy-plain.dll` | 614,400 | `c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794` |
| `xlua-proxy-bundle.json` | 590 | `261a54f92e79ccf397544cca30039f1dd7519943da2ba77632b0e6dce98db4da` |
| `xlua-legacy.dll` | 782,736 | `f4979b11e1990a7c064a937a80231fbb11fa13f5f8a19d2a321569dd6e5b254a` |
| `lwbridge-profile-launcher.exe` | 684,544 | `8f42adb9ed678445425e529cdee8f12a097c63053f9d4de314a758a8dfe362de` |
| `lwbridge-multi-hook.dll` | 416,256 | `a8b48120bbe3fb125d50c046acbd4f7c440a4d5e5775b8156fc291d01bd69d2d` |

`bridge-scripts.dat` begins with `LWBP`, package version 2, build ID `9BupJXpEgm34lybhNhbbcQ`. Its payload is encrypted/high-entropy; plaintext script semantics must not be guessed.

## Recovered runtime layers

1. Tauri host/UI issues profile, automation and map commands.
2. Profile manager builds a launch descriptor with profile/instance/build/runtime/hook integrity data.
3. Embedded profile launcher controls official launcher/game startup and hook injection.
4. `lwbridge-multi-hook.dll` installs process/registry/single-instance hooks and redirects the game's xLua load.
5. Secure/plain xLua proxy keeps the original xLua ABI while adding bridge script loading, native capture and named-pipe communication.
6. Host and proxy authenticate their local connection with profile/instance/build identity.
7. Map Scan combines direct block work with native manager capture and commits results through the map-index service.

See `lwbridge-injection.md` and `lwbridge-map-scan.md` for the first recovery milestones.
