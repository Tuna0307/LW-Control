# Owner guide ? Overview Launch, bridge message and Close

**Current PM review 17:** no new owner test is requested yet. Web must finish PM17-02/01 and prepare the exact corrected-build packet plus automatic collection. The accepted instructions/build below are historical. The next packet will use only normal Overview Launch/message/Close; you will not need commands, logs, a second game installation or damaged files. No Map Data test is assigned.

Updated 2026-09-11. **OWNER ACCEPTED (`LWB-OVL-003`).** The owner completed the normal Overview Launch -> in-game message -> Close sequence, supplied a screenshot showing **LWbridge is running** near the top centre, and reported ?yup all working?. This file now preserves the tested sequence; do not ask for another run unless a future change specifically requires regression testing.

## Before you start

Close Last War and LWBridge if either is already open. In the `LW-Control` folder, double-click **Start Overview Verification.cmd**. Do **not** use `Start Owner Resource Check.cmd`; that is the old read-only resource recorder.

The tested package behind this shortcut is the Release build from implementation commit `dc82bc9bebd9e1b56c56621edd18e905a7e3ee23`. The shortcut self-test, Release build, deterministic checks and GitHub Actions run `34603661402` all pass.

## What to do

1. Wait for LWBridge to open on **Overview / ??**.
2. Click **Launch Game / ????** **once**.
3. Wait for Last War to open. Allow up to about **2 minutes** for the launch/bridge handshake. Do not click Launch repeatedly.
4. In the game, look near the **top centre** for the exact text **LWbridge is running**.
5. Take a screenshot showing the game and that text, then send it to ChatGPT.
6. Return to LWBridge and click **Close Game / ????** **once**.
7. Wait for Last War to close and for Overview to return to its stopped/offline state. Tell ChatGPT that it closed; a second screenshot of the stopped Overview is helpful but not required.

If the game does not open, the text does not appear after the wait, LWBridge shows an error, or Close Game does not close the game, **stop there** and send a screenshot plus what you saw. Do not retry the sequence unless ChatGPT asks after reading the automatic evidence.

## What ChatGPT records automatically

The normal app and deployed helper write one session-correlated evidence bundle containing build identity, current-client identity, game PID/session/challenge correlation, game-side readiness/message evidence, Close/exit result, exact script restoration, and cleanup state. You do not need to run commands, find logs, inspect JSON/databases, or compare hashes.

Use this simple report: **I reached step __. I clicked __. I saw __. Here is the screenshot.**

Owner acceptance is complete for this milestone. Work now stops until **the owner explicitly chooses the next feature**; Resource/Monster work does not resume automatically.
