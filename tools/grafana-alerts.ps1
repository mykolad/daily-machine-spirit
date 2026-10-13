<#
.SYNOPSIS
    Puts observability/grafana/alerts.json into Grafana Cloud as one rule group, replacing the group's rules there.

.DESCRIPTION
    Run by hand after changing the file (observability/README.md). Applying it twice changes nothing: every rule keeps
    its uid. The rules stay editable in Grafana; -Export writes them back over the file afterwards, so the repository
    stays the truth. Notifications follow the stack's default notification policy (its default contact point emails the
    owner).

    It first checks that production's metrics and logs both reach the stack: the rules would otherwise watch nothing,
    and stay silent.

.PARAMETER GrafanaUrl
    The stack's address, e.g. https://<stack>.grafana.net.

.PARAMETER TokenPath
    A file holding a Grafana service account token (role Editor). The token is read from it and never printed.

.PARAMETER Export
    Instead of applying the file, write the group as Grafana has it back over the file, in the form the script reads.
    Grafana's own export (More → Export) is file provisioning's format, which this script doesn't read.
#>
param(
    [Parameter(Mandatory)]
    [string] $GrafanaUrl,

    [string] $TokenPath = (Join-Path $HOME '.machinespirit' 'grafana-alerts-token.txt'),

    [switch] $Export
)

$ErrorActionPreference = 'Stop'

$folderUid = 'daily-machine-spirit'
$folderTitle = 'Daily Machine Spirit'
$rulesPath = Join-Path $PSScriptRoot '..' 'observability' 'grafana' 'alerts.json'
$grafana = $GrafanaUrl.TrimEnd('/')
$headers = @{ Authorization = "Bearer $((Get-Content -Path $TokenPath -Raw).Trim())" }

# Grafana Cloud names the stack's data sources grafanacloud-<stack>-prom and grafanacloud-<stack>-logs.
$dataSources = Invoke-RestMethod -Uri "$grafana/api/datasources" -Headers $headers
$prometheus = $dataSources | Where-Object { $_.type -eq 'prometheus' -and $_.name -like 'grafanacloud-*-prom' } | Select-Object -First 1
$loki = $dataSources | Where-Object { $_.type -eq 'loki' -and $_.name -like 'grafanacloud-*-logs' } | Select-Object -First 1
if (-not $prometheus -or -not $loki) {
    throw "The stack's grafanacloud-*-prom and grafanacloud-*-logs data sources aren't both in $grafana."
}

$groupTitle = (Get-Content -Path $rulesPath -Raw | ConvertFrom-Json).title
$groupName = [Uri]::EscapeDataString($groupTitle)
$groupUri = "$grafana/api/v1/provisioning/folder/$folderUid/rule-groups/$groupName"

if ($Export) {
    $group = Invoke-RestMethod -Uri $groupUri -Headers $headers
    # Only what the file keeps: the script adds the folder, group and organisation, and Grafana its ids and timestamps.
    $file = [ordered]@{
        title = $group.title
        interval = $group.interval
        rules = @($group.rules | Select-Object -Property * -ExcludeProperty id, orgID, folderUID, ruleGroup, updated, provenance)
    }
    $json = ($file | ConvertTo-Json -Depth 20).Replace($prometheus.uid, '${prometheus}').Replace($loki.uid, '${loki}')
    Set-Content -Path $rulesPath -Value $json
    Write-Host "Exported $($file.rules.Count) alert rules from $folderTitle / $groupTitle to $rulesPath."
    return
}

$query = [Uri]::EscapeDataString('count(last_over_time(target_info{job="machinespirit-app"}[1d]))')
$seen = Invoke-RestMethod -Uri "$grafana/api/datasources/proxy/uid/$($prometheus.uid)/api/v1/query?query=$query" -Headers $headers
if ($seen.data.result.Count -eq 0) {
    throw "No telemetry from machinespirit-app in the last day: check production's OtlpHeaders secret first (infra/README.md)."
}
Write-Host "Production's metrics reach $($prometheus.name)."

# The rare-failure rules count log lines, and find nothing to count (no alert) if logs stopped arriving.
$query = [Uri]::EscapeDataString('sum(count_over_time({service_name="machinespirit-app"}[1d]))')
$logged = Invoke-RestMethod -Uri "$grafana/api/datasources/proxy/uid/$($loki.uid)/loki/api/v1/query?query=$query" -Headers $headers
if ($logged.data.result.Count -eq 0) {
    throw "No logs from machinespirit-app in the last day: check production's OtlpHeaders token can write logs (logs:write)."
}
Write-Host "Production's logs reach $($loki.name)."

# Looked for among the folders the token sees: asked for by its uid, a folder that doesn't exist yet answers 403 (no
# permission on it), not 404.
$folders = Invoke-RestMethod -Uri "$grafana/api/folders?limit=1000" -Headers $headers
if (-not ($folders | Where-Object { $_.uid -eq $folderUid })) {
    $folder = @{ uid = $folderUid; title = $folderTitle } | ConvertTo-Json
    Invoke-RestMethod -Method Post -Uri "$grafana/api/folders" -Headers $headers -ContentType 'application/json' -Body $folder | Out-Null
    Write-Host "Created the folder $folderTitle."
}

$group = (Get-Content -Path $rulesPath -Raw).Replace('${prometheus}', $prometheus.uid).Replace('${loki}', $loki.uid) | ConvertFrom-Json
foreach ($rule in $group.rules) {
    $rule | Add-Member -NotePropertyName folderUID -NotePropertyValue $folderUid
    $rule | Add-Member -NotePropertyName ruleGroup -NotePropertyValue $group.title
    $rule | Add-Member -NotePropertyName orgID -NotePropertyValue 1
}
$group | Add-Member -NotePropertyName folderUid -NotePropertyValue $folderUid
$body = $group | ConvertTo-Json -Depth 20

# Without X-Disable-Provenance, rules put through the API can't be edited in Grafana's UI.
Invoke-RestMethod -Method Put -Uri $groupUri `
    -Headers ($headers + @{ 'X-Disable-Provenance' = 'true' }) -ContentType 'application/json' -Body $body | Out-Null
Write-Host "Applied $($group.rules.Count) alert rules to $folderTitle / $($group.title)."
