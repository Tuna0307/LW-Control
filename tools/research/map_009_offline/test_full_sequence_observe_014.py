"""Controlled inverses of the private native-coverage admission algorithm."""
import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from full_sequence_observe_014 import run_full_sequence


class FullSequenceTests(unittest.TestCase):
    def run_case(self, corrupt=None):
        pending = {}
        def publish(path, values):
            pending.update(values)
            return 1
        def reply(path, expected, timeout):
            x, y = int(pending['targetTileX']), int(pending['targetTileY'])
            indices = [r * 100 + c
                for r in range(max(0,y//10-2), min(100,y//10+4))
                for c in range(max(0,x//10-7), min(100,x//10+8))]
            result = dict(state='proven', map013NativeReplyState='matched',
                serverLod=0, blockSize=10, blockCount=100,
                requestedIndices=indices, nativeCurrentSetCount=len(indices),
                map013PositionRetained=True, cameraGeometryConfirmed=True,
                cameraTileStable=True, point_records=[dict(kind='player_base',
                pointId=str(i), serverId=2212,worldId=0,x=i%100*10+5,y=i//100*10+5)
                for i in indices])
            if corrupt:
                corrupt(result,pending)
            return result
        observer=SimpleNamespace(publish_command=publish, wait_for=reply,
            retire_owned_reply=lambda *args: None, ExpectedReply=lambda *args,**kwargs: None)
        with tempfile.TemporaryDirectory() as root, contextlib.redirect_stdout(io.StringIO()):
            attempt=Path(root)
            run_full_sequence(observer, attempt=attempt, live_root=attempt, command={},
                world=dict(serverId=2212,worldId=0,playerTileX=498,playerTileY=63),
                profile='owned',session='owned',pid=123,challenge='owned',previous_id='first')
            return json.loads((attempt/'full-sequence-result.json').read_text())

    def test_every_native_cell_and_final_restore_are_required(self):
        receipt=self.run_case()
        self.assertEqual(receipt['nativeCoverage'],10000)
        self.assertEqual(len(receipt['nativeSourceRows']),10000)
        self.assertLess(receipt['probes'],400)
        self.assertTrue(receipt['exactCameraRestored'])
        self.assertFalse(receipt['productionEnabled'])

    def test_stale_or_foreign_rows_cannot_cover_cells(self):
        for corrupt in [lambda r,p:r.update(map013NativeReplyState='waiting'),
                        lambda r,p:r['point_records'][0].update(serverId=2213)]:
            with self.assertRaises(RuntimeError): self.run_case(corrupt)

    def test_duplicate_and_hole_footprints_reject(self):
        for corrupt in [lambda r,p:r['requestedIndices'].append(r['requestedIndices'][0]),
                        lambda r,p:(r['requestedIndices'].pop(0),r.update(nativeCurrentSetCount=len(r['requestedIndices'])))]:
            with self.assertRaises(RuntimeError): self.run_case(corrupt)

    def test_failed_final_restoration_cannot_be_success(self):
        def corrupt(r,p):
            if p['map013Sequence']=='final':r['cameraGeometryConfirmed']=False
        with self.assertRaisesRegex(RuntimeError,'camera'):self.run_case(corrupt)


if __name__=='__main__':unittest.main()
