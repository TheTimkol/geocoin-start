param(
    [string]$Dotnet = "dotnet",
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) "..\..\..\outputs\GeoCoin-Start")
)

$ErrorActionPreference = "Stop"
$projectDirectory = $PSScriptRoot
$publishDirectory = Join-Path $projectDirectory "bin\publish"
& $Dotnet publish (Join-Path $projectDirectory "GeoCoinStart.csproj") -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "GeoCoin-Start publish failed."
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$setupPath = Join-Path $OutputDirectory "GeoCoin-Start-Setup.exe"
Copy-Item -LiteralPath (Join-Path $publishDirectory "GeoCoin-Start.exe") -Destination $setupPath -Force
Write-Host "Built: $setupPath"
