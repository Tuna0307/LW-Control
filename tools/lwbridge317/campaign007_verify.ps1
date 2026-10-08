param([ValidateSet('native','frontend','static','package')][string]$Phase='native')
$ErrorActionPreference='Stop'
Set-Location 'C:\Users\chimw\OneDrive\Desktop\Github\LW-Control'
$base='evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007'
$log=Join-Path $base "verification-$Phase.txt"
$exe='tests/LWBridge.Desktop.Checks/bin/Release/net10.0-windows10.0.17763.0/LWBridge.Desktop.Checks.dll'
"phase=$Phase baseline=839f0bcd162eed560a52c17ccf61eeb6a8731af0 head=$(git rev-parse HEAD) utc=$((Get-Date).ToUniversalTime().ToString('o'))" |Set-Content $log
function Run([string]$name,[scriptblock]$command) {
  $before=Get-Date
  $scratch=Join-Path $env:TEMP ("lwb317-007-"+$name.Replace('/','_').Replace(':','_')+".log")
  & $command *> $scratch
  $exit=$LASTEXITCODE
  $seconds=[math]::Round(((Get-Date)-$before).TotalSeconds,1)
  $line="$name status=$(if($exit -eq 0){'PASS'}else{'FAIL'}) exit=$exit seconds=$seconds"
  Write-Output $line
  Add-Content $log $line
  if($exit -ne 0){Get-Content $scratch -Tail 22 |ForEach-Object{Add-Content $log $_;Write-Output $_};throw "verification failed: $name"}
}
$native=@(
 'overview-official-settle-check','overview-launch-spam-check',
 'overview-process-ownership-check','overview-reconnect-policy-check',
 'overview-fault-admission-check','overview-close-timing-check',
 'home-campaign-lifecycle-check','map-auto-scan-campaign-check',
 'map-campaign-canonical-check','map317-restart-check',
 'map317-native-boundary-check','map317-plunder-worker-boundary-check',
 'map317-dto-matrix-check','profile-runtime-owner-check',
 'overview-status-contract-check','production-root-isolation-check',
 'overview-bridge-host-transport-check','overview-bridge-rpc-session-transport-check',
 'world-resource-004-inverses'
)
if($Phase -eq 'native') {
  foreach($check in $native){Run $check {dotnet $exe "--$check"}}
}
elseif($Phase -eq 'static'){
  $py='C:\Users\chimw\.codex\tmp\lwb317-recovery003-lua\Scripts\python.exe'
  foreach($check in @('tests/pipe_heartbeat_world004_checks.py','tests/pipe_delegate_current_checks.py','tests/home_runtime_file_ownership_checks.py','tests/home_runtime_lease_lua_checks.py','tests/map_provider_decoded_body_oracles.py','tests/map_provider_semantics_lua_checks.py')){
    Run $check {& $py $check}
  }
  Run 'validate_original_controller_boundary' {& python tools/lwbridge317/audit_original_lua_controller_boundaries.py --output "$base/reference-original-controller-boundary.json"}
  Run 'validate_current_client_contract' {& python tools/check_current_client_runtime_contract.py}
  Run 'validate_map_provider_semantics' {& python tools/lwbridge317/validate_map_provider_semantics.py}
}
elseif($Phase -eq 'frontend'){
  Run 'canonical-ui-check' {npm.cmd --prefix src/LWBridge.UI-0.3.17 run check}
  Run 'canonical-ui-build' {npm.cmd --prefix src/LWBridge.UI-0.3.17 run build}
  Run 'canonical-ui-production-build-check' {npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:production-build}
  Run 'real-current-mapBackend-adapter' {node src/LWBridge.UI-0.3.17/scripts/check-campaign007-real-resource-frontend.mjs "$base/command-actual-resource-proof.payload.json" "$base/frontend-real-resource-adapter-proof.json"}
  Run 'mounted-canonical-MapDataPage' {node src/LWBridge.UI-0.3.17/scripts/check-campaign007-mounted-real-resource.mjs "$base/command-actual-resource-proof.payload.json" "$base/mounted-real-resource-results.json"}
  Run 'mounted-canonical-App-Home-Map' {node src/LWBridge.UI-0.3.17/scripts/check-campaign007-mounted-real-app.mjs "$base/command-actual-resource-proof.payload.json" "$base/mounted-app-resource-results.json"}
}
elseif($Phase -eq 'package'){
  Run 'desktop-Release-build' {dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release --no-restore -v:q}
  Run 'checks-Release-build' {dotnet build tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release --no-restore -v:q}
  $target=Join-Path $env:TEMP 'LWB317-CAMPAIGN-007-RELEASE'
  Run 'desktop-canonical-Release-publish' {dotnet publish src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release --no-restore -o $target -v:q}
  foreach($name in @('LWBridge.Desktop.exe','OverviewBridge/LWBridge.GamePipeAdapter.dll','OverviewBridge/current_overview_bridge.lua','ProductionUi/index.html')){
    if(!(Test-Path (Join-Path $target $name))){throw "missing packaged artifact $name"}
    $line="package-file $name PASS";Add-Content $log $line;Write-Output $line
  }
  $packLua=(Get-FileHash (Join-Path $target 'OverviewBridge/current_overview_bridge.lua') -Algorithm SHA256).Hash
  $sourceLua=(Get-FileHash 'tools/current_overview_bridge.lua' -Algorithm SHA256).Hash
  if($packLua -ne $sourceLua){throw 'package LUA mismatch'}
  $line="packaged lua SHA256 matches source $packLua"; Add-Content $log $line;Write-Output $line
}
Write-Output "CAMPAIGN007_$($Phase.ToUpperInvariant())_PASS"
