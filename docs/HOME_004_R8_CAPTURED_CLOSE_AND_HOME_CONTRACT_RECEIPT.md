# HOME-004 R8 — captured-owner Close and complete Home gate/source integration

Date: 2026-10-10. Assigned solo continuation on
`codex/home-complete-delivery-004`; verified R7 local/remote starting SHA
`a5429bce329356c0cd6de13e3effeed1b013acbd`.
Original target LWBridge **0.3.17**, EXE SHA256
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Whole Home **PARTIAL_NEEDS_INPUT**. Draft PR #6; no merge/publication.

## Broad bounded R8 audit and source authority

Read AGENTS, LOOP_WORKER_PROTOCOL, full 47-row contract and current matrices,
the R4 current queue and full owner continuation, independent R3/R4 reviews,
R6/R7 receipts, production `App.jsx`, `HomePage.jsx`, `ProfileSidebar.jsx`,
`backendBridge.js`, native WebView dispatcher, native profile status,
and actual A/B mounted proof. Read original 0.3.17 immutable frontend asset
`index-BVfnK1wp.js` from `origin/research/offline-controller` without
modifying protected research or contacting a service. Original UI hash:
`44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.

Compared original Home lifecycle command closures and UI control gates,
selected versus retained profile ownership, sidebar batch/read/Stop,
real native global repair and startup error projection, original R6
localized error priority, R5 poll revisions, R7 visible per-owner
busy states, original profile connection labels, launcher typed-failure
fences, recovery owner/state evidence and remaining protected transport
semantics. Earlier H-18 manual first-global-error behavior versus H-23
selected-only startup error **remains intentionally distinct**.

### Concrete H-14/H-39/H-40/H-47 difference: Home Close lost on A → B

**Original oracle:** 0.3.17 frontend `Pt`, reported in the original matrix
at byte ~365962, captures `r.selectedProfileId` for both awaited
`Ze(profileId)` instance status and subsequent
`tt(profileId, instance.instanceId)` Stop. A later view selection must not
switch or silently abandon that exact command.

**Pre-fix rebuild:** `App.jsx stopGame` acquired exact A lifecycle admission
but called `backendBridge.invokeProfileScoped` to read status. This
helper correctly rejects a *selected-view* response after A→B and thus
stopped Home's action **before** its native `profile_instance_stop`.
The native per-owner guard prevented accidentally stopping B, but the
already-clicked A Close was silently lost. The frontend should fence A's
stale screen projection, not its independently owned lifecycle completion.

**Actual production WebView/native inverse**, with no genuine game:

1. Use the real mounted A/B inert runtime and start owned A.
2. Click the actual A Home Close. Hold its native
   `profile_instance_status` **on reply after native dispatch**.
3. Select B through the real sidebar, release A status.
4. Demand exact A Stop once, B unaffected.

Immutable negative:
`artifacts/home-004/r8-home-close-switch-inverse-en-light.json.error.txt`
reported:
`A Home Close was abandoned or misrouted after switching to B.`

**Small correction:** `stopGame` captures the original owner at click and
invokes explicit native `profile_instance_status` and
`profile_instance_stop` with `{profileId:owner.profileId}`, preserving
the returned **exact A instanceId**. Existing shared raw-profile and
generation admission, original optional-ID/instance-mismatch behavior,
native exact ownership, and `refreshHomeProxyStatus(owner)` selected-view
fencing remain. No B switch, service authorization or new global lock.

Expanded positive production-mounted EN/light and JA/dark verifies:

- A Home Close status held while native selected-view changes to B;
- B independently Start/Stop while A's Close is pending;
- releasing A status Stops exactly A while B remains running until its
  independent sidebar Stop;
- A→B→A ABA before delayed Close status still Stops only original A;
- original R4–R7 native sidebar/poll/busy/global repair cases unchanged,
  both owners stopped and no native requests/listeners/roots survive.

Outputs:
`artifacts/home-004/r8-close-switch-aba-final-en-light.json` and
`r8-close-switch-aba-final-ja-dark.json`.

### Original conditional Home UI, localized labels, timing (not new defects)

Original 0.3.17 `Kr` at UTF-8 byte **336469** of the identified asset
is source-encoded in the canonical Home integration tests. The test
evaluates **the actual HomePage source expressions** for root picker,
Start, Stop and repair-on-Close against original `Kr` for all
`2^7 = 128` boolean combinations of root resolution/validity, running,
needs repair, recovery, proxy busy and launch busy, with live providers
present. H-02/H-18/H-45: **source parity in this truth table**, not original
conditional pixel or genuine fault parity.

Original 0.3.17 `Jr` connection labels after the Home renderer map exactly
nine states `offline, starting, recovering, awaitingLogin, connected,
reconnecting, grace, locked, error`; the test extracts the actual
`ProfileSidebar.jsx` table and compares those keys (H-42/H-45).
Prior direct original source scan also independently confirmed sidebar
`profile_instance_status` polling every 3 seconds versus Home status/proxy
every 5 seconds; the existing rebuild already matches. The current
locale suites still require all nine catalogs, including EN/JA. These
checks found **no additional product difference**, so no false fix.

Native/profile metadata reordering, sidebar batch/poll revision protection,
status/repair producer and launcher/update error source paths were read:
retained R4–R7 evidence still covers implemented paths. A typed original
`OFFICIAL_LAUNCHER_RESTARTED` producer, signed plaintext and protected
success responses are not available; no retry, schema, commercial-capacity
or synthetic genuine state has been invented.

## Validation and limits

R8 correction is subject to canonical frontend suite, Desktop native
Home/recovery/Map regressions, real Windows concurrent A/B named pipes,
typed launcher tests, mounted EN/light and JA/dark, source-identified
publish/ZIP extraction and disabled-registry normal GUI exact cleanup.
Generated, ignored machine receipt `artifacts/home-004/r8-final-package-receipt.json`
should reconcile final commit/remote SHA/ZIP/EXE/version/GUI PID.

The only **new negative** represents the actual rebuilt WebView/native
Close race; the original byte is source authority for captured-owner
continuation. Controlled A/B is **not** an authentic second supported game
installation, protected commercial entitlement or a real offline/update
fault. No Map scan, game launch, original protected service, updater,
unrelated Windows process or installation was touched during this pass.
Historic negatives remain immutable.

## Remaining whole Home gates, explicit H-IDs

The complete [47-row queue](HOME_004_R4_QUEUE.md) remains authoritative.
Original protected successful/failing ticket, lease, finalizer callback and
licensed capacity H-05/H-13/H-28 (and H-22/H-38); legitimate original
signed controller plaintext/key H-33/H-36; typed official self-restart,
descriptor/mutex, Lua/update and error producers H-06–09/H-18–20/H-44/H-46;
genuine independent supported concurrent installations and real host adoption
H-22/H-24/H-37–40; paired genuine offline-only, maintenance, pending Stop,
restoration/rollback, official update and all conditional original EN/JA
states H-01–04/H-10/H-12/H-15–17/H-25/H-29–32/H-34–36/H-40–47 are
not established where each row names such a witness. Original encrypted
transport timer is still only evidenced from 0.3.1 for this adapter (H-29).
Earlier scoped accepted genuine one-owner Launch/Connected/Close/adoption/
exit/hang remain accepted **only within that scope**.

Authorized alternatives attempted this pass: original archive static UI
oracle, nine-state localization lookup, exhaustive conditional helper
comparison, current native/production code and safe inert mounted Windows
A/B inverses, prior archived 0.3.17 semantic evidence and previous genuine
single-owner receipts. None supplies protected successful responses,
genuine licensed simultaneous installations or adverse paired original/
current game/update traces. Do not substitute fixture capacity, spoof
entitlement, decrypt without matching legitimate keys, induce dangerous
faults or mark whole Home accepted. Remain **PARTIAL_NEEDS_INPUT** until
those exact, legitimately supplied evidence gates can be tested and lead
independently accepts the complete feature.
