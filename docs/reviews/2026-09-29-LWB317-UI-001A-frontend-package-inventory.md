# LWB317-UI-001A — LWBridge 0.3.17 frontend package inventory

Scope: static/headless inspection of the exact LWBridge 0.3.17 reference only.
No LWBridge or Last War process was launched, no desktop-control tool was used,
and no gameplay/auth/runtime behavior was investigated.

## 1. Reference identity

State: `EXACT_BYTES`

Reference:

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`

Observed properties:

| Property | Direct observation |
|---|---|
| SHA-256 | `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783` |
| File size | 15,866,880 bytes |
| Windows version metadata | File/Product version `0.3.17`; product/file description `lwbridge` |
| PE machine | `0x8664` — x86-64 |
| PE optional-header magic | `0x20B` — PE32+ |
| Subsystem | `2` — Windows GUI |
| Sections | 6: `.text`, `.rdata`, `.data`, `.pdata`, `.rsrc`, `.reloc` |
| PE overlay | none (`pefile.PE(...).get_overlay_data_start_offset()` returned `None`) |
| Authenticode security directory | VA `0x0`, size `0` |

The observed SHA-256 matches the project baseline exactly.

## 2. Packaging/framework identification

State: `EXACT_BYTES`

Direct facts from the executable and recovered frontend:

- The executable contains Rust crate source-path strings for Tauri 2.11.5,
  including `tauri-2.11.5\src\...`; one directly observed locator begins at
  raw file offset `0x82E5C8`.
- The executable also contains versioned `tauri-runtime-wry-2.11.4`,
  `wry-0.55.1`, Tauri IPC scripts/identifiers, and WebView2 runtime strings.
- Raw bytes around `0x83E5E8` contain the application identifier
  `local.lastwar.xlua.bridge`, `index.html`, the application CSP, the dev URL
  `http://127.0.0.1:1420`, `../dist`, and build icon paths
  `../build/app-icon.ico` / `../build/app-icon.png`.
- The PE has no appended overlay. The frontend bytes are inside `.rdata`.
- At raw file offset `0x8BA550` there are 24 consecutive 32-byte asset records.
  Each observed record is four little-endian 64-bit values:
  path VA, path length, blob VA, blob length. The path/blob VAs map into
  `.rdata`; every blob starts immediately after its ASCII asset path.
- The 24 blobs losslessly decompress as Brotli streams. The extractor preserves
  both the embedded compressed stream and the expanded file bytes.
- Recovered `web/index.html` loads `./assets/index-BVfnK1wp.js` and
  `./assets/index-rIL9Fpht.css`, with a `#root` mount element.
- The recovered main JS begins with `__vite__mapDeps` and contains embedded
  React runtime code plus `react-dom` and `createRoot`.

Interpretation supported by those direct facts: LWBridge 0.3.17 is a native
Rust Tauri 2 desktop application using Wry/WebView2 and a Vite-built React web
frontend. The web distribution is compiled into the executable as Brotli
compressed `.rdata` assets.

The exact React package version and original source/build manifest are
`UNKNOWN`; no version is inferred from runtime code shape.

### Exact tools/commands used

The relevant commands were run from the repository root:

```powershell
git status --short
git branch --show-current
Get-FileHash -Algorithm SHA256 -LiteralPath 'C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe'
Get-Item -LiteralPath 'C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe'
python -c "import pefile; ..."
rg -a -b -o <framework-or-asset-pattern> 'C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe'
pwsh -NoProfile -File tools/lwbridge317/extract_frontend_package.ps1 -ReferencePath 'C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe' -OutputRoot 'evidence\lwbridge-0.3.17\ui\frontend-package'
rg -o -n <recovered-string-pattern> evidence/lwbridge-0.3.17/ui/frontend-package/web
```

Tool versions materially used:

- Python 3.12
- `pefile` from the existing Python environment
- PowerShell 7.6.6
- `.NET System.IO.Compression.Brotli` assembly version 10.0.0.0

The exact extraction implementation and repeatable invocation are in
`tools/lwbridge317/extract_frontend_package.ps1`.

## 3. Frontend entry point

State: `EXACT_BYTES`

Asset-table record 11 at raw file offset `0x8BA6B0` points to `/index.html`:

- path raw offset: `0x875A1C`
- Brotli blob raw offset: `0x875A27`
- compressed size: 284 bytes
- compressed SHA-256:
  `F073AB88C2F8BB0EAB02B0ADD75B39F7021F6DD515626B893F995A844BDB7B63`
- recovered size: 599 bytes
- recovered SHA-256:
  `C10FDD0131AD2E9A868785844D22BAD18974C0F3F1990F938618A34E2742715E`

The recovered HTML directly contains:

