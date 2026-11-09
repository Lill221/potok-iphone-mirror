# Requires administrator privileges. Rules are limited to these programs and LocalSubnet.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$engine = Join-Path $projectRoot 'tools/uxplay/uxplay-windows.exe'
if (!(Test-Path -LiteralPath $engine)) { throw 'Receiver is missing.' }
$logFile = Join-Path $projectRoot 'logs/network-setup.log'
Start-Transcript -Path $logFile -Force
try {
    foreach ($protocol in @('TCP', 'UDP')) {
        $ruleName = "Potok-Receiver-$protocol"
        $existing = Get-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue
        if ($existing) { $existing | Remove-NetFirewallRule }
        New-NetFirewallRule -Name $ruleName -DisplayName "Potok local receiver ($protocol)" -Group 'Potok' -Program $engine -Direction Inbound -Action Allow -Protocol $protocol -RemoteAddress LocalSubnet -Profile Any | Out-Null
    }
    $bonjour = 'C:\Program Files\Bonjour\mDNSResponder.exe'
    if (Test-Path -LiteralPath $bonjour) {
        $ruleName = 'Potok-Bonjour-mDNS'
        $existing = Get-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue
        if ($existing) { $existing | Remove-NetFirewallRule }
        New-NetFirewallRule -Name $ruleName -DisplayName 'Potok Bonjour discovery (local subnet)' -Group 'Potok' -Program $bonjour -Direction Inbound -Action Allow -Protocol UDP -LocalPort 5353 -RemoteAddress LocalSubnet -Profile Any | Out-Null
    }
    Get-NetFirewallRule -Group 'Potok' | Format-Table Name,Enabled,Action -AutoSize
} finally { Stop-Transcript }
