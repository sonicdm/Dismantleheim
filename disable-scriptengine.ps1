param(
    [string]$ProfileBepInEx = "$env:AppData\com.kesomannen.gale\valheim\profiles\Default\BepInEx"
)

$ErrorActionPreference = "Stop"

$ConfigDir = Join-Path $ProfileBepInEx "config"
$Scripts = Join-Path $ProfileBepInEx "scripts"
$SePluginDir = Join-Path $ProfileBepInEx "plugins\BepInEx-ScriptEngine"
$CfgPath = Join-Path $ConfigDir "com.bepis.bepinex.scriptengine.cfg"

$SeDll = Join-Path $SePluginDir "ScriptEngine.dll"
$SeDisabled = Join-Path $SePluginDir "ScriptEngine.dll.disabled"
if (Test-Path -LiteralPath $SeDll) {
    Move-Item -LiteralPath $SeDll -Destination $SeDisabled -Force
    Write-Host "Disabled ScriptEngine plugin: $SeDisabled"
}
elseif (Test-Path -LiteralPath $SeDisabled) {
    Write-Host "ScriptEngine already disabled: $SeDisabled"
}
else {
    Write-Host "No ScriptEngine.dll found under $SePluginDir"
}

@"
## Settings file was created by plugin Script Engine v11.1
## Plugin GUID: com.bepis.bepinex.scriptengine
## Disabled for normal Dismantleheim plugins\ installs.

[AutoReload]

# Setting type: Boolean
# Default value: false
EnableFileSystemWatcher = false

# Setting type: Single
# Default value: 3
AutoReloadDelay = 3

# Setting type: Boolean
# Default value: false
DumpAssemblies = false

[General]

# Setting type: Boolean
# Default value: false
LoadOnStart = false

# Setting type: KeyboardShortcut
# Default value: F6
# Unbound while ScriptEngine is disabled for normal installs.
ReloadKey =

# Setting type: Boolean
# Default value: false
QuietMode = false

# Setting type: Boolean
# Default value: false
IncludeSubdirectories = false
"@ | Set-Content -LiteralPath $CfgPath -Encoding UTF8
Write-Host "Config: $CfgPath (LoadOnStart=false, watcher off, ReloadKey unbound)"

if (Test-Path -LiteralPath $Scripts) {
    Get-ChildItem -LiteralPath $Scripts -Filter "Dismantleheim.*" -Force -ErrorAction SilentlyContinue |
        ForEach-Object {
            Remove-Item -LiteralPath $_.FullName -Force
            Write-Host "Removed: $($_.FullName)"
        }
}

Write-Host "Done. Run .\deploy-dev.ps1 (no -ScriptEngine) and restart Valheim."
