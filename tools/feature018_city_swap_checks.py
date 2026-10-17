"""Inert source-v26/Lua53 2x2 City swap, native ACK, cancel and retry inverses.

Uses exact genuine 9726 and an explicitly hypothetical 9724 replacement for
sendable two-building simulations. Historical 9725 is read-only/blocked in all
action tests; the real owner layout remains 9725/9726, not this fixture.
"""
from __future__ import annotations

from feature018_city_game_checks import fake, check_original_game_wire, MODULE
from feature018_city_game_checks import bytecode, probe, root_methods, disassemble


HISTORICAL = 1392040865799139725
ONE = 1392040865799139724  # synthetic peer, never claimed present in the account
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
local a=1392040865799139724
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


def restore(lua, job_id):
    return lua.globals().Act('city_layout_restore_preflight', lua.table_from({
        'jobId': job_id, 'profileId': 'p1',
        'launchSessionId': 'session1', 'challenge': 'nonce',
    }))


def restore_start(lua, job_id, *, opt_in='1', request_id='restore-new', extra=None):
    fields = {
        'jobId': job_id, 'profileId': 'p1', 'launchSessionId': 'session1',
        'challenge': 'nonce', 'requestId': request_id, 'restoreOptIn': opt_in,
    }
    if extra:
        fields.update(extra)
    return lua.globals().Act('city_layout_restore_start',lua.table_from(fields))


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
                'synthetic safe 2x2 peer and real 9726 int64 fixture identities changed')
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


def test_historical_original_uuid_never_sent():
    # Preserve the original genuine 9725/9726 read-only pair as historical
    # evidence, independently of the synthetic 9724/9726 actionable fixture.
    # Historical 9725's missing native Replace nonce prohibits direct and
    # temporary-buffer sends, irrespective of a current pointId match.
    lua, producer = scene()
    lua.execute(f'''
      local b=DataCenter.BuildManager.allBuilding[{ONE}]
      DataCenter.BuildManager.allBuilding[{ONE}]=nil
      b.uuid={HISTORICAL}
      DataCenter.BuildManager.allBuilding[{HISTORICAL}]=b
    ''')
    observed=state(lua)
    preflight=observed['swapPreflights'][1]
    assert_that({observed['buildings'][i]['uuid'] for i in (1,2)} ==
                {str(HISTORICAL),str(TWO)} and
                preflight['preflightOnly'] is True and
                preflight['dispatchAuthorized'] is False and
                preflight['historicalUncertaintyInPlan'] is True and
                preflight['dispatchBlockReason']=='CITY_LAYOUT_HISTORICAL_RESULT_UNCERTAIN',
                'genuine historical 2x2 pair advertised as action-ready')
    for placements in (
        f'{HISTORICAL}:4244',
        f'{HISTORICAL}:4442;{TWO}:4242',
        f'{TWO}:4242;{HISTORICAL}:4442',
    ):
        denied=lua.globals().Act('city_layout_apply_start',
                     lua.globals().Fields(observed['layoutRevision'],placements))
        assert_that(denied['state']=='failed' and
                    denied['errorCode']=='CITY_LAYOUT_HISTORICAL_RESULT_UNCERTAIN' and
                    len(lua.globals().ObservedMoves)==0,
                    'old 9725 uncertain native Replace was replayed through Move')
    # A different building remains usable; the old 9725 still cannot be sent,
    # even though this new request was admitted and positively acknowledged.
    approved=lua.globals().Act('city_layout_apply_start',
             lua.globals().Fields(observed['layoutRevision'],f'{TWO}:4444'))
    assert_that(approved['state']=='accepted' and
                len(lua.globals().ObservedMoves)==1 and
                lua.globals().ObservedMoves[1]['uuid']==TWO,
                'historical guard incorrectly disabled safe unrelated 9726 Move')
    confirm(lua,TWO,4444)
    producer.Pump()
    inverse=restore(lua,approved['data']['status']['jobId'])['data']
    assert_that(inverse['ready'] is True and
                inverse['historicalUncertaintyInPlan'] is False and
                inverse['historicalRequestsReconciled'] is False,
                'safe 9726 partial restore implied old 9725 ACK clearance')


