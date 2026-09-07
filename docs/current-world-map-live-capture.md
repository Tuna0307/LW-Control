# Current World Map live-capture checkpoint

Date: 2026-09-06

Status: **LOADED WORLD SNAPSHOT, MULTI-BLOCK AOI, SERIAL MULTI-BATCH COMPLETENESS, AND A TWO-INFLIGHT CONCURRENT WAVE ARE PROVEN; FULL-WORLD COMPLETENESS PENDING**

The current-build loaded-state World Map boundary is now proven end to end. The
successful candidate used the game's current `SceneUtils.ChangeToWorld` routine
once, waited until `CS.SceneManager.CurrSceneID` authoritatively equaled the
current `SceneManagerSceneID.World` value, found
`CS.SceneManager.World.PointManager`, waited for `_pointInfos` to stabilize, and
then exported the already-loaded collection through the schema-v1 read-only
builder. No explicit AOI/view request exists in this probe.

## Current-build transition recovery

Current `LWScripts.data` content version 12 decodes
`UI/LWMainUI/Component/UIMainBottom/UIMainChangeScene.luac` as a Lua 5.3 chunk.
The installed entry SHA-256 is
`3843dc02869330f060a199b946ef3cd30aa335504914c2f4c60f8a824e871c5b`;
the decoded chunk SHA-256 is
`3ff1ee95d6edafbbca58c0d29d5fdcd42c2fb71e0aeab34510a2123af9c3cef9`.

Static bytecode inspection proves:

- `UIMainChangeScene.OnClick` reads `CS.SceneManager.CurrSceneID` and handles the
  City/World switch.
- `ComponentDefine` creates a Lua `UIButton` and wires it with `btn:SetOnClick`;
  that closure calls `self:OnClick()`. It is not a raw Unity `Button.onClick`
  contract.
- the normal City-to-World branch calls `SceneUtils.ChangeToWorld`;
- `SceneUtils.GetIsInWorld` returns whether `CS.SceneManager.CurrSceneID ==
  SceneManagerSceneID.World`;
- current `SceneUtils.ChangeToWorld` is prototype `0.37`, source lines 577-653.
  It performs the current game transition, including `MsgDefines.GoToWorld`,
  city teardown, `SceneUtils.CreateWorld()`, `SetIsInCity(false)`, and
  `CS.SceneManager.World:CreateScene(...)`.

This corrected an older compatibility assumption inherited from the original
LW Control scanner: invoking the raw Unity button event is not sufficient on the
current UI implementation.

## Live candidate a12: negative control

Candidate a12 package SHA-256:
`58826ee01d0bbee2bb6e418d7b8414ddbb341abe140daf3a77a2f96b29706ab9`.
Generated probe-source SHA-256:
`3139b93a6b043e4ddb7d17c792eceb7fc3ddcd10fe50791b23d0f6be3c998527`.

a12 invoked the discovered raw Unity `onClick` once at
`LWMainUI/safeArea/bottomLayer/WorldBtn/Btn`. During the bounded run:

- `CurrSceneID` remained `1`;
- current City ID was `1` and World ID was `2`;
- no active `WorldScene` or `WorldPointManager` was found;
- no snapshot was emitted.

This proves the previous unattended button invocation did not execute the
current Lua scene-change path. The run restored all pre-run hashes exactly.

## Live candidate a13: loaded-state proof

Candidate a13 package SHA-256:
`c7e832d9054b6bbb3b31da2cab50787e6d604ee0b3f15c9d52c6f00ef300b81c`.
Generated probe-source SHA-256:
`de5fce1c67bf1b1c2bd4301f8ea04edc6cee70f825f6abf88f7e76f7e0851d16`.

a13 invoked `SceneUtils.ChangeToWorld` exactly once and then captured:

- `scene_state = world`;
- `current_scene_id = 2`, matching `world_scene_id = 2`;
- `world_source = CS.SceneManager.World`;
- `manager_source = CS.SceneManager.World.PointManager`;
- `_pointInfos` stabilized at **86 records** for two seconds;
- schema-v1 snapshot `live-world-1788642296` was emitted at
  `2026-09-05T21:04:56Z` (`2026-09-06T05:04:56+08:00`).

The 86 loaded records had these observed point-type counts:

| pointType | count |
| ---: | ---: |
| 6 | 72 |
| 7 | 12 |
| 17 | 1 |
| 25 | 1 |

These values prove structured current loaded-state enumeration. They do not yet
assign user-facing meanings to every numeric point type and do not prove map
completeness beyond the game's currently loaded AOI.

The runner reported zero explicit probe network sends and zero probe AOI
requests. The scene transition itself uses the game's recovered current
`SceneUtils.ChangeToWorld` behavior. After the run, `LWScripts.data`,
`LWScripts.txt`, `version.txt`, and `BaseUtils.rdl` all matched their exact
pre-run SHA-256 values. In particular:

- installed `LWScripts.data`:
  `73f69efea8bcd121ede8fa292d621eb68cea4c6368e331e8c614e95fff5a6a0c`;
