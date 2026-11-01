<#
.SYNOPSIS
Changes the application version in Version.txt before a release.

.DESCRIPTION
The version has the form 0.<minor>.<patch>. Use -Minor for a regular release: the second number grows and the third is reset to zero (0.3.0 -> 0.4.0). Use -Patch for a hotfix: only the third number grows (0.3.0 -> 0.3.1). Exactly one of the switches is required, because the choice is always made by the person preparing the release.

The script only edits Version.txt. Commit the change, then run Release.ps1, which builds the package and tags the version. The versionCode (BuildNumber.txt) is never reset, because Google Play requires it to grow.

.PARAMETER Minor
Starts a new minor version and resets the patch number.

.PARAMETER Patch
Prepares a hotfix of the current minor version.
#>
param(
    [switch]$Minor,
    [switch]$Patch
)

if ($Minor -eq $Patch) {
    throw "Choose exactly one of -Minor or -Patch."
}

$versionFile = Join-Path (Split-Path $PSScriptRoot) "Version.txt"

if (-not (Test-Path $versionFile)) {
    throw "Version file not found: $versionFile"
}

$current = ([IO.File]::ReadAllText($versionFile)).Trim()

if ($current -notmatch '^(\d+)\.(\d+)\.(\d+)$') {
    throw "Version.txt must contain a version like 0.3.0, found: $current"
}

$major = [int]$Matches[1]
$minorNumber = [int]$Matches[2]
$patchNumber = [int]$Matches[3]

if ($Minor) {
    $minorNumber++
    $patchNumber = 0
}
else {
    $patchNumber++
}

$next = "$major.$minorNumber.$patchNumber"

[IO.File]::WriteAllText($versionFile, $next + "`n", (New-Object System.Text.UTF8Encoding($false)))

Write-Host "Version changed: $current -> $next" -ForegroundColor Green
Write-Host "Commit Version.txt, then run Scripts\Release.ps1."