def test_explicit_restoration_action():
    # The opt-in handler is a fully separate native job. It must obtain its
    # destinations from the original immutable source, not arbitrary inputs,
    # and respect the same one-packet/native reply/full footprint lifecycle.
    phases = (
        (1, ((ONE,4244),), ((ONE,4242),)),
        (2, ((ONE,4244),(TWO,4242)), ((TWO,4442),(ONE,4242))),
        (3, ((ONE,4244),(TWO,4242),(ONE,4442)),
            ((ONE,4444),(TWO,4442),(ONE,4242))),
    )
    for count, forward, backward in phases:
        lua, producer=scene()
        revision=state(lua)['layoutRevision']
        start=lua.globals().Act('city_layout_apply_start',values(lua,revision))
        parent_id=start['data']['status']['jobId']
        # An explicit Restore request must not turn a running job into a
        # mutation or confuse its first already-issued native packet.
        still_pending=restore_start(lua,parent_id)
        assert_that(still_pending['errorCode']=='CITY_LAYOUT_BUSY' and
                    len(lua.globals().ObservedMoves)==1,
                    'Restore started while original native Move was outstanding')
        denied=restore_start(lua,parent_id,opt_in='0')
        assert_that(denied['errorCode']=='CITY_LAYOUT_RESTORE_CONSENT_REQUIRED' and
                    len(lua.globals().ObservedMoves)==1,
                    'Restore accepted without affirmative consent')
        for index,(uuid,target) in enumerate(forward,1):
            if count<3 and index==count:
                lua.globals().Act('city_layout_apply_cancel',lua.table_from({
                    'jobId':parent_id,'profileId':'p1','launchSessionId':'session1',
                    'challenge':'nonce',
                }))
            confirm(lua,uuid,target)
            producer.Pump()
        settled=status(lua,revision)
        assert_that(settled['completedMoves']==count and
                    settled['state']==('completed' if count==3 else 'cancelled') and
                    settled['inFlight'] is False,
                    'original 2x2 job not terminally settled before restore')
        preflight=restore(lua,parent_id)['data']
        assert_that(preflight['ready'] is True and
                    preflight['dispatchAuthorized'] is False and
                    len(preflight['plannedMoves'])==len(backward),
                    'read-only inverse missing source-backed original anchors')
        packets=len(lua.globals().ObservedMoves)
        wrong_job=restore_start(lua,parent_id+'-old')
        wrong_owner=restore_start(lua,parent_id,extra={'profileId':'other'})
        invalid_token=restore_start(lua,parent_id,request_id='forged nonce')
        forged=restore_start(lua,parent_id,opt_in='0',extra={
            'placements': f'{TWO}:9999','uuid':str(TWO),'pointId':9999})
        assert_that(wrong_job['errorCode']=='CITY_LAYOUT_JOB_MISSING' and
                    wrong_owner['errorCode']=='CITY_LAYOUT_JOB_MISSING' and
                    invalid_token['errorCode']=='CITY_LAYOUT_RESTORE_REQUEST_INVALID' and
                    forged['errorCode']=='CITY_LAYOUT_RESTORE_CONSENT_REQUIRED' and
                    len(lua.globals().ObservedMoves)==packets,
                    'invalid restore identity/consent caused native action')
        authorized=restore_start(lua,parent_id,extra={
            'placements':f'{TWO}:9999','uuid':str(TWO),'pointId':9999})
        assert_that(authorized['state']=='accepted' and
                    authorized['data']['status']['restoration'] is True and
                    authorized['data']['status']['restoreOfJobId']==parent_id and
                    authorized['data']['status']['jobId']!=parent_id and
                    len(lua.globals().ObservedMoves)==packets+1,
                    'explicit restore did not dispatch first exact child Move')
        child=authorized['data']['status']['jobId']
        reentry=restore_start(lua,parent_id)
        assert_that(reentry['errorCode']=='CITY_LAYOUT_JOB_MISSING' and
                    len(lua.globals().ObservedMoves)==packets+1,
                    'retired original job allowed duplicate opt-in restore')
        for index,(uuid,target) in enumerate(backward,1):
            observed_packet=lua.globals().ObservedMoves[packets+index]
            assert_that(observed_packet['uuid']==uuid and
                        observed_packet['pointId']==target,
                        'restore sender used forged destination or wrong building')
            producer.Pump()
            assert_that(len(lua.globals().ObservedMoves)==packets+index,
                        'restoration dispatched later stage before native ACK')
            confirm(lua,uuid,target)
            producer.Pump()
        completed=status(lua,revision)
        assert_that(completed['state']=='completed' and
                    completed['jobId']==child and
                    completed['restoration'] is True and
                    completed['completedMoves']==len(backward) and
                    completed['matchingNativeReplies']==len(backward) and
                    completed['inFlight'] is False and
                    lua.globals().DataCenter.BuildManager.allBuilding[ONE]['pointId']==4242 and
                    lua.globals().DataCenter.BuildManager.allBuilding[TWO]['pointId']==4442 and
                    lua.eval('SFSNetwork.SendMessage == OriginalNativeSend') and
                    lua.eval('DataCenter.BuildManager.BuildWorldMoveNewHandle == OriginalNativeMoveReply') and
                    lua.eval('DataCenter.BuildManager.FreeBuildingReplaceNewHandle == OriginalNativeReplaceReply'),
                    'original 2x2 native restoration lacked exact ACKs/state/hook cleanup')


