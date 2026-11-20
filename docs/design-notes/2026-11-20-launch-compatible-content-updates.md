# Keep compatible content updates launchable

On November 20 the official launcher applied the Lua v36-to-v37 delta after LW-Control
restored the original package. Both packages have 19,001 entries. Comparing the original
packages by entry showed one changed event screen module,
`UI/UIActivityCenterTable/Component/ActGiftGiving/ActGiftGivingMain.luac`.
No entries were added or removed. All three runtime files and all four pinned Lua entries
have the same hashes. Policy 5 blocked launch because it restricted the v36 anchor set
to a single package hash and content version.

Policy 6 recognizes the complete v36 anchor set on the supported 1106 runtime for content
version 36 or later. The existing compatibility path for the older anchor set still applies.
The Python helper validates the package size, CRC and version marker. The host and helper
continue to carry the actual package hash for settlement and restoration. This separates
compatibility, which can remain stable across content updates, from the exact identity of
the package to restore.

Controlled checks exercise an advancing content version and a different package hash,
including the observed case where the version marker stays at 36. They also check that
mixed anchors, an incompatible runtime, an invalid version and bad package metadata are
rejected. The official v37 package passed read-only compatibility inspection and an
offline candidate package roundtrip. Changes to runtime binaries or critical Lua entries
still require qualification.
