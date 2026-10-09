# HOME-004 R3 lead review — 2026-10-10

Submission: 7b36bfdd8de04a2193a32f49eb8465c64dcfd659.
Disposition: PARTIAL / CHANGES_REQUIRED. PR #6 remains draft; main unchanged.

## Credited work

Fresh current Release native suite passes the new real protected-adoption,
registration and timer-produced lease delayed-acknowledgement test. Five frontend
groups, nine 1,383-key catalogs and ProductionUi integrity also pass. No product
source changes were submitted in R3; tests, task tools and documentation changed.
The lead visually inspected actual EN/light and JA/dark relaunch screenshots.
LEADHOME004R2-01 capture absence receives credit; these images show Disconnected
and official-launcher starting, not authenticated recovery success.

The two saved helper sessions identify original PID 47684 and successor 40668.
Both Stop receipts confirm exit and script restoration; the successor uses
process-handle-signalled. Current installed triplet hashes match the preflight,
no game/launcher/host processes remain, and recovery/adoption records are absent.
Review ZIP hash matches E04D9497B53BC73055541ABF23A90513DA4A3B0713D1AB94680910F96E6092BF.
Remote and draft PR head match the submitted source. The native live attempt used
the R2 executable; R3 did not modify its production code, so identity is consistent.

## LEADHOME004R3-01 — session failure ends the shared listener

The successor's saved adapter status is:
`error:Win32Exception:WaitNamedPipeW failed (Win32 2)`.
Its pipe-transport report has adapterLoaded=true, adapterActive=false,
clientConnected=false, state=error. Both status files were last written at
17:11:52 UTC, before its Stop receipt at 17:11:54. The preserved world-state RPC
belongs to ORIGINAL PID 47684, not a successful successor 40668. A helper-ready
marker and host-start receipt alone do not prove authenticated host admission;
the host writes that start receipt before WaitAuthenticatedRouteAsync.

Independent isolated REAL Windows pipe reproduction, zero game launches:

1. Start the actual LWBridgeControlPipeHostState with a unique owned pipe and
   the probe process as explicitly permitted inert client identity.
2. Authenticate one valid hello/ACK with exact PID/path/token admission. Confirm
   AuthenticatedSessionCount=1 and connected route.
3. Keep the client open but idle through the CURRENT configured 30-second reader
   idle timeout, without asking the host to stop or altering its lifetime token.
4. The shared listener task ends with OperationCanceledException. IsStopped=false
   and IsRpcTransportStarted=true nevertheless.
5. EnsureRpcTransportAsync with the same build/path does not restore a listener.
   Register a new session; its native client connection times out.

This reproduces a production transport/lifecycle defect independently of LastWar.
The RPC session exception escapes RunAuthenticatedRpcSessionAsync into the accept
loop; its only outer cancellation catch requires the HOST token to be cancelled.
StartRpcTransport/EnsureRpcTransportAsync retain the completed loop object.
Evidence: proof/HOME-004-R3-LEAD-PIPE-INVERSE.json. Repeatable historical inverse:
`dotnet run --project tools/home_004_pipe_idle_probe/Probe.csproj -c Release`.
Exit 0 means the defect was reproduced, NOT that product behavior passed.

This is strong supporting evidence for the failed live successor, but there is
no saved host exception/event trace connecting every step of that particular
attempt. Preserve this causal limit. The configured timeout file cites 0.3.1;
revalidate affected timeout/session-failure/listener rules against the actual
0.3.17 artifact before claiming exact original semantics. Do not lengthen or
disable timeouts, weaken authentication or invent retry behavior to pass a test.

## Continuation

Finish the medium R3-R1 unit in HOME_004_R3_R1_CONTINUATION.md: recover the exact
affected listener/session contract, correct the demonstrated lifetime defect,
prove a valid successor after actual session failure, and repeat the bounded
packaged hang recovery with retained detector/relaunch/authenticated verification
events. Live pending Stop remains unproved and must be labelled accurately.
No whole-Home acceptance, merge or publication is granted.

The lead made no game launch, live desktop capture/input or installation mutation.
Only source review, passive cleanup checks, local saved image inspection and
isolated native pipe/inert delivery checks were executed.
