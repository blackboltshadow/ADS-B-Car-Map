<#
.SYNOPSIS
Downloads the newest OurAirports data and writes the compact airport and runway files bundled with the application.

.DESCRIPTION
Source: https://ourairports.com/data/ (public domain). The script downloads airports.csv and runways.csv, keeps large, medium and small airports and heliports (seaplane bases and closed airports are dropped) and the runways of those airports that have coordinates at both ends, and writes tab separated files with only the columns the application needs:

airports.tsv: icao, iata, type (L large, M medium, S small, H heliport), name, municipality, country, latitude, longitude, elevation in feet, scheduled service (0/1), military (0/1, guessed from the name).
runways.tsv: icao, length in feet, width in feet, surface, lighted (0/1), low end ident, low end latitude, low end longitude, high end ident, high end latitude, high end longitude.

Files are rewritten only when their content changed, and airports.meta (counts and SHA-256 of the files) only then, so a run with unchanged data leaves the working tree clean. Release.ps1 runs this script first; when the data changed it asks to commit the new files before building.

.PARAMETER OutputDirectory
Directory for the generated files. Default: Assets\Airports in the project.

.PARAMETER Url
Base address of the OurAirports data files.
#>
param(
    [string]$OutputDirectory,
    [string]$Url = "https://davidmegginson.github.io/ourairports-data"
)

$ErrorActionPreference = "Stop"

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path (Split-Path $PSScriptRoot) "Assets\Airports"
}

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$utf8 = New-Object System.Text.UTF8Encoding($false)
$download = Join-Path ([IO.Path]::GetTempPath()) ("airport-data-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force $download | Out-Null

try {
    foreach ($name in "airports.csv", "runways.csv") {
        Write-Host "Downloading $name ..."
        Invoke-WebRequest -UseBasicParsing -Uri "$Url/$name" -OutFile (Join-Path $download $name)
    }

    $militaryPattern = '(?i)(air force|air base|airbase|\bafb\b|naval air|army air|military|\braf\b|base a[eé]rea|fliegerhorst|luftwaffe|air station)'
    $types = @{ "large_airport" = "L"; "medium_airport" = "M"; "small_airport" = "S"; "heliport" = "H" }

    function Clean([string]$text) {
        if ($null -eq $text) { return "" }
        return ($text -replace "[\t\r\n]+", " ").Trim()
    }

    $airportLines = New-Object System.Collections.Generic.List[string]
    $included = @{}

    foreach ($row in (Import-Csv -Path (Join-Path $download "airports.csv") -Encoding UTF8)) {
        if (-not $types.ContainsKey($row.type)) { continue }
        if (-not $row.latitude_deg -or -not $row.longitude_deg) { continue }

        $icao = if ($row.gps_code) { $row.gps_code } else { $row.ident }
        $military = if ($row.name -match $militaryPattern) { 1 } else { 0 }
        $scheduled = if ($row.scheduled_service -eq "yes") { 1 } else { 0 }

        $fields = @(
            (Clean $icao), (Clean $row.iata_code), $types[$row.type], (Clean $row.name), (Clean $row.municipality), (Clean $row.iso_country),
            $row.latitude_deg, $row.longitude_deg, (Clean $row.elevation_ft), $scheduled, $military
        )

        $airportLines.Add($fields -join "`t")
        $included[$row.ident] = $icao
    }

    $airportLines.Sort([StringComparer]::Ordinal)

    $runwayLines = New-Object System.Collections.Generic.List[string]

    foreach ($row in (Import-Csv -Path (Join-Path $download "runways.csv") -Encoding UTF8)) {
        if (-not $included.ContainsKey($row.airport_ident)) { continue }
        if ($row.closed -eq "1") { continue }
        if (-not $row.le_latitude_deg -or -not $row.le_longitude_deg -or -not $row.he_latitude_deg -or -not $row.he_longitude_deg) { continue }

        $fields = @(
            (Clean $included[$row.airport_ident]), (Clean $row.length_ft), (Clean $row.width_ft), (Clean $row.surface), (Clean $row.lighted),
            (Clean $row.le_ident), $row.le_latitude_deg, $row.le_longitude_deg, (Clean $row.he_ident), $row.he_latitude_deg, $row.he_longitude_deg
        )

        $runwayLines.Add($fields -join "`t")
    }

    $runwayLines.Sort([StringComparer]::Ordinal)

    New-Item -ItemType Directory -Force $OutputDirectory | Out-Null

    $changed = $false

    foreach ($pair in @(@("airports.tsv", $airportLines), @("runways.tsv", $runwayLines))) {
        $path = Join-Path $OutputDirectory $pair[0]
        $text = ($pair[1] -join "`n") + "`n"
        $existing = if (Test-Path $path) { [IO.File]::ReadAllText($path, $utf8) } else { $null }

        if ($existing -ne $text) {
            [IO.File]::WriteAllText($path, $text, $utf8)
            $changed = $true
            Write-Host "Updated $($pair[0]): $($pair[1].Count) rows." -ForegroundColor Green
        }
        else {
            Write-Host "$($pair[0]) unchanged: $($pair[1].Count) rows."
        }
    }

    if ($changed) {
        $airportHash = (Get-FileHash (Join-Path $OutputDirectory "airports.tsv") -Algorithm SHA256).Hash
        $runwayHash = (Get-FileHash (Join-Path $OutputDirectory "runways.tsv") -Algorithm SHA256).Hash
        $meta = "source=OurAirports (public domain)`ndate=$((Get-Date).ToString('yyyy-MM-dd'))`nairports=$($airportLines.Count)`nrunways=$($runwayLines.Count)`nairports.sha256=$airportHash`nrunways.sha256=$runwayHash`n"

        [IO.File]::WriteAllText((Join-Path $OutputDirectory "airports.meta"), $meta, $utf8)
        Write-Host "Airport data changed. Commit the files in $OutputDirectory." -ForegroundColor Yellow
    }
    else {
        Write-Host "Airport data is up to date." -ForegroundColor Green
    }
}
finally {
    Remove-Item -Recurse -Force $download -ErrorAction SilentlyContinue
}
