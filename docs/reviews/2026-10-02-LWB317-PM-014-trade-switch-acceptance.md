# PM-014 — accept focused Trade cross-server setting UI

Date: 2026-10-02. Reviewed worker commit:
`bd80899491d24a7d9a081830fc844937fe976d10`.
Disposition: **COMPLETE / ACCEPTED for focused source/local cross-server setting**.
CORRECT-003D is closed. This accepts configuration UI, not cross-server gameplay,
native persistence, original pixels or full Automation parity. Parent CORRECT-003
remains PARTIAL.

Local HEAD and direct remote lookup match the submitted revision. The only product
change removes config.saving from the Trade cross-server switch's disabled
predicate. Offline disabling, default false and immediate edit(...,false)/flush()
remain unchanged, matching exact AutomationPanel-BJ0gIqFh.js at UTF-8 bytes 400,
7213, 8264 and 8330. Source hash:
`6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725`.

The lead read the actual-expression harness and independently reran it. It
extracts production onChange/save/disabled expressions and actual shared
Retry/Discard callbacks. A deferred first write permits a second toggle, records
the older confirmation without overwriting the newer draft, then confirms the
queued draft. Enabled state, goods and currencies remain intact. Actual failed
callbacks followed by Retry/Discard and the offline predicate also pass.

Independent checks passed:

- Focused cross-server callback/source-anchor check and evidence validator.
- Existing Trade selection, PM-011 composition and purchase-history checks.
- Canonical npm.cmd run check, build and check:production-build. Source fingerprint
  ef7d2cf373b994bf440569f9fdf1d9e2dfdd833155129c4c584e9c0261a212fc;
  artifact fingerprint 8e9748d670f921186da50d3f68104938751ce2353fdd7efad182f34bedb062e6.
- Exact executable/source/shared-UI hashes, evidence JSON and git diff --check.

Worker browser evidence records on/off preservation, failed-save Retry, failed-save
Discard and default offline disabling. No new screenshots were required; no new
lead browser/game session occurred. The production package includes preserved
uncommitted AFK fixtures, which are outside this acceptance. AFK/scratch/parent
screenshot WIP remains unchanged and unstaged.

Next bounded unit: CORRECT-003E, only Trade status counters/last-result and goods
loading/fetch-error presentation. Original counters use optional status fields
with zero defaults and '-' for absent last result; goods loading/error state is
separate from purchase status and config-save failure. The current fixture exposes
a boolean goods-fetch error and renders a generic action-failed label, while the
original displays its String(error) text. Recover the complete presentation
contract before fixing it; no template-fetch service or purchasing implementation.
Preserve accepted weekly, selection, history and cross-server switch work.

UI matrix/ledger overall states remain IMPLEMENTED_NOT_VALIDATED / BLOCKED for
original/native parity. No worker chat or subagent was automatically dispatched.
