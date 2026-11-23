param(
    [string]$Dotnet = "dotnet",
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) "..\..\..\outputs\GeoCoin-Start"),
    [string]$PublishDirectory = "",
    [string]$SigningCertificateThumbprint = $env:GEOCOIN_SIGNING_CERT_THUMBPRINT,
    [string]$TimestampUrl = "https://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"
$projectDirectory = $PSScriptRoot
$publishDirectory = if ([string]::IsNullOrWhiteSpace($PublishDirectory)) {
    Join-Path $projectDirectory "bin\publish"
} else {
    [System.IO.Path]::GetFullPath($PublishDirectory)
}
& $Dotnet publish (Join-Path $projectDirectory "GeoCoinStart.csproj") -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "GeoCoin-Start publish failed."
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$setupPath = Join-Path $OutputDirectory "GeoCoin-Start-Setup.exe"
Copy-Item -LiteralPath (Join-Path $publishDirectory "GeoCoin-Start.exe") -Destination $setupPath -Force

if (-not [string]::IsNullOrWhiteSpace($SigningCertificateThumbprint)) {
    $thumbprint = $SigningCertificateThumbprint -replace '\s', ''
    $certificate = Get-ChildItem Cert:\CurrentUser\My | Where-Object {
        $_.Thumbprint -eq $thumbprint -and $_.HasPrivateKey
    } | Select-Object -First 1
    if (-not $certificate) {
        throw "No code-signing certificate with a private key was found in CurrentUser\My for thumbprint $thumbprint."
    }
    $ekuExtension = $certificate.Extensions | Where-Object { $_.Oid.Value -eq "2.5.29.37" } | Select-Object -First 1
    $codeSigningEku = $null
    if ($ekuExtension) {
        $codeSigningEku = [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($ekuExtension, $false).EnhancedKeyUsages |
            Where-Object { $_.Value -eq "1.3.6.1.5.5.7.3.3" }
    }
    if (-not $codeSigningEku) {
        throw "The selected certificate does not include the Code Signing extended key usage."
    }

    $chain = [System.Security.Cryptography.X509Certificates.X509Chain]::new()
    $chain.ChainPolicy.ApplicationPolicy.Add([System.Security.Cryptography.Oid]::new("1.3.6.1.5.5.7.3.3"))
    $chain.ChainPolicy.RevocationMode = [System.Security.Cryptography.X509Certificates.X509RevocationMode]::Online
    $chain.ChainPolicy.UrlRetrievalTimeout = [TimeSpan]::FromSeconds(15)
    if (-not $chain.Build($certificate)) {
        $status = ($chain.ChainStatus | ForEach-Object { $_.StatusInformation.Trim() }) -join "; "
        throw "The code-signing certificate is not trusted on this computer: $status"
    }

    $signToolCommand = Get-Command signtool.exe -ErrorAction SilentlyContinue
    $signTool = if ($signToolCommand) { $signToolCommand.Source } else { $null }
    if (-not $signTool) {
        $kitsPath = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
        $signTool = Get-ChildItem -Path $kitsPath -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.Directory.Name -eq "x64" } |
            Sort-Object { [version]$_.Directory.Parent.Name } -Descending |
            Select-Object -First 1 -ExpandProperty FullName
    }
    if (-not $signTool) {
        throw "SignTool was not found. Install the Windows SDK or add signtool.exe to PATH."
    }

    & $signTool sign /fd SHA256 /tr $TimestampUrl /td SHA256 /sha1 $thumbprint $setupPath
    if ($LASTEXITCODE -ne 0) { throw "SignTool could not sign the installer." }
    & $signTool verify /pa /v $setupPath
    if ($LASTEXITCODE -ne 0) { throw "The Authenticode signature could not be verified." }
    $chain.Dispose()
    Write-Host "Signed: $setupPath ($($certificate.Subject))"
}
else {
    Write-Host "Unsigned build: set GEOCOIN_SIGNING_CERT_THUMBPRINT to a trusted code-signing certificate thumbprint to sign the installer."
}

$setupHash = (Get-FileHash -LiteralPath $setupPath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumPath = Join-Path $OutputDirectory "SHA256SUMS.txt"
Set-Content -LiteralPath $checksumPath -Value "$setupHash  GeoCoin-Start-Setup.exe" -Encoding ascii
Write-Host "Checksum: $checksumPath"
Write-Host "Built: $setupPath"
