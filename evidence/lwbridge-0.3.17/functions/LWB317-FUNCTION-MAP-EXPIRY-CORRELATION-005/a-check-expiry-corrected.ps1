param([string]$Repository = 'C:\Users\chimw\OneDrive\Desktop\Github\LW-Control')
$ErrorActionPreference = 'Stop'
$proofBin = Join-Path $Repository 'tests/LWBridge.Desktop.Checks/bin/Release/net10.0-windows10.0.17763.0'
$desktopPath = Join-Path $proofBin 'LWBridge.Desktop.dll'
$mapPath = Join-Path $proofBin 'LWBridge.Map_0_3_17.dll'
$mapAssembly = [Reflection.Assembly]::LoadFrom($mapPath)
$desktopAssembly = [Reflection.Assembly]::LoadFrom($desktopPath)
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$mapMethod = $mapAssembly.GetType('LWBridge.Map317.MapActionControlPlane').GetMethod('ValidateDispatchScheduleRows', $flags)
$desktopMethod = $desktopAssembly.GetType('LWBridge.Desktop.CurrentClientMap317ActionProvider').GetMethod('PrepareGhostPlunderRows', $flags)
if ($null -eq $mapMethod -or $null -eq $desktopMethod) { throw 'Production methods missing' }

$cases = @(
    @{ name='positive_after_plunder'; expiry=1789623200000L; include=$true; raw=$false },
    @{ name='zero'; expiry=0L; include=$true; raw=$false },
    @{ name='negative'; expiry=-1L; include=$true; raw=$false },
    @{ name='missing'; expiry=0L; include=$false; raw=$false },
    @{ name='positive_equal_plunder'; expiry=1789616300000L; include=$true; raw=$false },
    @{ name='positive_before_plunder'; expiry=1789616299999L; include=$true; raw=$false },
    @{ name='numeric_string_after_plunder'; expiry='1789623200000'; include=$true; raw=$true },
    @{ name='malformed_string_reader_rejects_string_kind'; expiry='not-a-number'; include=$true; raw=$true }
)

$results = foreach ($case in $cases) {
    $fixture = [ordered]@{
        serverId = 91; taskKind = 'ghost'; uuid = '123'; ownerServer = 33
        completionTime = 1789616000000L; protectTime = 300; plunderAt = 1789616300000L
        stealListCount = 1; stealMaxTimes = 3; stolenCount = 1; maxStealCount = 3
    }
    if ($case.include) { $fixture['taskExpireTime'] = $case.expiry }
    $json = $fixture | ConvertTo-Json -Compress
    $document = [System.Text.Json.JsonDocument]::Parse($json)
    try {
        $rows = [System.Collections.Generic.List[System.Text.Json.JsonElement]]::new()
        $rows.Add($document.RootElement.Clone())
        $arguments = [object[]]::new(1)
        $arguments[0] = $rows
        $outcomes = [ordered]@{ name = $case.name; inputJson = $json }
        foreach ($pair in @(@{ key='host'; method=$mapMethod }, @{ key='helper'; method=$desktopMethod })) {
            try {
                $result = $pair.method.Invoke($null, $arguments)
                $outcomes[$pair.key] = 'ACCEPTED'
                if ($pair.key -eq 'helper') {
                    $prepared = [System.Collections.IEnumerable]$result
                    $first = @($prepared)[0]
                    $outcomes['helperPreservedRawRow'] = ($first.GetRawText() -eq $document.RootElement.GetRawText())
                }
            } catch {
                $cause = $_.Exception
                while ($null -ne $cause.InnerException) { $cause = $cause.InnerException }
                $outcomes[$pair.key] = 'REJECTED'
                $outcomes[$pair.key + 'Error'] = $cause.GetType().FullName
            }
        }
        [pscustomobject]$outcomes
    } finally { $document.Dispose() }
}
$report = [ordered]@{
    scope = 'Fresh corrected actual packaged static production methods; no provider/store/game/transport invoked'
    historicalFailingEvidence = '../LWB317-FUNCTION-MAP-BLOCKER-BOUNDARIES-004/lead-review-2026-10-07/expiry-results.json'
    desktopAssemblySha256 = (Get-FileHash -LiteralPath $desktopPath -Algorithm SHA256).Hash
    mapAssemblySha256 = (Get-FileHash -LiteralPath $mapPath -Algorithm SHA256).Hash
    cases = @($results)
}
$resultPath = Join-Path $PSScriptRoot 'a-expiry-results-corrected.json'
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resultPath -Encoding utf8
$results | ConvertTo-Json -Depth 6
