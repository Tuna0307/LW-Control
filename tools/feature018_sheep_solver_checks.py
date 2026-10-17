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
        if (v1, v2) != (3, 26) or not original or \
           hashlib.sha256(original).hexdigest() != ORIGINAL_ENTRY_SHA256:
            raise unittest.SkipTest("Admitted original v26 Lua entry unavailable")

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
        self.assertIn("if not approved_native_owner() then", source)
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
        Account={source=2289,selfServer=2289,current=2289}
        LuaEntry={Player={uid='3133546747002289',
          GetSourceServerId=function() return Account.source end,
          GetSelfServerId=function() return Account.selfServer end,
          GetCurServerId=function() return Account.current end}}
        MiniGameSource={Season=4}
        GameOpens=0
        SmallGameManager={GSheep={source=4,
          Start=function() GameOpens=GameOpens+1 end}}
        DataCenter={LWSheepDataManager={sheepData={blockId=42},
          IsVail=function() return true end,
          IsEnd=function() return false end,
          GetCurrentLevel=function() return 3 end,
          CanChallenge=function() return true end}}
        UIWindowNames={UILUISheepCloud='FoodHouse'}
        UIManager={GetInstance=function() return
          {IsWindowOpen=function(self, name) return false end} end}
        ''')
        solver = lua.execute((ROOT / 'feature018_sheep_solver.lua').read_text(
            encoding='utf-8'))
        return lua, solver

    def test_authorized_native_season_start(self):
        lua, solver = self.fixture()
        result = solver.Start(lua.table_from({'taskName': 'sheepGame'}))
        self.assertEqual(result['state'], 'accepted')
        self.assertEqual(lua.globals().GameOpens, 1)

    def test_main_account_or_other_server_never_opens_season_game(self):
        for change in ("LuaEntry.Player.uid='3133546747002212'",
                       'Account.source=2212', 'Account.selfServer=2212',
                       'Account.current=2212'):
            lua, solver = self.fixture()
            lua.execute(change)
            self.assertEqual(solver.Start(lua.table_from({'taskName': 'sheepGame'}))['state'],
                             'unsupported')
            self.assertEqual(lua.globals().GameOpens, 0)

    def test_change_after_challenge_before_game_open_does_not_send(self):
        lua, solver = self.fixture()
        lua.execute('DataCenter.LWSheepDataManager.CanChallenge=function() '
                    'Account.current=2212; return true end')
        self.assertEqual(solver.Start(lua.table_from({'taskName': 'sheepGame'}))['state'],
                         'failed')
        self.assertEqual(lua.globals().GameOpens, 0)

    def test_synchronous_exact_native_click_ack_drains_queue_without_false_failure(self):
        # Simulated source UI ordering: OnClickCard enqueues a sent operation,
        # its matching reply invokes SeasonSheepOpHandle immediately, and
        # SyncFinish removes the operation before the click method returns.
        # This can occur on an immediate local callback; it must never resend.
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
            View:SeasonSheepOpHandle({type=3,opPointId=point})
          end}
        end
        function cls.SeasonSheepOpHandle(self,packet)
          local id=points[packet.opPointId]
          assert(id and #self.syncOps==1)
          assert(self.syncOps[1].serverPos==packet.opPointId)
          table.remove(self.syncOps,1)
          Game.mapCards[id]=nil
          Game.visualList[id]=nil
          local empty=true
          for _ in pairs(Game.mapCards) do empty=false end
          if empty then Game.gameState=2;Game.rewardsInfo={1} end
        end
        package.loaded['UI.LWSeason4.LWUISheepGame.View.LWUISheepGameView']=cls
        ''')
        self.assertEqual(solver.Start(lua.table_from({'taskName': 'sheepGame'}))['state'],
                         'accepted')
        self.assertTrue(solver.BindView(lua.globals().View))
        for _ in range(18):
            solver.Pump()
            if solver.Status()['data']['status'] == 'completed':
                break
        final = solver.Status()['data']
        self.assertEqual(final['status'], 'completed')
        self.assertEqual(final['confirmedMoves'], 3)
        self.assertEqual(lua.globals().ClickCount, 3)
        self.assertFalse(final['awaitingServer'])


if __name__ == "__main__":
    unittest.main()
