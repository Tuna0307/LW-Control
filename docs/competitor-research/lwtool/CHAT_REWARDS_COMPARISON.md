# Red Packet, Fireworks and Gold Egg comparison

25 October 2026. Source comparison and inert verification only; no product
change, game launch, claim, chat reply or external service request in this pass.
LW-Control baseline: `a1f9137267315356b341f2dcaea4568652edd3b3` on
`codex/lwtool-remaining-tabs`. The prior source-pinned test package is unchanged.

**Finding:** our combined “Fireworks / Egg” label does not establish parity with
LwTool's distinct Gold Egg and cross-server Gold Egg features. LwTool exposes
more controls and a broader cross-server discovery route. We cannot rank claim
speed or reliability without its game-side claim implementation and matched
live opportunities. An independently reproduced success-counter ambiguity in
our Fireworks path should also be corrected.

RECOVERED denotes the authenticated LwTool client snapshot; SOURCE-PROVEN denotes
our current code or decoded original game bytecode; CONTROLLED denotes offline
fixtures. None of these labels means a successful live claim was observed.

## Side by side

| Area | LW-Control | LwTool recovered client | Supported conclusion |
|---|---|---|---|
| Red Packet | Separate card, event listener, configurable min/max delay, active hours, optional replies, native template/channel eligibility | Separate `redPacket` control and `redPacketInterval` in milliseconds; separate reply-kind setting | Both expose the feature. LwTool's exact native claimant is not recovered |
| Fireworks | Combined Fireworks / Egg card; recognizes post 693, derives native gift type from its config and sends `GetFireworksGift` | Separate `fireworks` control and `fireworksInterval` in milliseconds | Both expose Fireworks; ours has a source-traced native route |
| Gold Egg | No distinct config kind or subtype selector; a compatible post-693 gift can use the generic path, and compatible post-611 gifts may use Red Packet | Separate `goldEgg`, `goldEggInterval`, `goldEggCross`; birthday-egg exclusion control | Our combined label is broader than our demonstrated taxonomy. Do not say every egg is unsupported or fully supported |
| Additional message families | Current classifier ignores 694, 695 and 710 | Fast cross-server feed forwards 611, 693, 695 and 710 when Gold Egg + cross-server are selected; merchant 662 is separate | Broader discovery is proven in the host. Successful claiming of each family is not |
| Cross-server discovery | Watches rooms already present in the official game; no dedicated cross-server collector, subscription controller or LwTool feed | Gets official season server list and consumes an authenticated SSE chat feed, forwarding selected events to its in-game Lua handler | These are different discovery methods. Ours can see messages in already-loaded remote/season rooms; that is not equivalent autonomous coverage |
| Cross-server claim routing | Red Packet forwards native message `targetServer/serverId`; Fireworks uses the native UUID/owner/type request | Events reach `_lwChatNhanhTin`; final native claim code is in the unavailable separate engine | Request routing and discovering remote opportunities must be assessed separately |
| Success/refusal reporting | One-send queue, UUID/server deduplication, native event/cache confirmation; counter ambiguity described below | User-log adapter distinguishes already claimed, exhausted, expired and missing response | LwTool's recovered reporting vocabulary is more explicit; it does not establish engine-side no-replay correctness |
| Timing | New game-chat events queue immediately; nominal engine tick is 50 ms. Retained-room backup runs every 15 seconds only when work exists | Local claim scheduling is in the separate engine. Optional fast feed adds server/SSE/Python/IPC stages | Zero configured delay does not mean zero end-to-end latency. No measured winner |

LwTool's UI comment describes Gold Egg as tax/station/magatama/diamond gifts and
places birthday goods `670001` under its Gold Egg filter. This is recovered UI
intent, not an independently recovered native classification table. Our generic
native template/type readers might claim some such gifts without identifying
them as Gold Egg; the exact overlap remains unmeasured.

## Actual methods recovered

LW-Control: official game room events → owner-bound job queue → configured due
time → original native game claim → native event/cache observation. It installs
two chat event listeners plus Red Packet/Fireworks result listeners, scans all
already-loaded rooms for retained messages, and caps the queue at 512. Event
messages do not wait for the 15-second backup scan. The shared feature scheduler
is nominally 20 Hz; a zero-delay queued job normally waits for a subsequent
eligible tick. Frame load and earlier work can add delay; this is source-based
reasoning, not a measured latency distribution.

Red Packet requires the game's `CanNotPickRedPacket`, packet template `Condition`
and a recognized native World/Alliance/Season/AliFriend channel. Fireworks reads
the original Firework config's `type`. These use different native requests.
Targets are keyed by kind/server/UUID; sent targets are not blindly replayed
after missing confirmation. Active hours and optional replies are per service.

