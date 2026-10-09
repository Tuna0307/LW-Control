# LWBridge 0.3.17 UI reconstruction

This is the clean, separate frontend project for the Phase 1 LWBridge 0.3.17
UI-parity campaign.

It intentionally contains no original LWBridge login/account/licensing flow and
no gameplay/backend implementation. During the UI campaign it is a static
preview/reconstruction surface backed only by recovered UI evidence.

## Commands

```powershell
npm.cmd install
npm.cmd run check
npm.cmd run build
npm.cmd run dev
```

The development preview binds to `127.0.0.1:4317`.

The legacy `src/LWBridge.Desktop` project remains unchanged and is not the
0.3.17 clone.
