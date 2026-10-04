# Source image frontend recovery — 2026-10-04

Status: isolated `EXACT_CONTRACT` local proof, integration awaiting lead review.
This packet covers canonical `GameAssetImage.jsx`; it does not implement a new
native reader, fetch original-service data, or claim original pixel equivalence.

Exact reference `GameAssetImage-Diy9VTIr.js` SHA-256:
`2F92A87C3268497DF6425B1175DB6140E10AABCBDFAE02615F065D00E16458E0`.
UTF-8 byte anchors: cache insertion `g`156, queue drain `v`421, subscribe `y`812,
source normalization `b`1165, component `x`1307. `caller-locators.json` pins all
16 recovered image field expressions across Map, Automation and Squads.

Run from the repository root:

```powershell
node evidence/lwbridge-0.3.17/ui/LWB317-UI-FINAL-INTEGRATION-001/images/check-images.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-FINAL-INTEGRATION-001/images/validate-images.mjs
```

The checker executes the actual original module from its recovered bytes with
controlled promises, queue and clock, and mounts original/current React in jsdom.
The 16 passing groups include 13 original/current comparisons and 36 individual
normalization comparisons. The additional groups verify intentional clone reader
absence, identity isolation, Context inheritance and explicit-null fencing. The
existing jsdom tool path can be overridden by `LWB317_JSDOM_PACKAGE`; React and
esbuild are resolved from the canonical UI package.

Recovered behavior retained:

- Exactly one trimmed nonempty `assetPath` or `spriteName`; invalid combinations
  retain the exact span placeholder. Loaded image has lazy loading and async
  decoding. Caller alt/classes are unchanged.
- Optional visibility gating uses IntersectionObserver margin `160px 0px`.
  Missing observer support loads immediately. Subscription cleanup fences stale
  React writes; already running requests may populate the source cache.
- Duplicate reads coalesce. Two requests run concurrently. Queued requests with
  no listeners do not dispatch. Queue drains are scheduled at zero milliseconds.
- The cache counts data-URL string characters up to `32 * 1024 * 1024`; it is not
  decoded-byte accounting. Eviction uses insertion order and cache hits do not
  promote entries. Oversized results are delivered but not cached.
- Failures suppress that key for 60 seconds. Expiry alone does not start retry;
  a later subscription can do so.

Integration contract:

```jsx
<GameAssetImageProvider readImage={stableVerifiedOrLocalReaderOrNull}>
  <GameAssetImage assetPath={item.iconPath} alt={resolvedName}
    className="map-reward-icon" />
</GameAssetImageProvider>
```

The reader accepts `{assetPath, spriteName}` and returns a Promise resolving to
`{dataUrl}`. An omitted per-image `readImage` inherits Context; explicit null
fences a particular image. Reader identity owns its cache and visible image
state, so preview data cannot carry over to another reader. Root owns binding the
already established available backend reader or explicit browser-local data URL
fixture. Unavailable reader stays null and makes no request.

The existing host contract was inspected, not invoked: current
`Map317CommandService.cs` case `game_asset_image` at line99, method211..224 and
`ManualMapScanCommandService.cs:GetAssetImageAsync`539..564 accept exactly one
source and return `dataUrl`; absent current-client reader rejects
`GAME_DISCONNECTED`. Native path mappings/content are separate proof obligations.

DOM tests intentionally use inert strings, not decoded reference icons. They
prove attributes and lifetime, not physical image geometry. Browser image and
all caller integration evidence belongs to the lead campaign. No native/game
action, original entitlement/session control, or protected service was invoked.