- `BaseUtils.rdl`:
  `b20865f42c9272e0fca2b6deb2e9142576b12dcf42f11ad1a8816ddd59b952d6`.

Machine-readable milestone evidence is in
[`current-world-map-live-evidence.json`](current-world-map-live-evidence.json).

## Live candidate a20: bounded wider AOI proof

Candidate a20 package SHA-256:
`180fc3ce979602a187b1e189252955b505b627c740f6a6fe6459776cb4f6dc46`.
Generated probe-source SHA-256:
`275d40f7e32a187016abdc2c8dc6f4efd86c5eaad887527a14f1dad1cdedcd14`.
Managed bridge source SHA-256:
`c4c8b39b630b150f7a72922f77f10d441631437da12108fe3efa3a5e9b83c5a4`.

The runner entered the current World scene through the already-proven
`SceneUtils.ChangeToWorld` path and observed `CurrSceneID = 2`. At request time,
the game's visible AOI was block X `53..57`, Y `46..49`. The probe selected one
adjacent logical target, block `(58,47)`, logical index `4758`.

Recovered original-LW-Control behavior pads even one requested logical block to
a minimum transport viewport of 5 by 4 AOI blocks. For this request the padded
coverage was X `56..60`, Y `46..49`, producing exactly these 20 row-major block
indexes:

`4656, 4657, 4658, 4659, 4660, 4756, 4757, 4758, 4759, 4760, 4856, 4857,
4858, 4859, 4860, 4956, 4957, 4958, 4959, 4960`.

With `_lwAoiBlockSize = 10` and `_lwAoiBlockCount = 100`, the padded request
covered tile X `560..609`, Y `460..499`. The request used:

- `leftBottom = 460561`;
- `rightTop = 500611`;
- request center `x = 585`, `y = 480`;
- `serverId = 2212`, `worldId = 0`;
- `bigMap = 0`, server LOD `0`.

The probe sent exactly one AOI request through the recovered managed
`WorldBlockSender.SendAoi` bridge, with zero retries and zero camera moves. It
observed exactly one target `world.get.block` response through
`DispatchResponse2`. The authoritative bounds were nested at
`$.array[serverPointArr][0]`, with `leftBottom = 460561`,
`rightTop = 500611`, `serverId = 2212`, and `worldId = 0`. Decoding those bounds
produced the same tile coverage X `560..609`, Y `460..499`, or AOI blocks X
`56..60`, Y `46..49`. The response therefore covered the requested target
logical block and the entire padded transport envelope. No target response was
rejected by the correlation logic.

The manager's `_pointInfos` count changed from 86 before the request to 60 after
the response, demonstrating that the collection is a replaceable loaded-AOI
state rather than an append-only scan result. Comparing identities before and
after found 13 newly observed point IDs:

`462595, 465591, 466598, 468588, 472587, 473589, 473594, 474591, 481598,
482594, 490590, 495596, 499591`.

Those records were all compatible with server `2212`, world `0`, and the
returned bounds. IDs such as `468588`, `473589`, and `490590` also decoded to
tile coordinates inside the returned envelope (`x=587,y=468`, `x=588,y=473`,
and `x=589,y=490` respectively). This proves the currently used point-ID tile
decode for these live records; it does not yet define every possible user-facing
coordinate presentation rule.

The response hook and the temporarily touched manager flag were restored. The
runner then closed the game and restored the exact pre-run hashes for
`LWScripts.data`, `LWScripts.txt`, `version.txt`, and `BaseUtils.rdl`.
`restore_hash_match` was true.

## Live candidate a21: three-logical-block coverage proof

Candidate a21 package SHA-256:
`3969bfefb53a2c4da757f184ca6a891e4b4c20d836b0150640233e2488c466ad`.
Probe-source SHA-256:
`6b067269071ab39d3f26502e6e9b9beac3a19634e8ca97efa8a33fa97fbf6869`.

The probe selected logical blocks `(58,46)`, `(58,47)`, and `(58,48)` outside
the visible AOI and carried them in the recovered minimum transport coverage X
`56..60`, Y `46..49` (20 row-major indexes). Exactly one AOI request was sent.
The correlated authoritative `world.get.block` response envelope at
`$.array[serverPointArr][0]`, server `2212`, world `0`, covered all three logical
targets. The final result was `state = proven`, with **3/3 logical blocks
covered**, zero retries and zero camera moves.

The response hook and manager flag were restored and the runner restored the
exact official hashes afterward. This resolves the previous uncertainty about
multi-block runtime acceptance.

## Live candidate a22: bounded serial multi-batch proof

Candidate a22 package SHA-256:
`6e8abb9bbbedb7292d959f6006ddb9db04370c8e5a7e6e94e2af935f9b08defd`.
Probe-source SHA-256:
`3a28a3b4a2c0cafa67bf35e7ba7f3a403660ed0980a8ed2acce0500cb4ce3475`.
Managed bridge source SHA-256:
`c4c8b39b630b150f7a72922f77f10d441631437da12108fe3efa3a5e9b83c5a4`.

