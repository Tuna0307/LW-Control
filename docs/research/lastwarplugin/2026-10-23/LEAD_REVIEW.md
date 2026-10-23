> Archived research record. The owner has clarified that Home comparison means game-connection methods and efficiency. Read CONNECTION_COMPARISON.md and the independent lead corrections before using the recommendations. Local user-path prefixes have been generalized.

# Independent lead review — 2026-10-23

Verdict: accept the bounded loader/renderer findings with corrections. Actual post-launch Home comparison remains PARTIAL. No production implementation is authorized by this review.

## Independently checked

- Repository remains main at 45e151c8, with only the pre-existing untracked .claude/ visible.
- Decoding the exact original EXE range 0x21AFD3 + 456 bytes with XOR 0x5A reproduces the saved bootstrap exactly. SHA-256: 6372938bcbc80b500816d10ac164b2f6ebf56aca9521065a32441dd27d0b00ee. It reads a 32-byte named-mapping key, reads/deletes the encrypted core, decrypts AES-GCM with the first 12 bytes as nonce, optionally decompresses and executes the plaintext. This check did not execute that bootstrap.
- The runtime receipt records seven processes (launcher plus six WebView2 processes), no encrypted artifacts, and identical before/after lwps.set observations. This is archived worker evidence, not a new native runtime observation by the lead.
- Current ProfileSidebar.jsx polls at 3000 ms and uses revision/action guards. Its delete path calls window.confirm.

## Required corrections

1. LastLaunchedAt is not established as populated launch history. A source-wide search finds the model, schema, two inserts initializing NULL, and the reader, but no writer that records a launch. The proposed improvement needs a defined successful-launch recording event, persistence, and display; it is not a display-only change. Relevant source: ProfileRegistryStore.cs:16,66,106,154,414.
2. Connecting-state serialization is not enforced by every renderer launch path. updateLaunchBtn disables the button for anyConnecting(), but lw('launch') checks only _launching. A controlled offline JSDOM execution of the byte-exact renderer, with mock IPC and dummy profiles, confirmed that double-click sends a second launch while another profile is ing and _launching is false. Receipt: lead-renderer-gate-check.json. Native/backend rejection remains UNKNOWN; do not claim that a second game actually launches.
3. AES-GCM ciphertext alone is insufficient for Home recovery without its matching key. Normal authenticated decrypted source or an appropriately scoped post-decryption process observation is useful; capturing ciphertext alone only establishes the artifact and format. No valid login has been supplied.
4. Keep comparisons bounded. Missing competitor backend evidence cannot establish general LW-Control backend superiority. An event feed is a candidate improvement, not a measured gain; retaining the same three-second polling schedule alone does not reduce periodic read volume.

## Return boundary

Correct the research report only. Preserve the original worker evidence, this independent receipt and the released application. Stop further Home recovery until the missing legitimate authenticated payload evidence is available; do not repeat the whole-program Ghidra route unchanged. No new game execution, product changes, merge or publication follows from this review.
