"""Inert source-v26/Lua53 2x2 City swap, native ACK, cancel and retry inverses.

Uses the two exact 2289 snapshot UUIDs and anchors from receipt 080448, while
explicitly modelling hypothetical zone permissions; does not access the game.
"""
from __future__ import annotations

from feature018_city_game_checks import fake, check_original_game_wire
from feature018_city_game_checks import bytecode, probe, root_methods, disassemble


ONE = 1392040865799139725
TWO = 1392040865799139726
SWAP = f'{ONE}:4442;{TWO}:4242'

FIXTURE = r'''
CityTileCount = 100
for pid=26,10000 do
  DataCenter.CityZoneManager.allZonePoint[pid] = {zone_type=1}
end
local template = {tileX=2,tileY=2,zoneType=1,
    CanMove=function() return true end}
DataCenter.BuildTemplateManager.GetBuildingDesTemplate = function(self,id)
  if id == 10210000 then return template end
  return nil
end
local function fixture(uuid, point)
  return {uuid=uuid,itemId=10210000,pointId=point,level=1,
    state=BuildingStateType.Normal,isWorldBuild=false,
    GetRangedPoints=function(self)
      local p=self.pointId
      return {[p]=true,[p-1]=true,[p-100]=true,[p-101]=true}
    end}
end
local a=1392040865799139725
local b=1392040865799139726
DataCenter.BuildManager.allBuilding = {
   [a]=fixture(a,4242),[b]=fixture(b,4442),
}
local function add_shape(out,point)
   out[point]=true; out[point-1]=true
   out[point-100]=true; out[point-101]=true
end
AllowedZones = {}
add_shape(AllowedZones,4242)
add_shape(AllowedZones,4442)
add_shape(AllowedZones,4244)
add_shape(AllowedZones,4444)
DataCenter.CityZoneManager.CanPutBuildByPoint = function(self,id,zone)
  return zone==1 and AllowedZones[id]==true
end
OriginalNativeMoveReply = DataCenter.BuildManager.BuildWorldMoveNewHandle
OriginalNativeReplaceReply = DataCenter.BuildManager.FreeBuildingReplaceNewHandle
OriginalNativeSend = SFSNetwork.SendMessage
'''


def assert_that(condition, message):
    if not condition:
        raise AssertionError('FEATURE018_CITY_SWAP: ' + message)


def check_historical_wrong_type_source():
    """Prior 9725 Replace was not a v26 existing-building Move ACK.

    Verify why a later equal point alone cannot resolve old request status:
    the Replace reply has no echoed request UUID/nonce and updates server-owned
    buildInfo/changeBuildInfos, possibly modifying other building records.
    """
    records = dict(probe.read_lwlf(probe.paths()['data'])[2])

    def source(module, method):
        root = bytecode.parse_chunk(probe.decode_lenc(records[module]))
        for index in root_methods(root)[method].split('/')[1:]:
            root = root['children'][int(index)]
        return list(disassemble(root))

    replace = source('DataCenter/BuildManager/BuildManager.luac',
                     'FreeBuildingReplaceNewHandle')
    native_reply = source('Net/Msgs/FreeBuildingReplaceNewMessage.luac',
                          'HandleMessage')
    assert_that(any('sourceLine=4062' in l and 'UIPlaceBuildSendMessageBack' in l
                    for l in replace) and
                any('sourceLine=4063' in l and 'errorCode' in l for l in replace) and
                any('sourceLine=4067' in l and 'buildInfo' in l for l in replace) and
                any('sourceLine=4075' in l and 'changeBuildInfos' in l
                    for l in replace) and
                any('sourceLine=4080' in l and 'AddBuilding' in l
                    for l in replace) and
                any('sourceLine=18' in l and 'FreeBuildingReplaceNewHandle' in l
                    for l in native_reply) and
                not any('requestId' in l or 'nonce' in l for l in native_reply),
                'v26 earlier Replace reply gained an attributable existing-Move ACK')


def scene():
    lua, producer = fake()
    lua.execute(FIXTURE)
    return lua, producer


def state(lua):
    response = lua.globals().Act('city_layout_snapshot_get')
    assert_that(response['state'] == 'confirmed', '2x2 scene unavailable')
    return response['data']


def values(lua, revision):
    return lua.globals().Fields(revision, SWAP)


