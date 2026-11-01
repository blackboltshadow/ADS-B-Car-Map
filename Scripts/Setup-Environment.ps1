param(
    [string]$SourceDirectory,
    [string]$JavaHome,
    [string]$PlatformTools = "C:\Android\platform-tools",
    [switch]$SetSigningVariables
)

$signingFiles = @("adsbcarmap-release.jks", "adsbcarmap-signing.props", "adsbcarmap-contact.props")

if ($SourceDirectory) {
    foreach ($file in $signingFiles) {
        $sourcePath = Join-Path $SourceDirectory $file

        if (-not (Test-Path $sourcePath)) {
            Write-Warning "Not found: $sourcePath"
            continue
        }

        Copy-Item $sourcePath (Join-Path $env:USERPROFILE $file) -Force
        Write-Host "Copied $file to $env:USERPROFILE"
    }
}

if ($SetSigningVariables) {
    $securePassword = Read-Host "Keystore password" -AsSecureString
    $passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)

    try {
        $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
        [Environment]::SetEnvironmentVariable("ADSBCARMAP_STOREPASS", $password, "User")
        [Environment]::SetEnvironmentVariable("ADSBCARMAP_KEYPASS", $password, "User")
        Write-Host "Set ADSBCARMAP_STOREPASS and ADSBCARMAP_KEYPASS (user scope)."
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
        $password = $null
        $securePassword.Dispose()
    }
}

if ($JavaHome) {
    if (-not (Test-Path $JavaHome)) {
        throw "JDK not found: $JavaHome"
    }

    [Environment]::SetEnvironmentVariable("JAVA_HOME", $JavaHome, "User")
    Write-Host "Set JAVA_HOME to $JavaHome (user scope)."
}

if (Test-Path $PlatformTools) {
    $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
    $entries = @()

    if ($userPath) {
        $entries = $userPath.Split(";", [StringSplitOptions]::RemoveEmptyEntries)
    }

    if ($entries -notcontains $PlatformTools) {
        $entries += $PlatformTools
        [Environment]::SetEnvironmentVariable("Path", ($entries -join ";"), "User")
        Write-Host "Added $PlatformTools to the user PATH."
    }
    else {
        Write-Host "$PlatformTools is already in the user PATH."
    }
}
else {
    Write-Warning "Platform-tools not found: $PlatformTools (pass -PlatformTools)."
}

Write-Host "`nRestart Visual Studio and open terminals to pick up the changes." -ForegroundColor Green
