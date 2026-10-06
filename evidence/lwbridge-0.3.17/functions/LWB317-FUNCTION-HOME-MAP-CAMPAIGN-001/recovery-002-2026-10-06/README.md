# RECOVERY-002 worker closeout

Status: **AWAITING_REVIEW**. This directory is the fresh isolated/offline proof packet
for `LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001-RECOVERY-002`. Final campaign acceptance
remains with the project lead. No LIVE_PROVEN status is created by this packet.

Implementation checkpoint: `b2e6089f4f0a99be53087482f04a240ac59d22a4`.
Earlier RECOVERY-002 checkpoints retained:
`7586c425e514143bc0852ed540064f7afacc85ef` (lifecycle isolation/ownership) and
`5d3ba44835b5cf283ec4c64f7b45802bc18d4c34` (frontend request ownership).

## Current successful package proofs

- `package-en-light.json/.png` and `package-en-light-ui-export.xlsx`
- `package-ja-dark-narrow.json/.png` and `package-ja-dark-narrow-ui-export.xlsx`

Both packets are schema v4, identify checkpoint RECOVERY-002, use the freshly built
Release Desktop package and record `externalGameActions: 0`. They exercise actual
packaged React controls against isolated roots and inert lifecycle/Map providers.
The JA packet measures a 900 x 720 narrow viewport and records Japanese/dark state.

The package identity recorded in both current JSON packets is:

- Desktop apphost SHA-256:
  `7bbdb937d836df56c105930e6e30bd04546f376cebacb974bf58515adcf6330b`
- managed Desktop DLL SHA-256:
  `ecd99e62c0edfe6fa8594bce350cea824b433844e4176b1ebcc7182dbcf9c545`
- canonical UI source fingerprint:
  `cd86aec04987bf5683d38e27d05af5a4c26a773e3be65ea3dc5133463be2d150`
- canonical UI artifact fingerprint:
  `1ae836263c78106f014fcc44d23f9df5b6f2b22453f0462c9e968094419390c5`

## Historical negative evidence

`package-en-light-attempt1.error.txt` through
`package-en-light-attempt10.error.txt` are immutable failure observations produced
while the distinguishing package proof was being repaired. They remain failures;
none has been rewritten as passing evidence. The lead's prior
`lead-closeout-2026-10-06` counterexample files are also preserved unchanged.

## Source/hash anchors

- Original reference executable:
  `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`
  SHA-256 `4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783`.
- Recovered Map Data panel:
  `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js`
  SHA-256 `ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089`.
- RECOVERY-002 DTO edge fixture:
  `tests/LWBridge.Desktop.Checks/Fixtures/map317-recovery-dto-matrix.json`
  SHA-256 `700e465605275122a66888435a1814f496245bc3f84221f19d8325579a17b314`.
  The fixture labels its rows as synthetic edge cases derived from recovered
  contracts/source locators; it does not claim to be original runtime data.

See `checks.md` for commands actually executed and `operation-matrix.md` for the
completed/blocked proof boundaries.