def status(lua, revision):
    return lua.globals().Act('city_layout_apply_status',
                             lua.globals().Fields(revision, ''))['data']


def confirm(lua, uuid, point):
    lua.execute(f'ConfirmNativeMove({uuid},{point})')


def planned_steps(preflight):
    return [(preflight['plannedMoves'][i]['uuid'],
             preflight['plannedMoves'][i]['fromPointId'],
             preflight['plannedMoves'][i]['pointId'],
             preflight['plannedMoves'][i]['temporary'])
            for i in range(1,len(preflight['plannedMoves'])+1)]


def test_exact_native_swap():
    lua, producer = scene()
    before = state(lua)
    assert_that(len(before['buildings']) == 2 and
                [before['buildings'][i]['uuid'] for i in (1,2)] ==
                [str(ONE),str(TWO)],
                'exact native 2289 large integer building identities changed')
    assert_that(before['swapPreflightSearch']['examinedPairs'] == 1 and
                before['swapPreflightSearch']['validPairs'] == 1 and
                before['swapPreflightSearch']['historicalRequestsReconciled'] is False and
                len(before['swapPreflightIssues']) == 0,
                'a read-only 2x2 swap preflight incorrectly claims historical send ACK')
    assert_that(len(lua.globals().ObservedMoves) == 0,
                'read-only 2x2 swap preflight mutated game fixture')
    preflight = before['swapPreflights'][1]
    assert_that(preflight['preflightOnly'] is True and
                preflight['bufferCount'] == 1 and
                planned_steps(preflight) == [
                    (str(ONE),4242,4244,True),
                    (str(TWO),4442,4242,False),
                    (str(ONE),4244,4442,False)],
                'source-zone 2x2 buffer and three-step plan incorrect')
    revision=before['layoutRevision']
    start = lua.globals().Act('city_layout_apply_start', values(lua,revision))
    assert_that(start['state'] == 'accepted' and
                len(lua.globals().ObservedMoves) == 1,
                '2x2 swap sender did not dispatch exactly one move')
    for i,(who,target,prior) in enumerate(((ONE,4244,4242),
                                           (TWO,4242,4442),
                                           (ONE,4442,4244)),1):
        packet = lua.globals().ObservedMoves[i]
        assert_that(packet['uuid'] == who and packet['pointId'] == target and
                    packet['lastIndex'] == prior and
                    packet['message'] == 'native_build_world_move_new',
                    '2x2 move lost original v26 existing-Move wire or int64 UUID')
        producer.Pump()
        assert_that(len(lua.globals().ObservedMoves) == i,
                    'future move dispatched before current exact server reply')
        confirm(lua,who,target)
        producer.Pump()
        assert_that(status(lua,revision)['completedMoves'] == i,
                    'native ACK poststate did not advance exact 2x2 move')
    complete = status(lua,revision)
    assert_that(complete['state'] == 'completed' and
                complete['matchingNativeReplies'] == 3 and
                complete['inFlight'] is False and
                lua.eval('DataCenter.BuildManager.BuildWorldMoveNewHandle == OriginalNativeMoveReply') and
                lua.eval('DataCenter.BuildManager.FreeBuildingReplaceNewHandle == OriginalNativeReplaceReply') and
                lua.eval('SFSNetwork.SendMessage == OriginalNativeSend'),
                '2x2 swap declared success without three native ACKs/full hook cleanup')
    assert_that(lua.globals().DataCenter.BuildManager.allBuilding[ONE]['pointId'] == 4442
                and lua.globals().DataCenter.BuildManager.allBuilding[TWO]['pointId'] == 4242,
                '2x2 final authoritative positions incorrect')