def test_restore_cancel_preserves_true_original_anchors():
    # Three-step completed pair swap, then a second original-restore job
    # cancelled with its second native packet in flight. A subsequent inverse
    # MUST finish the first job's original layout, not undo the partial
    # restore back to the swapped arrangement.
    lua, producer=scene()
    rev=state(lua)['layoutRevision']
    first=lua.globals().Act('city_layout_apply_start',values(lua,rev))
    for who,target in ((ONE,4244),(TWO,4242),(ONE,4442)):
        confirm(lua,who,target)
        producer.Pump()
    first_id=first['data']['status']['jobId']
    child_start=restore_start(lua,first_id)
    assert_that(child_start['state']=='accepted', 'first restoration not admitted')
    child_id=child_start['data']['status']['jobId']
    initial_packet_count=len(lua.globals().ObservedMoves)
    confirm(lua,ONE,4444)
    producer.Pump()
    assert_that(len(lua.globals().ObservedMoves)==initial_packet_count+1 and
                lua.globals().ObservedMoves[initial_packet_count+1]['uuid']==TWO,
                'second restore step was not sent after first source ACK')
    cancelled=lua.globals().Act('city_layout_apply_cancel',lua.table_from({
        'jobId':child_id,'profileId':'p1','launchSessionId':'session1',
        'challenge':'nonce',
    }))
    assert_that(cancelled['data']['status']['state']=='cancelling',
                'restore-child in-flight cancellation not acknowledged')
    confirm(lua,TWO,4442)
    producer.Pump()
    stopped=status(lua,rev)
    assert_that(stopped['state']=='cancelled' and
                stopped['completedMoves']==2 and
                stopped['matchingNativeReplies']==2 and
                stopped['inFlight'] is False and
                len(lua.globals().ObservedMoves)==initial_packet_count+1,
                'restore-child cancel dispatched last packet')
    inverse=restore(lua,child_id)['data']
    assert_that(inverse['ready'] is True and
                inverse['restorationRequired'] is True and
                inverse['originalPlacements']==f'{ONE}:4242' and
                planned_steps(inverse)==[(str(ONE),4444,4242,False)],
                'nested restored job incorrectly defined swapped intermediate anchors as original')
    followup=restore_start(lua,child_id)
    assert_that(followup['state']=='accepted' and
                followup['data']['status']['restoreOfJobId']==child_id and
                len(lua.globals().ObservedMoves)==initial_packet_count+2 and
                lua.globals().ObservedMoves[initial_packet_count+2]['uuid']==ONE and
                lua.globals().ObservedMoves[initial_packet_count+2]['pointId']==4242,
                'second restore did not continue towards true initial layout')
    confirm(lua,ONE,4242)
    producer.Pump()
    latest_id=followup['data']['status']['jobId']
    complete=status(lua,rev)
    assert_that(complete['state']=='completed' and
                complete['completedMoves']==1 and
                complete['matchingNativeReplies']==1 and
                restore(lua,latest_id)['data']['originalLayoutConfirmed'] is True and
                restore(lua,latest_id)['data']['restorationRequired'] is False and
                lua.globals().DataCenter.BuildManager.allBuilding[ONE]['pointId']==4242 and
                lua.globals().DataCenter.BuildManager.allBuilding[TWO]['pointId']==4442,
                'nested restoration failed genuine original native footprint readiness')


