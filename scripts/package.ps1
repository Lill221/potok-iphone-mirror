[CmdletBinding()]
param([ValidatePattern('^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?$')][string]$Version = '0.3.2-preview')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$localSdk = Join-Path $projectRoot 'tools/dotnet/dotnet.exe'
$sdk = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
$distribution = Join-Path $projectRoot 'dist'
$stage = Join-Path $distribution ('staging/Potok-' + $Version + '-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
& $sdk publish (Join-Path $projectRoot 'src/PhoneMirror/PhoneMirror.csproj') -c Release -r win-x64 --self-contained true -o $stage -p:PublishSingleFile=false
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
foreach ($name in @('potok.json', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'README.md', 'README.ru.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $stage
}
New-Item -ItemType Directory -Path (Join-Path $stage 'scripts') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/licenses') -Destination (Join-Path $stage 'licenses') -Recurse
foreach ($name in @('setup-local-network.ps1', 'diagnose.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $stage 'scripts')
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/GETTING_STARTED.md') -Destination (Join-Path $stage 'START_HERE.md')
foreach ($argument in @('--self-test', '--native-host-test')) {
    $test = Start-Process -FilePath (Join-Path $stage 'PhoneMirror.exe') -ArgumentList $argument -PassThru -Wait
    if ($test.ExitCode -ne 0) { throw "Release validation failed: $argument (see test report in $stage)" }
}
# A release contains only fresh published files, public documentation and setup scripts.
# Remove precisely named test outputs; never copy local app/, tools/ or logs/ into it.
foreach ($name in @('self-test.passed.txt','self-test.failed.txt','native-host-test.passed.txt','native-host-test.failed.txt','frame-antialias-preview.png')) {
    $testOutput = Join-Path $stage $name
    if (Test-Path -LiteralPath $testOutput) { Remove-Item -LiteralPath $testOutput }
}
$stageLogs = Join-Path $stage 'logs'
if ((Test-Path -LiteralPath $stageLogs) -and @(Get-ChildItem -LiteralPath $stageLogs -Force).Count -eq 0) { Remove-Item -LiteralPath $stageLogs }
$archive = Join-Path $distribution "Potok-$Version-win-x64.zip"
if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archive -CompressionLevel Optimal
$hash = Get-FileHash -LiteralPath $archive -Algorithm SHA256
"$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($archive))" | Set-Content -LiteralPath (Join-Path $distribution 'SHA256SUMS.txt') -Encoding ASCII
Write-Output "Release archive: $archive"
Write-Output "SHA256: $($hash.Hash)"