def test_negative_native_zones():
    lua, _ = scene()
    lua.execute('AllowedZones[4244] = nil; AllowedZones[4444] = nil')
    observed = state(lua)
    assert_that(observed['swapPreflightSearch']['examinedPairs'] == 1
                and observed['swapPreflightSearch']['validPairs'] == 0
                and len(observed['swapPreflights']) == 0 and
                len(observed['swapPreflightIssues']) == 1 and
                observed['swapPreflightIssues'][1]['issueCode'] == 'CITY_LAYOUT_NO_BUFFER',
                'missing source-permitted temporary buffer still advertised a swap')
    plan=lua.globals().Act('city_layout_validate',
                           values(lua,observed['layoutRevision']))['data']
    assert_that(plan['valid'] is False and
                plan['issues'][1]['code'] == 'CITY_LAYOUT_NO_BUFFER' and
                len(lua.globals().ObservedMoves) == 0,
                'native no-buffer zone rejection dispatched an unsafe packet')

    lua, _=scene()
    lua.execute('AllowedZones[4242] = nil')
    observed=state(lua)
    assert_that(len(observed['swapPreflights'])==0 and
                len(observed['swapPreflightIssues']) == 1 and
                observed['swapPreflightIssues'][1]['issueCode'] == 'CITY_LAYOUT_ZONE' and
                lua.globals().Act('city_layout_validate',
                    values(lua,observed['layoutRevision']))['data']['valid'] is False,
                'invalid final destination advertised as a safe swap')


def test_cancel_partial_and_uncertain():
    for cancel_step in (1,2):
        lua, producer=scene()
        revision=state(lua)['layoutRevision']
        started=lua.globals().Act('city_layout_apply_start',values(lua,revision))
        assert_that(started['state']=='accepted','cancel test did not begin')
        if cancel_step==2:
            confirm(lua,ONE,4244)
            producer.Pump()
            assert_that(len(lua.globals().ObservedMoves)==2,
                        'second packet not dispatched before controlled cancellation')
        cancellation=lua.globals().Act('city_layout_apply_cancel', lua.table_from({
            'profileId':'p1','launchSessionId':'session1','challenge':'nonce',
            'jobId':started['data']['status']['jobId']}))
        receipt=cancellation['data']['status']
        assert_that(receipt['state']=='cancelling' and
                    receipt['cancelRequestedWhileInFlight'] is True and
                    receipt['dispatchedMoves']==cancel_step,
                    'in-flight cancellation omitted pending native packet')
        pending=len(lua.globals().ObservedMoves)
        producer.Pump()
        assert_that(len(lua.globals().ObservedMoves)==pending,
                    'cancel sent later 2x2 swap packet without matching ACK')
        if cancel_step==1:
            confirm(lua,ONE,4244)
        else:
            confirm(lua,TWO,4242)
        producer.Pump()
        after=status(lua,revision)
        assert_that(after['state']=='cancelled' and
                    after['completedMoves']==cancel_step and
                    after['matchingNativeReplies']==cancel_step and
                    not after['inFlight'] and
                    lua.eval('SFSNetwork.SendMessage == OriginalNativeSend') and
                    lua.eval('DataCenter.BuildManager.BuildWorldMoveNewHandle == OriginalNativeMoveReply') and
                    lua.eval('DataCenter.BuildManager.FreeBuildingReplaceNewHandle == OriginalNativeReplaceReply') and
                    len(lua.globals().ObservedMoves)==pending,
                    'cancel misreported partial native 2x2 movement or leaked hooks')
    lua,producer=scene()
    lua.execute('Clock=100; os.time = function() return Clock end')
    revision=state(lua)['layoutRevision']
    started=lua.globals().Act('city_layout_apply_start',values(lua,revision))
    assert_that(started['state']=='accepted','timeout test failed to dispatch')
    lua.execute('Clock=116')
    stale=status(lua,revision)
    assert_that(stale['state']=='failed' and
                stale['errorCode']=='GAME_MOVE_RESULT_TIMEOUT' and
                stale['inFlight'] is True,
                '2x2 request timeout incorrectly unblocked duplicate moves')
    retry=lua.globals().Act('city_layout_apply_start',values(lua,revision))
    assert_that(retry['errorCode']=='CITY_LAYOUT_RESULT_UNCERTAIN' and
                len(lua.globals().ObservedMoves)==1,
                'unconfirmed old 2x2 request was resent')
    # Another building's success must not be confused with the pending move.
    confirm(lua,TWO,4442)
    producer.Pump()
    assert_that(status(lua,revision)['completedMoves']==0 and
                status(lua,revision)['matchingNativeReplies']==0,
                'foreign UUID native reply released an unrelated 2x2 pending move')
    confirm(lua,ONE,4244)
    producer.Pump()
    reconciled=status(lua,revision)
    assert_that(reconciled['state']=='failed' and
                reconciled['completedMoves']==1 and
                not reconciled['inFlight'] and
                reconciled['matchingNativeReplies']==1 and
                lua.eval('SFSNetwork.SendMessage == OriginalNativeSend') and
                lua.eval('DataCenter.BuildManager.BuildWorldMoveNewHandle == OriginalNativeMoveReply') and
                lua.eval('DataCenter.BuildManager.FreeBuildingReplaceNewHandle == OriginalNativeReplaceReply') and
                len(lua.globals().ObservedMoves)==1,
                'late exact Move ACK resumed swap, lost poststate, or leaked hooks')