def test_settled_owner_loss_retains_original_restore():
    # R9 erased the entire M.active job on a temporary owner-token nil if
    # the most recent native Move had already settled. This destroyed the
    # *only* originalAnchors map: the client could no longer restore even a
    # positively ACKed pair on exactly the same approved owner/session.
    for end_step in (1, 3):
        lua,producer=scene()
        revision=state(lua)['layoutRevision']
        started=lua.globals().Act('city_layout_apply_start',values(lua,revision))
        parent=started['data']['status']['jobId']
        for i,(who,point) in enumerate(((ONE,4244),(TWO,4242),(ONE,4442)),1):
            if end_step == i and end_step < 3:
                cancelled=lua.globals().Act('city_layout_apply_cancel', lua.table_from({
                    'profileId':'p1', 'launchSessionId':'session1',
                    'challenge':'nonce', 'jobId':parent,
                }))
                assert_that(cancelled['state']=='confirmed',
                            'native partial cancel not accepted before ACK')
            confirm(lua,who,point)
            producer.Pump()
            if end_step == i: break
        terminal=status(lua,revision)
        assert_that(terminal['state']==('cancelled' if end_step==1 else 'completed') and
                    terminal['completedMoves']==end_step and
                    terminal['inFlight'] is False,
                    'native terminal state missing before lease-loss fixture')
        count=len(lua.globals().ObservedMoves)
        producer.CleanupAfterLeaseLoss()
        after=status(lua,revision)
        assert_that(after['state']==terminal['state'] and
                    after['jobId']==parent and
                    after['completedMoves']==end_step and
                    after['inFlight'] is False and
                    len(lua.globals().ObservedMoves)==count,
                    'transient owner loss destroyed exact settled native state')
        foreign=lua.globals().Fields(revision,'')
        foreign['profileId']='different-owner'
        assert_that(lua.globals().Act('city_layout_apply_status',foreign)
                    ['data']['state']=='idle' and
                    restore_start(lua,parent,extra={'profileId':'different-owner'})
                    ['errorCode']=='CITY_LAYOUT_JOB_MISSING' and
                    restore(lua,parent)['data']['ready'] is True,
                    'terminal owner-bound source anchors leaked or disappeared')
        restore_job=restore_start(lua,parent,request_id=f'restore-after-loss-{end_step}')
        assert_that(restore_job['state']=='accepted' and
                    restore_job['data']['status']['restoration'] is True and
                    len(lua.globals().ObservedMoves)==count+1,
                    'owner re-admission lost affirmative original restore action')
        child=restore_job['data']['status']['jobId']
        expected=((ONE,4242),) if end_step==1 else (
            (ONE,4444),(TWO,4442),(ONE,4242))
        for who,target in expected:
            confirm(lua,who,target)
            producer.Pump()
        result=status(lua,revision)
        assert_that(result['state']=='completed' and result['jobId']==child and
                    restore(lua,child)['data']['originalLayoutConfirmed'] is True and
                    lua.globals().DataCenter.BuildManager.allBuilding[ONE]['pointId']==4242 and
                    lua.globals().DataCenter.BuildManager.allBuilding[TWO]['pointId']==4442 and
                    lua.eval('SFSNetwork.SendMessage == OriginalNativeSend') and
                    lua.eval('DataCenter.BuildManager.BuildWorldMoveNewHandle == OriginalNativeMoveReply') and
                    lua.eval('DataCenter.BuildManager.FreeBuildingReplaceNewHandle == OriginalNativeReplaceReply'),
                    'same owner post-loss restore failed complete native original/observer state')


def test_same_vm_reload_preserves_inflight_and_job_ids():
    # Re-executing the producer chunk in an unchanged Lua VM previously
    # discarded active, resetting nextJobSequence while leaving SFS/BuildManager
    # wrappers from the old producer in place. The second instance could
    # accept an overlapping native Move with no shared in-flight tombstone.
    lua,producer=scene()
    revision=state(lua)['layoutRevision']
    started=lua.globals().Act('city_layout_apply_start',values(lua,revision))
    first_id=started['data']['status']['jobId']
    original_sends=len(lua.globals().ObservedMoves)
    reloaded=lua.execute(MODULE.read_text(encoding='utf-8'))
    lua.globals().CityOldProducer=producer
    lua.globals().CityReloadedProducer=reloaded
    assert_that(lua.eval('CityOldProducer == CityReloadedProducer') and
                reloaded.VERSION==producer.VERSION and
                reloaded.Register(lua.globals().Feature) is True and
                len(lua.globals().ObservedMoves)==original_sends,
                'same-VM reload lost producer/native wrapper or existing feature registry')
    pending=status(lua,revision)
    assert_that(pending['jobId']==first_id and
                pending['inFlight'] is True and pending['completedMoves']==0,
                'same-VM reload erased pending in-flight Move')
    second=lua.globals().Act('city_layout_apply_start',
                             lua.globals().Fields(revision,f'{TWO}:4444'))
    assert_that(second['errorCode']=='CITY_LAYOUT_BUSY' and
                len(lua.globals().ObservedMoves)==original_sends,
                'reloaded producer dispatched a duplicate native packet')
    reloaded.CleanupAfterLeaseLoss()
    assert_that(status(lua,revision)['errorCode']=='CITY_LAYOUT_OWNER_LOSS_UNCERTAIN' and
                len(lua.globals().ObservedMoves)==original_sends,
                'reloaded producer failed to preserve native owner-loss fence')
    reloaded_again=lua.execute(MODULE.read_text(encoding='utf-8'))
    lua.globals().CityReloadedAgain=reloaded_again
    assert_that(lua.eval('CityOldProducer == CityReloadedAgain'),
                'second source eval reset the same in-flight native identity')
    confirm(lua,ONE,4244)
    reloaded_again.Pump()
    terminal=status(lua,revision)
    assert_that(terminal['state']=='failed' and
                terminal['errorCode']=='CITY_LAYOUT_OWNER_LOSS_RECONCILED' and
                terminal['completedMoves']==1 and
                terminal['inFlight'] is False and
                len(lua.globals().ObservedMoves)==original_sends,
                'reload leaked queued second Move or failed to reconcile late original ACK')
    authorized_restore=restore_start(lua,first_id,request_id='after-reload')
    assert_that(authorized_restore['state']=='accepted' and
                authorized_restore['data']['status']['jobId']!=first_id and
                len(lua.globals().ObservedMoves)==original_sends+1,
                'reloaded producer lost original anchors or generated old job ID')
    child_id=authorized_restore['data']['status']['jobId']
    stale_cancel=lua.globals().Act('city_layout_apply_cancel',lua.table_from({
        'profileId':'p1', 'launchSessionId':'session1',
        'challenge':'nonce', 'jobId':first_id,
    }))
    assert_that(stale_cancel['errorCode']=='CITY_LAYOUT_JOB_MISSING' and
                status(lua,revision)['jobId']==child_id,
                'old Cancel affected restored child after VM reload')
    confirm(lua,ONE,4242)
    reloaded_again.Pump()
    assert_that(status(lua,revision)['state']=='completed' and
                restore(lua,child_id)['data']['originalLayoutConfirmed'] is True,
                'reloaded producer did not settle exact native restore ACK')


