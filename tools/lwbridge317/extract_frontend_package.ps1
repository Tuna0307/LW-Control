[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ReferencePath,

    [Parameter(Mandatory = $true)]
    [string]$OutputRoot
)

$ErrorActionPreference = 'Stop'

$ExpectedSha256 = '4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783'
$AssetTableOffset = [int64]0x8BA550
$AssetRecordSize = [int64]32
$AssetRecordCount = 24

# For this exact PE, every frontend path/blob referenced by the asset table lives
# in .rdata.  A .rdata virtual address maps to its file offset by subtracting
# this constant (ImageBase + section RVA - section raw offset).
$RdataVaToFileOffsetDelta = [int64]0x140001200

$RsrcRva = [int64]0xF02000
$RsrcRawOffset = [int64]0xEFCA00

$PeResources = @(
    [pscustomobject]@{ Type = 'RT_ICON'; Id = 1; Rva = [int64]0xF02430; Size = 734; FileName = 'rt-icon-1.bin' },
    [pscustomobject]@{ Type = 'RT_ICON'; Id = 2; Rva = [int64]0xF02710; Size = 1255; FileName = 'rt-icon-2.bin' },
    [pscustomobject]@{ Type = 'RT_ICON'; Id = 3; Rva = [int64]0xF02BF8; Size = 1943; FileName = 'rt-icon-3.bin' },
    [pscustomobject]@{ Type = 'RT_ICON'; Id = 4; Rva = [int64]0xF03390; Size = 3450; FileName = 'rt-icon-4.bin' },
    [pscustomobject]@{ Type = 'RT_ICON'; Id = 5; Rva = [int64]0xF04110; Size = 5490; FileName = 'rt-icon-5.bin' },
    [pscustomobject]@{ Type = 'RT_ICON'; Id = 6; Rva = [int64]0xF05688; Size = 17271; FileName = 'rt-icon-6.bin' },
    [pscustomobject]@{ Type = 'RT_ICON'; Id = 7; Rva = [int64]0xF09A00; Size = 62187; FileName = 'rt-icon-7.bin' },
    [pscustomobject]@{ Type = 'RT_GROUP_ICON'; Id = 32512; Rva = [int64]0xF18CF0; Size = 104; FileName = 'rt-group-icon-32512.bin' },
    [pscustomobject]@{ Type = 'RT_MANIFEST'; Id = 1; Rva = [int64]0xF18D58; Size = 334; FileName = 'rt-manifest-1.xml' },
    [pscustomobject]@{ Type = 'RT_VERSION'; Id = 1; Rva = [int64]0xF02250; Size = 480; FileName = 'rt-version-1.bin' }
)

function Get-ByteSha256 {
    param([byte[]]$Bytes)

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '')
    }
    finally {
        $sha.Dispose()
    }
}

function Get-U64 {
    param(
        [byte[]]$Bytes,
        [int64]$Offset
    )

    return [System.BitConverter]::ToUInt64($Bytes, [int]$Offset)
}

function Get-Slice {
    param(
        [byte[]]$Bytes,
        [int64]$Offset,
        [int64]$Length
    )

    if ($Offset -lt 0 -or $Length -lt 0 -or ($Offset + $Length) -gt $Bytes.LongLength) {
        throw "Byte slice is outside the reference file: offset=$Offset length=$Length fileLength=$($Bytes.LongLength)"
    }

    $result = New-Object byte[] ([int]$Length)
    [System.Array]::Copy($Bytes, $Offset, $result, 0, $Length)
    return $result
}

function Write-Bytes {
    param(
        [string]$Path,
        [byte[]]$Bytes
    )

    $parent = Split-Path -Parent $Path
    if ($parent) {
        [System.IO.Directory]::CreateDirectory($parent) | Out-Null
    }
    [System.IO.File]::WriteAllBytes($Path, $Bytes)
}

function Expand-Brotli {
    param([byte[]]$Bytes)

    $inputStream = [System.IO.MemoryStream]::new($Bytes, $false)
    $brotliStream = [System.IO.Compression.BrotliStream]::new(
        $inputStream,
        [System.IO.Compression.CompressionMode]::Decompress
    )
    $outputStream = [System.IO.MemoryStream]::new()

    try {
        $brotliStream.CopyTo($outputStream)
        return $outputStream.ToArray()
    }
    finally {
        $outputStream.Dispose()
        $brotliStream.Dispose()
        $inputStream.Dispose()
    }
}

$referenceItem = Get-Item -LiteralPath $ReferencePath
$referenceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $referenceItem.FullName).Hash.ToUpperInvariant()
if ($referenceHash -ne $ExpectedSha256) {
    throw "Reference SHA-256 mismatch. Expected $ExpectedSha256, got $referenceHash"
}

if ($PSVersionTable.PSEdition -ne 'Core' -or $PSVersionTable.PSVersion.Major -lt 7) {
    throw 'PowerShell 7+ (pwsh) is required for System.IO.Compression.BrotliStream.'
}

$referenceBytes = [System.IO.File]::ReadAllBytes($referenceItem.FullName)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
[System.IO.Directory]::CreateDirectory($resolvedOutputRoot) | Out-Null

