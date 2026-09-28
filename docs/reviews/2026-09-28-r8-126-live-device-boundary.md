# R8-126 — live device-boundary observation

Date: 2026-09-28

A fresh structured authorization probe reached the original secure proxy's `authorization device mismatch` diagnostic. This proves the ticket advanced beyond the previously observed missing/format/encoding/signature stages into the device-binding comparison.

Existing static evidence links the host fingerprint source boundary to the Windows MachineGuid path, but the exact transformation into the proxy's expected 64-character value remains unproven. No device identifier or credential value was read or recorded in this checkpoint.

Current runtime cleanup is complete: Last War is closed and the bridge-runtime directory contains only `authorization.challenge` and `build.manifest`. No authorization ticket or package-key envelope remains. The original LWBridge process was restarted on its default auth configuration, and no reference binary on disk was modified.
