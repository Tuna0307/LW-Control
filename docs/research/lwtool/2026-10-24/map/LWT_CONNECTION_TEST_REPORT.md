# LwTool legitimate connection and Quick Map access test — 2026-10-24

**Answer: the owner's genuine account authenticated successfully, but a Quick Map scan was not admitted or completed.** A fresh login in the unmodified shipped LwTool application reported the account's current plan as **Free**. The protected Map RPC returned an error and did not provide a scan job receipt. The recovered implementation requires **Premium** before its remote scan submission. No usable results or live scan duration can be claimed.

This is a separate, bounded external-service investigation. It does not qualify any outstanding LW-Control Map packaging, scan, Stop or Home acceptance gate, and it does not integrate LwTool into production.

## Test identity and boundary

- Target: owner-supplied `C:\Users\<owner>\Downloads\Programs\LwTool.exe`, 13,800,960 bytes, SHA-256 `3f35daeb9b20f058556b8e675e8d8affeaff16b7119f9a0a0d058b528ff1a8f6`. The target hash matches the retained October 23 recovery handoff.
- Recovered evidence: ignored local `artifacts/lwtool-connection-20261023/` and canonical [SCAN_METHOD_REPORT.md](SCAN_METHOD_REPORT.md). Original code and recovered private keys are absent from this report.
- Account: the owner's existing saved license on the same Windows user/device; **no replacement identity, borrowed account, subscription change, device spoofing or forged plan claim**. The original application's own login button refreshed the authenticated session.
- Requested Map target: server **2212**, identified as the owner's main home server in repository history. No Last War game process was running, so the app could not independently re-read the connected game's current server or satisfy the ordinary Map UI's `requireGame()` prerequisite.
- Test kept `autostartChk` unchecked before invoking the original login button; native app launch did not use `--auto` or override multi-instance settings. No gameplay automation, other feature command or game launch occurred.

## Observed result by stage

| Stage | Observation | Conclusion |
| --- | --- | --- |
| Service reachable | Two HTTPS POST requests to the recovered default service's `/api/auth/proof` returned **HTTP 200**, in **0.625 s** and **0.437 s**. Each response was an encrypted `_enc` envelope. | **Yes, reachable.** An encrypted HTTP 200 is not by itself a decoded proof or scan. |
| Current account authentication | Original LwTool saved-key login succeeded; `session.json` was freshly rewritten at approximately **2026-10-24 06:05:54 UTC** with a new, then-unexpired token. | **Yes, original app accepted the existing account.** No token/key is recorded here. |
| Current entitlement | The **new** original-app session reported **`free`** (not the older cached October 23 plan). `VIP Check` was off; game connection was absent. | **No Premium eligibility** in this observed session. |
| Normal Quick Map attempt | One original-client `py_scan_nhanh('map', '2212', false)` RPC invocation returned an error, with no machine-readable error code captured. Recovered core calls `_require_premium('py_scan_nhanh')` before resolving/submitting jobs. | **Client Quick Map was not admitted.** The precise RPC error text was not preserved, and service-side rejection was not tested. |
| Remote submission | The isolated test sent **zero** `POST /api/scan` requests. No accepted `job` response was obtained from the original-client RPC. | **Not established**; no authenticated scan job was submitted. |
| Remote completion and results | No job token, `/phan` part, `xong` event, city/resource row, completeness metadata or collection timestamp was received. | **Not established**; **no usable results**. |

Structured sanitized evidence: [LWT_CONNECTION_TEST_EVIDENCE.json](LWT_CONNECTION_TEST_EVIDENCE.json). Proof envelopes, saved license, JWT, device identifiers and any account fields remain private; they were neither printed nor committed.

## Documented normal service protocol

The recovered client's default service is `https://lw-tool.com:443` (`recovered-core.py:40103-40111`); the scanner connection actually uses runtime `_SERVER_URL` / `_SERVER_PORT` / `_SERVER_HTTPS` (`24892-24900`), so an independently captured runtime hostname override is **not** available. Our HTTPS proof checks specifically reached the default host. The protected login/proof route and bearer-token handling are shown at `22768-22843` and `24901-24944`; live proof replies here were encrypted and not independently decrypted.

The normal Quick Map UI path is `toggleScan` → `scanNhanh('map')` → `py_scan_nhanh` (`recovered-ui.html:3791-3797,1611-1640`; `recovered-core.py:36514-36576`). The UI requires a game connection (`recovered-ui.html:1750-1754`). Core Premium enforcement is at `recovered-core.py:21569-21573,36521`; enabling `VIP Check` alone would not establish an entitlement. When admitted, the client sends **one** `POST /api/scan` with JSON `{loai: 'map', servers: [selected server], tung_server: true}` (`25046-25085`). Submission HTTP 200/202 **plus a nonempty job ID** establishes only job acceptance.

If accepted, the client uses `GET /api/scan/{job}/phan` with `X-Con`, `X-Xong` and `X-Trong` response headers to collect parts and a final completion marker (`25093-25176`). Otherwise it polls `GET /api/scan/{job}` for `trang_thai`, `ok` and `loi` (`25179-25208`). It handles HTTP 403 (scan key not enabled), 429 (another job), 422 `vip_tat` or `thieu_acc`, 404 expired jobs, network errors, stalls and timeouts. Partial Map rows merge through `_dat_ket_qua` and `scan_partial` (`25520-25584,36612-36626`); successful background completion emits `scan_nhanh` / `xong` (`36673-36678`). A short in-process dedupe/result cache exists for 30 seconds (`24953-25043`). None of those accepted-job/result events occurred in this live investigation.

## Times, freshness and next legitimate prerequisite

The only measured network latency here is the **0.625/0.437-second proof-response time**, not Map scanning. The original app's fresh login establishes a new authenticated session, but login-button-to-session time was not instrumented as a benchmark. Submission-to-first-result and submission-to-completion are **not available** because there was no accepted job. Likewise, row coverage, power/army-power field availability, service farm cache age, source collection timestamps and freshness cannot be evaluated. The recovered quick service may stream pre-enriched records, but that observation is independent of a completed current live job.

**Access blocker:** the existing, freshly authenticated account reports **Free**. The legitimate Quick Map entry requires a **Premium** plan, and the ordinary UI additionally requires an attached game. The owner must obtain genuine Premium access and connect their own home-server game before an ordinary button-to-first-row/completion test becomes possible. Do not retry this Free-plan trial against alternate endpoints or override the plan gate.

## Restoration and follow-up boundary

The original EXE launched one task-owned login root and a runtime child; the root exited during normal login. After the test, the exact task-owned runtime child was closed (graceful close attempted, forced process close when needed). Final process inventory showed **zero LwTool and zero LastWar** processes. The original file's SHA-256 was unchanged, and no game scripts were installed or altered. The application's own persisted session/settings may have refreshed during authentic login; those records were **left as the app wrote them** rather than restoring stale credentials.

No LW-Control code, production service configuration, source acceptance label, main branch, package, merge or release was changed by this test. This report records a valid authenticated account and a clearly bounded access blocker; it does not claim a service-denied `/api/scan` POST that was never made.
