<#
.SYNOPSIS
    Puts observability/grafana/alerts.json into Grafana Cloud as one rule group, replacing the group's rules there.

.DESCRIPTION
    Run by hand after changing the file (observability/README.md). Applying it twice changes nothing: every rule keeps
    its uid. The rules stay editable in Grafana; export them back over the file afterwards, so the repository stays the
    truth. Notifications follow the stack's default notification policy (its default contact point emails the owner).

    It first checks that production's telemetry reaches the stack: the rules would otherwise watch nothing, and only
    "No telemetry from production" would ever fire.

.PARAMETER GrafanaUrl
    The stack's address, e.g. https://<stack>.grafana.net.

.PARAMETER TokenPath
    A file holding a Grafana service account token (role Editor). The token is read from it and never printed.
#>
param(
    [Parameter(Mandatory)]
    [string] $GrafanaUrl,

    [string] $TokenPath = (Join-Path $HOME '.machinespirit' 'grafana-alerts-token.txt')
)

$ErrorActionPreference = 'Stop'

$folderUid = 'daily-machine-spirit'
$folderTitle = 'Daily Machine Spirit'
$rulesPath = Join-Path $PSScriptRoot '..' 'observability' 'grafana' 'alerts.json'
$grafana = $GrafanaUrl.TrimEnd('/')
$headers = @{ Authorization = "Bearer $((Get-Content -Path $TokenPath -Raw).Trim())" }

# Grafana Cloud names the stack's Prometheus data source grafanacloud-<stack>-prom.
$prometheus = Invoke-RestMethod -Uri "$grafana/api/datasources" -Headers $headers |
    Where-Object { $_.type -eq 'prometheus' -and $_.name -like 'grafanacloud-*-prom' } |
    Select-Object -First 1
if (-not $prometheus) {
    throw "No grafanacloud-*-prom data source in $grafana."
}

$query = [Uri]::EscapeDataString('count(last_over_time(target_info{job="machinespirit-app"}[1d]))')
$seen = Invoke-RestMethod -Uri "$grafana/api/datasources/proxy/uid/$($prometheus.uid)/api/v1/query?query=$query" -Headers $headers
if ($seen.data.result.Count -eq 0) {
    throw "No telemetry from machinespirit-app in the last day: check production's OtlpHeaders secret first (infra/README.md)."
}
Write-Host "Production's telemetry reaches $($prometheus.name)."

try {
    Invoke-RestMethod -Uri "$grafana/api/folders/$folderUid" -Headers $headers | Out-Null
}
catch {
    if ($_.Exception.Response.StatusCode -ne [Net.HttpStatusCode]::NotFound) {
        throw
    }
    $folder = @{ uid = $folderUid; title = $folderTitle } | ConvertTo-Json
    Invoke-RestMethod -Method Post -Uri "$grafana/api/folders" -Headers $headers -ContentType 'application/json' -Body $folder | Out-Null
    Write-Host "Created the folder $folderTitle."
}

$group = (Get-Content -Path $rulesPath -Raw).Replace('${prometheus}', $prometheus.uid) | ConvertFrom-Json
foreach ($rule in $group.rules) {
    $rule | Add-Member -NotePropertyName folderUID -NotePropertyValue $folderUid
    $rule | Add-Member -NotePropertyName ruleGroup -NotePropertyValue $group.title
    $rule | Add-Member -NotePropertyName orgID -NotePropertyValue 1
}
$group | Add-Member -NotePropertyName folderUid -NotePropertyValue $folderUid
$body = $group | ConvertTo-Json -Depth 20

# Without X-Disable-Provenance, rules put through the API can't be edited in Grafana's UI.
$groupName = [Uri]::EscapeDataString($group.title)
Invoke-RestMethod -Method Put -Uri "$grafana/api/v1/provisioning/folder/$folderUid/rule-groups/$groupName" `
    -Headers ($headers + @{ 'X-Disable-Provenance' = 'true' }) -ContentType 'application/json' -Body $body | Out-Null
Write-Host "Applied $($group.rules.Count) alert rules to $folderTitle / $($group.title)."
