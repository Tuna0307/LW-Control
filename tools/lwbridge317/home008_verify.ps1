param([ValidateSet('native','static','frontend','package')][string]$Phase='native')
$ErrorActionPreference='Stop'
$repo='C:\Users\chimw\OneDrive\Desktop\Github\LW-Control'
Set-Location $repo
$ev='evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-NATIVE-SEMANTICS-008'
New-Item -ItemType Directory -Force $ev | Out-Null
$log=Join-Path $ev "checks-$Phase.txt"
"008 phase=$Phase startingCheckpoint=3d2fd106eb06110f4993dc74e845521297b1c5b6 commit=$(git rev-parse HEAD) UTC=$((Get-Date).ToUniversalTime().ToString('o'))" | Set-Content $log
$exe='tests/LWBridge.Desktop.Checks/bin/Release/net10.0-windows10.0.17763.0/LWBridge.Desktop.Checks.dll'
function Test([string]$name,[scriptblock]$operation){
 $scratch=Join-Path $env:TEMP ('LWB317-008-'+$name.Replace('/','_')+'.txt')
 try {
   $prev=$ErrorActionPreference;$ErrorActionPreference='Continue'
   try { & $operation *> $scratch } finally { $ErrorActionPreference=$prev }
   $code=$LASTEXITCODE
   $line="$name status=$(if($code -eq 0){'PASS'}else{'FAIL'}) exit=$code"
   Write-Output $line; Add-Content $log $line
   if($code -ne 0){Get-Content $scratch -Tail 38|ForEach-Object{Write-Output $_;Add-Content $log $_};throw "008 $name failed"}
 } finally {Remove-Item -LiteralPath $scratch -Force -ErrorAction SilentlyContinue}
}
switch($Phase){
 native {
  Test 'real-production-reconcile-original-default' {dotnet $exe --home008-original-reconcile-contract "$ev/startup-contract.json"}
  foreach($name in @('home-campaign-lifecycle-check','overview-close-timing-check',
    'overview-reconnect-policy-check','overview-fault-admission-check',
    'overview-official-settle-check','overview-launch-spam-check',
    'profile-runtime-owner-check','map-campaign-canonical-check',
    'map317-native-boundary-check','world-resource-004-inverses')){
    Test $name {dotnet $exe "--$name"}
  }
  Test 'accepted-R1-engine-sink-cancel' {dotnet $exe --campaign007-r1-engine-sink "$ev/accepted-r1-engine-sink.json"}
  Test 'accepted-R1-positive-staging' {dotnet $exe --campaign007-positive-stage-cancel "$ev/accepted-r1-positive-staging.json"}
 }
 static {
  Test 'original-close-semantic-contract' {python tools/lwbridge317/home008_close_contract.py}
  Test 'original-startup-value-flow-contract' {python tools/lwbridge317/home008_start_contract.py}
  Test 'original-launch-frame-uses' {python tools/lwbridge317/home008_frame_refs.py}
  Test 'original-launch-reconcile-xrefs' {python tools/lwbridge317/home008_valueflow.py}
  Test 'original-controller-boundary' {python tests/original_lua_recovery_contract_checks.py}
  Test 'current-client-contract' {python tools/check_current_client_runtime_contract.py}
 }
 frontend {
  Test 'canonical-frontend-check' {npm.cmd --prefix src/LWBridge.UI-0.3.17 run check}
  Test 'canonical-frontend-build' {npm.cmd --prefix src/LWBridge.UI-0.3.17 run build}
  Test 'canonical-production-UI-integrity' {npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:production-build}
 }
 package {
  Test 'desktop-Release' {dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release --no-restore -v:q}
  Test 'checks-Release' {dotnet build tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release --no-restore -v:q}
  $target=Join-Path $env:TEMP 'LWB317-008-PACKAGE'
  try {
   Test 'desktop-Release-publish' {dotnet publish src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release --no-restore -o $target -v:q}
   foreach($asset in @('LWBridge.Desktop.exe','OverviewBridge/LWBridge.GamePipeAdapter.dll','OverviewBridge/current_overview_bridge.lua','ProductionUi/index.html')){
    if(!(Test-Path(Join-Path $target $asset))){throw "missing package $asset"}
    Add-Content $log "asset $asset PASS"
   }
   $actual=(Get-FileHash (Join-Path $target 'OverviewBridge/current_overview_bridge.lua') -Algorithm SHA256).Hash
   $source=(Get-FileHash 'tools/current_overview_bridge.lua' -Algorithm SHA256).Hash
   if($actual -ne $source){throw 'packaged Lua/source digest mismatch'}
   Add-Content $log "packaged lua/source digest $actual PASS"
  } finally {Remove-Item -LiteralPath $target -Force -Recurse -ErrorAction SilentlyContinue}
 }
}
Write-Output "HOME008_$($Phase.ToUpperInvariant())_PASS"
