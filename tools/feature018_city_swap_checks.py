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
                lua.eval('DataCenter.BuildManager.BuildWorldMoveNewHandle == OriginalNativeMoveReply'),
                '2x2 swap declared success without three native ACKs/cleanup')
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
                    len(lua.globals().ObservedMoves)==pending,
                    'cancel misreported partial native 2x2 movement')
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
                len(lua.globals().ObservedMoves)==1,
                'late exact Move ACK resumed abandoned swap or lost partial poststate')


def main():
    check_original_game_wire()
    check_historical_wrong_type_source()
    test_exact_native_swap()
    test_negative_native_zones()
    test_cancel_partial_and_uncertain()
    print('FEATURE018_CITY_SWAP: PASS v26 exact 2x2 native UUID '
          'swap preflight/3x ACK/cancel/no-buffer/late uncertainty')


if __name__=='__main__':
    main()
