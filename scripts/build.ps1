[CmdletBinding()]
param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$localSdk = Join-Path $projectRoot 'tools/dotnet/dotnet.exe'
$sdk = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $projectRoot 'app' }
& $sdk publish (Join-Path $projectRoot 'src/PhoneMirror/PhoneMirror.csproj') -c Release -o $OutputDirectory --self-contained false
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
