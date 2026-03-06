# Discovers the API and Frontend ports from running Aspire processes.
# Outputs environment variable assignments for use by batch scripts.

# Find the API port: Courier.Api process, HTTP port (lower of two)
$apiProcess = Get-Process -Name "Courier.Api" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($apiProcess) {
    $ports = Get-NetTCPConnection -OwningProcess $apiProcess.Id -State Listen -ErrorAction SilentlyContinue |
        Where-Object { $_.LocalAddress -eq "127.0.0.1" } |
        Sort-Object LocalPort |
        Select-Object -ExpandProperty LocalPort
    # The higher port is HTTP, lower is HTTPS (Aspire convention)
    if ($ports.Count -ge 2) {
        $httpPort = $ports[$ports.Count - 1]
    } elseif ($ports.Count -eq 1) {
        $httpPort = $ports[0]
    }
    if ($httpPort) {
        Write-Host "API_URL=http://localhost:$httpPort"
    }
}

# Find the Frontend port: scan node processes for one serving the Next.js app
$nodeProcesses = Get-Process -Name "node" -ErrorAction SilentlyContinue
foreach ($proc in $nodeProcesses) {
    $connections = Get-NetTCPConnection -OwningProcess $proc.Id -State Listen -ErrorAction SilentlyContinue
    foreach ($conn in $connections) {
        $port = $conn.LocalPort
        $addr = $conn.LocalAddress
        # Try connecting to each port
        try {
            $testUrl = "http://localhost:$port"
            $r = Invoke-WebRequest -Uri $testUrl -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
            # If it responds, it's likely our frontend
            Write-Host "FRONTEND_URL=$testUrl"
            exit
        } catch {}
    }
}
