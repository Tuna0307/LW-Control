# Lead closeout — old pending changes

Status: COMPLETE, 2026-10-04. Owner requested assessment/cleanup before dispatch.
Starting HEAD:223ca757b4e70c422af1bf79509aa10313982124.

All ten legacy protected files were copied byte for byte into the closeout evidence
archive before any restoration/ignore rule. Manifest and check-archive.mjs compare
them against the existing historical ten-file baseline. Archive attributes prevent
Git line-ending conversion. No file was deleted, and old guards/results were not
rewritten to make the new disposition appear unchanged.

Pending AFK fixture edits are redundant: the actual accepted closeout adapter
overrides every changed flag. Before restoration, executing the accepted adapter
with HEAD and pending base modules matched39 discovered/boundary states. The
durable check-afk-equivalence.mjs repeats that comparison using archived pending
and restored current base. The base source was restored to its committed version;
all accepted adapter behavior remains. Canonical check/build/package pass.

The pending R1 independent-results.json altered only its historical currentSha256
to another obsolete source identity. Archived the generated variant and restored
the committed historical file. AUTO-CONFIG regression-results.json was byte-exact
to HEAD; git add refreshed stat metadata with no staged content change.

The two formatted reference scratch files and early baseline script are archived;
their original local cache remains at .scratch-lwb317, now ignored. The parent
weekly-save-error-light-en.png is0 bytes: an invalid placeholder, not screenshot
proof. Its empty body is archived and only that exact local file is ignored.
Motion LICENSE/NOTICE files are retained beside the already tracked recovered
runtime, which points to the notice in its source comment. No package-version
claim was added; the notice keeps it UNKNOWN.

An attempted old AFK standalone checker still looks for functions in pre-split
Pages.jsx and fails extraction before executing cases. This is the known obsolete
harness shape, not caused by the restored fixture. The maintained split-module
Equipment/AFK replay is the applicable regression gate; its fresh outcome is saved
in this closeout packet. Historical checker/evidence remains untouched.

Maintained current replay: Equipment105, Equipment R1 61 and AFK729 all PASS;
only its fresh output was redirected into this closeout packet. Canonical check,
fresh build and package integrity pass. New package fingerprints:
269b09476f16671350c5ec133e52f3086c8271461637b0326f408d047c0b41b1 /
5892c78fb0247feaac48d56dbfe33281684ade66c6cbec80dbe49a356f9ec897.
Archive preservation10/10 and repeat equivalence39/39 pass after restoration.

Future campaign rules now verify the archival preservation guard and capture new
starting WIP. The old live-path guard is historical because the superseded source/
generated metadata was deliberately restored after exact preservation. The worker
must not reset that old baseline to conceal these dispositions.

The large VISUAL-FINAL-CAMPAIGN-001 assignment is otherwise unchanged. Toolbar
Treasure color/evidence findings remain CHANGES_REQUIRED; this cleanup does not
advance global UI or native parity. Owner port4335 was not touched.
