# Milestone J — current-client compatibility reconciliation

Campaign: `LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001`
Observed locally: 2026-10-06
Validation mode: read-only installed-file hashing/LWLF parsing plus existing inert deterministic checks. No Last War launch/stop, IPC, scan, navigation, action, update, original-service access, or installation mutation was performed.

## Installed client identity

`python tools/check_current_client_compat.py` passed the fail-closed policy
`lwbridge-current-client-critical-anchors-3` with `ok=true` and no problems.

| Artifact | SHA-256 / observed identity |
| --- | --- |
| `LastWarLauncher.exe` | `F05F302959D3F7DAD08CF477F44415591675C586C81A1C86814BA8A51C72AE62` |
| `Game/LastWar.exe` | `905C98C1F89841F90B492556192BA0642F3D209A873CB8C1F7B3C340ACA0733D` |
| `Game/LastWar_Data/Plugins/x86_64/xlua.dll` | `D22D912F031C60F2649FDAF76D359D695511F7A37B93CD637B557F8346569D45` |
| `Game/LastWar_Data/Assemblies/Assembly-CSharp.rdl` | `BFB740B4570C58BD2BCC7FB83F9B83D8121CE10FB1BF49040E9FB8B08E958B3E` |
| LocalLow `lwScripts/LWScripts.data` | `248F3AEAC712B3F14F86BFF37A0C365E467897A2248403837C44C1B774F05B22`; 41,320,262 bytes; LWLF file version 3; content version 22; 18,741 entries |
| `LWScripts.txt` | exact metadata `41320262|3283604722` matched package size/CRC |
| `version.txt` | exact marker `22`, equal to parsed LWLF content version |

The four compatibility-critical Lua entries also matched the pinned policy exactly:

- `DataCenter/Global/LuaEntry.luac`: `50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137`
- `Global/ConstDefine.luac`: `95e6c733b98dc641c330f36efdef044845033b37ea6703068f7c7ed5fb0048fd`
- `Util/CSharpCallLuaInterface.luac`: `af1559723afba0fa5773bb815c486f3233fecb3928768abd082a96b51960229e`
- `UI/LWMainUI/Component/UIMainBottom/UIMainChangeScene.luac`: `3843dc02869330f060a199b946ef3cd30aa335504914c2f4c60f8a824e871c5b`

This is compatibility evidence, not live execution evidence. The installed source anchors are supported; no claim is made here that every category currently has positive rows or that a state-changing action was exercised.

## Eight-category reconciliation

The canonical production owner remains `LWBridge.Map317` through
`Map317CommandService`. The current-client compatibility gate above closes the
installed-artifact identity prerequisite for the source-backed producers, while
the campaign's inert checks exercise their contracts without touching the owner
game process.

| Kind | Source-supported production path | Installed v22 anchor compatibility | Historical live evidence | Current campaign inert proof | Remaining limit |
| --- | --- | --- | --- | --- | --- |
| City | current-view fallback + Fast full-world batch | PASS | positive historical V22 category run (6,850 rows) | current-client injected-source tests plus all-eight admission; canonical Map317 acceptance tracked separately | no campaign live rerun |
| Resource | current-view fallback + Fast full-world detail collection | PASS | historical resource acquisition/query/clear classified LIVE_PROVEN | current-view/filter/zero-row/Fast/all-eight injected-source checks | no campaign live rerun |
| Monster | Fast batch world producer | PASS | positive historical V22 category run (9,568 rows) | Fast/coarse/boss/protection/retry/all-eight deterministic checks | no generic fallback if Fast admission fails |
| Truck | Fast batch/train-list source | PASS | positive historical V22 category run (61 rows); historical Follow positive | Fast Truck/all-eight deterministic checks | no generic fallback if supported source unavailable |
| Railway | Fast batch/train-list source | PASS | source path live-proven, but historical current run returned zero rows | Railway/all-eight deterministic admission and source-shape checks | positive current-row/Follow proof remains population-gated |
| Secret Task / Dispatch | Fast AOI batch source | PASS | positive historical V22 category run (18 rows) | Fast Dispatch/all-eight deterministic checks | no generic fallback if supported source unavailable |
| Ghost Ops | Fast AOI batch source | PASS for scan/source anchors | historical seven-kind run returned zero rows | Ghost/all-eight deterministic scan admission | positive producer output unproven; Ghost plunder preparation remains explicitly unavailable |
| Treasure | Fast AOI batch + source-backed read-only state inspection | PASS for scan/state anchors | historical scan population may be zero; read-only Treasure state refresh was historically live-proven separately | Treasure/all-eight deterministic scan admission; read-only state contract/cache tests | positive campaign producer output unproven; claim/status execution provider remains explicitly unavailable |

The producer fallback boundary is unchanged: only City and Resource have the
generic current-view capture fallback. Monster, Truck, Railway, Dispatch, Ghost
and Treasure must fail closed if their supported Fast/train/current-client path
cannot be admitted. Zero-row Railway/Ghost/Treasure runs are not positive output
proof.

## Evidence separation

- **Recovered original/current source contract:** source locators and DTO/timing
  contracts recorded in `map-lane/findings.md` and the recovered frontend/host
  evidence.
- **Installed compatibility:** the read-only policy run above proves the currently
  installed v22 critical executable/assembly/xLua/Lua anchors still match the
  supported current-client contract.
- **Historical live proof:** retained only as dated provenance; it is not promoted
  to current-campaign execution proof.
- **Current campaign implementation/inert proof:** deterministic injected-source,
  Map lifecycle, Auto Scan, canonical UI and isolated native checks; these do not
  operate the owner's game.
- **Still blocked/unknown:** positive current Railway/Ghost/Treasure populations;
  Treasure claim/status execution; Ghost plunder preparation; and any fresh live
  state-changing action without separate authorization.

## Recovery closeout status

Milestone J's read-only installed-source compatibility result remains valid and was
not converted into live proof by the recovery. The implementation checkpoint
`eaa73ce571d0c419a0e2880e212c46151ee7fd1f` and isolated package evidence close the
source-supported Home/Map host/runtime work without touching the installed game.
The population/action limits above remain the only J-facing external limits and are
carried unchanged into final `AWAITING_REVIEW` delivery.
