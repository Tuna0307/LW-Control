$ErrorActionPreference='Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$exe = (Resolve-Path (Join-Path $repo 'src\LWBridge.Desktop\bin\Release\net10.0-windows10.0.17763.0\LWBridge.Desktop.exe')).Path
$out = Join-Path $repo 'evidence\lwbridge-0.3.17\functions\LWB317-FUNCTION-HOME-MAP-COMPLETION-010\R1\ui'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$results = @()
foreach($pair in @(
  @{ view='overview'; language='en'; theme='light' },
  @{ view='map-data'; language='en'; theme='light' },
  @{ view='overview'; language='ja'; theme='dark' },
  @{ view='map-data'; language='ja'; theme='dark' }
)){
  $name = "$($pair.view)-$($pair.language)-$($pair.theme)"
  $path = Join-Path $out "$name.png"
  $args = @('--capture', ('"'+$path+'"'), '--view', $pair.view, '--language', $pair.language, '--theme', $pair.theme)
  $p = Start-Process -FilePath $exe -ArgumentList $args -WindowStyle Hidden -PassThru
  if(-not $p.WaitForExit(60000)){Stop-Process -Id $p.Id -ErrorAction SilentlyContinue;throw "Capture timed out: $name"}
  if($p.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $path)){throw "Capture exit or PNG missing: $name exit=$($p.ExitCode)"}
  $details = Get-Content -LiteralPath ([IO.Path]::ChangeExtension($path, '.json')) -Raw | ConvertFrom-Json
  if(@($details.errors | Where-Object { $null -ne $_ }).Count -gt 0){throw "Native errors $name : $($details.errors|ConvertTo-Json -Compress)"}
  $body = [string]$details.text
  if($body.Length -lt 200){throw "Empty canonical UI: $name"}
  if($details.commands | Where-Object { $_ -match '^auth_' }){throw "Auth/unassigned command emitted by $name"}
  if($pair.language -eq 'ja' -and $body -notmatch '[\u3040-\u30ff\u3400-\u9fff]'){throw "JA language not present in rendered body: $name"}
  $info = Get-Item -LiteralPath $path
  $record = @{ name=$name; view=$pair.view; language=$pair.language; theme=$pair.theme;
    pngBytes=$info.Length; output=$info.Name; textChars=$body.Length;
    nativeErrors=0; authCalls=0; realGameLaunches=0 }
  $results += $record
  Write-Output "CANONICAL_UI_CAPTURE $name $($info.Length) bytes textChars=$($body.Length)"
}
$results | ConvertTo-Json -Depth 4 | Out-File -FilePath (Join-Path $out 'capture-results.json') -Encoding utf8
Write-Output "CANONICAL_UI_CAPTURE_DONE $(@($results).Count)/4"