def test_outbound_native_collision_fences():
    # The native Move reply contains no nonce. Two requests in flight to any
    # destinations, or an overlapping original Replace operation, mean the
    # BuildManager response cannot be attributed solely by matching position.
    for collision in ('same-move','other-move','old-replace','after-reply'):
        lua, producer=scene()
        revision=state(lua)['layoutRevision']
        lua.execute('OriginalNativeSend = SFSNetwork.SendMessage')
        started=lua.globals().Act('city_layout_apply_start',values(lua,revision))
        assert_that(started['state']=='accepted','outbound collision fixture not sent')
        before=status(lua,revision)
        assert_that(before['nativeMoveOutboundObserved'] is True and
                    before['nativeMoveOutboundCount']==1 and
                    before['outboundCollisionObserved'] is False,
                    'producer did not register its one direct original Move send')
        if collision == 'after-reply':
            confirm(lua,ONE,4244)
        if collision == 'old-replace':
            lua.execute(f'''SFSNetwork.SendMessage(MsgDefines.FreeBuildingReplaceNew,
                {{buildUuid={ONE}, pointId=4244}})''')
        else:
            target=4244 if collision != 'other-move' else 4444
            who=ONE if collision != 'other-move' else TWO
            original=4242 if collision != 'other-move' else 4442
            lua.execute(f'''SFSNetwork.SendMessage(MsgDefines.BuildWorldMoveNew,
                {{uuid={who},pointId={target},lastIndex={original}}})''')
        if collision != 'after-reply':
            confirm(lua,ONE,4244)
        producer.Pump()
        uncertain=status(lua,revision)
        assert_that(uncertain['state']=='failed' and
                    uncertain['errorCode']=='CITY_LAYOUT_RESULT_UNCERTAIN' and
                    uncertain['inFlight'] is True and
                    uncertain['completedMoves']==0 and
                    uncertain['outboundCollisionObserved'] is True and
                    uncertain['matchingNativeReplies'] <= 1,
                    f'{collision}: native overlapping send incorrectly completed move')
        assert_that(lua.globals().Act('city_layout_apply_start',
                    lua.globals().Fields(state(lua)['layoutRevision'],
                      f'{TWO}:4444'))['errorCode']=='CITY_LAYOUT_RESULT_UNCERTAIN',
                    f'{collision}: native overlap unlocked risky new send')
        pending_count=len(lua.globals().ObservedMoves)
        producer.Pump()
        assert_that(len(lua.globals().ObservedMoves)==pending_count,
                    f'{collision}: uncertain multi-step plan sent a later move')