LwTool: settings flow into the account/window configuration and `InitConfig`.
The three feature switches are distinct. Free-plan configuration masking lists
Red Packet, Fireworks, Gold Egg and cross-server Gold Egg among the restricted
auto flags; no entitlement was changed or tested against a service here.

Its optional fast chat branch requests a live SSE stream at
`/api/chat/luong?sv=<server-list>&tu=<sequence>` using normal authentication.
It discovers server IDs through the official player/season managers, filters
message posts according to selected features, preserves the largest sequence,
and sends normalized announcements through `GameClient.send('rlua', ...,
nhanh=True)` to `_lwChatNhanhTin`. Gold Egg cross-server plus `vipCheck` must
request this branch, and a global diagnostic-disable gate must permit it;
merchant cross-server selection can independently request the same feed.
The host signals fast mode off on HTTP/network failure or a negative keepalive.
The engine's complete fallback behavior is not recovered.

The stream supplies **announcements/opportunities**, not proof that LwTool's
server awards the game's rewards. The last game-side parser/claimant is absent
from the recovered core/UI; it belongs to the separately downloaded Lua engine.
No current local `_lwl.cache`/datnew payload was found in the selected retained
artifacts or beside the selected executable. We did not invent its contents,
enable Premium, contact the stream or claim that its advertised speed is proven.

## Current game taxonomy and controlled checks

Decoded original game v27 maps 611 to `RedPackge_New`, 693 to
`FIREWORK_GIFT_REWARD`, 694 to its double-gift post, 695 to
`S0_ALLIANCE_BOSS_GIFT_REWARD`, and 710 to `EasterEggChat`. These names identify
distinct game message families; the Gold Egg marketing group does not make all
of them the same reward request. In particular, native Easter Egg requests have
their own manager/messages. Do not handle every additional post by blindly
sending `GetFireworksGift`.

The exact v27 original package hash is
`8f61102f401f3052598365ca9750c2c496969895b5add2da138cb2147d045493`.
Its 95 selected relevant decoded modules were compared with the current installed
container: all 95 encoded module bodies matched. The installed whole-container
hash differed, so it was not mislabeled as an untouched original. Everything
was read-only; an existing app session was left alone.

Controlled replay of our actual Lua, with all five native post constants
present, sent one mocked Red Packet request for 611 and one mocked Fireworks
request for 693. It sent none for 694/695/710. This demonstrates classifier
coverage, not missed live rewards. Separately, the exact recovered pure LwTool
gate/filter functions reproduced its feature-dependent post sets. Its 695/710
forwarding is not evidence that every forwarded item is claimable.

## Confirmed reporting ambiguity in ours

The native `GetFireworksGiftMessage.HandleMessage` calls `SetGiftUuidGot(uuid)`
for error `firework_tips_1016` (already claimed), as well as setting that cache
after a successful receipt. Our `claimed(job)` polls this same boolean, and
`ack()` then increments `claimed` and can schedule a reply. It cannot distinguish
“already claimed before this attempt” from “new reward confirmed by this attempt”
on that path.

A controlled counterexample submitted one mock request, delivered no success
event/reward response, then changed the native got-cache as the already-claimed
handler would. Our counter advanced to 1. The bytecode and inert reproduction
support this defect; its occurrence rate in real play is unknown. An existing
positive engine suite still passes because it does not distinguish these two
sources of the cache update. LwTool's log adapter explicitly maps this refusal
to already claimed; its native handling is still unknown.

## Recommended next implementation, not performed here

1. Separate Fireworks and Gold Egg controls, first defining a native-source
   taxonomy for every desired subtype. Preserve correct existing 611/693 routes.
2. Add only source-qualified missing discovery/claim families with exact
   eligibility, cross-server identity and success/refusal correlation. Additional
   announcements alone do not justify another reward sender.
3. Distinguish new success, already claimed, exhausted, expired, refused and
   unknown. Keep no-replay tombstones; do not send a success reply for cache-only
   already-claimed outcomes. This is the clearest immediate correctness fix.
4. Qualify optional cross-server discovery through authorized native game
   facilities, with bounded subscriptions and cleanup. A dependency on LwTool's
   paid feed is not included in this recommendation.
5. Measure announcement arrival → eligible send → actual native outcome on the
   same reward instances before changing the 50 ms scheduler or declaring a
   speed winner. An immediate zero-delay event path needs reentrancy/ownership
   qualification rather than simply increasing polling frequency.

Evidence, source anchors and bounds: [EVIDENCE.json](../../design-notes/evidence/CHAT-REWARDS-2026-10-25.json). Reproduction:
run `python docs/research/lwtool/2026-10-25/chat-rewards/verify_controlled_comparison.py`
from the repository root with the retained private inputs and existing inert
Lua runtime. Recovered competitor/game code stays ignored; only the comparison,
hashes and our reproducible analysis harness are committed. No new package,
merge or release was needed for this documentation-only comparison.
