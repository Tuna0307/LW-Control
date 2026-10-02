# Truck and Alliance Train retained-goods menu

Status: **COMPLETE implementation for focused source/local UI scope**.
Project lead owns this takeover unit; independent peer review is a follow-up.
Global Map/UI/native/original-pixel acceptance is unchanged.

The clone's native select differed from actual original ct at UTF-8 byte 28232
in MapDataPanel-B4GXEND2.js, SHA-256
CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089.
Canonical MapRetainedGoodsFilter now reproduces its details/summary and ordered
icon/name buttons, strict selected-key matching, summary fallback/active rules,
raw key callbacks and close-after-callback behavior. Original parent byte 53802
remains handled by existing changeItemFilter: page 1, per-kind goods state and
only itemCount sort removal when clearing. No CSS/catalog/provider changes.

Native game icons are still unavailable; the original GameAssetImage unloaded
span branch (byte 2250, asset SHA-256
2F92A87C3268497DF6425B1175DB6140E10AABCBDFAE02615F065D00E16458E0)
is used for the slots and accessible names. This is not loaded-icon parity.

144 actual original/current render/handler comparisons pass across nine
languages, empty/populated and numeric/string/zero/missing/nameless cases.
Six actual original unloaded-asset comparisons pass. Thrown callback does not
close; null ref is safe. Actual original/current page callbacks produce the
same request/sort traces and preserve separate Truck/Train choices. Immutable
fbde5d6 confirms the old native-select mismatch.

Fresh English/light and Japanese/dark browser QA verifies selection/close,
real Enter opening/selection/All, active reopening and tab retention. Real
Japanese goods sorting then clearing removes item sorting and retains Updated
At. Screenshots were saved and inspected; no captured errors/warnings.

Maintained Map tests pass: prior Treasure picker 28, filter lifecycle/R1,
request lifetime 38/0, navigation 17/0, interactions 38/38, integration 14/14,
historical filter/table/Checking/row/state. Historical adapters change only
test entry points/new child bindings and redirect writes; assertions are intact.
Canonical check/build/package and new evidence/WIP validator pass. Product
fingerprints: 25eb7cb08532f43912dce8b53ed9d1f6b81cfa82d871950a643476f71a66d1af /
2ad8afb5be9924832bc39151094d77995a8e1f5ad786aaa527643055a47c9b62.

Evidence: evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-GOODS-PICKER-001/.
Owned tab/Vite preview is closed before delivery. Existing AFK, scratch,
CORRECT-003 screenshots and parent lifecycle normalization-only WIP are retained.
Next: returning worker can review both picker units together; next lead UI
implementation can cover manual scan header/summary/timing and feedback.
No Last War/native gameplay operation or original protected runtime was used.
