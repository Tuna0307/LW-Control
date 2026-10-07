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
    @{ name = 'positive_after_plunder'; expiry = 1789623200000L; includeExpiry = $true },
    @{ name = 'zero'; expiry = 0L; includeExpiry = $true },
    @{ name = 'negative'; expiry = -1L; includeExpiry = $true },
    @{ name = 'missing'; expiry = 0L; includeExpiry = $false },
    @{ name = 'positive_equal_plunder'; expiry = 1789616300000L; includeExpiry = $true },
    @{ name = 'positive_before_plunder'; expiry = 1789616299999L; includeExpiry = $true }
)
$results = foreach ($case in $cases) {
    $fixture = [ordered]@{
        serverId = 91; taskKind = 'ghost'; uuid = '123'; ownerServer = 33
        completionTime = 1789616000000L; protectTime = 300; plunderAt = 1789616300000L
        stealListCount = 1; stealMaxTimes = 3; stolenCount = 1; maxStealCount = 3
    }
    if ($case.includeExpiry) { $fixture['taskExpireTime'] = $case.expiry }
    $json = $fixture | ConvertTo-Json -Compress
    $document = [System.Text.Json.JsonDocument]::Parse($json)
    try {
        $rows = [System.Collections.Generic.List[System.Text.Json.JsonElement]]::new()
        $rows.Add($document.RootElement.Clone())
        $arguments = [object[]]::new(1)
        $arguments[0] = $rows
        $outcomes = [ordered]@{ name = $case.name }
        foreach ($pair in @(@{ key='host'; method=$mapMethod }, @{ key='helper'; method=$desktopMethod })) {
            try {
                $null = $pair.method.Invoke($null, $arguments)
                $outcomes[$pair.key] = 'ACCEPTED'
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
    scope = 'Actual packaged static production methods; no provider constructed, no game/transport invoked'
    desktopAssemblySha256 = (Get-FileHash -LiteralPath $desktopPath -Algorithm SHA256).Hash
    mapAssemblySha256 = (Get-FileHash -LiteralPath $mapPath -Algorithm SHA256).Hash
    cases = @($results)
}
$resultPath = Join-Path $PSScriptRoot 'expiry-results.json'
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resultPath -Encoding utf8
$results | ConvertTo-Json -Depth 4
