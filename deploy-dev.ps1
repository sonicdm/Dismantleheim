param(
    [string]$LibDir = "E:\Scripts\Valheim Mods\Reqs",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$PluginsDir = "$env:AppData\com.kesomannen.gale\valheim\profiles\Default\BepInEx\plugins",
    [string]$ScriptsDir = "$env:AppData\com.kesomannen.gale\valheim\profiles\Default\BepInEx\scripts",
    [switch]$ScriptEngine
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

& (Join-Path $ProjectRoot "build.ps1") -LibDir $LibDir -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { throw "build.ps1 failed" }

$Dll = Join-Path $ProjectRoot "bin\$Configuration\Dismantleheim.dll"
$Pdb = Join-Path $ProjectRoot "bin\$Configuration\Dismantleheim.pdb"
if (-not (Test-Path -LiteralPath $Dll)) { throw "DLL missing: $Dll" }

$PluginFolder = Join-Path $PluginsDir "Dismantleheim"
New-Item -ItemType Directory -Force -Path $PluginFolder, $ScriptsDir | Out-Null

$PluginDll = Join-Path $PluginFolder "Dismantleheim.dll"
$PluginPdb = Join-Path $PluginFolder "Dismantleheim.pdb"
$PluginDisabled = $PluginDll + ".disabled"
$ScriptDll = Join-Path $ScriptsDir "Dismantleheim.dll"
$ScriptPdb = Join-Path $ScriptsDir "Dismantleheim.pdb"

if ($ScriptEngine) {
    # Prefer ScriptEngine scripts\ — disable plugins\ copy so Chainloader does not double-load.
    if (Test-Path -LiteralPath $PluginDll) {
        Move-Item -LiteralPath $PluginDll -Destination $PluginDisabled -Force
        Write-Host "Disabled plugins copy: $PluginDisabled"
    }
    if (Test-Path -LiteralPath $PluginPdb) {
        Remove-Item -LiteralPath $PluginPdb -Force
    }

    Copy-Item -LiteralPath $Dll -Destination $ScriptDll -Force
    if (Test-Path -LiteralPath $Pdb) {
        Copy-Item -LiteralPath $Pdb -Destination $ScriptPdb -Force
    }
    else {
        Write-Warning "No PDB beside build output — ScriptEngine may fail to load."
    }

    Write-Host "Deployed (ScriptEngine scripts): $ScriptDll"
    Write-Host "Press F9 in-game to reload (or wait for file watcher ~1.5s)."
}
else {
    if ((-not (Test-Path -LiteralPath $PluginDll)) -and (Test-Path -LiteralPath $PluginDisabled)) {
        Move-Item -LiteralPath $PluginDisabled -Destination $PluginDll -Force
        Write-Host "Re-enabled: $PluginDll"
    }

    Copy-Item -LiteralPath $Dll -Destination $PluginDll -Force
    if (Test-Path -LiteralPath $Pdb) {
        Copy-Item -LiteralPath $Pdb -Destination $PluginPdb -Force
    }

    foreach ($name in @("Dismantleheim.dll", "Dismantleheim.pdb")) {
        $scriptPath = Join-Path $ScriptsDir $name
        if (Test-Path -LiteralPath $scriptPath) {
            Remove-Item -LiteralPath $scriptPath -Force
            Write-Host "Removed scripts copy: $scriptPath"
        }
    }

    Write-Host "Deployed (plugins): $PluginDll"
    Write-Host "Restart Valheim to load (normal BepInEx plugin — no ScriptEngine / F9)."
}

Write-Host "Console: dismantleheim status | dismantleheim why"
