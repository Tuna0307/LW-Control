# UI baseline lead acceptance — 2026-10-09

Worker reviewed: e6c2a55ccf30cdb0cd8b12beb5a9ea3744d8789e, PR #3.

ACCEPTED for the recovered-source/local UI baseline and runnable Windows delivery.
Merge into main and publish UI baseline v0.1.0. This is the first product delivery;
full native Home/Map and protected-original runtime pixel parity remain incomplete.

The lead independently reran the import audit: 82 accepted files, 88 imported,
no missing or unexpectedly divergent production UI files. Worker changes add
release tools/tests/docs and dev-only Playwright; they do not alter product UI.
Canonical frontend checks pass with nine 1,383-key locale catalogs. A fresh UI
build and production identity check pass with source dc5da045...f5b419 and artifact
66339778...3909. The prior local dist identity was stale after test manifests
changed; the worker ZIP already contains the correct current identity. The worker
ZIP hash matches both the worktree and Downloads copies. Historical records are
preserved rather than rewritten.

Independent mounted production-browser replay passes eight routes EN/light and
JA/dark, ten conditional fixtures, navigation, locale/theme reload and popover
checks, with no page/console/request errors or document overflow. The lead also
inspected representative captures. Release Desktop/Checks rebuild reports zero
warnings/errors; actual-backend root and ordered-profile admission checks pass
with zero game launches. Published fixture capture remains explicitly inert.

Before final publication, the lead strengthens the release gates: new per-run
proof directories preserve prior observations, full-page screenshots include
below-fold content, a 375px Japanese variant exercises the small-screen layout,
overflow now fails the check, and error/dialog fixtures require their actual
elements. Packaging now verifies the published executable's source commit and
UI identity, and refuses to delete a pre-existing staging directory. No product
workflow/default/provider behavior changes are included.

The strengthened mounted replay passes all 24 route/variant combinations and
ten conditional states, producing 34 full-page captures with zero page/console/
request errors or document overflow. Its unique proof directory is
`%TEMP%/lwbridge-ui-release-proof-G4c2PY/`. The earlier lead attempt is retained
at `%TEMP%/lwbridge-ui-release-proof-4zXprQ/`: a new assertion incorrectly expected
a visible Map search-error banner. Original rr instead clears rows and displays
the empty table without that banner. The assertion was corrected to the recovered
contract; product code was not changed. Representative 375px and dialog captures
were inspected. Packaged fixture boot/render/disposal also passes independently,
with zero residual capture roots. Reference EXE SHA-256 matches the target.

Publication follows fresh verification of the extracted release ZIP.
The release notes identify prerequisites, Auto Launch default and unfinished
native functionality. No Last War launch/scan, original service access or gameplay
is part of this acceptance. Fixtures do not establish live functional parity.
