"""Inert multi-city Space data checks. No native input, game or installation."""
WORLD = """
CS = CS or {}
SceneManagerSceneID = {World=2, City=1}
visibleCities = {Count=3,
 [0]={uuid='1001',ownerUid='other-A',playerName='A',pointIndex=11,protectEndTime=130},
 [1]={uuid='1002',ownerUid='other-B',playerName='B',pointIndex=12,protectEndTime=160},
 [2]={uuid='1003',ownerUid='other-C',playerName='C',pointIndex=13,protectEndTime=90}}
CS.SceneManager = {CurrSceneID=2, World={
 PointManager={GetMainByScreen=function() return visibleCities end},
 TileIndexToWorld=function(self,point) return {x=point,y=0,z=0} end,
 Camera={__camera={WorldToViewportPoint=function(self,pos) return {x=pos.x/100,y=0.5,z=10} end}}}}
"""


def main():
    from feature018_hotkey_game_checks import create_runtime, BASE
    lua, module = create_runtime(BASE + WORLD)
    result = module.VisibleCityShieldSnapshot()
    assert result['state'] == 'confirmed'
    data = result['data']
    assert data['readOnly'] and data['observation'] == 'native_visible_city_shields'
    rows = data['cities']
    assert len(rows) == 3
    assert rows[1]['ownerUid'] == 'other-A' and rows[2]['ownerUid'] == 'other-B'
    assert rows[1]['remainingMilliseconds'] == 30000 and rows[2]['remainingMilliseconds'] == 60000
    assert rows[3]['active'] is False and rows[3]['remainingMilliseconds'] == 0
    lua.execute('visibleCities[0].protectEndTime=1300000000000')
    assert module.VisibleCityShieldSnapshot()['data']['cities'][1]['shieldEndTimeMilliseconds'] == 1300000000000
    lua.execute('CS.SceneManager.CurrSceneID=1')
    assert module.VisibleCityShieldSnapshot()['state'] == 'unsupported'
    lua.execute('CS.SceneManager.CurrSceneID=2; visibleCities[0].protectEndTime=nil')
    assert module.VisibleCityShieldSnapshot()['state'] == 'unsupported'
    lua.execute('visibleCities={Count=0}')
    assert len(module.VisibleCityShieldSnapshot()['data']['cities']) == 0
    print('FEATURE018_VISIBLE_CITY_SHIELDS_LUA53_PASS')


if __name__ == '__main__':
    main()
