<#
.SYNOPSIS
Generates release notes: a summary of the commits since the previous release tag.

.DESCRIPTION
Release tags are created by Release.ps1 and are named v<version> (for example v0.2.137). By default the notes cover the commits between the latest v* tag and HEAD. When HEAD is exactly on the latest tag, the range is the previous tag to that tag. The result is printed and saved to release-notes\<version>.md (the directory is ignored by git). Commits are grouped by the first word of the subject (Add, Fix, Change, Remove, other).

.PARAMETER From
Start of the range (tag or commit), excluded. Default: the previous release tag.

.PARAMETER To
End of the range (tag or commit), included. Default: HEAD.

.PARAMETER Version
Version shown in the header. Default: the tag at the end of the range, otherwise the current version from BuildNumber.txt.

.PARAMETER NoFile
Prints the notes without saving a file.
#>
param(
    [string]$From,
    [string]$To = "HEAD",
    [string]$Version,
    [switch]$NoFile
)

$gitPath = (Get-Command git -ErrorAction SilentlyContinue).Source
if (-not $gitPath) { $gitPath = "C:\Program Files\Git\cmd\git.exe" }

$root = Split-Path $PSScriptRoot
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Invoke-Git {
    $result = & $gitPath -C $root @args
    if ($LASTEXITCODE -ne 0) {
        throw "git $($args -join ' ') failed."
    }
    return $result
}

$toCommit = (Invoke-Git rev-parse "$To^{commit}").Trim()
$tags = @(Invoke-Git tag --list "v*" --sort=-v:refname)
$tagsOnTarget = @(Invoke-Git tag --points-at $toCommit | Where-Object { $_ -like "v*" })

if (-not $Version) {
    if ($tagsOnTarget.Count -gt 0) {
        $Version = $tagsOnTarget[0].TrimStart("v")
    }
    else {
        $displayVersion = ([xml](Get-Content -Raw (Join-Path $root "AdsbCarMap.csproj"))).Project.PropertyGroup.ApplicationDisplayVersion | Where-Object { $_ } | Select-Object -First 1
        $buildNumber = ([IO.File]::ReadAllText((Join-Path $root "BuildNumber.txt"))).Trim()
        $Version = "$displayVersion.$buildNumber (unreleased)"
    }
}

if (-not $From) {
    foreach ($tag in $tags) {
        $tagCommit = (Invoke-Git rev-parse "$tag^{commit}").Trim()
        if ($tagCommit -ne $toCommit) {
            $From = $tag
            break
        }
    }
}

if ($From) {
    $range = "$From..$toCommit"
    $rangeText = "$From -> $To"
}
else {
    $range = $toCommit
    $rangeText = "start of history -> $To"
}

$separator = [string][char]0x1e
$field = [string][char]0x1f
$raw = (Invoke-Git log --no-merges "--format=%h$field%s$field%b$separator" $range) -join "`n"
$entries = @($raw -split $separator | Where-Object { $_.Trim() })

$groups = [ordered]@{
    "Add"    = @()
    "Fix"    = @()
    "Change" = @()
    "Remove" = @()
    "Other"  = @()
}

foreach ($entry in $entries) {
    $parts = $entry.Trim() -split $field, 3
    $hash = $parts[0].Trim()
    $subject = $parts[1].Trim()
    $body = @()

    if ($parts.Count -gt 2) {
        $body = @($parts[2] -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^-\s' })
    }

    $firstWord = ($subject -split ' ')[0]
    $key = switch -Regex ($firstWord) {
        '^(Add|Introduce|Create)$' { "Add"; break }
        '^(Fix|Repair|Correct)$' { "Fix"; break }
        '^(Change|Update|Improve|Refactor|Replace|Rework|Sync|Move|Rename|Translate|Document)$' { "Change"; break }
        '^(Remove|Delete|Drop)$' { "Remove"; break }
        default { "Other" }
    }

    $groups[$key] += [pscustomobject]@{ Hash = $hash; Subject = $subject; Body = $body }
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# Release $Version")
$lines.Add("")
$lines.Add("Date: $((Get-Date).ToString('yyyy-MM-dd'))")
$lines.Add("Range: $rangeText ($($entries.Count) commits)")
$lines.Add("")

if ($entries.Count -eq 0) {
    $lines.Add("No changes since the previous release.")
}

foreach ($key in $groups.Keys) {
    if ($groups[$key].Count -eq 0) { continue }

    $lines.Add("## $key")
    $lines.Add("")

    foreach ($item in $groups[$key]) {
        $lines.Add("- $($item.Subject) ($($item.Hash))")

        foreach ($bodyLine in $item.Body) {
            $lines.Add("  $bodyLine")
        }
    }

    $lines.Add("")
}

$text = ($lines -join "`r`n")
Write-Host $text

if (-not $NoFile) {
    $directory = Join-Path $root "release-notes"
    New-Item -ItemType Directory -Force $directory | Out-Null
    $fileName = ($Version -replace '[^0-9A-Za-z._-]', '_') + ".md"
    $file = Join-Path $directory $fileName
    [IO.File]::WriteAllText($file, $text + "`r`n", $utf8)
    Write-Host "`nSaved: $file" -ForegroundColor Green
}
