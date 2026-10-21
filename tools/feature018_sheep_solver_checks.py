"""Offline contract checks against the ADMITTED original v26 Season 4 code.

No game process, credentials, installed-file mutation or SFSNetwork connection.
Decode the current original Lua methods and execute selected real bytecode with
bounded/inert Python fixtures. The new Lua solver's syntax/guard surface is
checked separately; these tests are not live proof of auto-clear success.
"""

from __future__ import annotations

import hashlib
import re
import sys
import unittest
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent
sys.path[:0] = [str(ROOT), str(ROOT / "map_009_offline")]
sys.path.insert(0, str(ROOT.parent / "artifacts" / "map-013-lua-site"))
import compact_lua53 as lua  # noqa: E402
import execute_selected as vm  # noqa: E402
import run_live_resource_probe as probe  # noqa: E402
from feature018_mini_lua import ORIGINAL_ENTRY_SHA256  # noqa: E402
from inspect_current import root_methods, walk, disassemble  # noqa: E402


class NativeSeasonFourBytecode(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        v1, v2, items = probe.read_lwlf(probe.paths()["data"])
        cls.items = dict(items)
        original = cls.items.get(probe.ORIGINAL_LUA_ENTRY, cls.items.get(probe.LUA_ENTRY))
        if (v1, v2) not in ((3, 26), (3, 27)) or not original or \
           hashlib.sha256(original).hexdigest() != ORIGINAL_ENTRY_SHA256:
            raise AssertionError("Admitted original v26/v27 Lua entry unavailable")

    def method(self, module, name):
        proto = lua.parse_chunk(probe.decode_lenc(self.items[module]))
        path = root_methods(proto).get(name)
        self.assertIsNotNone(path, f"Missing native v26 {module}:{name}")
        for part in path.split("/")[1:]:
            proto = proto["children"][int(part)]
        return proto

    def test_current_season_is_source_specific(self):
        method = self.method("DataCenter/SmallGameManager/GSheep.luac", "GetCurrentLevel")
        season = {"GetCurrentLevel": lambda self: 4}
        context = {"source": "Season", "TryGetValue": lambda *args: 3}
        globals_ = {"MiniGameSource": {"Season": "Season"},
                    "DataCenter": {"LWSheepDataManager": season}}
        self.assertEqual(vm.execute(method, context, globals_=globals_), (4,))

    def test_food_house_native_enter_owns_exact_window_name(self):
        original = self.method("DataCenter/SmallGameManager/GSheep.luac", "Enter")
        constants = {str(value) for value in original["constants"]}
        self.assertTrue({"UIManager", "OpenWindow", "UIWindowNames",
                         "UILUISheepCloud"} <= constants)

    def test_source_start_can_send_before_any_window_or_board_exists(self):
        # The original GSheep.Start branches into the manager's *mutating*
        # CheckNeedSendStartGameMsgOrOnlyCheck(true). Its native manager method
        # sends LWSheepStartGame when the server has no resumable game, and
        # success comes later through UpdateGameInfo + ToEnter.
        start = self.method("DataCenter/SmallGameManager/GSheep.luac", "Start")
        manager = self.method("DataCenter/LWSheep/LWSheepDataManager.luac",
                              "CheckNeedSendStartGameMsgOrOnlyCheck")
        reply = self.method("Net/Msgs/LWSheep/LWSheepStartGameMessage.luac",
                            "HandleMessage")
        self.assertTrue({"CheckNeedSendStartGameMsgOrOnlyCheck", "Enter"} <=
                        {str(c) for c in start["constants"]})
        self.assertTrue({"LWSheepStartGame", "SendMessage", "CheckHasGame"} <=
                        {str(c) for c in manager["constants"]})
        self.assertTrue({"UpdateGameInfo", "ToEnter", "errorCode"} <=
                        {str(c) for c in reply["constants"]})
        sends = []
        context = {"sheepData": None}
        network = {"SFSNetwork": {"SendMessage": lambda *args: sends.append(args)},
                   "MsgDefines": {"LWSheepStartGame": 912}}
        self.assertEqual(vm.execute(manager, context, False, globals_=network), (True,))
        self.assertEqual(sends, [], "Read-only native start eligibility must never send")
        self.assertEqual(vm.execute(manager, context, True, globals_=network), (True,))
        self.assertEqual(len(sends), 1,
                         "The same native source path sends when Start is requested")

    def test_native_can_click_requires_visible_and_tray_room(self):
        method = self.method("DataCenter/LWSheep/LWSheep.luac", "CanClick")
        game = {"visualList": defaultdict(lambda: None, {11: "present"}),
                "removeList": {}}
        globals_ = {"table": {"count": lambda table: len(table)}}
        self.assertEqual(vm.execute(method, game, 11, globals_=globals_,
                                    upvalues={1: 7}), (True,))
        self.assertEqual(vm.execute(method, game, 12, globals_=globals_,
                                    upvalues={1: 7}), (False,))
        game["removeList"] = {i: i for i in range(1, 8)}
        self.assertEqual(vm.execute(method, game, 11, globals_=globals_,
                                    upvalues={1: 7}), (False,))

    def test_card_has_precise_original_server_coordinates(self):
        method = self.method("DataCenter/LWSheep/LWSheepCard.luac", "GetPosToServer")
        globals_ = {"string": {"format": lambda spec, x, y, z: spec.replace('%d', '{}').format(x, y, z)}}
        self.assertEqual(vm.execute(method, {"pos": {"x": 3, "y": 9, "z": 2}},
                                    globals_=globals_), ("3_9_2",))

    def test_ui_card_routes_local_engine_then_view_sync(self):
        method = self.method("UI/LWSeason4/LWUISheepGame/Component/LWUISheepCardItem.luac",
                             "OnClickCard")
        callbacks = []
        board_card = object()
        parent = {
            "refresh": False,
            "isMoveItemAnimating": False,
            "ISCanOp": lambda self: True,
            "engine": {"ClickCard": lambda self, card: (
                callbacks.append(("native_click", card)) or True, 2, {}, 3, False)},
            "OnClickCard": lambda self, position, visual, item, temporary: callbacks.append(
                ("queue_sync", position, visual, item, temporary)),
        }
        button = {"SetInteractable": lambda self, flag: callbacks.append(("interactable", flag))}
        component = {"parent": parent, "card": board_card, "btn_card": button}
        globals_ = {"SmallGameManager": {"GSheep": {"IsEnd": lambda self: False}}}
        self.assertEqual(vm.execute(method, component, globals_=globals_), ())
        self.assertEqual(callbacks[0], ("native_click", board_card))
        self.assertEqual(callbacks[1], ("interactable", False))
        self.assertEqual(callbacks[2], ("queue_sync", {}, 3, component, False))

    def test_ui_card_refuses_closed_game_or_unsafe_animation(self):
        method = self.method("UI/LWSeason4/LWUISheepGame/Component/LWUISheepCardItem.luac",
                             "OnClickCard")
        callbacks = []
        parent = {"refresh": False, "isMoveItemAnimating": True,
                  "ISCanOp": lambda _: False,
                  "engine": {"ClickCard": lambda *args: callbacks.append("click")}}
        component = {"parent": parent}
        terminal = {"SmallGameManager": {"GSheep": {"IsEnd": lambda _: True}},
                    "UIUtil": {"ShowTipsId": lambda *_: None}}
        self.assertEqual(vm.execute(method, component, globals_=terminal), ())
        self.assertFalse(callbacks)
        terminal["SmallGameManager"]["GSheep"]["IsEnd"] = lambda _: False
        self.assertEqual(vm.execute(method, component, globals_=terminal), ())
        self.assertFalse(callbacks)

    def test_server_reconciliation_and_no_item_action_contract(self):
        module = "UI/LWSeason4/LWUISheepGame/View/LWUISheepGameView.luac"
        op = self.method(module, "SeasonSheepOpHandle")
        update = self.method(module, "Update")
        sync_server = self.method(module, "SyncServer")
        sync_finish = self.method(module, "SyncFinish")
        constants = lambda p: {str(value) for value in p["constants"]}
        self.assertTrue({"OpCheck", "SyncFinish", "opPointId", "Error"} <= constants(op))
        self.assertTrue({"SyncServer", "IsSyncFinish", "ReBuild", "GetPosToServer"} <= constants(update))
        self.assertTrue({"isSend", "serverPos", "Move", "GSheep"} <= constants(sync_server))
        self.assertTrue({"syncOps", "serverPos", "remove"} <= constants(sync_finish))
        game = self.method("DataCenter/LWSheep/LWSheep.luac", "CheckGameState")
        self.assertTrue({"removeList", "Fair", "Success", "rewardsInfo"} <= constants(game))

    def test_invalid_native_click_reply_does_not_run_sync_finish(self):
        # Real v26 SeasonSheepOpHandle returns early when OpCheck rejects the
        # reply. A matching opPointId alone must not acknowledge our move.
        handler = self.method("UI/LWSeason4/LWUISheepGame/View/LWUISheepGameView.luac",
                              "SeasonSheepOpHandle")
        calls = []
        view = {"engine": {"OpCheck": lambda *_: None},
                "GeSyncCheckData": lambda *_: None,
                "SyncFinish": lambda *_: calls.append("sync_finished")}
        enum = {"Error": 1, "Refresh": 2, "Click": 3}
        self.assertEqual(vm.execute(handler, view, {"type": 3, "opPointId": "3_9_2"},
                                    globals_={"SheepGameOpType": enum}), ())
        self.assertEqual(calls, [])

    def test_source_sync_finish_removes_only_confirmed_point(self):
        sync = self.method("UI/LWSeason4/LWUISheepGame/View/LWUISheepGameView.luac",
                           "SyncFinish")
        # Interpreter does not support TFORCALL/TFORLOOP/LEN, so inspect the
        # exact admitted opcodes for the source-native matching/removal path.
        instructions = list(disassemble(sync))
        self.assertTrue(any("sourceLine=859" in s and "='serverPos'" in s for s in instructions))
        self.assertTrue(any("sourceLine=864" in s and "='remove'" in s for s in instructions))
        self.assertTrue(any("sourceLine=864" in s and " CALL " in s for s in instructions))
        self.assertTrue(any("sourceLine=863" in s and " EQ " in s for s in instructions))

    def test_reconciliation_and_temporary_cards_guards_match_original_source(self):
        temporary = self.method("DataCenter/LWSheep/LWSheep.luac", "BuildTemporaryList")
        self.assertIn("temporaryList", {str(value) for value in temporary["constants"]})
        source = (ROOT / "feature018_sheep_solver.lua").read_text(encoding="utf-8")
        self.assertIn("dictionary_size(temporary, MAX_CARDS) ~= 0", source)
        self.assertIn("queued_before == 1 and queued_after == 0", source)
        self.assertIn("get(op, 'isSend') ~= true", source)

    def test_bounded_plan_only_through_source_native_card_ui(self):
        source = (ROOT / "feature018_sheep_solver.lua").read_text(encoding="utf-8")
        from luaparser import ast
        ast.parse(source)
        self.assertIn("MAX_SEARCH_NODES, SEARCH_SLICE", source)
        self.assertIn("if run.plan == nil", source)
        self.assertIn("run.pending = {point = next_card.point", source)
        self.assertIn("if not pending.ack then return false end", source)
        self.assertIn("M.CleanupAfterLeaseLoss", source)
        self.assertIn("feature018_sheep_automation_status", source)
        self.assertIn("coroutine.yield('searching'", source)
        self.assertIn("get(packet, 'opPointId') == pending.point", source)
        self.assertIn("if not approved_native_owner(run.account) then", source)
        self.assertIn("run.pending ~= nil and run.pending.ack", source)
        self.assertNotIn("SFSNetwork", source.replace("-- Source", "-- Source"))
        for forbidden in (r"call\([^\n]*'UseItem'", r"call\([^\n]*'NextLevel'",
                          r"call\([^\n]*'Again'", r"SendMessage\("):
            self.assertIsNone(re.search(forbidden, source))


class NativeSeasonFourOwnerLua53Checks(unittest.TestCase):
    @staticmethod
    def fixture():
        from lupa.lua53 import LuaRuntime
        lua = LuaRuntime(unpack_returned_tuples=True)
        lua.execute(r'''
        Account={source=4455,selfServer=4455,current=4455}
        LuaEntry={Player={uid='7812345600004455',
          GetSourceServerId=function() return Account.source end,
          GetSelfServerId=function() return Account.selfServer end,
          GetCurServerId=function() return Account.current end}}
        MiniGameSource={Season=4}
        GameOpens=0
        WindowOpen=false
        SmallGameManager={GSheep={source=4,
          Start=function() GameOpens=GameOpens+1 end}}
        DataCenter={LWSheepDataManager={sheepData={blockId=42},
          IsVail=function() return true end,
          IsEnd=function() return false end,
          GetCurrentLevel=function() return 3 end,
          CanChallenge=function() return true end}}
        UIWindowNames={UILUISheepCloud='FoodHouse'}
        UIManager={GetInstance=function() return
          {IsWindowOpen=function(self, name) return name=='FoodHouse' and WindowOpen end} end}
        ''')
        solver = lua.execute((ROOT / 'feature018_sheep_solver.lua').read_text(
            encoding='utf-8'))
        return lua, solver

    @staticmethod
    def request(lua):
        return lua.table_from({'taskName': 'sheepGame',
                               'authorizedUid': lua.globals().LuaEntry.Player.uid})

    def test_authorized_native_season_start(self):
        lua, solver = self.fixture()
        result = solver.Start(self.request(lua))
        self.assertEqual(result['state'], 'accepted')
        self.assertEqual(lua.globals().GameOpens, 1)

    def test_any_consistent_logged_in_uid_server_may_start(self):
        lua, solver = self.fixture()
        lua.execute("""LuaEntry.Player.uid='7812345600005566'
                       Account.source=5566
                       Account.selfServer=5566
                       Account.current=5566""")
        self.assertEqual(solver.Start(self.request(lua))['state'], 'accepted')
        self.assertEqual(lua.globals().GameOpens, 1)

    def test_missing_or_foreign_request_uid_never_opens_season_game(self):
        for invalid in (None, '7812345600005566', 'not-a-uid', 7812345600004455):
            lua, solver = self.fixture()
            payload = {'taskName': 'sheepGame'}
            if invalid is not None:
                payload['authorizedUid'] = invalid
            self.assertEqual(solver.Start(lua.table_from(payload))['state'], 'failed')
            self.assertEqual(lua.globals().GameOpens, 0)

    def test_stop_and_lease_loss_cannot_duplicate_pending_native_start(self):
        lua, solver = self.fixture()
        request = self.request(lua)
        self.assertEqual(solver.Start(request)['state'], 'accepted')
        self.assertTrue(solver.Stop(request)['data']['restartFenced'])
        self.assertTrue(solver.Stop(request)['data']['awaitingServer'])
        self.assertEqual(solver.Start(request)['state'], 'failed')
        self.assertEqual(lua.globals().GameOpens, 1)
        solver.CleanupAfterLeaseLoss()
        self.assertEqual(solver.Status()['data']['status'], 'idle')
        self.assertTrue(solver.Status()['data']['restartFenced'])
        lua.globals().WindowOpen = True  # A window alone has no board proof.
        self.assertEqual(solver.Start(request)['state'], 'failed')
        self.assertEqual(lua.globals().GameOpens, 1)

    def test_open_timeout_and_exception_do_not_retry_uncertain_start(self):
        for failing_start in (False, True):
            lua, solver = self.fixture()
            if failing_start:
                lua.execute('SmallGameManager.GSheep.Start=function() '
                            'GameOpens=GameOpens+1; error("sent then failed") end')
            request = self.request(lua)
            state = solver.Start(request)['state']
            self.assertEqual(state, 'failed' if failing_start else 'accepted')
            if not failing_start:
                lua.execute('local now=os.time(); os.time=function() return now+30 end')
                solver.Pump()
                self.assertEqual(solver.Status()['data']['status'], 'solve_failed')
            self.assertTrue(solver.Status()['data']['restartFenced'])
            self.assertEqual(solver.Start(request)['state'], 'failed')
            self.assertEqual(lua.globals().GameOpens, 1)

    def test_unauthenticated_uid_or_inconsistent_server_never_opens_season_game(self):
        for change in ("LuaEntry.Player.uid='invalid'",
                       'Account.source=5566', 'Account.selfServer=5566',
                       'Account.current=5566'):
            lua, solver = self.fixture()
            lua.execute(change)
            self.assertEqual(solver.Start(self.request(lua))['state'],
                             'unsupported')
            self.assertEqual(lua.globals().GameOpens, 0)

    def test_change_after_challenge_before_game_open_does_not_send(self):
        for switch in ("Account.current=5566",
                       "LuaEntry.Player.uid='7812345600005566'"):
            lua, solver = self.fixture()
            lua.execute('DataCenter.LWSheepDataManager.CanChallenge=function() '
                        + switch + '; return true end')
            self.assertEqual(solver.Start(self.request(lua))['state'],
                             'failed')
            self.assertEqual(lua.globals().GameOpens, 0)

    def test_pending_start_cannot_be_inherited_after_server_switch(self):
        lua, solver = self.fixture()
        old_request = self.request(lua)
        self.assertEqual(solver.Start(old_request)['state'], 'accepted')
        lua.execute('Account.source=5566; Account.selfServer=5566; Account.current=5566')
        self.assertFalse(solver.Pump())
        status = solver.Status()['data']
        self.assertEqual(status['status'], 'solve_failed')
        self.assertIn('account changed', status['error'])
        self.assertTrue(status['restartFenced'])
        self.assertEqual(solver.Start(self.request(lua))['state'], 'failed')
        self.assertEqual(lua.globals().GameOpens, 1)
        solver.CleanupAfterLeaseLoss()
        self.assertEqual(solver.Start(self.request(lua))['state'], 'failed')
        self.assertEqual(lua.globals().GameOpens, 1)

    def three_card_board(self, auto_reply):
        # Inert source-shaped current level, UI tile, sent syncOps queue,
        # matching response and SyncFinish. Delayed replies are delivered by
        # test code only; neither fixture nor solver contacts a real server.
        lua, solver = self.fixture()
        lua.execute(r'''
        SheepGameState={Doing=1,Success=2}
        SheepGameOpType={Click=3,Error=4}
        local cls={}
        for _,name in ipairs({'Update','OnDestroy','OnClickCard',
          'OnUseItem','NextLevel','Again'}) do
          cls[name]=function() end
        end
        ClickCount=0
        AutoReply=false
        ReplySwitch=false
        Game={gameState=1,showCards={{}},visualList={},mapCards={},
          removeList={},temporaryList={}}
        function Game:CanClick(id) return self.visualList[id]~=nil end
        local points={}
        View=setmetatable({engine={game=Game,ClickCard=function() end},
          syncOps={},cardViews={},lock=false,isMoveItemAnimating=false,
          ISCanOp=function() return true end}, {__index=cls})
        for i=1,3 do
          local id=i*11
          local x=i*10
          local point=tostring(x)..'_0_1'
          local card={gridId=id,pos={x=x,y=0,z=1},pictureId=7,
            occGridList={1,2,3,4},
            GetPosToServer=function() return point end}
          Game.showCards[1][id]=card
          Game.visualList[id]=card
          Game.mapCards[id]=card
          points[point]=id
          View.cardViews[id]={card=card,OnClickCard=function()
            ClickCount=ClickCount+1
            table.insert(View.syncOps,{serverPos=point,isSend=true})
            if AutoReply then View:SeasonSheepOpHandle({type=3,opPointId=point}) end
          end}
        end
        function cls.SeasonSheepOpHandle(self,packet)
          if packet.type~=3 or points[packet.opPointId]==nil then return end
          if self ~= View then
            -- A separate source-native window can independently carry the
            -- same point ID. Its own SyncFinish removes only its own queue.
            assert(#self.syncOps==1)
            assert(self.syncOps[1].serverPos==packet.opPointId)
            table.remove(self.syncOps,1)
            return
          end
          local id=points[packet.opPointId]
          assert(id and #self.syncOps==1)
          assert(self.syncOps[1].serverPos==packet.opPointId)
          table.remove(self.syncOps,1)
          Game.mapCards[id]=nil
          Game.visualList[id]=nil
          local empty=true
          for _ in pairs(Game.mapCards) do empty=false end
          if empty then Game.gameState=2;Game.rewardsInfo={1} end
          if ReplySwitch then
            Account.source=5566
            Account.selfServer=5566
            Account.current=5566
          end
        end
        function AckFirst()
          local queued=View.syncOps[1]
          assert(queued)
          View:SeasonSheepOpHandle({type=3,opPointId=queued.serverPos})
        end
        package.loaded['UI.LWSeason4.LWUISheepGame.View.LWUISheepGameView']=cls
        ''')
        lua.globals().AutoReply = auto_reply
        self.assertEqual(solver.Start(self.request(lua))['state'],
                         'accepted')
        lua.globals().WindowOpen = True
        self.assertTrue(solver.BindView(lua.globals().View))
        return lua, solver

    def test_closed_current_native_window_refuses_card_click(self):
        lua, solver = self.three_card_board(auto_reply=False)
        # Capture and plan against the real open UI, then close it before the
        # first irreversible card action. The new opening gate correctly waits
        # on a closed window instead of accepting an old bound board object.
        for _ in range(2):
            solver.Pump()
        lua.globals().WindowOpen = False
        for _ in range(12):
            solver.Pump()
            if solver.Status()['data']['terminal']:
                break
        status = solver.Status()['data']
        self.assertEqual(status['status'], 'solve_failed')
        self.assertIn('window closed', status['error'])
        self.assertEqual(lua.globals().ClickCount, 0)

    def test_server_uid_or_native_player_switch_before_first_click_stops_run(self):
        for change in (
            'Account.current=5566',
            'Account.source=5566;Account.selfServer=5566;Account.current=5566',
            "LuaEntry.Player.uid='7812345600005566'",
            'LuaEntry.Player=setmetatable({}, {__index=LuaEntry.Player})',
        ):
            lua, solver = self.three_card_board(auto_reply=False)
            for _ in range(2):
                solver.Pump()  # Capture board, then generate plan.
            lua.execute(change)
            self.assertFalse(solver.Pump())
            status = solver.Status()['data']
            self.assertEqual(status['status'], 'solve_failed')
            self.assertIn('account changed', status['error'])
            self.assertEqual(status['confirmedMoves'], 0)
            self.assertEqual(lua.globals().ClickCount, 0)

    def test_server_switch_inside_native_ui_click_fences_queued_send(self):
        lua, solver = self.three_card_board(auto_reply=False)
        lua.execute(r'''
          for _,item in pairs(View.cardViews) do
            local original=item.OnClickCard
            item.OnClickCard=function(...)
              original(...)
              Account.source=5566
              Account.selfServer=5566
              Account.current=5566
            end
          end
        ''')
        for _ in range(8):
            solver.Pump()
            if solver.Status()['data']['terminal']:
                break
        status = solver.Status()['data']
        self.assertEqual(status['status'], 'solve_failed')
        self.assertIn('account changed', status['error'])
        self.assertEqual(lua.globals().ClickCount, 1)
        self.assertEqual(len(lua.globals().View.syncOps), 1)
        self.assertTrue(status['restartFenced'])
        self.assertEqual(solver.Start(self.request(lua))['state'], 'failed')

    def test_ready_native_board_releases_start_without_resending_and_stale_closed_view_reopens(self):
        lua, solver = self.three_card_board(auto_reply=False)
        request = self.request(lua)
        self.assertEqual(lua.globals().GameOpens, 1)
        self.assertTrue(solver.Stop(request)['data']['restartFenced'])
        self.assertEqual(solver.Start(request)['state'], 'accepted',
                         'The exact current native board can resume without Start')
        self.assertEqual(lua.globals().GameOpens, 1)
        solver.Pump()  # Native current board has become the source of truth.
        self.assertFalse(solver.Status()['data']['restartFenced'])
        solver.Stop(request)
        lua.globals().WindowOpen = False
        self.assertEqual(solver.Start(request)['state'], 'accepted',
                         'A bound stale view must not suppress original UI Start')
        self.assertEqual(lua.globals().GameOpens, 2)
        solver.Stop(request)
        self.assertEqual(solver.Start(request)['state'], 'failed',
                         'The new pending native Start cannot be repeated')
        self.assertEqual(lua.globals().GameOpens, 2)

    def test_lost_native_response_watcher_refuses_any_tile_click(self):
        lua, solver = self.three_card_board(auto_reply=False)
        lua.execute('''local cls=package.loaded['UI.LWSeason4.LWUISheepGame.View.LWUISheepGameView']
                       cls.SeasonSheepOpHandle = function() end''')
        for _ in range(12):
            solver.Pump()
            if solver.Status()['data']['terminal']:
                break
        status = solver.Status()['data']
        self.assertEqual(status['status'], 'solve_failed')
        self.assertIn('callback observer', status['error'])
        self.assertEqual(lua.globals().ClickCount, 0)

    def test_synchronous_exact_native_click_ack_drains_queue_without_false_failure(self):
        # Original native callback can synchronously drain the exactly sent
        # operation while OnClickCard is on the stack. Never resend that click.
        lua, solver = self.three_card_board(auto_reply=True)
        for _ in range(18):
            solver.Pump()
            if solver.Status()['data']['status'] == 'completed':
                break
        final = solver.Status()['data']
        self.assertEqual(final['status'], 'completed')
        self.assertEqual(final['confirmedMoves'], 3)
        self.assertEqual(lua.globals().ClickCount, 3)
        self.assertFalse(final['awaitingServer'])

    def test_delayed_original_click_replies_confirm_three_moves_once_each(self):
        lua, solver = self.three_card_board(auto_reply=False)
        acknowledgements = 0
        for _ in range(40):
            solver.Pump()
            if len(lua.globals().View.syncOps) == 1:
                # A Pump while queued cannot issue a second native tile click.
                before = lua.globals().ClickCount
                solver.Pump()
                self.assertEqual(lua.globals().ClickCount, before)
                lua.globals().AckFirst()
                acknowledgements += 1
            if solver.Status()['data']['status'] == 'completed':
                break
        status = solver.Status()['data']
        self.assertEqual(status['status'], 'completed')
        self.assertEqual(status['confirmedMoves'], 3)
        self.assertEqual(acknowledgements, 3)
        self.assertEqual(lua.globals().ClickCount, 3)

    def test_stop_fences_uncertain_click_until_exact_old_ack(self):
        lua, solver = self.three_card_board(auto_reply=False)
        for _ in range(8):
            solver.Pump()
            if len(lua.globals().View.syncOps) == 1:
                break
        self.assertEqual(len(lua.globals().View.syncOps), 1)
        self.assertEqual(lua.globals().ClickCount, 1)
        stopped = solver.Stop(self.request(lua))['data']
        self.assertEqual(stopped['status'], 'stopped')
        self.assertTrue(stopped['restartFenced'])
        self.assertTrue(stopped['awaitingServer'])
        attempt = solver.Start(self.request(lua))
        self.assertEqual(attempt['state'], 'failed')
        lua.execute("View:SeasonSheepOpHandle({type=3,opPointId='999_0_1'})")
        lua.execute("View:SeasonSheepOpHandle({type=4,opPointId='10_0_1'})")
        self.assertEqual(solver.Start(self.request(lua))['state'],
                         'failed', 'wrong point and error event never retire old request')
        lua.globals().AckFirst()
        self.assertFalse(solver.Status()['data']['restartFenced'])
        self.assertEqual(solver.Start(self.request(lua))['state'],
                         'accepted', 'matching original SyncFinish may retire old fence')
        self.assertEqual(lua.globals().ClickCount, 1)

    def test_server_switch_before_late_ack_cannot_retire_old_click(self):
        lua, solver = self.three_card_board(auto_reply=False)
        for _ in range(8):
            solver.Pump()
            if len(lua.globals().View.syncOps) == 1:
                break
        self.assertEqual(lua.globals().ClickCount, 1)
        lua.execute('Account.source=5566;Account.selfServer=5566;Account.current=5566')
        lua.globals().AckFirst()  # Actual original callback runs before next Pump.
        status = solver.Status()['data']
        self.assertEqual(status['status'], 'solve_failed')
        self.assertIn('account changed', status['error'])
        self.assertEqual(status['confirmedMoves'], 0)
        self.assertTrue(status['restartFenced'])
        self.assertEqual(lua.globals().ClickCount, 1)
        self.assertEqual(solver.Start(self.request(lua))['state'], 'failed')

    def test_server_switch_inside_original_reply_prevents_ack_promotion(self):
        lua, solver = self.three_card_board(auto_reply=False)
        for _ in range(8):
            solver.Pump()
            if len(lua.globals().View.syncOps) == 1:
                break
        self.assertEqual(lua.globals().ClickCount, 1)
        lua.globals().ReplySwitch = True
        lua.globals().AckFirst()
        status = solver.Status()['data']
        self.assertEqual(status['status'], 'solve_failed')
        self.assertIn('account changed', status['error'])
        self.assertEqual(status['confirmedMoves'], 0)
        self.assertTrue(status['restartFenced'])

    def test_stopped_old_account_click_ack_on_new_server_cannot_release_fence(self):
        lua, solver = self.three_card_board(auto_reply=False)
        for _ in range(8):
            solver.Pump()
            if len(lua.globals().View.syncOps) == 1:
                break
        self.assertEqual(lua.globals().ClickCount, 1)
        self.assertTrue(solver.Stop(self.request(lua))['data']['restartFenced'])
        lua.execute('Account.source=5566;Account.selfServer=5566;Account.current=5566')
        lua.globals().AckFirst()
        self.assertTrue(solver.Status()['data']['restartFenced'])
        self.assertEqual(solver.Start(self.request(lua))['state'], 'failed')
        self.assertEqual(lua.globals().ClickCount, 1)

    def test_account_switch_before_final_reply_and_destroy_cannot_complete(self):
        lua, solver = self.three_card_board(auto_reply=False)
        for _ in range(32):
            solver.Pump()
            if len(lua.globals().View.syncOps) == 1:
                if lua.globals().ClickCount < 3:
                    lua.globals().AckFirst()
                else:
                    break
        self.assertEqual(lua.globals().ClickCount, 3)
        self.assertEqual(len(lua.globals().View.syncOps), 1)
        lua.execute('Account.source=5566;Account.selfServer=5566;Account.current=5566')
        lua.globals().AckFirst()  # Native view receives its final Success/rewards.
        lua.execute('View:OnDestroy()')
        status = solver.Status()['data']
        self.assertEqual(status['status'], 'solve_failed')
        self.assertTrue(status['restartFenced'])
        self.assertEqual(status['confirmedMoves'], 2)
        self.assertNotEqual(status['status'], 'completed')

    def test_new_native_view_same_point_cannot_ack_old_in_flight_click(self):
        lua, solver = self.three_card_board(auto_reply=False)
        for _ in range(8):
            solver.Pump()
            if len(lua.globals().View.syncOps) == 1:
                break
        self.assertEqual(lua.globals().ClickCount, 1)
        lua.execute(r'''
          local cls=getmetatable(View).__index
          OtherGame={gameState=1,CanClick=function() return true end}
          Other=setmetatable({
            engine={game=OtherGame,ClickCard=function() end},
            syncOps={{serverPos='10_0_1',isSend=true}},
            ISCanOp=function() return true end
          }, {__index=cls})
          Other:Update()
        ''')
        status = solver.Status()['data']
        self.assertEqual(status['status'], 'solve_failed',
                         'A new bound view must terminate the old run')
        self.assertTrue(status['restartFenced'])
        self.assertTrue(status['awaitingServer'])
        self.assertEqual(len(lua.globals().View.syncOps), 1)
        lua.execute("Other:SeasonSheepOpHandle({type=3,opPointId='10_0_1'})")
        self.assertEqual(len(lua.globals().Other.syncOps), 0)
        self.assertEqual(len(lua.globals().View.syncOps), 1)
        self.assertTrue(solver.Status()['data']['restartFenced'],
                        'A different view draining an identical point is not our ACK')
        self.assertEqual(solver.Start(self.request(lua))['state'],
                         'failed')
        self.assertEqual(lua.globals().ClickCount, 1)
        lua.globals().AckFirst()
        self.assertFalse(solver.Status()['data']['restartFenced'],
                         'The original view receiving its own exact ACK releases fence')

    def test_stop_then_rebind_retains_source_view_for_delayed_ack(self):
        lua, solver = self.three_card_board(auto_reply=False)
        for _ in range(8):
            solver.Pump()
            if len(lua.globals().View.syncOps) == 1:
                break
        self.assertEqual(lua.globals().ClickCount, 1)
        self.assertTrue(solver.Stop(self.request(lua))
                        ['data']['restartFenced'])
        lua.execute(r'''
          local cls=getmetatable(View).__index
          Another=setmetatable({engine={game=Game,ClickCard=function() end},
            syncOps={{serverPos='10_0_1',isSend=true}},
            ISCanOp=function() return true end}, {__index=cls})
          Another:Update()
          Another:SeasonSheepOpHandle({type=3,opPointId='10_0_1'})
        ''')
        self.assertTrue(solver.Status()['data']['restartFenced'])
        lua.globals().AckFirst()
        self.assertFalse(solver.Status()['data']['restartFenced'])

    def test_native_engine_replacement_cannot_promote_same_point_reply(self):
        lua, solver = self.three_card_board(auto_reply=False)
        for _ in range(8):
            solver.Pump()
            if len(lua.globals().View.syncOps) == 1:
                break
        self.assertEqual(lua.globals().ClickCount, 1)
        lua.execute(r'''
          OtherGame={gameState=1,CanClick=function() return true end}
          View.engine.game=OtherGame
          -- Same view and source point, but different native game instance.
          View:SeasonSheepOpHandle({type=3,opPointId='10_0_1'})
        ''')
        self.assertFalse(solver.Status()['data']['restartFenced'])
        solver.Pump()
        result = solver.Status()['data']
        self.assertEqual(result['status'], 'solve_failed')
        self.assertTrue(result['restartFenced'],
                        'Unrelated view-engine state must never clear in-flight fence')
        self.assertEqual(result['confirmedMoves'], 0)
        self.assertEqual(lua.globals().ClickCount, 1)
        self.assertEqual(solver.Start(self.request(lua))['state'],
                         'failed')

    def test_original_view_destroy_after_new_view_binding_preserves_click_fence(self):
        lua, solver = self.three_card_board(auto_reply=False)
        for _ in range(8):
            solver.Pump()
            if len(lua.globals().View.syncOps) == 1:
                break
        self.assertEqual(lua.globals().ClickCount, 1)
        lua.execute(r'''
          local cls=getmetatable(View).__index
          Another=setmetatable({engine={game=Game,ClickCard=function() end},
            syncOps={},ISCanOp=function() return true end}, {__index=cls})
        ''')
        self.assertTrue(solver.BindView(lua.globals().Another))
        lua.execute('View:OnDestroy()')
        status = solver.Status()['data']
        self.assertEqual(status['status'], 'solve_failed')
        self.assertTrue(status['restartFenced'])
        self.assertTrue(status['awaitingServer'])
        self.assertEqual(len(lua.globals().View.syncOps), 1)
        lua.globals().AckFirst()
        self.assertFalse(solver.Status()['data']['restartFenced'])

    def test_lease_loss_preserves_uncertain_request_even_if_hook_unloaded(self):
        lua, solver = self.three_card_board(auto_reply=False)
        for _ in range(8):
            solver.Pump()
            if len(lua.globals().View.syncOps) == 1:
                break
        self.assertEqual(len(lua.globals().View.syncOps), 1)
        solver.CleanupAfterLeaseLoss()
        self.assertTrue(solver.Status()['data']['restartFenced'])
        self.assertTrue(solver.Status()['data']['awaitingServer'])
        self.assertEqual(solver.Start(self.request(lua))['state'],
                         'failed')
        self.assertEqual(lua.globals().ClickCount, 1)


if __name__ == "__main__":
    unittest.main()
