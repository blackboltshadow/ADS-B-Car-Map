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

    Get-ChildItem $outputPath -Filter "*.aab" |
        Select-Object FullName, Length, LastWriteTime
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
