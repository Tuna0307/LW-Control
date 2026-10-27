-- Recipe (NOT executed by this analysis): send one world.get.block for an arbitrary
-- AOI index list and tile rectangle, mirroring decoded WorldPointManager.SendAoiRequest
-- -> BaseMessage.Send -> WorldGetBlockMessage.CSSetData -> NetworkManager.Send.
-- Source: decoded IL in il_WorldPointManager_aoi.txt / il_WorldGetBlockMessage.txt.

local function tileIndex(tx, ty)            -- TileCoord.TilePosToIndex, 1000x1000 world
    tx = math.max(0, math.min(999, tx)); ty = math.max(0, math.min(999, ty))
    return tx + ty * 1000 + 1
end

local function int32Array(t)                -- System.Int32[] for PutIntArray / Request.index
    local arr = CS.System.Array.CreateInstance(typeof(CS.System.Int32), #t)
    for i = 1, #t do arr[i - 1] = t[i] end
    return arr
end

-- aoiIndexes : Lua list of AOI block indexes (by * (1000/blockSize) + bx), <=160 per request
-- blockSize  : WorldPointManager._lwAoiBlockSize (= lwAoiBlockSizeArray[clamp(svLod,0,2)])
-- lod        : svLod, clamped to <= 2 (sent as "viewLvl")
-- cx, cy     : centre tile (camera target / 2), clamped 0..999
-- lbx,lby,rtx,rty : rectangle in tiles (native: LBblock*blockSize, (RTblock+1)*blockSize, clamped 0..999)
local function sendWorldGetBlock(aoiIndexes, blockSize, lod, cx, cy, lbx, lby, rtx, rty, opts)
    opts = opts or {}
    local player = CS.GameEntry.Data.Player
    local serverId = player:IsInSelfServer() and player:GetSelfServerId() or player:GetCrossServerId()
    if opts.serverId then serverId = opts.serverId end           -- remote-server target (see notes)

    -- Variant A: through the game's own message object (identical bytes, incl. _id + timeStamp).
    -- WorldGetBlockMessage.Request is a public nested class with a public .ctor.
    local req = CS.WorldGetBlockMessage.Request()
    req.bigMap = 0
    req.x, req.y = cx, cy
    req.type = 0
    req.lod = math.min(lod, 2)
    req.serverId = serverId
    req.worldId = player:GetWorldId()
    req.index = int32Array(aoiIndexes)
    req.blockSize = blockSize
    req.firstTime = opts.firstTime or false         -- true adds "clearUuidSet"=1
    req.leftBottom = tileIndex(lbx, lby)
    req.rightTop = tileIndex(rtx, rty)
    req.battleFieldFirst = opts.force or false      -- true adds "force"=true
    -- BaseMessage.Send is PUBLIC and declared `params object[] args`: pass the Request
    -- itself as the single vararg.  (CSSetData is PROTECTED: not callable from xLua.)
    CS.WorldGetBlockMessage.Instance:Send(req)
end

-- Variant B: raw SFS (no dependency on the nested Request type).
local function sendWorldGetBlockRaw(aoiIndexes, blockSize, lod, cx, cy, lbx, lby, rtx, rty, serverId, worldId)
    local net = CS.GameEntry.Network
    local o = CS.Sfs2X.Entities.Data.SFSObject.NewInstance()
    local fm = net:getFutureManager()
    local id = fm:getFutureId()
    o:PutInt("_id", id)
    fm:onSendRequest(id, "world.get.block")          -- latency bookkeeping only
    o:PutInt("bigMap", 0)
    o:PutInt("x", cx); o:PutInt("y", cy)
    o:PutInt("serverId", serverId)
    o:PutInt("worldId", worldId)
    o:PutInt("type", 0)
    o:PutInt("viewLvl", math.min(lod, 2))
    local ms = CS.GameEntry.Timer:GetServerTime() & 0xFFFFFFFF   -- (int)GetServerTime(): ms, truncated
    if ms >= 0x80000000 then ms = ms - 0x100000000 end
    o:PutInt("timeStamp", ms)
    o:PutIntArray("index", int32Array(aoiIndexes))
    o:PutInt("blockSize", blockSize)
    o:PutInt("leftBottom", tileIndex(lbx, lby))
    o:PutInt("rightTop", tileIndex(rtx, rty))
    net:Send(CS.Sfs2X.Requests.ExtensionRequest("world.get.block", o))
end

return { send = sendWorldGetBlock, sendRaw = sendWorldGetBlockRaw, tileIndex = tileIndex }
