<#
.SYNOPSIS
    Puts the Today's rite check (observability/synthetics/todays-rite.js) into Grafana Cloud's Synthetic Monitoring.

.DESCRIPTION
    Run by hand after changing the script (observability/README.md). The check is found by its job and target, so
    running it again updates the same check rather than adding another. Its results are metrics in the stack's
    Prometheus (probe_all_success_*), which the "Today unreachable or stale" rule in observability/grafana/alerts.json
    watches.

.PARAMETER ApiUrl
    The Synthetic Monitoring API's address for the stack's region, as Synthetic Monitoring's Config page shows it.

.PARAMETER TokenPath
    A file holding a Synthetic Monitoring access token (Config → Generate access token). The token is read from it and
    never printed.

.PARAMETER Probes
    The public probes that run the check, by name. Two, so one probe's own trouble doesn't look like the site's.
#>
param(
    [Parameter(Mandatory)]
    [string] $ApiUrl,

    [string] $TokenPath = (Join-Path $HOME '.machinespirit' 'grafana-synthetics-token.txt'),

    [string[]] $Probes = @('Frankfurt', 'London')
)

$ErrorActionPreference = 'Stop'

$job = "Today's rite"
$target = 'https://dailymachinespirit.fyi/'
$scriptPath = Join-Path $PSScriptRoot '..' 'observability' 'synthetics' 'todays-rite.js'
$api = "$($ApiUrl.TrimEnd('/'))/api/v1"
$headers = @{ Authorization = "Bearer $((Get-Content -Path $TokenPath -Raw).Trim())" }

$available = Invoke-RestMethod -Uri "$api/probe/list" -Headers $headers
$chosen = @($available | Where-Object { $_.name -in $Probes })
if ($chosen.Count -ne $Probes.Count) {
    throw "Not every probe in $($Probes -join ', ') exists. The probes: $(($available | ForEach-Object name | Sort-Object) -join ', ')."
}

$script = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((Get-Content -Path $scriptPath -Raw)))
$check = [ordered]@{
    job = $job
    target = $target
    # Every five minutes from each probe: about 17,000 runs a month, within the free tier's executions.
    frequency = 300000
    timeout = 30000
    enabled = $true
    labels = @()
    probes = @($chosen | ForEach-Object id)
    # Success and duration only: the alert needs nothing more, and every series counts against the stack's limits.
    basicMetricsOnly = $true
    alertSensitivity = 'none'
    settings = @{ scripted = @{ script = $script } }
}

# Assigned first: Invoke-RestMethod sends a JSON array down the pipeline as one object, which Where-Object would keep
# or drop whole.
$checks = Invoke-RestMethod -Uri "$api/check/list" -Headers $headers
$existing = $checks | Where-Object { $_.job -eq $job -and $_.target -eq $target } | Select-Object -First 1
if ($existing) {
    $check.id = $existing.id
    $check.tenantId = $existing.tenantId
    Invoke-RestMethod -Method Post -Uri "$api/check/update" -Headers $headers -ContentType 'application/json' `
        -Body ($check | ConvertTo-Json -Depth 10) | Out-Null
    Write-Host "Updated the check $job, run from $($Probes -join ' and ')."
}
else {
    Invoke-RestMethod -Method Post -Uri "$api/check/add" -Headers $headers -ContentType 'application/json' `
        -Body ($check | ConvertTo-Json -Depth 10) | Out-Null
    Write-Host "Added the check $job, run from $($Probes -join ' and ')."
}
