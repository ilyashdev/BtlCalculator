<#
.SYNOPSIS
    Packs the Windows builds of the app into an MSIX bundle for the Microsoft Store.

.DESCRIPTION
    Takes the Native AOT builds (dotnet publish src/Calculator.Desktop -c Release -r win-x64 / win-arm64), adds the
    package manifest and pictures of src/Calculator.Desktop/Package, makes one .msix per architecture with makeappx
    (Windows SDK) and bundles them into BtlCalculator-<version>.msixbundle, the file to upload in Partner Center.

    The package is not signed: the Store signs it. The identity values are on the app's page in Partner Center
    (Product management > Product identity).

.EXAMPLE
    ./tools/New-MsixPackage.ps1 -Build 'x64=publish/win-x64','arm64=publish/win-arm64' -Version 0.2.0 `
        -IdentityName 12345Name.BTLCalculator -Publisher 'CN=...' -PublisherDisplayName 'Name' -OutputFolder dist
#>
param(
    # Builds as "<architecture>=<folder>", architecture x64 or arm64.
    [Parameter(Mandatory)] [string[]]$Build,
    # The app version (0.2.0); a pre-release suffix (-beta.1) is dropped: the Store only takes numbers.
    [Parameter(Mandatory)] [string]$Version,
    [Parameter(Mandatory)] [string]$IdentityName,
    [Parameter(Mandatory)] [string]$Publisher,
    [Parameter(Mandatory)] [string]$PublisherDisplayName,
    [Parameter(Mandatory)] [string]$OutputFolder
)

$ErrorActionPreference = 'Stop'
$packageSource = Join-Path (Split-Path -Parent $PSScriptRoot) 'src\Calculator.Desktop\Package'

# The Store wants four numbers with the last one 0.
$numbers = @(($Version -split '[-+]')[0] -split '\.')
while ($numbers.Count -lt 3) { $numbers += '0' }
$packageVersion = (($numbers | Select-Object -First 3) + '0') -join '.'

$makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.*\x64\makeappx.exe" |
    Sort-Object { [version]$_.Directory.Parent.Name } | Select-Object -Last 1
if (-not $makeappx) {
    throw 'makeappx.exe not found: install the Windows SDK.'
}

$work = Join-Path ([IO.Path]::GetTempPath()) "BtlCalculator-msix-$([guid]::NewGuid().ToString('N'))"
$packages = Join-Path $work 'packages'
New-Item -ItemType Directory -Force $packages | Out-Null
New-Item -ItemType Directory -Force $OutputFolder | Out-Null

# Values go into XML attributes.
function Escape([string]$text) { [Security.SecurityElement]::Escape($text) }

try {
    foreach ($entry in $Build) {
        $architecture, $folder = $entry -split '=', 2
        if ($architecture -notin 'x64', 'arm64') {
            throw "Unknown architecture '$architecture' (x64 or arm64)."
        }

        $stage = Join-Path $work $architecture
        New-Item -ItemType Directory -Force $stage | Out-Null
        Copy-Item -Recurse (Join-Path $folder '*') $stage -Exclude '*.pdb', '*.dbg'
        Copy-Item -Recurse (Join-Path $packageSource 'Assets') $stage

        $manifest = Get-Content -Raw (Join-Path $packageSource 'AppxManifest.xml')
        $manifest = $manifest.Replace('$IdentityName$', (Escape $IdentityName)).
            Replace('$Publisher$', (Escape $Publisher)).
            Replace('$PublisherDisplayName$', (Escape $PublisherDisplayName)).
            Replace('$Version$', $packageVersion).
            Replace('$Architecture$', $architecture)
        Set-Content -Encoding utf8 (Join-Path $stage 'AppxManifest.xml') $manifest

        & $makeappx.FullName pack /o /d $stage /p (Join-Path $packages "BtlCalculator-$architecture.msix")
        if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed for $architecture." }
    }

    $bundle = Join-Path $OutputFolder "BtlCalculator-$packageVersion.msixbundle"
    & $makeappx.FullName bundle /o /d $packages /bv $packageVersion /p $bundle
    if ($LASTEXITCODE -ne 0) { throw 'makeappx bundle failed.' }
    Write-Output $bundle
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