def test_historical_inbound_replace_collision():
    # Key R7 inverse: a v26 Replace reply can arrive without any fresh
    # FreeBuildingReplaceNew SendMessage being observed by this job. This
    # includes the historical unconfirmed 9725 4242->4144 request; neither a
    # generic UI callback nor an original building position can attribute it.
    # The original handler must still run once, including its error path.
    for phase, reply in (
        ('error_before_move_ack', '{errorCode=80001}'),
        ('old_9725_success', f'{{buildInfo={{uuid={ONE},pointId=4144,bId=10210000}}}}'),
        ('foreign_building_success', f'{{buildInfo={{uuid={TWO},pointId=4444,bId=10210000}}}}'),
        ('after_move_ack_before_pump', '{errorCode=81000}'),
        ('after_timeout', '{errorCode=82000}'),
        ('cancel_in_flight', '{errorCode=83000}'),
    ):
        lua, producer = scene()
        revision = state(lua)['layoutRevision']
        started = lua.globals().Act('city_layout_apply_start', values(lua, revision))
        assert_that(started['state']=='accepted',f'{phase}: first native packet not sent')
        if phase == 'after_move_ack_before_pump':
            confirm(lua, ONE, 4244)
        elif phase == 'after_timeout':
            lua.execute('Clock=9999999999; os.time=function() return Clock end')
            producer.Pump()
            assert_that(status(lua,revision)['errorCode']=='GAME_MOVE_RESULT_TIMEOUT',
                        'late old Replace reply test did not first time out Move')
        elif phase == 'cancel_in_flight':
            cancel=lua.globals().Act('city_layout_apply_cancel',lua.table_from({
                'profileId':'p1','launchSessionId':'session1',
                'challenge':'nonce','jobId':started['data']['status']['jobId']}))
            assert_that(cancel['data']['status']['state']=='cancelling' and
                        cancel['data']['status']['cancelRequestedWhileInFlight'] is True,
                        'fixture failed to request in-flight cancellation')
        lua.execute(f'DataCenter.BuildManager:FreeBuildingReplaceNewHandle({reply})')
        assert_that(lua.eval('NativeReplaceReplyInvocations')==1,
                    f'{phase}: original v26 Replace handler not called exactly once')
        if phase not in ('after_move_ack_before_pump', 'after_timeout'):
            confirm(lua, ONE, 4244)
        producer.Pump()
        observed = status(lua, revision)
        assert_that(observed['state']=='failed' and
                    observed['inFlight'] is True and
                    observed['completedMoves']==0 and
                    observed['outboundCollisionObserved'] is False and
                    observed['inboundReplaceCollisionObserved'] is True and
                    observed['nativeMoveCollisionObserved'] is True and
                    observed['nativeReplaceReplyCount']==1 and
                    observed['nativeMoveOutboundCount']==1 and
                    observed['matchingNativeReplies']<=1,
                    f'{phase}: old non-attributable Replace reply advanced 2x2 Move')
        if phase == 'cancel_in_flight':
            assert_that(observed['cancelRequested'] is True and
                        observed['cancelRequestedWhileInFlight'] is True,
                        'old Replace reply made a pending Cancel look settled')
        new_revision=state(lua)['layoutRevision']
        retry=lua.globals().Act('city_layout_apply_start',
                                lua.globals().Fields(new_revision,f'{TWO}:4444'))
        assert_that(retry['errorCode']=='CITY_LAYOUT_RESULT_UNCERTAIN' and
                    len(lua.globals().ObservedMoves)==1,
                    f'{phase}: original old Replace reply unlocked unsafe re-send')

    # A replaced native Replace handler is as dangerous as losing the Move
    # response handler or outgoing observer: it hides late historical replies.
    lua, producer = scene()
    revision=state(lua)['layoutRevision']
    assert_that(lua.globals().Act('city_layout_apply_start', values(lua,revision))
                ['state']=='accepted', 'displacement fixture dispatch failed')
    confirm(lua, ONE, 4244)
    lua.execute('DataCenter.BuildManager.FreeBuildingReplaceNewHandle = OriginalNativeReplaceReply')
    producer.Pump()
    lost=status(lua,revision)
    assert_that(lost['state']=='failed' and lost['inFlight'] is True and
                lost['completedMoves']==0 and
                lost['errorCode']=='CITY_LAYOUT_RESULT_UNCERTAIN',
                'displaced native Replace observer allowed fake Move completion')

    # A missing handler is not evidence that no old Replace response can be
    # processed; no request must escape with a partially installed hook.
    lua,producer=scene()
    revision=state(lua)['layoutRevision']
    lua.execute('DataCenter.BuildManager.FreeBuildingReplaceNewHandle=nil')
    refused=lua.globals().Act('city_layout_apply_start', values(lua,revision))
    assert_that(refused['state']=='failed' and
                refused['errorCode']=='GAME_METHOD_UNAVAILABLE' and
                len(lua.globals().ObservedMoves)==0 and
                lua.eval('DataCenter.BuildManager.BuildWorldMoveNewHandle == OriginalNativeMoveReply'),
                'missing Replace reply handler allowed uncorrelatable Move send')


