"""Inert proof-account fences; no game, process, bridge or installation access."""
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from feature018_live_queue import submit
from feature018_r30_recall_cleanup import recover


class ProofAccountChecks(unittest.TestCase):
    def test_exact_pairs_and_no_default_fallthrough_to_temporary_account(self):
        for uid,server in (('4799432499002212',2212),('1183884595002296',2296)):
            with tempfile.TemporaryDirectory() as folder:
                root=Path(folder)/'native-ui-inert';root.mkdir()
                (root/'feature018-account-admission.json').write_text(json.dumps(
                    dict(uid=uid,serverId=server,loggedIn=True,connected=True)))
                (root/'owned-exit.json').write_text('{}')
                expected={p.name:p.read_bytes()for p in root.iterdir()}
                with self.assertRaisesRegex(ValueError,'Session already exited'):
                    submit(root,{'kind':'command'},expected_uid=uid,expected_server=server)
                for wrong_uid,wrong_server in ((uid,2212 if server==2296 else 2296),('owner-main',server)):
                    with self.assertRaises(ValueError):
                        submit(root,{'kind':'command'},expected_uid=wrong_uid,expected_server=wrong_server)
                if server==2296:
                    with self.assertRaisesRegex(ValueError,'Wrong game account admission'):submit(root,{'kind':'command'})
                self.assertEqual(expected,{p.name:p.read_bytes()for p in root.iterdir()})

    def test_known_server_does_not_authorize_other_uid(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder)/'native-ui-inert';root.mkdir()
            (root/'feature018-account-admission.json').write_text(json.dumps(
                dict(uid='owner-main',serverId=2212,loggedIn=True,connected=True)))
            with self.assertRaisesRegex(ValueError,'Wrong game account admission'):submit(root,{'kind':'command'})
            self.assertFalse((root/'feature018-live-request.json').exists())

    def test_reconciliation_never_recalls_an_uncertain_march_and_checks_prior_account(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);prior=root/'prior';prior.mkdir();output=root/'out';output.mkdir()
            proof=root/'performance-result.json'
            (root/'root.json').write_text(json.dumps({'root':str(prior)}))
            admission=prior/'feature018-account-admission.json'
            admission.write_text(json.dumps({'uid':'1183884595002296','serverId':2296}))
            proof.write_text(json.dumps({'result':{'cleanup':{'services':{'resourceGather':{'after':{'squadIndex':1,'marchUuid':'61'}}}}}}))
            for collecting in (True,False):
                reply={'state':'returned','result':{'gatherSquads':[{'squadIndex':1,'marchUuid':'61','collecting':collecting}]}}
                with patch('feature018_r30_recall_cleanup.submit',return_value=reply)as send:
                    result=recover(root,output,'1183884595002296',2296,proof)
                    self.assertEqual(not collecting,result['result']['success'])
                    self.assertEqual(1,send.call_count)
                    self.assertEqual('feature018_automation_observe',send.call_args.args[1]['command'])
            admission.write_text(json.dumps({'uid':'owner-main','serverId':2296}))
            with patch('feature018_r30_recall_cleanup.submit')as send:
                with self.assertRaises(RuntimeError):recover(root,output,'1183884595002296',2296,proof)
                send.assert_not_called()


if __name__=='__main__':unittest.main(verbosity=2)
