# Independent Cross-server lead review

Accepted for the assigned source/local UI scope at worker delivery
`52710d3d1950cceccee2439c4979d3b99c797782`. No product files changed during review.
See `docs/reviews/2026-10-04-LWB317-REVIEW-SHELL-CROSSSERVER-001.md`.

Run `node evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-SHELL-CROSSSERVER-001/run-independent.mjs`.
It reuses only the worker's actual-App mount/compiler scaffold, executes original
bytes directly, and writes this directory's `independent-results.json`.
All 11 additional cases passed. It requires the existing isolated jsdom package
at the worker's documented path (or `LWB317_JSDOM_PACKAGE` override), plus current
UI dependencies. The fixture APIs are inert; native/game/network calls are excluded.

The lead also executed the worker's current-check runner and integrity validator,
inspected all three saved images and directly verified the submitted remote SHA.
`verification.json` records this rerun and input identities. Browser interactions
remain worker evidence; no new lead browser session is claimed. The abandoned
subagent attempt returned a usage-limit error and contributed no acceptance proof.

Exact continuation: assigned unit is closed. Preserve product/history/WIP and
await a separate bounded shell/integration task. No native or global parity claim.
