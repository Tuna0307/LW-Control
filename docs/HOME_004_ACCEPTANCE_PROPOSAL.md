# HOME-COMPLETE-DELIVERY-004 — acceptance proposal

**Disposition: PARTIAL.** This candidate has source-backed corrections and
verified real native Home preference/reopen behavior. Whole Home does not yet
match the original 0.3.17 and is not ready for lead merge as a full feature.

## Original authority and delivery identity

Original LWBridge **0.3.17** executable SHA-256:
**4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783**.
The source authority is origin/research/offline-controller (009-R3/010-R1
reviews and original bytes/traces). Production base is origin/main at
**0f0a435c5804db84107af3980f68686248e78775**.

The [47-row matrix](HOME_004_CONTRACT_MATRIX.md) preserves every H-01 to
H-47 input/state, expected original result and source locator, production path,
meaningful distinction, evidence type and dependency. Earlier research checks
remain research evidence; they are not promoted to real native observations.
Final source commit SHA and ZIP SHA-256 are reported in the PR after
packaging, and source commit is embedded in the RC executable and
SOURCE-COMMIT.txt. The ignored candidate lives under artifacts/home-004.

## Ready Home corrections and before/after proof

| Contract | Earlier divergence | This candidate / check |
| --- | --- | --- |
| H-02 Launch gate | Clone-only launch-status diagnostic blocked original-valid Home Launch | Removed extra UI diagnostic gate; original root/status/busy gates retained, mounted Home and integration checks pass |
| H-02 long lifecycle | 360-second frontend timeout cancelled native lifecycle operations | No additional frontend lifecycle deadline, while teardown cancels and short status calls retain deadlines; transport integration passes |
| H-03 Start errors | Missing helper could preempt original root and running conditions | Original root/running/unmanaged precedence restored; native regression uses physically absent helper |
| H-20 repair error | PascalCase JSON lookup hid actual camelCase repair failures | Native serialized restart result roundtrip preserves error, message and profile |
| H-21 reconcile default | Non-object reconcile request could throw during boolean extraction | Missing/non-object payload uses recovered default true; native error-path tests pass |
| RC validator | Windows ZIP backslash entry rejected by forward-slash check | ZIP entry separators normalized and extracted package independently smoke-tested |

The canonical UI integration suite, mounted Home six-case reconciliation
error test, Release Desktop build, root/profile/lifecycle native negative
suite and production UI package integrity pass. Existing Map checks also
run as regression only: **no new Map work was undertaken**.

Real packaged Windows Home controls, with one disabled isolated profile,
showed Auto Launch ON by original WebView default even when native config
was false. The UI was used to turn it OFF. Native reconnect ON/OFF, English
light and Japanese dark Home, shutdown and reopened language/theme/both
disabled preferences were witnessed with two exact exited PIDs, and zero
game/launcher processes. See
[the new native receipt](proof/HOME-004-NATIVE-ISOLATED-2026-10-09.md).
Earlier accepted actual one-profile Launch → authenticated Connected →
Close and script restoration remain in
[the lead receipt](HOME_LAUNCH_LEAD_ACCEPTANCE.md); the new HOME-004
verification did not repeat that live launch.

## Precise dependencies preventing full Home acceptance

1. H-05 protected original ticket/lease/capacity/finalizer responses remain
   unavailable; original entitlement decisions cannot be invented.
2. H-07/H-08 original launcher retry requires the typed
   OFFICIAL_LAUNCHER_RESTARTED producer, still unmapped in the current client.
   Adverse launcher/Lua errors and cancellation need matching proof.
3. Current **packaged live** recovery/reconnect enabled versus disabled,
   failure cleanup, journal repair, host crash/restart, same-build adoption,
   and outdated-build repair/relaunch have not been witnessed end-to-end.
   Controlled native seam checks are not sufficient for these cases.
4. H-38 independent simultaneous per-profile owner sessions and protected
   admission/capacity remain unavailable; ordered registry reconciliation
   alone is insufficient.
5. EN/light and JA/dark labels and preference states were witnessed, but
   original-vs-native paired proof for every conditional error, busy,
   cancellation, retry and teardown branch is missing.

Original input recovery plus exact owned native process and backup/restoration
gates must close the outstanding per-row matrix obligations before Home can
be called DONE. Do not merge, publish or move to another product tab on the
strength of this PARTIAL proposal.
