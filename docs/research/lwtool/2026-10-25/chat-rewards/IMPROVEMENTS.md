# Chat reward implementation and qualification

Product source `6d9c107166e92dbbf204d5c611ecd2a4d497625d` on
`codex/lwtool-remaining-tabs`, draft PR #17. The owner approved implementation
after the [historical comparison](COMPARISON.md). This report supersedes that
comparison's description of our current implementation, while retaining its
original findings and negative evidence. Independent review remains required;
no main merge or release occurred.

The useful improvements are implemented and have controlled regression proof.
Read-only native configuration inspection and exact script restoration passed.
**No new live reward claim, chat reply, cross-server history request or matched
LwTool speed comparison was performed.** Random reward availability and the
current main-account login leave those live positives unqualified.

## What changed

| Area | Current behavior | Evidence and practical limit |
|---|---|---|
| Outcome accounting | A got-cache change alone never increments new rewards or schedules a reply. Exact native success does; identified refusal, already claimed, exhausted, expired and unknown outcomes are distinct | Original handlers plus distinguishing controlled inverses. An unidentified error remains unknown; it is never assigned to the only pending request by assumption |
| Gold Egg | Separate card, delay, active hours, reply settings, birthday exclusion and cross-server opt-in | Source-qualified native taxonomy; not a claim that every event called an egg is supported |
| Existing profiles | Legacy Gold follows the original Red Packet/Fireworks per-route switches, delays and replies until explicit separation | Migration is read-only and uses a sparse native marker. No new 695 discovery or cross-server collection is enabled by migration |
| Packet families | Tax, trade/magatama, lucky, diamond and birthday gifts use their original packet template/channel checks and `OpenRedPacket` | Fresh native template metadata confirms the fields; real claims were not sent |
| Boss gifts | Post 695 and compatible type-1 post 693 can enter Gold Egg | A location announcement alone cannot cause a claim. Exact native UUID/owner/config/type-1 gift cache, native availability, remaining count and home-server eligibility are required. Missing cache is checked for at most two seconds, without a new lookup RPC |
| Ordinary Fireworks | Separate displayed Fireworks card retains the original type-0 gift route | Native type **0 is valid**. The older internal title remains for compatibility |
| Special activity eggs | Post 710 remains unsupported and visibly described that way | Native activity/chat/view flow does not establish an exactly attributable generic reward request. Post 694 is also not newly enabled |
| Cross-server discovery | Optional bounded reads of native, already available country rooms on native season servers | No fabricated room IDs, joins, entitlement changes, LwTool feed or server dependency. This is not equivalent to LwTool's broader SSE feed |
| UI efficiency | Four mounted Chat cards share one non-overlapping status read every two seconds | Controlled test proves four consumers receive one read, and A→B→A generation changes discard old results. This reduces those status requests by 75% when all four cards are mounted; it is not a measured claim-latency or CPU improvement |

The exact native ledger uses route/server/UUID, so moving a reward between cards
or receiving both announcement types cannot send it twice. Unknown attempts
retain the sent tombstone and can accept a later exact success without replay.
Native response wrappers preserve original execution, return arity and errors;
cleanup removes only our own wrapper and leaves a successor wrapper intact.
The protocol has no client attempt nonce: exact UUID/server observation is the
available correlation, not cryptographic proof that no other game UI acted.

Cross-server history uses at most eight rooms per batch, one batch per fifteen
seconds, at most 32 eligible rooms, and coalesces history callbacks into one room
inspection per tick with at most 200 messages processed. Native pending-request
deduplication is retained. Permissions and account/session ownership are checked
again before reading or claiming. Disabling cross-server claims is checked again
at send time. This deliberately bounded native route does not establish worldwide
coverage, freshness or parity with LwTool's private collector.

## Native findings and account boundary

