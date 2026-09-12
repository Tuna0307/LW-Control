# Recovered LWBridge 0.3.1 frontend

Source authority:

`external-reference:lwbridge-0.3.1.exe`

Executable SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

This directory persists the decompressed embedded frontend recovered from the
verified executable. It exists so UI reconstruction does not depend on temporary
`.codex-live` files.

`manifest.sha256.txt` hashes only the recovered frontend files (`index.html` and
`assets/`). This explanatory README and the manifest itself are maintained
metadata and are intentionally outside that immutable-byte check. The main UI
authority is `assets/index-C5e98iqj.css`, `assets/index-sfL2sT3K.js`, the locale
chunks, and the feature chunks referenced by the recovered Vite entry.

These files are static recovery evidence. They do not by themselves establish a
successful login, current game connection, or live feature behavior.
