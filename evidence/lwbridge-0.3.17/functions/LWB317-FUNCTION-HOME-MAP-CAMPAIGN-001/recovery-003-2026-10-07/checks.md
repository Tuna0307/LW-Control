# Executed verification — RECOVERY-003

All execution is offline/inert. Read-only process inventory before native checks
found no LastWar or LWBridge process. Temp roots and controlled providers/helpers
are explicit in each named test; no live runner or owner runtime is invoked.

## A — native boundary

- Desktop checks Release compilation (`--no-restore
  -p:SkipCanonicalProductionUiBuild=true --nologo`): PASS, zero warnings/errors.
- `--profile-runtime-owner-check`: corrected PASS. The initial new barrier test
  failed because it did not catch the Windows replacement-denied
  UnauthorizedAccessException. That exact test-only classification was corrected;
  the production ownership predicate was not weakened. Initial failure is retained.
- `python tests/home_runtime_file_ownership_checks.py`: four tests PASS.
- Task-owned lupa runtime `tests/home_runtime_lease_lua_checks.py`: six full-module
  inert Lua tests PASS; coordinator independently reran them. Runtime versions and
  additional parser/version evidence are in checkpoint A.
- Eleven other named Desktop flags: PASS, with fresh outputs in checkpoint A:
  Map native boundary (including post-commit caller retirement), Home lifecycle,
  Map Auto campaign, plunder worker boundary, all-eight DTO matrix, canonical Map
  host, restart, root selection, Overview launch binding/host transport/composition.
- Canonical Map project Release `dotnet run ... --no-restore`: PASS.

## B — current frontend composition

- Canonical `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check`: PASS.
- Build and `check:production-build`: PASS. Fingerprints:
  `6d83a69870ad62131d7b21895b485ad2acf126ccc074d0759ea249be1360a685` /
  `6633977812436f36c2f9a0d0e887e5224a5e664e6f27441324c5a7a0b7ae3909`.
- Extracted actual App/Map handler/coordinator inverse suite: 14 scenarios,
  62 counted assertions PASS; coordinator independently reran `--verify` without
  writing. It reproduces starting-commit rollback/array negatives and distinguishes
  cap/no-op replay, last-type, failed hydration/save retry and owner retirement.
  This is Node composition, not mounted evidence.

## Integrity

- Reference EXE SHA-256 matches `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
- Legacy archive guard: PASS, all ten exact archived files.
- Repository diff check: PASS, only normal Windows line-ending notices.

Checkpoint C package execution/source binding, screenshots and cleanup are recorded
in the final packet after implementation checkpoints. No LIVE_PROVEN upgrade.

## C — mounted package and final rerun

- Fresh canonical frontend `check`, `build`, `check:production-build`: PASS.
  Source/artifact fingerprints remain
  `6d83a69870ad62131d7b21895b485ad2acf126ccc074d0759ea249be1360a685` /
  `6633977812436f36c2f9a0d0e887e5224a5e664e6f27441324c5a7a0b7ae3909`.
- Release Desktop build from `d90f764a`: PASS, zero warnings/errors.
- EN/light corrected mounted package:
  `package-en-light-attempt5.json/.png`: PASS, 1120x720,
  `externalGameActions=0`, complete-session `unexpectedCount=0`, clean shutdown.
- JA/dark corrected narrow package:
  `package-ja-dark-narrow-attempt1.json/.png`: PASS, 900x720,
  `externalGameActions=0`, complete-session `unexpectedCount=0`, clean shutdown.
- Both packages bind to product version
  `1.0.0+d90f764aa2a721e6bf26952a81fb1a6161bec0d4`, apphost
  `8613040D327D2F517D9F0BE0962285D7580E05D691FB57AE8450F97F02BE71CF`
  and managed DLL
  `B26211DA46305C54EFD57CD9CB8D6B2038BFF0D12F01373ACB9BD4F6CEC22803`.
- Historical package attempts 1-4 remain immutable beside the successful packet;
  their failures and adaptations are summarized in `README.md`.
- Exact-source final focused rerun in
  `checkpoint-c/final-focused-native-rerun.txt`: checks Release build PASS with
  zero warnings/errors, then profile owner, Map native boundary, Home lifecycle,
  Map Auto, canonical Map, restart and game-root selection all PASS.
- Final integrity in `checkpoint-c/final-integrity.txt`: reference EXE hash PASS,
  legacy archive `exactFiles=10`, successful packet hashes recorded, no
  LastWar/LWBridge process, `git diff --check` PASS.
- Failed-attempt cleanup in `checkpoint-c/failed-attempt-temp-cleanup.txt`:
  12 exact task-prefix temporary roots removed, after-count 0, process inventory
  remains empty.

No LIVE_PROVEN upgrade is made. Protected/live-provider limits remain as documented
in the work item and closeout README.