def test_native_manager_rebind_during_ack_fences_move():
    # The R9 receiver checked only that the old observer *object* was intact.
    # A new City BuildManager/SFSNetwork/MsgDefines may replace the originals
    # after a previous exact reply but before Pump. It is unsafe to treat the
    # obsolete handler's receipt as authority to send step two in the NEW
    # manager, even if building UUID, position and footprint still match.
    for change in ('manager', 'network', 'message_ids'):
        lua,producer=scene()
        revision=state(lua)['layoutRevision']
        started=lua.globals().Act('city_layout_apply_start',values(lua,revision))
        assert_that(started['state']=='accepted',
                    f'{change}: native two-building fixture never issued step one')
        confirm(lua,ONE,4244)
        if change=='manager':
            lua.execute('''
               OldCityManager=DataCenter.BuildManager
               DataCenter.BuildManager=setmetatable({
                  allBuilding=OldCityManager.allBuilding,
               },{__index=OldCityManager})
            ''')
        elif change=='network':
            lua.execute('''
               OldCityNetwork=SFSNetwork
               SFSNetwork={SendMessage=OldCityNetwork.SendMessage}
            ''')
        else:
            lua.execute('''
               MsgDefines={
                   BuildWorldMoveNew='different_current_move_kind',
                   FreeBuildingReplaceNew='native_free_building_replace_new',
               }
            ''')
        producer.Pump()
        stalled=status(lua,revision)
        assert_that(stalled['state']=='failed' and
                    stalled['errorCode']=='CITY_LAYOUT_RESULT_UNCERTAIN' and
                    stalled['inFlight'] is True and
                    stalled['completedMoves']==0 and
                    stalled['matchingNativeReplies']==1 and
                    len(lua.globals().ObservedMoves)==1,
                    f'{change}: stale native observer permitted a cross-manager step two')
        rejected=lua.globals().Act('city_layout_apply_start',
            lua.globals().Fields(state(lua)['layoutRevision'],f'{TWO}:4444'))
        assert_that(rejected['errorCode']=='CITY_LAYOUT_RESULT_UNCERTAIN' and
                    len(lua.globals().ObservedMoves)==1,
                    f'{change}: rebind lost pending server result fence')


