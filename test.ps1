param(
    [string]$LibDir = "E:\Scripts\Valheim Mods\Reqs",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$Sln = Join-Path $ProjectRoot "Dismantleheim.sln"

Push-Location $ProjectRoot
try {
    # Core + Installer tests never need Valheim refs. AssemblyCompatibility skips if Reqs missing.
    $env:DISMANTLEHEIM_REQS = $LibDir
    dotnet test $Sln -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet test failed with exit code $LASTEXITCODE" }
}
finally {
    Pop-Location
}