The proof uses the smallest odd square that exceeds the recovered original
scanner's 160-index native ceiling: **13 by 13 = 169 logical blocks**. With the
live visible AOI at X `53..57`, Y `46..49`, the bounded logical scan window was
X `58..70`, Y `41..53`.

The recovered batch builder produced exactly two batches:

| Batch | Logical coverage | Logical blocks | Transport coverage | Transport indexes |
| ---: | --- | ---: | --- | ---: |
| 1 | X `58..69`, Y `41..53` | 156 | X `58..69`, Y `41..53` | 156 |
| 2 | X `70..70`, Y `41..53` | 13 | X `68..72`, Y `41..53` | 65 |

Batch 1 was sent first as the serial capability probe. Its authoritative
`world.get.block` envelope arrived through `DispatchResponse2` from
`$.array[serverPointArr][0]`, server `2212`, world `0`, and covered **156/156**
logical blocks. Only after that correlated coverage completed did the probe send
batch 2. Its authoritative envelope covered **13/13** logical blocks. The final
result was therefore **169/169 requested logical blocks covered**, exactly two
AOI sends, two completed batches, zero retries, and zero camera moves.

The request geometry was:

- batch 1 center `(640,475)`, `leftBottom = 410581`, `rightTop = 540701`;
- batch 2 center `(705,475)`, `leftBottom = 410681`, `rightTop = 540731`.

The response hook and manager flag both restored successfully. The runner then
closed the game and restored the exact pre-run hashes for `LWScripts.data`,
`LWScripts.txt`, `version.txt`, and `BaseUtils.rdl`; `restore_hash_match` was
true.

This proves bounded serial multi-batch completeness on the current build for an
explicit 13-by-13 request. It does not prove full-world completion, concurrent
native scheduling, retry behavior, or camera fallback.

## Live candidate w23: bounded concurrent native scheduling proof

Candidate w23 package SHA-256:
`c67b7c71dc1e422e46d86f5a54ecba330c6c1a369f4419d37ee7aa679acd33fd`.
Probe-source SHA-256:
`659fb3eaeab2311ad5b4d85ebe9af38325c894ab4913ad1ecd01299c03cd76ab`.

The proof used a 19-by-19 logical window, X `58..76`, Y `38..56`, totaling
**361 logical blocks**. The recovered planner selected an 8-by-19 native batch
shape and produced exactly **152 + 152 + 57** logical blocks. The final narrow
batch kept the recovered minimum-width padding, producing a 95-index transport
envelope X `73..77`, Y `38..56`.

Batch 1 was sent and completed first as the recovered serial capability probe,
covering **152/152** logical blocks. Batches 2 and 3 were then issued as a
bounded two-request concurrent wave. Event ordering proves both send calls
completed before either correlated response arrived:

- batch 2 send start/completion events: `1` / `2`;
- batch 3 send start/completion events: `3` / `4`;
- concurrent-wave launch-complete event: `5`;
- batch 2 response event: `6`;
- batch 3 response event: `7`.

`concurrent_peak_inflight` was therefore `2`, and
`concurrent_response_before_wave_complete` was false. The two authoritative
responses were matched uniquely by server/world plus exact request bounds:

| Batch | Logical coverage | Logical blocks | Transport coverage | Bounds |
| ---: | --- | ---: | --- | --- |
| 1 | X `58..65`, Y `38..56` | 152 | X `58..65`, Y `38..56` | `380581` .. `570661` |
| 2 | X `66..73`, Y `38..56` | 152 | X `66..73`, Y `38..56` | `380661` .. `570741` |
| 3 | X `74..76`, Y `38..56` | 57 | X `73..77`, Y `38..56` | `380731` .. `570781` |

All three responses came through `DispatchResponse2` from
`$.array[serverPointArr][0]`, server `2212`, world `0`. The final result was
**361/361 logical blocks covered**, exactly three sends, three completed
batches, zero retries, and zero camera moves. The response hook and manager flag
restored successfully, and all official protected hashes matched their exact
pre-run values afterward.

This proves the recovered serial-first then bounded-concurrent scheduling shape
for two simultaneous remaining native batches. It does not yet prove the
original default concurrency of eight, retry/concurrency-reduction behavior,
camera fallback, or full-world completion.

## Remaining boundary

The current build now has live proof for loaded-state capture, one padded wider
AOI request, multi-block logical coverage, serial multi-batch completion, and a
two-inflight concurrent native wave. Completion succeeds only when every
requested logical block is covered by correlated authoritative response
envelopes. `_pointInfos` remains replaceable loaded-AOI state and is not used as
completeness proof.

The remaining World Scan work is broader scan orchestration: the original wider
concurrency policy, retry/fallback behavior, and a full-world sweep policy with a
terminal proof that every requested logical block was accounted for. The C#
`RecoveredWorldMapScanPlanner` already mirrors the recovered
original centered odd windows, row-major block IDs, 160-index native limit,
minimum 5-by-4 transport padding, serpentine batch order, and fail-closed
coverage accounting.
