<#
.SYNOPSIS
Runs all test projects with code coverage and prints the combined line coverage.

.DESCRIPTION
Runs dotnet test with coverage.runsettings for every *.Tests.csproj under Tests and Sources, merges the Cobertura reports by file and line (a line counts as covered when any test project covers it) and prints the total and the least covered files. The Sources code is tested by its own projects, so a single report is not enough: AdsbCarMap.Tests alone shows only part of the coverage.

Results are written to the coverage-results directory in the project (ignored by git) and recreated on every run.

.PARAMETER Lowest
Number of the least covered files to list. Default: 15.
#>
param(
    [int]$Lowest = 15
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$results = Join-Path $root "coverage-results"
$settings = Join-Path $root "coverage.runsettings"

if (Test-Path $results) {
    Remove-Item -Recurse -Force $results
}

$projects = @(Get-ChildItem -Path (Join-Path $root "Tests"), (Join-Path $root "Sources") -Recurse -Filter "*.Tests.csproj" | Where-Object { $_.FullName -notmatch "\\(obj|bin)\\" })

foreach ($project in $projects) {
    Write-Host "Testing $($project.Name)"
    & dotnet test $project.FullName --collect:"XPlat Code Coverage" --settings $settings --results-directory (Join-Path $results $project.BaseName) --nologo -v q
    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed in $($project.Name)."
    }
}

$rootMarker = ($root.Substring(2) -replace "\\", "/").TrimStart("/").ToLowerInvariant() + "/"
$hits = New-Object "System.Collections.Generic.Dictionary[string,System.Collections.Generic.Dictionary[int,int]]"

foreach ($report in Get-ChildItem -Path $results -Recurse -Filter "coverage.cobertura.xml") {
    $xml = [xml](Get-Content -LiteralPath $report.FullName -Raw)

    foreach ($class in $xml.SelectNodes("//class")) {
        $name = ($class.filename -replace "\\", "/")
        $index = $name.ToLowerInvariant().IndexOf($rootMarker)

        if ($index -lt 0) {
            continue
        }

        $name = $name.Substring($index + $rootMarker.Length)

        if (-not $hits.ContainsKey($name)) {
            $hits[$name] = New-Object "System.Collections.Generic.Dictionary[int,int]"
        }

        foreach ($line in $class.SelectNodes("lines/line")) {
            $number = [int]$line.number
            $count = [int]$line.hits

            if (-not $hits[$name].ContainsKey($number) -or $hits[$name][$number] -lt $count) {
                $hits[$name][$number] = $count
            }
        }
    }
}

$rows = New-Object System.Collections.Generic.List[object]
$valid = 0
$covered = 0

foreach ($name in $hits.Keys) {
    $lineCount = $hits[$name].Count
    $coveredCount = @($hits[$name].Values | Where-Object { $_ -gt 0 }).Count
    $valid += $lineCount
    $covered += $coveredCount
    $rows.Add([pscustomobject]@{ Rate = $coveredCount / $lineCount; Covered = $coveredCount; Lines = $lineCount; File = $name })
}

"{0:P1} ({1} of {2} lines)" -f ($covered / $valid), $covered, $valid
$rows | Sort-Object Rate | Select-Object -First $Lowest | ForEach-Object { "{0,4:P0} {1,4}/{2,-4} {3}" -f $_.Rate, $_.Covered, $_.Lines, $_.File }
