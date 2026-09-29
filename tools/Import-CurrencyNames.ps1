<#
.SYNOPSIS
    Builds src/Calculator.UI/Resources/Currency/CurrencyNames.json: the names of the currencies offered by the
    exchange rate service, in every language of the app.

.DESCRIPTION
    .NET only knows English currency names.
    The names come from the Unicode CLDR (cldr-json, Unicode License v3). For each culture of
    Resources/Strings/Strings.<culture>.resx the most specific CLDR locale is used (zh-Hans is CLDR "zh").
    Only currencies that https://open.er-api.com returns are kept, which keeps the file small.

    The script only reads from the network and writes the JSON file; run it again to refresh the names.
#>
param(
    [string]$AppRoot
)

$ErrorActionPreference = 'Stop'
if (-not $AppRoot) {
    $AppRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'src\Calculator.UI'
}

$cldrBase = 'https://raw.githubusercontent.com/unicode-org/cldr-json/main/cldr-json/cldr-numbers-full/main'
$stringsDir = Join-Path $AppRoot 'Resources\Strings'
$outputDir = Join-Path $AppRoot 'Resources\Currency'
$outputFile = Join-Path $outputDir 'CurrencyNames.json'

# Currency codes offered by the exchange rate service.
$rates = Invoke-RestMethod -Uri 'https://open.er-api.com/v6/latest/USD'
$codes = $rates.rates.PSObject.Properties.Name | Sort-Object

# App cultures: the neutral resources are English.
$cultures = @('en') + (Get-ChildItem $stringsDir -Filter 'Strings.*.resx' | ForEach-Object {
    $_.BaseName.Substring('Strings.'.Length)
})

# CLDR locales to try for a culture, most specific first (zh-Hans is called "zh" in CLDR).
function Get-CldrCandidates([string]$culture) {
    $special = @{ 'zh-Hans' = @('zh') }
    if ($special.ContainsKey($culture)) {
        return $special[$culture]
    }

    $parts = $culture.Split('-')
    return @($culture, $parts[0]) | Select-Object -Unique
}

$names = [ordered]@{}
foreach ($culture in $cultures) {
    $currencies = $null
    foreach ($locale in Get-CldrCandidates $culture) {
        try {
            $data = Invoke-RestMethod -Uri "$cldrBase/$locale/currencies.json"
            $currencies = $data.main.$locale.numbers.currencies
            break
        }
        catch {
            # This locale does not exist in CLDR; try the next, less specific one.
        }
    }

    if ($null -eq $currencies) {
        Write-Warning "No CLDR currency names for $culture"
        continue
    }

    $cultureNames = [ordered]@{}
    foreach ($code in $codes) {
        $entry = $currencies.$code
        if ($null -ne $entry -and $entry.displayName) {
            $cultureNames[$code] = $entry.displayName
        }
    }

    $names[$culture] = $cultureNames
    Write-Host "$culture`: $($cultureNames.Count) names"
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$json = $names | ConvertTo-Json -Depth 3 -Compress
[System.IO.File]::WriteAllText($outputFile, $json, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Written $outputFile"
