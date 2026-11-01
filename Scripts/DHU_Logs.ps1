$logDirectory = Join-Path (Split-Path $PSScriptRoot) "logs"
New-Item -ItemType Directory -Force $logDirectory | Out-Null

adb logcat -c
adb logcat -v threadtime > (Join-Path $logDirectory "android-auto.log")
