"""Inert original-schema shield provisioning and no-resend checks."""
import feature018_hotkey_shield_checks as base


def fixture():
    lua, module, state = base.run()
    lua.execute('''
    LuaEntry={Player={uid='3133546747002289',gold=1000,
      GetSourceServerId=function() return 2289 end,
      GetSelfServerId=function() return 2289 end,
      GetCurServerId=function() return 2289 end}}
    state.items={}
    state.templates[1].price=500
    MsgDefines.UIShopBuy='UIShopBuy'
    SFSNetwork.SendMessage=function(name,data)
      assert(name=='UIShopBuy' and data.itemId=='9' and data.num==1)
      state.sent=state.sent+1
    end
    ''')
    def call(mode='inspect', **extra):
        return module.ShieldShop(lua.table_from(dict(
            slot='8',mode=mode,profileId='profile',launchSessionId='launch',
            challenge='challenge',gamePid='7020',**extra)))
    return lua, state, call


def main():
    lua, state, call = fixture()
    assert call()['data']['nativePrice'] == 500 and state.sent == 0
    assert call('buy')['state'] == 'failed' and state.sent == 0
    lua.globals().LuaEntry.Player.uid='3133546747002212'
    assert call('buy',gemOptIn='1')['state'] == 'failed' and state.sent == 0
    lua.globals().LuaEntry.Player.uid='3133546747002289'
    assert call('buy',gemOptIn='1')['state'] == 'accepted' and state.sent == 1
    assert call('buy',gemOptIn='1')['state'] == 'failed' and state.sent == 1
    assert call('status')['state'] == 'accepted' and state.sent == 1
    lua.execute("state.items={{itemId=9,uuid=123,count=1}}")
    assert call('status')['state'] == 'accepted', 'Inventory alone does not prove price paid'
    lua.globals().LuaEntry.Player.gold=500
    result=call('status')
    assert result['state']=='confirmed' and result['data']['spentGems']==500 and state.sent==1
    assert call('buy',gemOptIn='1')['state']=='failed' and state.sent==1
    print('FEATURE018_SHIELD_SHOP_LUA53_OFFLINE_PASS')


if __name__=='__main__': main()
