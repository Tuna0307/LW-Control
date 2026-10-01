# LWB317-UI-CORRECT-003 evidence

This package records the independent CORRECT-002 baseline review and the
source-backed CORRECT-003 Automation/AFK completion work. The exact reference
executable SHA-256 is
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.

`source-contracts.json` records the exact 0.3.17 bundle hashes and the contracts
used by the implementation. `check-state.mjs` is an executable state/contract
check that covers generation acknowledgements, weekly quality validation,
Trade/Assist/Gather fixtures, Automation timing/shield states, AFK target/range
and member variants, toolbar runtime variants, and retained Map fencing.

`browser-qa.json`, `coverage-matrix.md`, screenshots and `verification.json`
are populated by the continuation browser/verification pass. Browser fixture
names and values are deliberately prefixed or described as Fixture data and
must not be treated as current Last War/game data.