```html
<title>lwbridge</title>
<script type="module" crossorigin="" src="./assets/index-BVfnK1wp.js"></script>
<link rel="stylesheet" crossorigin="" href="./assets/index-rIL9Fpht.css">
<div id="root"></div>
```

Therefore `/index.html` is the embedded web entry point, the main executable
frontend bundle is `/assets/index-BVfnK1wp.js`, and the linked stylesheet is
`/assets/index-rIL9Fpht.css`.

## 4. Asset inventory

State: `EXACT_BYTES`

There are exactly 24 records in the recovered Tauri asset table: 19 JavaScript
files, 1 CSS file, 1 HTML file, and 3 PNG files. No standalone font asset is
present in this table.

| Embedded path | Brotli bytes | Recovered bytes | Recovered SHA-256 |
|---|---:|---:|---|
| `/assets/AutomationCard-LCx_jIi7.js` | 1,638 | 5,092 | `24ED773623C237F8219EFF5443FAC3BA7B55B6E99E168E52FF066FAF2ABE7A61` |
| `/assets/SquadPanel-HC3-DJei.js` | 54,976 | 192,855 | `ED466E353AA896D8C3FC783FF1802930156FB9E9EB54327AEF73F2FF86FA06C7` |
| `/assets/rewardDisplay-eZWrd6iS.js` | 147 | 234 | `7F65DD3F5B81C96117AF5E83E8310D6C6A3A48787B1F693D387020255C78E73E` |
| `/assets/vi-BAUQ1cwP.js` | 20,466 | 81,129 | `F3290C7D819FDD3032A42D396B56FD3CADC313759FF7CD6A5165C51171ED085E` |
| `/assets/zh-CN-ByrbNejR.js` | 19,972 | 69,293 | `521521C8DF711A9D49FD98CE951C4D2A316A9DBFE704BD9E5F65E7686079E9BE` |
| `/assets/AutomationPanel-BJ0gIqFh.js` | 16,388 | 75,388 | `6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725` |
| `/assets/ko-SaFvwwmo.js` | 19,814 | 74,996 | `A1F507D7624A20CFB7136C18D6E91DBC2FB6453F808D8BC0AFD093D9AD76A409` |
| `/assets/pt-BaH2E9ai.js` | 20,647 | 75,010 | `18FE76A3A039EC0CE8041B178013752A83E7A913C4593227F6BAEF0859C5B862` |
| `/assets/HotkeyPanel-XA8idRHB.js` | 2,194 | 7,801 | `9BD7C11768693705391060D4F79119B5EA4A5229E042758165D3AD751A961BE6` |
| `/assets/SettingsPanel-DqxIWv_E.js` | 1,098 | 3,673 | `129CCBACDD5F309070A3E2728912165006F0FA35FCF748D56F7AA9500D690C2B` |
| `/assets/id-D9oeC8zM.js` | 19,603 | 71,315 | `06379AC43DEC11717971160BB090156C0B0BD307788BBFCA5CC5C5B0D46CE0B1` |
| `/index.html` | 284 | 599 | `C10FDD0131AD2E9A868785844D22BAD18974C0F3F1990F938618A34E2742715E` |
| `/assets/index-rIL9Fpht.css` | 18,682 | 126,217 | `3D87E9F65B39EACE6A1A254BFC90A38ACB72613D1FFF7236C7CEE7AB9BFAF545` |
| `/assets/en-BisSXcTB.js` | 19,511 | 72,319 | `0BF43D180EB93692A93B830B5D984E9D01DDEEA524340F2334FC63CBA527A731` |
| `/assets/zh-TW-B9GVGLk3.js` | 19,135 | 66,072 | `94580DA17BFCE95E121D49274C70724BB92BE2FA5806527CC01B96EDBB564C8E` |
| `/assets/dot-offline-CDBA2902.png` | 5,892 | 5,888 | `F118D321CCD185313695DB337823E1CFD04CCA96EB9FA3447B65455E2673C31D` |
| `/assets/CityLayoutPanel-DoNWkywK.js` | 5,680 | 19,322 | `C1B83A0B6524EF9AF510A114DF9679DAE0260D34B633659EB1A5FFB2AC5CF49C` |
| `/assets/icon-warning-D9WXqS3t.png` | 41,057 | 41,053 | `DA1A18E0007068C11D371A9D130C598F9B9A38F39F87D758FCC7FB9CC467853F` |
| `/assets/GameAssetImage-Diy9VTIr.js` | 1,049 | 2,347 | `2F92A87C3268497DF6425B1175DB6140E10AABCBDFAE02615F065D00E16458E0` |
| `/assets/MapDataPanel-B4GXEND2.js` | 14,640 | 57,600 | `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089` |
| `/assets/dot-online-gjSbTzBh.png` | 6,111 | 6,107 | `2875CC9945B55F113A7D1E194EE2A13050BF9DE6AAEC94D1DE1D664112A027D6` |
| `/assets/index-BVfnK1wp.js` | 105,654 | 377,972 | `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6` |
| `/assets/ja-UrbzJu-m.js` | 20,333 | 77,547 | `35969183AB623B6701BC850FDF28BCF268C93127FF6C9693F77E588EE7BDBE38` |
| `/assets/ru-YncvPDv4.js` | 23,015 | 101,169 | `1C41C89AD46D07A34673B9A72B0F66E139A28992C6D393A00133B870EC10C25D` |

