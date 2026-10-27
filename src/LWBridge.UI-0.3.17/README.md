# LW-Control frontend (LWBridge 0.3.17 UI)

The React 19 + Vite frontend for the desktop app. It reconstructs the original LWBridge 0.3.17 UI,
without login/licensing. `src/LWBridge.Desktop` builds it automatically: `npm ci`, then
`npm run build` into `ProductionUi/`. The UI talks to the C# backend through WebView2
(`src/backendBridge.js`).

```powershell
npm install
npm run check   # all UI checks (two need Microsoft Edge on Windows)
npm run build
npm run dev     # browser preview with fixture providers at 127.0.0.1:4317
```

- `src/`: pages (`*Page.jsx`), feature cards, runtime hubs, and `locales/` (nine languages, kept
  complete by `check-ui-complete.mjs`).
- `scripts/check-*.mjs`: UI checks (jsdom-mounted pages with fake backends; Playwright/Edge for
  layout checks). `check-production-build.mjs` validates a built bundle and is also used by the
  release packager.
- `reference/original-0.3.17/`: one original LWBridge asset that a check compares against.