$assetManifest = @()
for ($index = 0; $index -lt $AssetRecordCount; $index++) {
    $recordOffset = $AssetTableOffset + ($index * $AssetRecordSize)
    $pathVa = [int64](Get-U64 -Bytes $referenceBytes -Offset $recordOffset)
    $pathLength = [int64](Get-U64 -Bytes $referenceBytes -Offset ($recordOffset + 8))
    $blobVa = [int64](Get-U64 -Bytes $referenceBytes -Offset ($recordOffset + 16))
    $compressedLength = [int64](Get-U64 -Bytes $referenceBytes -Offset ($recordOffset + 24))

    $pathOffset = $pathVa - $RdataVaToFileOffsetDelta
    $blobOffset = $blobVa - $RdataVaToFileOffsetDelta
    if ($blobOffset -ne ($pathOffset + $pathLength)) {
        throw "Asset record $index does not have a contiguous path/blob layout."
    }

    $pathBytes = Get-Slice -Bytes $referenceBytes -Offset $pathOffset -Length $pathLength
    $assetPath = [System.Text.Encoding]::ASCII.GetString($pathBytes)
    if (-not $assetPath.StartsWith('/')) {
        throw "Asset record $index has an unexpected path: $assetPath"
    }

    $compressedBytes = Get-Slice -Bytes $referenceBytes -Offset $blobOffset -Length $compressedLength
    $decompressedBytes = Expand-Brotli -Bytes $compressedBytes

    $relativePath = $assetPath.TrimStart('/')
    $webPath = Join-Path $resolvedOutputRoot (Join-Path 'web' $relativePath)
    $compressedPath = Join-Path $resolvedOutputRoot (Join-Path 'embedded-brotli' ($relativePath + '.br'))
    Write-Bytes -Path $webPath -Bytes $decompressedBytes
    Write-Bytes -Path $compressedPath -Bytes $compressedBytes

    $assetManifest += [pscustomobject]@{
        record_index = $index
        record_file_offset = ('0x{0:X}' -f $recordOffset)
        path = $assetPath
        path_va = ('0x{0:X}' -f $pathVa)
        path_file_offset = ('0x{0:X}' -f $pathOffset)
        path_length = $pathLength
        blob_va = ('0x{0:X}' -f $blobVa)
        blob_file_offset = ('0x{0:X}' -f $blobOffset)
        embedded_brotli_size = $compressedLength
        embedded_brotli_sha256 = Get-ByteSha256 -Bytes $compressedBytes
        recovered_size = $decompressedBytes.LongLength
        recovered_sha256 = Get-ByteSha256 -Bytes $decompressedBytes
        recovered_path = ('web/' + $relativePath.Replace('\', '/'))
        embedded_path = ('embedded-brotli/' + $relativePath.Replace('\', '/') + '.br')
    }
}

$resourceManifest = @()
$resourceRoot = Join-Path $resolvedOutputRoot 'pe-resources'
foreach ($resource in $PeResources) {
    $fileOffset = $resource.Rva - $RsrcRva + $RsrcRawOffset
    $resourceBytes = Get-Slice -Bytes $referenceBytes -Offset $fileOffset -Length $resource.Size
    $resourcePath = Join-Path $resourceRoot $resource.FileName
    Write-Bytes -Path $resourcePath -Bytes $resourceBytes

    $resourceManifest += [pscustomobject]@{
        type = $resource.Type
        id = $resource.Id
        rva = ('0x{0:X}' -f $resource.Rva)
        file_offset = ('0x{0:X}' -f $fileOffset)
        size = $resource.Size
        sha256 = Get-ByteSha256 -Bytes $resourceBytes
        extracted_path = ('pe-resources/' + $resource.FileName)
    }
}

$manifest = [ordered]@{
    work_item = 'LWB317-UI-001A'
    reference = [ordered]@{
        path = $referenceItem.FullName
        size = $referenceItem.Length
        sha256 = $referenceHash
    }
    extractor = [ordered]@{
        script = 'tools/lwbridge317/extract_frontend_package.ps1'
        powershell = $PSVersionTable.PSVersion.ToString()
        brotli_assembly = [System.IO.Compression.BrotliStream].Assembly.FullName
        asset_table_file_offset = ('0x{0:X}' -f $AssetTableOffset)
        asset_record_size = $AssetRecordSize
        asset_record_count = $AssetRecordCount
        rdata_va_to_file_offset_delta = ('0x{0:X}' -f $RdataVaToFileOffsetDelta)
    }
    assets = $assetManifest
    pe_resources = $resourceManifest
}

$manifestPath = Join-Path $resolvedOutputRoot 'frontend-package-manifest.json'
$manifestJson = $manifest | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText($manifestPath, $manifestJson + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))

Write-Host "Verified reference SHA-256: $referenceHash"
Write-Host "Recovered frontend assets: $($assetManifest.Count)"
Write-Host "Extracted PE resources: $($resourceManifest.Count)"
Write-Host "Manifest: $manifestPath"