def test_unexpected_native_occupancy_during_swap():
    # A game-created building not in the initial 35-building layout must
    # invalidate the ACK->NEXT transition even if no old UUID or point moved.
    # The original 2x2 fixtures model an independent real native addition;
    # no other action or game session is used here.
    new_uuid=1392040865799139727
    for drift in ('new_building','missing_building'):
        lua,producer=scene()
        revision=state(lua)['layoutRevision']
        started=lua.globals().Act('city_layout_apply_start',values(lua,revision))
        assert_that(started['state']=='accepted',f'{drift}: no first Move send')
        confirm(lua,ONE,4244)
        if drift=='new_building':
            lua.execute(f'''
              local p=4646
              DataCenter.BuildManager.allBuilding[{new_uuid}]={{
                uuid={new_uuid},itemId=10210000,pointId=p,
                level=1,state=BuildingStateType.Normal,isWorldBuild=false,
                GetRangedPoints=function(self)
                  local q=self.pointId
                  return {{[q]=true,[q-1]=true,[q-100]=true,[q-101]=true}}
                end}}
            ''')
        else:
            lua.execute(f'DataCenter.BuildManager.allBuilding[{TWO}]=nil')
        producer.Pump()
        observed=status(lua,revision)
        assert_that(observed['state']=='failed' and
                    observed['errorCode']=='CITY_LAYOUT_STALE' and
                    observed['completedMoves']==0 and
                    observed['matchingNativeReplies']==1 and
                    observed['inFlight'] is False and
                    len(lua.globals().ObservedMoves)==1 and
                    lua.eval('DataCenter.BuildManager.BuildWorldMoveNewHandle == OriginalNativeMoveReply') and
                    lua.eval('DataCenter.BuildManager.FreeBuildingReplaceNewHandle == OriginalNativeReplaceReply') and
                    lua.eval('SFSNetwork.SendMessage == OriginalNativeSend'),
                    f'{drift}: native occupancy drift sent the next buffered 2x2 Move')


def test_displaced_native_observers_and_sync_reply():
    # Even a previously matching reply cannot confirm an in-flight packet if
    # either observer was replaced before the owner pump revalidated its state.
    for lost in ('send','reply'):
        lua, producer=scene()
        revision=state(lua)['layoutRevision']
        lua.execute('OriginalNativeSend = SFSNetwork.SendMessage')
        start=lua.globals().Act('city_layout_apply_start',values(lua,revision))
        assert_that(start['state']=='accepted','displaced observer start failed')
        confirm(lua,ONE,4244)
        if lost == 'send':
            lua.execute('SFSNetwork.SendMessage = OriginalNativeSend')
        else:
            lua.execute('DataCenter.BuildManager.BuildWorldMoveNewHandle = OriginalNativeMoveReply')
        producer.Pump()
        observed=status(lua,revision)
        assert_that(observed['state']=='failed' and
                    observed['errorCode']=='CITY_LAYOUT_RESULT_UNCERTAIN' and
                    observed['completedMoves']==0 and
                    observed['inFlight'] is True,
                    f'{lost}: replaced observer still attributed native result')
        assert_that(lua.globals().Act('city_layout_apply_start',
                    lua.globals().Fields(state(lua)['layoutRevision'],
                      f'{TWO}:4444'))['errorCode']=='CITY_LAYOUT_RESULT_UNCERTAIN',
                    f'{lost}: displaced observer unlocked duplicate move')

    # An event-loop mock may deliver a successful game reply before the
    # original void-return SendMessage finishes. Own request observation
    # precedes delegate call and must not lose this valid matched result.
    lua, producer=scene()
    lua.execute('''
      SFSNetwork.SendMessage=function(message,args)
        ObservedMoves[#ObservedMoves+1]={
          message=message,uuid=args.uuid,pointId=args.pointId,
          lastIndex=args.lastIndex}
        ConfirmNativeMove(args.uuid,args.pointId)
      end
    ''')
    revision=state(lua)['layoutRevision']
    started=lua.globals().Act('city_layout_apply_start',values(lua,revision))
    assert_that(started['state']=='accepted','synchronous native request/reply not accepted')
    for _ in range(3):
        producer.Pump()
    outcome=status(lua,revision)
    assert_that(outcome['state']=='completed' and
                outcome['matchingNativeReplies']==3 and
                outcome['completedMoves']==3 and
                outcome['outboundCollisionObserved'] is False and
                len(lua.globals().ObservedMoves)==3,
                'synchronous original Move replies lost native request correlation')

    # A native host may expose a readable but non-replaceable sender. A partial
    # reply-hook installation must be undone before dispatching any packet.
    lua,producer=scene()
    lua.execute('''
      local previous = SFSNetwork.SendMessage
      SFSNetwork = setmetatable({}, {
          __index = function(_, key)
            if key == 'SendMessage' then return previous end
          end,
          __newindex = function() end,
      })
    ''')
    revision=state(lua)['layoutRevision']
    rejected=lua.globals().Act('city_layout_apply_start',values(lua,revision))
    assert_that(rejected['state']=='failed' and
                rejected['errorCode']=='GAME_METHOD_UNAVAILABLE' and
                len(lua.globals().ObservedMoves)==0 and
                lua.eval('DataCenter.BuildManager.BuildWorldMoveNewHandle == OriginalNativeMoveReply'),
                'non-hookable outgoing sender left reply override or sent native Move')