def test_restoration_late_ack_and_historical_inbound_fence():
    # Actual source .9726 has previously moved/restored. A new restoration
    # child still must not send a second Move while its reply is uncertain.
    lua,producer=scene()
    lua.execute('Clock=100; os.time=function() return Clock end')
    revision=state(lua)['layoutRevision']
    first=lua.globals().Act('city_layout_apply_start',
             lua.globals().Fields(revision,f'{TWO}:4444'))
    assert_that(first['state']=='accepted','safe 9726 fixture not dispatched')
    confirm(lua,TWO,4444)
    producer.Pump()
    parent=first['data']['status']['jobId']
    child=restore_start(lua,parent)
    assert_that(child['state']=='accepted','9726 restore child not sent')
    child_id=child['data']['status']['jobId']
    count=len(lua.globals().ObservedMoves)
    lua.execute('Clock=116')
    timed=status(lua,revision)
    assert_that(timed['state']=='failed' and
                timed['errorCode']=='GAME_MOVE_RESULT_TIMEOUT' and
                timed['inFlight'] is True and timed['restoration'] is True and
                restore_start(lua,child_id)['errorCode']=='CITY_LAYOUT_RESULT_UNCERTAIN' and
                restore(lua,child_id)['data']['ready'] is False and
                len(lua.globals().ObservedMoves)==count,
                'timed-out restore child unlocked uncertain duplicate Move')
    confirm(lua,TWO,4442)
    producer.Pump()
    settled=status(lua,revision)
    final_inverse=restore(lua,child_id)['data']
    assert_that(settled['state']=='failed' and
                settled['completedMoves']==1 and
                settled['matchingNativeReplies']==1 and
                settled['inFlight'] is False and
                final_inverse['originalLayoutConfirmed'] is True and
                final_inverse['restorationRequired'] is False and
                len(lua.globals().ObservedMoves)==count and
                lua.eval('SFSNetwork.SendMessage == OriginalNativeSend'),
                'late exact native child ACK resumed abandoned plan or lost original layout')

    # The historical 9725 Replace reply has no nonce, even after the .9726
    # parent settled. It may arrive while the new child is pending, so keep
    # the child quarantined despite matching Move buildInfo/after-state.
    lua,producer=scene()
    revision=state(lua)['layoutRevision']
    parent=lua.globals().Act('city_layout_apply_start',
             lua.globals().Fields(revision,f'{TWO}:4444'))
    confirm(lua,TWO,4444)
    producer.Pump()
    child=restore_start(lua,parent['data']['status']['jobId'])
    assert_that(child['state']=='accepted','inbound child fence fixture not sent')
    count=len(lua.globals().ObservedMoves)
    lua.execute('DataCenter.BuildManager:FreeBuildingReplaceNewHandle({errorCode=99001})')
    assert_that(lua.eval('NativeReplaceReplyInvocations')==1,
                'original v26 historical Replace handler was bypassed')
    confirm(lua,TWO,4442)
    producer.Pump()
    stuck=status(lua,revision)
    assert_that(stuck['state']=='failed' and
                stuck['errorCode']=='CITY_LAYOUT_RESULT_UNCERTAIN' and
                stuck['inFlight'] is True and
                stuck['nativeReplaceReplyCount']==1 and
                stuck['inboundReplaceCollisionObserved'] is True and
                stuck['historicalRequestsReconciled'] is False and
                restore_start(lua,child['data']['status']['jobId'])
                    ['errorCode']=='CITY_LAYOUT_RESULT_UNCERTAIN' and
                len(lua.globals().ObservedMoves)==count,
                'historical old Replace inbound reply falsely cleared 9726 restoration')


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


def test_nearest_full_2x2_buffer():
    # Both 4144 (Manhattan 3) and 4244 (Manhattan 2) are source-zone-valid
    # empty buffers. Row-index scanning previously chose distant 4144.
    lua, _ = scene()
    lua.execute('''
       for _, pid in ipairs({4144, 4143, 4044, 4043}) do
           AllowedZones[pid] = true
       end
    ''')
    before=state(lua)
    assert_that(planned_steps(before['swapPreflights'][1])[0] ==
                (str(ONE),4242,4244,True),
                'buffer planner preferred lower pointId over closer legal 2x2 anchor')

    # A partial self-overlapping 4242->4243 is not a freed 2x2 buffer, even
    # if every native zone cell in that overlapping footprint is admitted.
    lua, _ = scene()
    lua.execute('''
       for _, pid in ipairs({4143,4243}) do AllowedZones[pid] = true end
       for _, pid in ipairs({4144,4244,4344,4444,4544}) do AllowedZones[pid] = nil end
    ''')
    blocked=state(lua)
    assert_that(len(blocked['swapPreflights'])==0 and
                blocked['swapPreflightIssues'][1]['issueCode']=='CITY_LAYOUT_NO_BUFFER' and
                len(lua.globals().ObservedMoves)==0,
                'partial self-overlap was misreported as valid native swap buffer')


