# Lead review: LWB317-UI-HOME-PREFERENCE-LIFETIME-001A

Decision, 2026-10-04: **ACCEPT / COMPLETE for the assigned source/local Auto
Launch correction and controlled existing-adapter scope**. Reviewed implementation
commit: `c0d9082ee8f8b4b8985e8025657967a6898a4097`. Parent remains PARTIAL.

The lead inspected the App/Home/helper production diff, exact recovered storage
contract, mounted checker, preservation checker, worker browser record and evidence
validator. No acceptance-blocking defect was found in this bounded unit.

Independent current executions passed:

- actual mounted App/Home checker: 7/7;
- unrelated callback preservation: 10 callbacks plus root/profile/retention invariants;
- evidence validator and assignment-start WIP guard: 10/10, including seven protected paths;
- canonical frontend `check` and `check:production-build`;
- direct remote lookup: exactly the submitted commit;
- independent read-only SHA verification of the reference EXE, main asset and four
  pinned byte slices (storage contract, state initializer, setter, Home binding).

The EXE SHA-256 is
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`;
main `index-BVfnK1wp.js` SHA-256 is
`44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.
Exact source offsets: storage block 246750, key 246758, initializer 247632,
setter 248381, Home binding 338639 and shared switch 213332.

Only literal storage `"false"` restores false; absence restores true. The original
setter writes storage then React state synchronously, and its Home switch has no
saving-only disablement. The correction reproduces those facts. Serial native
writes and revision/commit/poll ownership preserve immediate edits while preventing
obsolete replies from replacing a newer visible value. Auto Launch remains global
across local profile selection; Automatic Reconnection was preserved for unit B.

The latest-failure rollback to a native-confirmed value is an explicitly documented
clone adapter policy. It is **not** recovered original local-setter failure behavior
or proof of native persistence parity. Acceptance retains that distinction.

Worker build/package fingerprints were independently confirmed by the current
package checker: `7c31d29fbab27d5f785f97323e7a4f9824f74ad7400358b74a2372482019fedc`
and `f1e58a73dc134eb4436e91187ef35d241a4a95e2cf041b9c04196fe3a9fdaa8d`.
The lead inspected saved controlled browser observations; no fresh independent
browser interaction or build was claimed by this review. Mounted production tests
were independently executed. No Last War/native action, protected original runtime,
original pixel comparison or live persistence was exercised.

Historical pinned evidence remains unchanged. The rerun current-record writers
produced identical files; the pre-existing dirty/untracked paths remained unchanged.

Continuation: assign `LWB317-UI-HOME-PREFERENCE-LIFETIME-001B` for Automatic
Reconnection alone. Preserve A. Global UI remains PARTIAL; broader offline visual
comparison, protected-original pixels and native functions remain separate.
