# MAP-016 lead audit — 2026-10-17 (source 010db520, draft PR #12)

Verdict: **ACCEPTED for owner testing.** Worker status PARTIAL is for two gates the lead accepts as limits:
first-use official launcher settle and direct off-server parsing.

Verified by lead: remote SHA 010db520; ZIP `artifacts/release-map016-010db520/LW-Control-MAP016-OWNER-RC-010db520d6fe.zip`
SHA-256 f64a432b…f723d matches its integrity record; Desktop.Checks, map_014 native tests, map_015/016 package tests
and production UI build pass (lead re-run). Source changes after the 21d1c9b1 proven package are the opt-in
no-game UI proof hook only (`LWBridgeWindow.Map016NoGameUiProof.cs`).

| # | Owner item | Lead decision |
|---|---|---|
| 1 | Launch opens/closes/reopens | Fixed for normal launches (verified settlement reused, one game start). First launch after install or a game update still runs the official launcher once; `first_use_official_launcher_required` denial exists only in the env-gated proof profile. Accepted. |
| 2 | Scan starts by itself | Fixed: Auto first run waits one full interval (real 20-min proof), labelled Auto; React no longer defaults auto-launch to true. |
| 3 | Clear leaves servers | Fixed: genuine 2213 clear/reopen proof. |
| 4 | Train missed on 2211 | Fixed: typed 625-rectangle Railway sweep; genuine 2211 positive. |
| 5 | Auto tab generic error | Fixed: precise error text. |
| 6–8 | Remove plunder/schedule/share/Normal-Fast | Done (legacy schema kept for old profiles). |
| 9 | All servers | Done. |
| 10 | Remote scan without switching | Not feasible from available sources (client drops off-server replies before parsing). Guarded switch→fast scan→return accepted: ~3.5 s out, ~1.9 s back. |
| 11 | Cleaner Map page | Done; fits at 1100 px. |

Timing: same-server all-nine 144–163 s across trials (MAP-015 149–161 s); within world variance.
