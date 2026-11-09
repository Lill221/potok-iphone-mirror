[CmdletBinding()]
param([switch]$Save)

# Read-only diagnostic. Run in Windows PowerShell 5.1 or PowerShell 7.
$ErrorActionPreference = 'Stop'
$report = [ordered]@{
    capturedAt = (Get-Date).ToString('o')
    devices = @()
    adapters = @()
    services = @()
    receiver = @()
    listeners = @()
    errors = @()
}
try {
    $report.devices = @(Get-PnpDevice -PresentOnly |
        Where-Object { $_.FriendlyName -match 'Apple|iPhone' } |
        Select-Object Status, Class, FriendlyName)
} catch { $report.errors += "Devices: $($_.Exception.Message)" }
try {
    $report.adapters = @(Get-NetAdapter |
        Select-Object Name, InterfaceDescription, Status, LinkSpeed)
} catch { $report.errors += "Adapters: $($_.Exception.Message)" }
try {
    $report.services = @(Get-Service |
        Where-Object { $_.DisplayName -match 'Apple|Bonjour' } |
        Select-Object Name, @{Name='Status';Expression={$_.Status.ToString()}})
} catch { $report.errors += "Services: $($_.Exception.Message)" }
try {
    $report.receiver = @(Get-Process -Name uxplay-windows -ErrorAction SilentlyContinue |
        Select-Object Id, ProcessName)
    foreach ($receiverProcess in $report.receiver) {
        $report.listeners += @(Get-NetTCPConnection -OwningProcess $receiverProcess.Id |
            Where-Object { $_.State -eq 'Listen' } |
            Select-Object LocalAddress, LocalPort)
    }
} catch { $report.errors += "Receiver: $($_.Exception.Message)" }
$report.usbNetworkDetected = @($report.adapters | Where-Object {
    $_.InterfaceDescription -match 'Apple.*(Ethernet|Mobile Device)' -and $_.Status -eq 'Up'
}).Count -gt 0
$json = $report | ConvertTo-Json -Depth 5
if ($Save) {
    $logDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) 'logs'
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    $json | Set-Content -LiteralPath (Join-Path $logDirectory 'diagnostic.json') -Encoding UTF8
}
$json