The seven language-named JS files contain locale dictionaries, directly
verified from their recovered content. The named panel/component chunks are
Vite lazy chunks; their filenames establish their build-time chunk names, but
this work item does not claim behavior from those names alone.

The recovered PNG dimensions are:

- `dot-offline-CDBA2902.png`: 52 x 58
- `dot-online-gjSbTzBh.png`: 50 x 58
- `icon-warning-D9WXqS3t.png`: 170 x 198

The recovered CSS contains system font stacks such as
`Segoe UI,-apple-system,BlinkMacSystemFont,Arial,sans-serif` and
`Consolas,monospace`. No `@font-face` rule or font URL was found.

The executable also has seven `RT_ICON` resources, one `RT_GROUP_ICON`, one
`RT_MANIFEST`, and one `RT_VERSION` resource. Exact raw bytes, sizes, RVAs,
file offsets, and SHA-256 hashes for all 10 are recorded in
`frontend-package-manifest.json` and preserved under `pe-resources/`.

## 5. Exact recovered strings/navigation evidence

State: `EXACT_BYTES` for the strings and array below. Runtime visibility of the
conditional Advanced item is `UNKNOWN`.

The recovered main bundle contains this navigation definition on the minified
bundle line containing `var Zr=`:

```text
overview -> nav.overview
automation -> nav.automation
map-data -> nav.mapData
march -> nav.squads
city-layout -> nav.cityLayout
hotkeys -> nav.hotkeys
mini-games -> nav.miniGames
settings -> nav.settings
```

The recovered English locale bytes directly map those labels to:

| Navigation key | Exact English string |
|---|---|
| `nav.overview` | `Home` |
| `nav.automation` | `Automation` |
| `nav.mapData` | `Map Data` |
| `nav.squads` | `Squads / AFK` |
| `nav.cityLayout` | `City Layout` |
| `nav.hotkeys` | `Hotkeys` |
| `nav.miniGames` | `Mini Games` |
| `nav.settings` | `Settings` |
| `nav.title` | `Navigation` |

The same main-bundle code conditionally inserts
`{key:'advanced', label:'nav.advanced'}` immediately before Settings when its
`showAdvanced` input is true. The exact English value for `nav.advanced` was
not directly established in the recovered English overlay during this task, so
that visible string remains `UNKNOWN` rather than guessed.

No claim is made here that a build-time chunk name such as `MapDataPanel` is a
visible tab label; visible labels above come from the navigation array plus
locale dictionary.

## 6. What is still unknown

- `UNKNOWN`: exact React package version.
- `UNKNOWN`: original source tree, `package.json`, Vite configuration, and
  original unminified component filenames beyond names preserved in chunks.
- `UNKNOWN`: source maps. No source-map asset is present in the 24-record table,
  and no `sourceMappingURL` marker was found in the recovered web files, but
  this does not prove no source map existed in the original build environment.
- `UNKNOWN`: whether the conditional `advanced` navigation entry is visible in
  the reference state and its exact visible English string.
- `UNKNOWN`: exact runtime window geometry, hover/focus behavior, state-specific
  rendering, and pixel parity; no desktop/runtime observation was allowed.
- `UNKNOWN`: backend meaning of frontend IPC calls. They were deliberately not
  traced because gameplay/auth/runtime reverse engineering is outside this
  work item.

No 0.3.1 result was promoted as a 0.3.17 fact in this review.

## 7. Recommended next UI task

Recommended next task: `LWB317-UI-001B — static app-shell/navigation contract`.

Use only the recovered `index-BVfnK1wp.js`, `index-rIL9Fpht.css`, locale bundle,
and PNG/icon evidence to build a source-locator map for the top-level shell:
navigation order, exact labels, shell classes, CSS variables, dimensions,
spacing, typography, icon usage, and conditional states that are statically
provable. Keep implementation and gameplay/backend tracing out of that task.

That gives the later UI reproduction worker an exact static shell contract
before visual/runtime comparison becomes available again.
