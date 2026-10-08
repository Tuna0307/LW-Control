param([ValidateSet('native','static','frontend','package')][string]$Phase='native')
$ErrorActionPreference='Stop'
$root='C:\Users\chimw\OneDrive\Desktop\Github\LW-Control'
Set-Location $root
$base='evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007'
$r1=Join-Path $base 'r1-2026-10-08'
New-Item -ItemType Directory -Force $r1 | Out-Null
$log=Join-Path $r1 "checks-$Phase.txt"
"R1 phase=$Phase checkpoint=$(git rev-parse HEAD) utc=$((Get-Date).ToUniversalTime().ToString('o'))"|Set-Content $log
$exe='tests/LWBridge.Desktop.Checks/bin/Release/net10.0-windows10.0.17763.0/LWBridge.Desktop.Checks.dll'
function Run([string]$name,[scriptblock]$command) {
 $scratch=Join-Path $env:TEMP ('LWB317-R1-'+$name.Replace('/','_').Replace(':','_')+'.log')
 try {
   $priorPreference=$ErrorActionPreference
   $ErrorActionPreference="Continue"
   try { & $command *> $scratch } finally { $ErrorActionPreference=$priorPreference }
   $code=$LASTEXITCODE
   $line="$name status=$(if($code -eq 0){'PASS'}else{'FAIL'}) exit=$code"
   Write-Output $line
   Add-Content $log $line
   if($code -ne 0) {Get-Content $scratch -Tail 32|ForEach-Object{Write-Output $_;Add-Content $log $_};throw "R1 verification failed: $name"}
 } finally {Remove-Item -LiteralPath $scratch -Force -ErrorAction SilentlyContinue}
}
switch($Phase){
 native {
  foreach($name in @(
    'overview-official-settle-check','overview-launch-spam-check','overview-process-ownership-check',
    'overview-reconnect-policy-check','overview-fault-admission-check','overview-close-timing-check',
    'home-campaign-lifecycle-check','map-auto-scan-campaign-check','map-campaign-canonical-check',
    'map317-restart-check','map317-native-boundary-check','map317-plunder-worker-boundary-check',
    'map317-dto-matrix-check','profile-runtime-owner-check','overview-status-contract-check',
    'production-root-isolation-check','overview-bridge-host-transport-check',
    'overview-bridge-rpc-session-transport-check','world-resource-004-inverses'
  )) {Run $name {dotnet $exe "--$name"}}
  Run 'r1-actual-engine-sink-stop' {dotnet $exe --campaign007-r1-engine-sink "$r1/engine-sink-stop-proof.json"}
  Run '007-inert-positive-staged-stop' {dotnet $exe --campaign007-positive-stage-cancel "$r1/inert-stop-regression.json"}
 }
 static {
  $py='C:\Users\chimw\.codex\tmp\lwb317-recovery003-lua\Scripts\python.exe'
  foreach($name in @('tests/pipe_heartbeat_world004_checks.py','tests/pipe_delegate_current_checks.py',
    'tests/home_runtime_file_ownership_checks.py','tests/home_runtime_lease_lua_checks.py',
    'tests/map_provider_decoded_body_oracles.py','tests/map_provider_semantics_lua_checks.py')){
    Run $name {& $py $name}
  }
  Run 'original-controller-boundary' {python tools/lwbridge317/audit_original_lua_controller_boundaries.py --output "$r1/original-controller-boundary.json"}
  Run 'current-client-contract' {python tools/check_current_client_runtime_contract.py}
  Run 'provider-semantics' {python tools/lwbridge317/validate_map_provider_semantics.py}
  Run 'ghost-treasure-structural-mutations' {python tests/campaign007_provider_guard_mutations.py}
  Run 'R1-native-home-trace' {python tools/lwbridge317/campaign007_r1_home_trace.py}
  Run 'R1-native-home-focus' {python tools/lwbridge317/campaign007_r1_home_focus.py}
 }
 frontend {
  Run 'canonical-UI-check' {npm.cmd --prefix src/LWBridge.UI-0.3.17 run check}
  Run 'canonical-UI-build' {npm.cmd --prefix src/LWBridge.UI-0.3.17 run build}
  Run 'production-UI-check' {npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:production-build}
  Run 'R1-mounted-App-ABA-deferred' {
   node src/LWBridge.UI-0.3.17/scripts/check-campaign007-r1-mounted-app-profiles.mjs "$base/command-actual-resource-proof.payload.json" "$r1/mounted-app-profiles.json"
  }
  Run 'historical-mounted-App-repeat-R1-output' {
   node src/LWBridge.UI-0.3.17/scripts/check-campaign007-mounted-real-app.mjs "$base/command-actual-resource-proof.payload.json" "$r1/mounted-app-replay.json"
  }
  Run 'historical-mounted-Map-repeat-R1-output' {
   node src/LWBridge.UI-0.3.17/scripts/check-campaign007-mounted-real-resource.mjs "$base/command-actual-resource-proof.payload.json" "$r1/mounted-map-replay.json"
  }
 }
 package {
  Run 'desktop-Release-build' {dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release --no-restore -v:q}
  Run 'checks-Release-build' {dotnet build tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release --no-restore -v:q}
  $target=Join-Path $env:TEMP 'LWB317-CAMPAIGN-007-R1-RELEASE'
  try {
   Run 'desktop-Release-publish' {dotnet publish src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release --no-restore -o $target -v:q}
   foreach($name in @('LWBridge.Desktop.exe','OverviewBridge/LWBridge.GamePipeAdapter.dll','OverviewBridge/current_overview_bridge.lua','ProductionUi/index.html')){
    if(!(Test-Path(Join-Path $target $name))) {throw "missing R1 publish asset: $name"}
    Add-Content $log "package-file $name PASS"
   }
   $pack=(Get-FileHash(Join-Path $target 'OverviewBridge/current_overview_bridge.lua') -Algorithm SHA256).Hash
   $source=(Get-FileHash 'tools/current_overview_bridge.lua' -Algorithm SHA256).Hash
   if($pack -ne $source){throw 'source/package Lua mismatch'}
   Add-Content $log "Lua package/source SHA256 $source PASS"
  } finally {Remove-Item $target -Recurse -Force -ErrorAction SilentlyContinue}
 }
}
Write-Output "CAMPAIGN007_R1_$($Phase.ToUpperInvariant())_PASS"