def test_read_only_original_restoration():
    # Terminal and partial 2x2 outcomes expose a fresh native inverse plan
    # without ever sending a packet. No user-supplied building UUID/anchor is
    # accepted for this owner/job-bound read-only method.
    lua,producer=scene()
    revision=state(lua)['layoutRevision']
    started=lua.globals().Act('city_layout_apply_start',values(lua,revision))
    job_id=started['data']['status']['jobId']
    confirm(lua,ONE,4244)
    # The first successful source handler has updated native state; the next
    # Move has NOT yet been dispatched by the authorized owner Pump. Merely
    # querying restore readiness must not take that mutating next step.
    pending=restore(lua,job_id)['data']
    assert_that(pending['ready'] is False and
                pending['pendingNativeMove'] is True and
                len(lua.globals().ObservedMoves)==1 and
                lua.globals().ObservedMoves[1]['uuid']==ONE,
                'read-only restore preflight progressed ACK into a second send')

    for confirmed_moves, final_state, expected_steps in (
        (1, 'cancelled', [(str(ONE),4244,4242,False)]),
        (2, 'cancelled', [
            (str(TWO),4242,4442,False),
            (str(ONE),4244,4242,False),
        ]),
        (3, 'completed', [
            (str(ONE),4442,4444,True),
            (str(TWO),4242,4442,False),
            (str(ONE),4444,4242,False),
        ]),
    ):
        lua,producer=scene()
        revision=state(lua)['layoutRevision']
        start=lua.globals().Act('city_layout_apply_start',values(lua,revision))
        job_id=start['data']['status']['jobId']
        denied=restore(lua,job_id)['data']
        assert_that(denied['ready'] is False and
                    denied['issueCode']=='CITY_LAYOUT_RESULT_UNCERTAIN' and
                    denied['pendingNativeMove'] is True and
                    len(denied['plannedMoves'])==0,
                    'restore preflight claimed in-flight 2x2 original state')
        for index,(uuid,destination) in enumerate(((ONE,4244),(TWO,4242),(ONE,4442)),1):
            if confirmed_moves<3 and confirmed_moves==index:
                receipt=lua.globals().Act('city_layout_apply_cancel', lua.table_from({
                    'jobId':job_id,'profileId':'p1','launchSessionId':'session1',
                    'challenge':'nonce',
                }))
                assert_that(receipt['data']['status']['state']=='cancelling',
                            'cancel test failed to record pending original restore')
            confirm(lua,uuid,destination)
            producer.Pump()
            if confirmed_moves==index: break
        terminal=status(lua,revision)
        assert_that(terminal['state']==final_state and
                    terminal['completedMoves']==confirmed_moves and
                    terminal['inFlight'] is False,
                    'controlled 2x2 fixture did not terminally settle')
        sent_count=len(lua.globals().ObservedMoves)
        preflight=restore(lua,job_id)
        assert_that(preflight['state']=='confirmed','native restore preflight unavailable')
        info=preflight['data']
        assert_that(info['ready'] is True and
                    info['restorationRequired'] is True and
                    info['originalLayoutConfirmed'] is False and
                    info['historicalRequestsReconciled'] is False and
                    info['historicalUncertaintyInPlan'] is False and
                    info['dispatchAuthorized'] is False and
                    info['preflightOnly'] is True and
                    info['requestDispatched'] is False and
                    info['pendingNativeMove'] is False and
                    info['currentRevision']==state(lua)['layoutRevision'] and
                    planned_steps(info)==expected_steps and
                    len(lua.globals().ObservedMoves)==sent_count,
                    f'partial/complete {confirmed_moves}-step original restore plan unsafe or sent')
        independently_revalidated=lua.globals().Act('city_layout_validate',
            lua.globals().Fields(info['currentRevision'],
                                 info['originalPlacements']))['data']
        assert_that(independently_revalidated['valid'] is True and
                    planned_steps(independently_revalidated)==planned_steps(info) and
                    len(lua.globals().ObservedMoves)==sent_count,
                    'source native restoration preflight differed from fresh standard City validator')
        wrong_owner=lua.globals().Act('city_layout_restore_preflight',
                                      lua.table_from({
                                      'jobId':job_id,'profileId':'other-owner',
                                      'launchSessionId':'session1','challenge':'nonce'}))
        wrong_job=restore(lua,job_id+'-stale')
        assert_that(wrong_owner['errorCode']=='CITY_LAYOUT_JOB_MISSING' and
                    wrong_job['errorCode']=='CITY_LAYOUT_JOB_MISSING',
                    'restore preflight leaked original anchors to foreign owner/job')

    # A timeout is not a negative server ACK: even a read-only restore plan
    # must remain unavailable until a matching native reply and all footprints.
    lua,producer=scene()
    lua.execute('Clock=100; os.time=function() return Clock end')
    revision=state(lua)['layoutRevision']
    start=lua.globals().Act('city_layout_apply_start',values(lua,revision))
    job_id=start['data']['status']['jobId']
    lua.execute('Clock=116')
    assert_that(status(lua,revision)['errorCode']=='GAME_MOVE_RESULT_TIMEOUT',
                'late restore fixture did not time out')
    blocked=restore(lua,job_id)['data']
    assert_that(blocked['ready'] is False and
                blocked['pendingNativeMove'] is True and
                blocked['issueCode']=='CITY_LAYOUT_RESULT_UNCERTAIN' and
                len(lua.globals().ObservedMoves)==1,
                'unconfirmed late packet exposed an original-layout restore send')
    confirm(lua,ONE,4244)
    producer.Pump()
    after=restore(lua,job_id)['data']
    assert_that(after['ready'] is True and
                after['restorationRequired'] is True and
                after['historicalUncertaintyInPlan'] is False and
                planned_steps(after)==[(str(ONE),4244,4242,False)] and
                len(lua.globals().ObservedMoves)==1,
                'late exact native Move ACK did not permit fresh read-only restore plan')

    # Moving a building independently after cancellation invalidates the
    # planned restore even when the original draft revision is re-read.
    lua,producer=scene()
    revision=state(lua)['layoutRevision']
    start=lua.globals().Act('city_layout_apply_start',values(lua,revision))
    job_id=start['data']['status']['jobId']
    lua.globals().Act('city_layout_apply_cancel',lua.table_from({
        'jobId':job_id,'profileId':'p1','launchSessionId':'session1','challenge':'nonce',
    }))
    confirm(lua,ONE,4244)
    producer.Pump()
    lua.execute(f'DataCenter.BuildManager.allBuilding[{TWO}].pointId=4544')
    stale=restore(lua,job_id)['data']
    assert_that(stale['ready'] is False and
                stale['issueCode']=='CITY_LAYOUT_STALE' and
                stale['plannedMoves'] is not None and
                len(stale['plannedMoves'])==0,
                'foreign native move was hidden by auto-restoration planning')

    # A separate known-working second building (.9726) can be restored to its
    # own pre-job point without claiming that unrelated historical .9725's
    # unACKed Replace has been reconciled. This remains preflight-only.
    lua,producer=scene()
    revision=state(lua)['layoutRevision']
    only_second=lua.globals().Fields(revision,f'{TWO}:4444')
    start=lua.globals().Act('city_layout_apply_start',only_second)
    assert_that(start['state']=='accepted','safe 9726-only fixture not dispatched')
    job_id=start['data']['status']['jobId']
    confirm(lua,TWO,4444)
    producer.Pump()
    safe=restore(lua,job_id)['data']
    assert_that(safe['ready'] is True and
                safe['historicalRequestsReconciled'] is False and
                safe['historicalUncertaintyInPlan'] is False and
                safe['dispatchAuthorized'] is False and
                planned_steps(safe)==[(str(TWO),4444,4442,False)] and
                len(lua.globals().ObservedMoves)==1,
                '9726-only source restore mistakenly included unresolved 9725')
    lua.execute('AllowedZones[4442]=nil')
    blocked=restore(lua,job_id)['data']
    assert_that(blocked['ready'] is False and
                blocked['issueCode']=='CITY_LAYOUT_RESTORE_NOT_VALID' and
                blocked['issues'][1]['code']=='CITY_LAYOUT_ZONE' and
                len(lua.globals().ObservedMoves)==1,
                'native zoning change failed to invalidate read-only restore plan')


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
        ('old_9725_success', f'{{buildInfo={{uuid={HISTORICAL},pointId=4144,bId=10210000}}}}'),
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
    test_historical_original_uuid_never_sent()
    test_explicit_restoration_action()
    test_restore_cancel_preserves_true_original_anchors()
    test_settled_owner_loss_retains_original_restore()
    test_same_vm_reload_preserves_inflight_and_job_ids()
    test_native_manager_rebind_during_ack_fences_move()
    test_restoration_late_ack_and_historical_inbound_fence()
    test_negative_native_zones()
    test_nearest_full_2x2_buffer()
    test_read_only_original_restoration()
    test_cancel_partial_and_uncertain()
    test_outbound_native_collision_fences()
    test_historical_inbound_replace_collision()
    test_unexpected_native_occupancy_during_swap()
    test_displaced_native_observers_and_sync_reply()
    test_stale_cancel_does_not_hit_reused_request_id()
    print('FEATURE018_CITY_SWAP: PASS source-v26 2x2/9726; historical '
          '9725 no-send; native ACK/cancel/restore; terminal lease-loss '
          'original recovery, same-VM reload tombstone, manager rebind fence')


if __name__=='__main__':
    main()
