# HOME008 continuation after review

**AWAITING_REVIEW. Original Home/Map A→A PARTIAL.** Preserve accepted 007/R1 and do not rerun broad campaign.

Available ORIGINAL native code still needs additional semantic decoding; this is READY_STATIC_RECOVERY, not BLOCKED_EXTERNAL: (1) `0x19A0DE→0xE5725` and `0x204593/0x2060C5→0x1DDC84` cancellation/waker/identity/error propagation via `0x641410` and `0x41E3CF`; (2) shared `0x1D5009–0x1DDBB2` async branch on `0x2A2887` and 5-second deadline `[r14+0x498/0x4A0]` up to `0x1D74E5`; (3) `0x1E11A5`, registry helpers `0x2C75CA/0x2C6A65`, 90-second deadline `[r14+0x808]` and terminal `0x1DD4FD/0x1DD6B5`; (4) original health monitor and ordinary/maintenance retry duration producer, cap and success reset, never infer those from older 0.3.1.

Original encrypted Lua/controller matching authorized signed envelope/CNG state and original post-auth runtime/WebView are separate missing external inputs/observations. No service/key probing. Current 5-second and 90-second native waits are specific launch/registry conditions, **not** confirmed automatic reconnect thresholds. No product Home change without independently reproduced original-vs-clone behavioral mismatch.

Re-run offline: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/lwbridge317/home008_verify.ps1 -Phase native|static|frontend|package`; preservation and proof scope: `python tools/lwbridge317/home008_validate.py`. Results are written ONLY in the new HOME008 evidence subtree; original records are immutable. No game launch, desktop capture/input/focus, updater or protected service.
