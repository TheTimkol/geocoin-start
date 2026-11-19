param(
    [string]$Dotnet = "dotnet",
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) "..\..\..\outputs\GeoCoin-Start")
)

$ErrorActionPreference = "Stop"
$projectDirectory = $PSScriptRoot
$publishDirectory = Join-Path $projectDirectory "bin\publish"
& $Dotnet publish (Join-Path $projectDirectory "GeoCoinStart.csproj") -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "Не удалось собрать GeoCoin-Start."
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $publishDirectory "GeoCoin-Start.exe") -Destination (Join-Path $OutputDirectory "GeoCoin-Start-Setup.exe") -Force
Write-Host "Готово: $(Join-Path $OutputDirectory 'GeoCoin-Start-Setup.exe')"
