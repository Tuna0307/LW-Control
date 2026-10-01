# Home error channels evidence

Task: LWB317-UI-HOME-ERROR-002. Date: 2026-10-02. AWAITING_REVIEW.

channel-results.json pins original qr/Xt/Jt and helper source expressions at
zero-based UTF-8 byte offsets. check-home-channels.mjs extracts actual App/Home
callbacks and JSX, uses the real bridge transport with synthetic local response
envelopes, and compares original rendering. It covers 9 callback scenarios,
4 original picker cases and 72 render combinations. Run with --verify-record.

native-contract.json pins current host selection response fields and camel-case
serialization. This is source inspection, not execution of a native picker.
browser-results.json records five browser-only fixture observations and hashes
two screenshots. Lifecycle controls remain disabled. verification.json records
checks, build fingerprints and limits. validate-evidence.mjs checks source bytes,
counts, localized browser results, image hashes and verification data.

No original post-auth pixel proof, native lifecycle or live preference/persistence
claim. Existing preference error string reduction and polling remain unchanged.
The prior HOME-ERROR-001 checker needs only two state-name adapters; its saved
translation/render report is unchanged and verifies successfully.
