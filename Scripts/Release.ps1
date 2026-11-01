<#
.SYNOPSIS
Builds the signed Release AAB and tags the built commit in git.

.DESCRIPTION
The tag is named v<version from Version.txt> (for example v0.3.0). Change the version first with Bump-Version.ps1 and commit Version.txt. The tag is an annotated tag that records the version, the build number (versionCode), the commit, the AAB file name, its size and SHA-256. The same values are appended to release-history.csv. The tag is not pushed unless -Push is given.

.PARAMETER NoTag
Builds the AAB only, without checking the working tree and without creating a tag.

.PARAMETER Push
Pushes the created tag to origin.

.PARAMETER Force
Allows tagging when the working tree has changes other than BuildNumber.txt.
#>
param(
    [switch]$NoTag,
    [switch]$Push,
    [switch]$Force
)

$gitPath = (Get-Command git -ErrorAction SilentlyContinue).Source
if (-not $gitPath) { $gitPath = "C:\Program Files\Git\cmd\git.exe" }
$projectPath = Join-Path (Split-Path $PSScriptRoot) "AdsbCarMap.csproj"
$keystorePath = "$env:USERPROFILE\adsbcarmap-release.jks"
$signingPropsPath = "$env:USERPROFILE\adsbcarmap-signing.props"
$outputPath = Join-Path (Split-Path $projectPath) "bin\Release\net10.0-android\publish"

if (-not (Test-Path $projectPath)) {
    throw "Project not found: $projectPath"
}

if (-not (Test-Path $keystorePath)) {
    throw "Keystore not found: $keystorePath"
}

if (-not $NoTag) {
    $dirty = & $gitPath -C (Split-Path $projectPath) status --porcelain | Where-Object { $_ -notmatch '^\s*M\s+(BuildNumber\.txt|release-history\.csv)$' }

    if ($dirty -and -not $Force) {
        Write-Host ($dirty -join "`n")
        throw "Working tree has uncommitted changes (other than BuildNumber.txt and release-history.csv). Commit them first or use -Force."
    }

    $version = ([IO.File]::ReadAllText((Join-Path (Split-Path $projectPath) "Version.txt"))).Trim()
    $tag = "v$version"
    & $gitPath -C (Split-Path $projectPath) rev-parse -q --verify "refs/tags/$tag" | Out-Null

    if ($LASTEXITCODE -eq 0) {
        throw "Tag $tag already exists. Run Scripts\Bump-Version.ps1 (-Minor or -Patch), commit Version.txt and run Release.ps1 again."
    }
}

$passwordPointer = [IntPtr]::Zero
$securePassword = $null
$previousStorePass = $env:ADSBCARMAP_STOREPASS
$previousKeyPass = $env:ADSBCARMAP_KEYPASS

try {
    # The csproj imports the signing props file when it exists; otherwise it reads ADSBCARMAP_STOREPASS/KEYPASS.
    if (-not (Test-Path $signingPropsPath) -and -not $env:ADSBCARMAP_STOREPASS) {
        $securePassword = Read-Host "Keystore password" -AsSecureString
        $passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
        $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)

        $env:ADSBCARMAP_STOREPASS = $password
        $env:ADSBCARMAP_KEYPASS = $password
    }

    Set-Location (Split-Path $projectPath)

    dotnet publish $projectPath `
        -c Release `
        -f net10.0-android `
        -p:AndroidPackageFormat=aab

    if ($LASTEXITCODE -ne 0) {
        throw "AAB build failed with exit code $LASTEXITCODE"
    }

    Write-Host "`nBuild completed successfully." -ForegroundColor Green

    $aabFiles = Get-ChildItem $outputPath -Filter "*.aab"
    $aabFiles | Select-Object FullName, Length, LastWriteTime

    if (-not $NoTag) {
        $root = Split-Path $projectPath
        $buildNumber = ([IO.File]::ReadAllText((Join-Path $root "BuildNumber.txt"))).Trim()
        $commit = (& $gitPath -C $root rev-parse HEAD).Trim()
        $signedAab = $aabFiles | Where-Object { $_.Name -like "*-Signed.aab" } | Select-Object -First 1

        if (-not $signedAab) { $signedAab = $aabFiles | Select-Object -First 1 }

        $hash = (Get-FileHash $signedAab.FullName -Algorithm SHA256).Hash
        $date = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")

        & $gitPath -C $root rev-parse -q --verify "refs/tags/$tag" | Out-Null
        if ($LASTEXITCODE -eq 0) {
            throw "Tag $tag already exists."
        }

        $message = @"
Release $version

Version:     $version
VersionCode: $buildNumber
Commit:      $commit
AAB:         $($signedAab.Name)
Size:        $($signedAab.Length)
SHA-256:     $hash
Built:       $date
"@
        $messageFile = Join-Path $env:TEMP "adsbcarmap-tag-message.txt"
        [IO.File]::WriteAllText($messageFile, $message, (New-Object System.Text.UTF8Encoding($false)))

        & $gitPath -C $root tag -a $tag -F $messageFile
        if ($LASTEXITCODE -ne 0) { throw "Creating tag $tag failed." }

        $historyFile = Join-Path $root "release-history.csv"
        if (-not (Test-Path $historyFile)) {
            [IO.File]::WriteAllText($historyFile, "date,version,versionCode,commit,aab,size,sha256`r`n", (New-Object System.Text.UTF8Encoding($false)))
        }
        [IO.File]::AppendAllText($historyFile, "$date,$version,$buildNumber,$commit,$($signedAab.Name),$($signedAab.Length),$hash`r`n", (New-Object System.Text.UTF8Encoding($false)))

        Write-Host "`nTag $tag created on $($commit.Substring(0, 7)). Release history: $historyFile" -ForegroundColor Green

        if ($Push) {
            & $gitPath -C $root push origin $tag
        }
        else {
            Write-Host "Push it with: git push origin $tag"
        }
    }
}
finally {
    $env:ADSBCARMAP_STOREPASS = $previousStorePass
    $env:ADSBCARMAP_KEYPASS = $previousKeyPass

    if ($passwordPointer -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    }

    $password = $null

    if ($null -ne $securePassword) {
        $securePassword.Dispose()
    }
}
