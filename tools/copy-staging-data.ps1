<#
.SYNOPSIS
    Copies staging's data from the shared Cosmos DB account to staging's own serverless account (#129), once.

.DESCRIPTION
    Run by hand after the infra deploy that creates staging's account (infra/README.md). It reads every document of
    staging's database on the shared account and writes it, as it was, to the same database on staging's account:
    rites, drafts, the Record, the Precepts, the Scribes' names, votes. Running it again writes the same documents over
    themselves, so it can be repeated if it stops halfway. It signs in as your `az login` (the owner's data role on both
    accounts, infra/) and reads staging's database alone: it refuses any database or account not named for staging, so
    production's data is never read.

.PARAMETER SourceAccount
    The shared, free-tier account staging's database was on.

.PARAMETER TargetAccount
    Staging's own serverless account.

.PARAMETER Database
    Staging's database, by the same name on both accounts.
#>
param(
    [string] $SourceAccount = 'machinespirit-cosmos',
    [string] $TargetAccount = 'machinespirit-cosmos-staging',
    [string] $Database = 'machinespirit-staging'
)

$ErrorActionPreference = 'Stop'

if ($Database -notlike '*-staging' -or $TargetAccount -notlike '*-staging') {
    throw "Only staging's database is copied, to staging's account: $Database to $TargetAccount"
}

# One token serves every account: Entra ID's audience for Cosmos DB's data plane. It's kept in memory, never printed.
$token = az account get-access-token --resource 'https://cosmos.azure.com' --query accessToken -o tsv
if (-not $token) {
    throw 'No token for Cosmos DB: run az login first.'
}

function Invoke-Cosmos([string] $account, [string] $path, [hashtable] $headers, [string] $body) {
    $all = @{
        Authorization  = [Uri]::EscapeDataString("type=aad&ver=1.0&sig=$token")
        'x-ms-version' = '2018-12-31'
        'x-ms-date'    = [DateTime]::UtcNow.ToString('r')
    } + $headers
    # Throttled (429): wait as long as Cosmos DB asks, then again.
    while ($true) {
        try {
            return Invoke-WebRequest -Method Post -Uri "https://$account.documents.azure.com/$path" -Headers $all -Body $body `
                -ContentType $headers['Content-Type'] -UseBasicParsing
        }
        catch {
            $response = $_.Exception.Response
            if ($response -and [int]$response.StatusCode -eq 429) {
                Start-Sleep -Milliseconds ([int]($response.Headers.GetValues('x-ms-retry-after-ms') | Select-Object -First 1))
                continue
            }
            throw
        }
    }
}

$documents = "dbs/$Database/colls/rites/docs"
# What Cosmos DB adds to every document; the target adds its own.
$system = '_rid', '_self', '_etag', '_attachments', '_ts'
$copied = 0
$continuation = $null
do {
    $headers = @{
        'Content-Type'                               = 'application/query+json'
        'x-ms-documentdb-isquery'                    = 'True'
        'x-ms-documentdb-query-enablecrosspartition' = 'True'
        'x-ms-max-item-count'                        = '100'
    }
    if ($continuation) {
        $headers['x-ms-continuation'] = $continuation
    }
    $page = Invoke-Cosmos $SourceAccount $documents $headers '{"query":"SELECT * FROM c","parameters":[]}'
    # JsonNode, not ConvertFrom-Json: that would turn dates into DateTime and write them back in another format,
    # changing the stored strings the app sorts by.
    foreach ($document in [System.Text.Json.Nodes.JsonNode]::Parse($page.Content)['Documents'].AsArray()) {
        foreach ($name in $system) {
            $document.AsObject().Remove($name) | Out-Null
        }
        $upsert = @{
            'Content-Type'                 = 'application/json'
            'x-ms-documentdb-is-upsert'    = 'True'
            'x-ms-documentdb-partitionkey' = "[`"$($document['partition'].GetValue[string]())`"]"
        }
        Invoke-Cosmos $TargetAccount $documents $upsert $document.ToJsonString() | Out-Null
        $copied++
    }
    $continuation = $page.Headers['x-ms-continuation'] | Select-Object -First 1
} while ($continuation)

Write-Host "Copied $copied documents of $Database from $SourceAccount to $TargetAccount."
