"""Actual Lua restore functions against an explicit Unity-contract inert model.

Distinguishes assigning projectionMatrix (custom mode) from resetting the
automatic projection. Native positive proof remains separate.
"""
from pathlib import Path
import sys, unittest
REPO=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(REPO/'artifacts/map-013-lua-site'))
from lupa.lua53 import LuaRuntime
SOURCE=REPO/'src/LWBridge.GameScripts/current_live_resource_probe.lua'
class ProjectionRestore(unittest.TestCase):
    def test_actual_restore_preserves_future_projection_updates(self):
        text=SOURCE.read_text(encoding='utf8')
        actual=text.split('function M._map014CameraCheckpointMatches',1)[1].split('local function restore_bulk_aoi_state',1)[0]
        lua=LuaRuntime()
        model=r'''
local M={}
local function safe_get(o,k) return o and o[k] end
local function reflected_set_value(o,k,v) o[k]=v; return true end
local function call(o,k,...) if type(o[k])~="function" then return false end return pcall(o[k],o,...) end
CS={UnityEngine={Matrix4x4={Perspective=function(fov,aspect,near,far)
  local cot=1/math.tan(fov*math.pi/360)
  return {m00=cot/aspect,m11=cot,m22=-(far+near)/(far-near),m23=-2*far*near/(far-near),m32=-1}
end}}}
local custom=nil
local camera={fieldOfView=10,aspect=1.9,nearClipPlane=1,farClipPlane=800,orthographic=false,orthographicSize=12.5}
setmetatable(camera,{__index=function(o,k) if k=="projectionMatrix" then
 return custom or CS.UnityEngine.Matrix4x4.Perspective(o.fieldOfView,o.aspect,o.nearClipPlane,o.farClipPlane) end end,
 __newindex=function(o,k,v) if k=="projectionMatrix" then custom=v else rawset(o,k,v) end end})
function camera:ResetProjectionMatrix() custom=nil end
local checkpoint={fov=10,aspect=1.9,nearClipPlane=1,farClipPlane=800,orthographic=false,orthographicSize=12.5,projection=camera.projectionMatrix}
'''
        checks=r'''
assert(M._map014StandardCameraProjection(checkpoint))
camera.fieldOfView=120; camera.aspect=4; camera.farClipPlane=7000
assert(M._map014RestoreCameraProjection(camera,{checkpoint=checkpoint,projection=checkpoint.projection,standardProjection=true}))
assert(custom==nil and camera.farClipPlane==800)
assert(M._map014CameraCheckpointMatches(checkpoint.projection,camera.projectionMatrix))
camera.fieldOfView=120
assert(camera.projectionMatrix.m11<1,"later FOV stuck at restored narrow projection")
local special={m00=99}
assert(M._map014RestoreCameraProjection(camera,{checkpoint=checkpoint,projection=special,standardProjection=false}))
assert(camera.projectionMatrix.m00==99,"genuine custom projection was discarded")
camera.ResetProjectionMatrix=nil
assert(not M._map014RestoreCameraProjection(camera,{checkpoint=checkpoint,projection=checkpoint.projection,standardProjection=true}))
return true
'''
        self.assertTrue(lua.execute(model+'function M._map014CameraCheckpointMatches'+actual+checks))
    def test_whole_production_chunk_compiles(self):
        lua=LuaRuntime()
        lua.execute('assert(load(...))',SOURCE.read_text(encoding='utf8'))
if __name__=='__main__': unittest.main()
