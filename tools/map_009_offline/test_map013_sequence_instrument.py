"""Execute actual generated sequence helpers under isolated Lua 5.3, inert only."""
from pathlib import Path
import sys
import unittest
from luaparser import ast
import run_map013_owned_observer as observer
from native_reply_instrument_013 import instrument_native_reply
from sequence_instrument_013 import RESTORE_VARS, instrument_sequence

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'artifacts/map-013-lua-site'))
from lupa.lua53 import LuaRuntime


class SequenceInstrumentTests(unittest.TestCase):
    def source(self):
        return instrument_sequence(instrument_native_reply(observer.overview.resource_probe_source())).decode()

    def setup_runtime(self):
        body = self.source()
        helpers = body[body.index('function M._map013SequenceAdmit'):body.index('local function write_bulk_aoi_result')]
        initial = '\n'.join(f'local {n}="initial:{n}"' for n in RESTORE_VARS)
        changed = '\n'.join(f'{n}="later:{n}"' for n in RESTORE_VARS)
        runtime = LuaRuntime(unpack_returned_tuples=True)
        module = runtime.execute('''local M={};local now=10;local os={time=function()return now end}
local root="inert";local bulk_aoi_request=nil;local restores=0;local last=nil
local bulk_aoi_original_manager_response_flag=true;local bulk_aoi_original_world_response_flag=true
local bulk_aoi_response_flags_reset=false;local bulk_aoi_skip_restore_update=true
local function restore_bulk_aoi_state(manager) restores=restores+1;return manager=="owned" end
local function write_json(path,value) last=value end
''' + initial + '\n' + helpers + '''
return {M=M,change=function() ''' + changed + ''' end,
getHome=function()return bulk_aoi_native_original_pos end,
clock=function(t)now=t end,active=function(v)bulk_aoi_request=v end,
restores=function()return restores end,last=function()return last end,
flags=function()return bulk_aoi_original_manager_response_flag,bulk_aoi_original_world_response_flag,bulk_aoi_skip_restore_update end}
''')
        request = runtime.table_from({'profileId': 'p', 'launchSessionId': 's', 'gamePid': 123,
                                      'serverId': 2212, 'requestMode': 'coverage', 'map013Sequence': 'keep'})
        return runtime, module, request

    def test_actual_snapshot_survives_later_steps_and_restores_original(self):
        runtime, m, r = self.setup_runtime()
        self.assertTrue(m.M._map013SequenceAdmit(r, 'owned', 498, 63))
        m.change()
        self.assertTrue(m.M._map013SequenceAdmit(r, 'owned', 455, 425))
        self.assertEqual(m.M._map013Group.homeX, 498)
        m.M._map013UseOriginalHome()
        self.assertEqual(m.getHome(), 'initial:bulk_aoi_native_original_pos')
        self.assertEqual(m.flags(), (False, False, False))

    def test_foreign_owner_server_mode_and_unowned_final_reject(self):
        runtime, m, r = self.setup_runtime()
        r.map013Sequence = 'final'
        self.assertFalse(m.M._map013SequenceAdmit(r, 'owned', 498, 63))
        r.map013Sequence = 'keep'
        self.assertTrue(m.M._map013SequenceAdmit(r, 'owned', 498, 63))
        for field, changed in [('profileId', 'foreign'), ('launchSessionId', 'foreign'),
                               ('gamePid', 456), ('serverId', 2213), ('requestMode', 'direct')]:
            old = r[field]; r[field] = changed
            self.assertFalse(m.M._map013SequenceAdmit(r, 'owned', 455, 425))
            r[field] = old
        r.map013Sequence = None
        self.assertFalse(m.M._map013SequenceAdmit(r, 'owned', 455, 425))

    def test_actual_idle_boundary_restores_once_and_never_claims_success(self):
        runtime, m, r = self.setup_runtime()
        self.assertTrue(m.M._map013SequenceAdmit(r, 'owned', 498, 63))
        m.clock(21); m.M._map013SequenceGuard()
        self.assertEqual(m.restores(), 0)
        m.clock(22); m.active(runtime.table());m.M._map013SequenceGuard()
        self.assertEqual(m.restores(), 0)
        m.active(None);m.M._map013SequenceGuard()
        self.assertEqual(m.restores(), 1)
        self.assertEqual(m.last().state, 'failed')
        self.assertEqual(m.last().error, 'sequence_idle_timeout')
        self.assertIsNone(m.M._map013Group)
        m.M._map013SequenceGuard();self.assertEqual(m.restores(), 1)

    def test_full_private_source_parses_and_fits_package(self):
        text = self.source()
        ast.parse(text)
        packed = '\n'.join(l.rstrip() for l in text.splitlines() if l.strip() and not l.lstrip().startswith('--'))+'\n'
        self.assertLess(len(observer.overview.lr.encode_lenc(packed.encode())), 65536)
