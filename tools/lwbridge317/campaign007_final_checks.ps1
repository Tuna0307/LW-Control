$ErrorActionPreference='Stop'
Set-Location 'C:\Users\chimw\OneDrive\Desktop\Github\LW-Control'
$base='evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007'
$exe='tests/LWBridge.Desktop.Checks/bin/Release/net10.0-windows10.0.17763.0/LWBridge.Desktop.Checks.dll'
$log=Join-Path $base 'verification-final-affected.txt'
"campaign007 final affected checks baseline=839f0bcd head=$(git rev-parse HEAD)"|Set-Content $log
function Run([string]$name,[scriptblock]$cmd){
 $output=Join-Path $env:TEMP ("LWB317-007-final-"+$name+".txt")
 & $cmd *> $output
 $code=$LASTEXITCODE
 $msg="$name $(if($code -eq 0){'PASS'}else{'FAIL'}) exit=$code"
 Write-Output $msg;Add-Content $log $msg
 if($code -ne 0){Get-Content $output -Tail 25|ForEach-Object{Write-Output $_;Add-Content $log $_};throw "failed: $name"}
}
Run 'staging-readonly-run-inverses' {dotnet $exe --background-witness-002-self-test}
Run 'production-map-store-post-stage-stop' {dotnet $exe --campaign007-positive-stage-cancel "$base/positive-staged-cancel-inert-proof.json"}
Run 'positive-native-command-real-data-and-context' {dotnet $exe --campaign007-command-proof "$base/safe-real-db-snapshot.json" "$base/command-context-positive-proof.json"}
Run 'native-home-campaign-lifecycle' {dotnet $exe --home-campaign-lifecycle-check}
Run 'native-map317-boundary-city-export' {dotnet $exe --map317-native-boundary-check}
Run 'native-map317-plunder-workers' {dotnet $exe --map317-plunder-worker-boundary-check}
Run 'native-profile-runtime-owner-aba' {dotnet $exe --profile-runtime-owner-check}
Run 'producer-unavailable-inverse-mutations' {python tests/campaign007_provider_guard_mutations.py}
Run 'original-controller-boundary-contract' {python tests/original_lua_recovery_contract_checks.py}
Run 'independent-negative-real-game-staging-stop' {python tools/lwbridge317/audit_campaign007_live_stop.py}
Run 'independent-final-preservation' {python tools/lwbridge317/campaign007_preservation.py}
Run 'actual-mounted-app-current-replay' {node src/LWBridge.UI-0.3.17/scripts/check-campaign007-mounted-real-app.mjs "$base/command-actual-resource-proof.payload.json" "$base/mounted-app-resource-results.json"}
Run 'actual-mounted-map-page-current-replay' {node src/LWBridge.UI-0.3.17/scripts/check-campaign007-mounted-real-resource.mjs "$base/command-actual-resource-proof.payload.json" "$base/mounted-real-resource-results.json"}
Write-Output 'CAMPAIGN007_FINAL_AFFECTED_CHECKS_PASS'
