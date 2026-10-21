-- R30 current v26 adapter. Native routes are recovered from admitted 3/26 bytecode.
-- Original 0.3.17 engines are UNKNOWN; policy/confirmation is OWN_DESIGN.
local M={}
local function get(o,k)local ok,v=pcall(function()return o and o[k]end);if ok then return v end end
local function call(o,k,...)local f=get(o,k);if type(f)~='function'then error('Native '..k..' is unavailable')end;return f(o,...)end
local function center(k)return get(rawget(_G,'DataCenter'),k)end
local function route(k)return get(rawget(_G,'MsgDefines'),k)end
local function help()
    local list=call(center('AllianceHelpDataManager'),'GetOtherHelpList')
    if type(list)~='table'then error('Alliance Help list is not ready')end
    local ids={};for k,v in pairs(list)do
        ids[#ids+1]=tostring(get(v,'uuid')or get(v,'id')or k)
    end
    table.sort(ids);return {available=#ids,identity=table.concat(ids,',')}
end
function M.Supports(kind)return kind=='allianceHelp'end
function M.Interval(kind,c)return 60000 end
function M.Plan(kind,c,at)
    if kind=='allianceHelp'then
        local before=help()
        if before.available==0 then return nil,before end
        -- UIAllianceHelpCtrl.OnHelpAllClick 126-129: server milliseconds, UI effect position.
        -- The optional effect position is omitted for background operation.
        return {route=route('AlHelpAll'),args={math.floor(at)},key=before.identity},before
    end
    return {error='This native Automation route is unavailable'}
end
function M.Verify(kind,job)
    if kind=='allianceHelp'then
        local after=help()
        return after.available==0 and job.before.available>0,after
    end
    return false
end
return M