The ordinary gameplay harness stopped at account admission because the login
was the main account, before submitting a feature command. It exited normally
and restored originals. Following the owner's clarification, a separate fixed
read-only catalog proof was used. It requires explicit isolated UID/server
identity, rejects the staged gameplay queue, calls only `feature018_chat_catalog`
and verifies ownership/account again after the read. The gameplay sub-account
guard was preserved.

The catalog returned **37 packet templates and 10 Firework configurations**:

- Packet template 1 has goods 653052: diamond; 401 has goods 670001/type 5/
  special type 5: birthday. Tax is type 1/special 2, trade type 3/special 3,
  lucky type 2/special 4. Other templates are not guessed into those families.
- Five Firework configurations have type 0; five have type 1. Boss announcement
  source routes through finding a world point, while the actual native bubble
  claimant takes the eligible gift object's UUID/owner/type.
- This session exposed **eight native season servers but zero eligible known
  remote rooms**. Native room-list/history methods exist, but no remote history
  request was sent. The enabled UI displays available remote-room count, including
  zero. No live cross-server discovery or claim is established.

Decoded original v27 bytecode, rather than a conjectured endpoint, supplies the
native method evidence. The original package SHA-256 is
`8f61102f401f3052598365ca9750c2c496969895b5add2da138cb2147d045493`.
Relevant original modules include `RedPacketTemplate`, `ChatRoomManager`,
`GetFireworksGiftMessage`, `LWFireworkGiftManager`, `LWFireworkGift`,
`ChatItemS0AllianceBossPushMsg`, `FireworkWorldBuildBubble` and
`ActEasterEggManager`. Original module hashes and the earlier 95-module comparison
remain in [EVIDENCE.json](EVIDENCE.json); new proof receipts are indexed in
[IMPROVEMENT_EVIDENCE.json](IMPROVEMENT_EVIDENCE.json). Raw account/chat material,
recovered competitor code and native dumps remain ignored locally.

## Qualification and reproduction

The full UI suite and Desktop delivery suite passed, including existing Home/Map
regressions. New suites distinguish cache-only from success, refusal variants,
wrong/missing UUID and wrong/ambiguous server, late success, no replay, original
handler errors/returns, owner loss and successor cleanup. Gold fixtures cover
native packet families, independent controls, migration, birthday/cross-server
filters, exact boss-gift eligibility and 710 refusal. Cross-room fixtures cover
native permissions, revoked permissions, request bounds, deduplication, callback
coalescing, off-hours/opt-out/owner loss and cleanup. Mounted tests cover migration,
separation, saved options, zero coverage, refusal presentation, stale profiles and
all nine catalogs' 17 added strings. All 31 package-auditor tests passed.

Run from the repository root:

```powershell
python docs/research/lwtool/2026-10-25/chat-rewards/verify_improvements.py
dotnet run --project tests/LWBridge.Desktop.Checks -p:SkipCanonicalProductionUiBuild=true -- --chat-rewards
npm --prefix src/LWBridge.UI-0.3.17 run check
python -m unittest discover -s tools -p test_map_015_package_ui.py
```

The historical comparison reproducer now loads its old engine from its explicit
Git baseline; it still reproduces the old counterexample without misreporting
the corrected current engine as defective.

## Delivery and remaining limits

A new source-pinned Windows test package was built from the pushed product commit,
with all 105 ZIP entries checked, 749 source inputs fingerprinted and 29 bridge
files verified. The extracted package is independently checked against every
archive entry. Package receipt and final native read-only outcome are retained
in the improvement evidence. The package is an independent-review candidate.

What remains is **live qualification**, not a proven live superiority result:
new ordinary/Gold reward receipts and refusal observations on the approved
sub-account; a naturally permitted remote-room opportunity; post-710 native
claim attribution if that activity is later investigated; and matched same-state
LwTool/LW-Control timing and resource measurements. Random events are not forced
by spending, sending gifts, chat messages or fabricated requests. Main remains
read-only. No claim that our speed or full cross-server coverage matches or beats
LwTool is supported by this pass.