def test_stale_cancel_does_not_hit_reused_request_id():
    # The bridge's requestId is an input token, not a unique native job
    # incarnation. A late Cancel for an already completed layout must not
    # cancel a newly started layout that legitimately reuses that token.
    lua,producer=scene()
    revision=state(lua)['layoutRevision']
    start=lua.globals().Act('city_layout_apply_start',values(lua,revision))
    first_id=start['data']['status']['jobId']
    for who,target in ((ONE,4244),(TWO,4242),(ONE,4442)):
        confirm(lua,who,target)
        producer.Pump()
    settled=status(lua,revision)
    assert_that(settled['state']=='completed' and
                settled['jobId']==first_id and
                settled['cancelRequested'] is False,
                'first 2x2 job did not reach exact native terminal state')
    cancel_fields={'profileId':'p1','launchSessionId':'session1',
                   'challenge':'nonce','jobId':first_id}
    late=lua.globals().Act('city_layout_apply_cancel',
                           lua.table_from(cancel_fields))
    assert_that(late['state']=='confirmed' and
                late['data']['accepted'] is False and
                late['data']['status']['state']=='completed' and
                late['data']['status']['cancelRequested'] is False,
                'retrospective cancel falsely rewrote confirmed completion')

    new_revision=state(lua)['layoutRevision']
    reverse=lua.globals().Fields(new_revision,
                                  f'{ONE}:4242;{TWO}:4442')
    second=lua.globals().Act('city_layout_apply_start',reverse)
    assert_that(second['state']=='accepted' and
                second['data']['status']['jobId'] != first_id and
                second['data']['status']['inFlight'] is True,
                'reused owner requestId produced the same native job ID')
    old_cancel=lua.globals().Act('city_layout_apply_cancel',
                                 lua.table_from(cancel_fields))
    assert_that(old_cancel['state']=='failed' and
                old_cancel['errorCode']=='CITY_LAYOUT_JOB_MISSING' and
                status(lua,new_revision)['state']=='running' and
                status(lua,new_revision)['cancelRequested'] is False,
                'delayed Cancel for prior request improperly stopped a newer job')
    pending=status(lua,new_revision)
    fresh_cancel_fields=dict(cancel_fields)
    fresh_cancel_fields['jobId']=second['data']['status']['jobId']
    accepted_cancel=lua.globals().Act('city_layout_apply_cancel',
                                      lua.table_from(fresh_cancel_fields))
    assert_that(accepted_cancel['data']['accepted'] is True and
                accepted_cancel['data']['status']['state']=='cancelling',
                'valid new-job Cancel did not preserve in-flight receipt')
    packets_before=len(lua.globals().ObservedMoves)
    confirm(lua,int(pending['pendingUuid']),pending['pendingPointId'])
    producer.Pump()
    after=status(lua,new_revision)
    assert_that(after['state']=='cancelled' and
                after['cancelRequestedWhileInFlight'] is True and
                len(lua.globals().ObservedMoves)==packets_before,
                'new-job Cancel dispatched remaining 2x2 steps')


def main():
    check_original_game_wire()
    check_historical_wrong_type_source()
    test_exact_native_swap()
    test_negative_native_zones()
    test_cancel_partial_and_uncertain()
    test_outbound_native_collision_fences()
    test_historical_inbound_replace_collision()
    test_unexpected_native_occupancy_during_swap()
    test_displaced_native_observers_and_sync_reply()
    test_stale_cancel_does_not_hit_reused_request_id()
    print('FEATURE018_CITY_SWAP: PASS v26 exact 2x2 native UUID '
          'swap preflight/3x ACK/cancel/no-buffer/late uncertainty/'
          'outbound collision/old Replace inbound reply/native occupancy')


if __name__=='__main__':
    main()
