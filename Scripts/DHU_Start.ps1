$sdk = "$env:LOCALAPPDATA\Android\Sdk"
adb forward tcp:5277 tcp:5277
& "$sdk\extras\google\auto\desktop-head-unit.exe"