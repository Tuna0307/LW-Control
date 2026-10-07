$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../../../..')).Path
$adapterPath = Join-Path $repoRoot 'tests/LWBridge.Desktop.Checks/bin/Release/net10.0-windows10.0.17763.0/OverviewBridge/LWBridge.GamePipeAdapter.dll'
$assembly = [System.Reflection.Assembly]::LoadFrom($adapterPath)
$adapterType = $assembly.GetType('LWBridge.GamePipe.PipeClientAdapter', $true)
$reader = $adapterType.GetField('ReadRuntimeSnapshot').GetValue($null)
if ($reader.GetType() -ne [System.Func[string,string]]) { throw 'Reader is not Func<string,string>' }
$isolatedRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('lwb317-lead-reader-' + [Guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($isolatedRoot) | Out-Null
$cases = @(
    @{name='ascii-4096'; text=('a' * 4096); encoding=[System.Text.UTF8Encoding]::new($false,$true); expected='ok'},
    @{name='ascii-4097'; text=('a' * 4097); encoding=[System.Text.UTF8Encoding]::new($false,$true); expected='unavailable'},
    @{name='utf8-over-4096-bytes-under-char-limit'; text=([char]0x00E9).ToString() * 2049; encoding=[System.Text.UTF8Encoding]::new($false,$true); expected='ok'},
    @{name='utf16-bom-autodetection'; text='schema=1'; encoding=[System.Text.UnicodeEncoding]::new($false,$true,$true); expected='ok'}
)
$results = @()
try {
    foreach ($case in $cases) {
        $path = Join-Path $isolatedRoot 'control.txt'
        [System.IO.File]::WriteAllText($path, $case.text, $case.encoding)
        $result = $reader.Invoke($path)
        $prefix = $result.Split("`n")[0]
        if ($prefix -ne $case.expected) { throw "Incorrect result: $($case.name) $prefix" }
        if ($prefix -eq 'ok' -and $result.Substring(3) -ne $case.text) { throw 'Decoded payload mismatch' }
        $results += [pscustomobject]@{case=$case.name; utf16CodeUnits=$case.text.Length; utf8Bytes=[System.Text.Encoding]::UTF8.GetByteCount($case.text); result=$prefix}
    }
    $invalidPath = Join-Path $isolatedRoot 'lease.txt'
    [System.IO.File]::WriteAllBytes($invalidPath, [byte[]]@(0xff))
    if ($reader.Invoke($invalidPath) -ne "unavailable`n") { throw 'Invalid UTF-8 admitted without BOM' }
    $results += [pscustomobject]@{case='invalid-utf8-without-bom'; result='unavailable'}
    $wrongName = Join-Path $isolatedRoot 'other.txt'
    [System.IO.File]::WriteAllText($wrongName, 'schema=1')
    if ($reader.Invoke($wrongName) -ne "unavailable`n") { throw 'Unexpected basename admitted' }
    $results += [pscustomobject]@{case='unapproved-basename'; result='unavailable'}
} finally {
    $absoluteRoot = [System.IO.Path]::GetFullPath($isolatedRoot)
    $expectedParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\')
    if ([System.IO.Path]::GetDirectoryName($absoluteRoot).TrimEnd('\') -ne $expectedParent -or
        -not [System.IO.Path]::GetFileName($absoluteRoot).StartsWith('lwb317-lead-reader-')) { throw 'Unsafe cleanup path' }
    [System.IO.Directory]::Delete($absoluteRoot, $true)
}
[pscustomobject]@{adapterSha256=(Get-FileHash -LiteralPath $adapterPath -Algorithm SHA256).Hash; readerType=$reader.GetType().FullName; cases=$results; isolatedRootRemoved=(-not [System.IO.Directory]::Exists($isolatedRoot)); connectCalls=0} | ConvertTo-Json -Depth 6
