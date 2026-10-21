"""Read-only reconciliation of a prior worker gather. Never sends a recall."""
import json
from pathlib import Path
from feature018_live_queue import submit


def recover(root,output,uid,server,proof,observe_only=False):
    prior_root=json.loads((proof.parent/'root.json').read_text(encoding='utf-8-sig'))['root']
    admission=json.loads((Path(prior_root)/'feature018-account-admission.json').read_text(encoding='utf-8-sig'))
    if admission.get('uid')!=uid or admission.get('serverId')!=server:
        raise RuntimeError('Previous proof belongs to a different account; no command sent')
    prior=json.loads(proof.read_text(encoding='utf-8-sig'))['result']
    record=prior['cleanup']['services']['resourceGather']['after']
    reply=submit(root,{'kind':'command','command':'feature018_automation_observe',
        'payload':{'kind':'resourceGather'}},timeout=60,expected_uid=uid,expected_server=server)
    (output/'recall-cleanup-operations.json').write_text(json.dumps([reply],indent=2))
    if reply.get('state')!='returned'or reply.get('error'):raise RuntimeError('Read-only reconciliation failed')
    observed=reply['result']
    squad=next(s for s in observed['gatherSquads']if s['squadIndex']==record['squadIndex'])
    pending=squad.get('collecting')is True and squad.get('marchUuid')==record['marchUuid']
    value={'state':'returned','result':{'success':not pending,'readOnly':True,'pending':pending,
        'priorMarchUuid':record['marchUuid'],'observed':observed,
        'reason':'Prior exact gather remains active; no uncertain recall is replayed'if pending else'Prior march is no longer actively collecting; no recall sent'}}
    (output/'recall-cleanup-result.json').write_text(json.dumps(value,indent=2))
    return value
