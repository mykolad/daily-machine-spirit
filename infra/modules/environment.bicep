// One environment (staging or production): its Cosmos DB database, Key Vault, the identity GitHub deploys the app with,
// and its Functions app (app.bicep). Each app reaches only its own environment's database, vault and storage.
// Production has a second app on standby in another region, behind Traffic Manager (#113).
param location string
param prefix string
@allowed(['staging', 'production'])
param environmentName string
@description('Added to the names of resources that need one per environment, e.g. "-staging"; empty for production.')
param nameSuffix string
@description('Storage account names allow only lowercase letters and digits, up to 24.')
param storageAccountName string
@description('The only IPv4 address the app admits (staging); empty sets no such rule.')
// Secure, so the nested deployment's history masks it too (secure doesn't carry across module boundaries).
@secure()
param allowedIp string
@description('Whether the app admits only Cloudflare (production), whose address ranges are in infra/cloudflare-ips.json.')
param admitsOnlyCloudflare bool
param cosmosAccountName string
// What the database reserves, shared by its container; 0 for a serverless account, which reserves none.
param databaseThroughput int
param aiAccountName string
param ownerPrincipalId string
param githubSubjectPrefix string
@description('Lets whoever reaches the Scriptorium in without a sign-in: only where the app admits its owner\'s address alone (staging).')
param scriptoriumWithoutSignIn bool
@description('The Cloudflare Access team and application guarding the Scriptorium (CloudflareAccessOptions); empty until set up.')
param cloudflareAccessTeamDomain string
param cloudflareAccessAudience string
@description('The Access application on /scriptorium/expurgate*, whose policy admits the Expurgators; empty, nobody may expurgate.')
param cloudflareAccessExpurgatorAudience string
@description('The domain readers reach the app at, through Cloudflare (production); empty for none.')
param customDomain string
@description('The name of the vault secret holding the Cloudflare Origin CA certificate for the domain, a PFX (infra/README.md).')
param customDomainCertificate string
@description('The object id, in this tenant, of the App Service service principal (Microsoft.Azure.WebSites): it imports the domain certificate.')
param appServicePrincipalId string
@description('The GitHub OAuth app readers sign in with to vote (GitHubOptions); empty until it exists. Not the deploy identity.')
param signInGitHubClientId string
@description('Where GitHub sends readers back after signing in: exactly the OAuth app\'s callback URL.')
param signInCallbackUrl string
@description('The Cloudflare zone readers reach the site through, whose cached pages the app purges when they change (CloudflareOptions); empty where nothing caches the pages.')
param cloudflareZoneId string
@description('The address of the site in that zone, e.g. https://example.com.')
param cloudflareSiteUrl string
@description('Whether the smoke tests vote as a test voter here, reading the session secret as the GitHub deploy identity. Never in production, whose counts are public.')
param smokeTestsVote bool
@description('The storage account outside the app region that keeps the snapshot of the rites (#112), where this environment has a container of its own.')
param snapshotAccountName string
@description('The region of a second app on standby, which Traffic Manager sends readers to while the first doesn\'t answer (#113); empty for none. The database must have a replica there.')
param standbyLocation string
@description('The standby app\'s storage account, by the same rules as storageAccountName; unused without a standby.')
param standbyStorageAccountName string
@description('The MCP connector\'s storage account (#119), and its standby\'s (unused without a standby), by the same rules as storageAccountName.')
param connectorStorageAccountName string
param standbyConnectorStorageAccountName string
@description('The domain clients reach the connector at, through Cloudflare (production); empty for none.')
param connectorDomain string
@description('Where the connector reads the public rites API, e.g. https://dailymachinespirit.fyi/.')
param ritesApiBaseUrl string

var keyVaultSecretsOfficer = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'
var keyVaultSecretsUser = '4633458b-17de-408a-b874-0445c86b69e6'
var appName = '${prefix}-app${nameSuffix}'
var databaseName = '${prefix}${nameSuffix}'
var hasStandby = !empty(standbyLocation)
var standbyAppName = '${appName}-${standbyLocation}'
var connectorAppName = '${prefix}-mcp${nameSuffix}'
// The Grafana Cloud stack's OTLP gateway (its region, not a secret).
var otlpEndpoint = 'https://otlp-gateway-prod-eu-north-0.grafana.net/otlp'
// Cosmos DB's names for the regions (CosmosOptions.Region), which differ from Azure's locations.
var cosmosRegions = {
  swedencentral: 'Sweden Central'
  uksouth: 'UK South'
}
// The timers the standby leaves to the first app, which still runs a midnight it missed once it's back (the host's
// schedule monitoring). Each is safe twice, guarded by its ETags, but a second publishing would also ask for a second
// refill, and a second distillation would pay the models again.
var standbyTimers = ['DailyRite', 'DistilPrecepts', 'WriteRiteSnapshot']

resource cosmos 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' existing = {
  name: cosmosAccountName
}

resource ai 'Microsoft.CognitiveServices/accounts@2025-06-01' existing = {
  name: aiAccountName
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-11-15' = {
  parent: cosmos
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
    options: databaseThroughput > 0 ? { throughput: databaseThroughput } : {}
  }
}

// Matches RiteRepository: one container, partition key /partition.
resource rites 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: database
  name: 'rites'
  properties: {
    resource: {
      id: 'rites'
      partitionKey: {
        paths: ['/partition']
        kind: 'Hash'
        version: 2
      }
    }
  }
}

// A vault per environment, so staging can't read production's secrets. The values (the Jev key, the Grafana token) are
// set by hand (infra/README.md), never in this file.
resource vault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: '${prefix}-kv${nameSuffix}'
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    enablePurgeProtection: true
  }
}

resource ownerSecrets 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: vault
  name: guid(vault.id, ownerPrincipalId, keyVaultSecretsOfficer)
  properties: {
    principalId: ownerPrincipalId
    principalType: 'User'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsOfficer)
  }
}

resource snapshotAccount 'Microsoft.Storage/storageAccounts@2024-01-01' existing = {
  name: snapshotAccountName
}

resource snapshotBlobs 'Microsoft.Storage/storageAccounts/blobServices@2024-01-01' existing = {
  parent: snapshotAccount
  name: 'default'
}

// This environment's copy of its rites (#112), which its app writes and reads back while Cosmos DB doesn't answer.
resource snapshotContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2024-01-01' = {
  parent: snapshotBlobs
  name: 'rites-${environmentName}'
}

// Every app's settings in this environment, but for the host's storage (app.bicep) and what sets a region apart (below).
var settings = concat([
  // Outside the region (resources.bicep): the copy the app reads while Cosmos DB doesn't answer (#112).
  {
    name: 'RiteSnapshot__ContainerUri'
    value: '${snapshotAccount.properties.primaryEndpoints.blob}${snapshotContainer.name}'
  }
  {
    name: 'Cosmos__Endpoint'
    value: cosmos.properties.documentEndpoint
  }
  {
    name: 'Cosmos__Database'
    value: databaseName
  }
  {
    name: 'AzureOpenAI__Endpoint'
    value: ai.properties.endpoint
  }
  {
    // Resolved by the app's identity; until the secret is set (infra/README.md), Jev's calls fail and drafts go without its scores.
    name: 'Jev__ApiKey'
    value: '@Microsoft.KeyVault(VaultName=${vault.name};SecretName=JevApiKey)'
  }
  {
    name: 'Scriptorium__WithoutSignIn'
    value: scriptoriumWithoutSignIn ? 'true' : 'false'
  }
  {
    name: 'CloudflareAccess__TeamDomain'
    value: cloudflareAccessTeamDomain
  }
  {
    name: 'CloudflareAccess__Audience'
    value: cloudflareAccessAudience
  }
  {
    name: 'CloudflareAccess__ExpurgatorAudience'
    value: cloudflareAccessExpurgatorAudience
  }
  // Readers' sign-in with GitHub, to vote. Until the app and the three secrets exist (infra/README.md), the sign-in
  // pages don't either.
  {
    name: 'GitHub__ClientId'
    value: signInGitHubClientId
  }
  {
    name: 'GitHub__ClientSecret'
    value: '@Microsoft.KeyVault(VaultName=${vault.name};SecretName=GitHubClientSecret)'
  }
  {
    name: 'GitHub__CallbackUrl'
    value: signInCallbackUrl
  }
  // Purging pages from Cloudflare's cache when votes or a new rite change them. The token's secret (infra/README.md)
  // is referenced only where there's a zone, so staging has no reference to a secret that doesn't exist.
  {
    name: 'Cloudflare__ZoneId'
    value: cloudflareZoneId
  }
  {
    name: 'Cloudflare__SiteUrl'
    value: cloudflareSiteUrl
  }
  {
    name: 'Cloudflare__ApiToken'
    value: empty(cloudflareZoneId) ? '' : '@Microsoft.KeyVault(VaultName=${vault.name};SecretName=CloudflareApiToken)'
  }
  {
    name: 'Voters__KeySecret'
    value: '@Microsoft.KeyVault(VaultName=${vault.name};SecretName=VoterKeySecret)'
  }
  {
    name: 'Voters__SessionSecret'
    value: '@Microsoft.KeyVault(VaultName=${vault.name};SecretName=VoterSessionSecret)'
  }
], telemetrySettings)

// Telemetry to Grafana Cloud, read by both the Functions host and the app (Program.cs), and by the connector. The
// header carries the stack's token, from the vault (infra/README.md).
var telemetrySettings = [
  {
    name: 'OTEL_EXPORTER_OTLP_ENDPOINT'
    value: otlpEndpoint
  }
  {
    name: 'OTEL_EXPORTER_OTLP_PROTOCOL'
    value: 'http/protobuf'
  }
  {
    name: 'OTEL_EXPORTER_OTLP_HEADERS'
    value: '@Microsoft.KeyVault(VaultName=${vault.name};SecretName=OtlpHeaders)'
  }
]

// What GitHub deploys the environment's apps as: it can change them and nothing else.
resource githubDeployer 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-github-${environmentName}'
  location: location
}

resource githubTrust 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: githubDeployer
  name: 'github-${environmentName}'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: '${githubSubjectPrefix}:environment:${environmentName}'
    audiences: ['api://AzureADTokenExchange']
  }
}

// The smoke tests vote and take the vote back as a test voter, with a cookie they sign themselves (CLAUDE.md,
// "Deploying"): the deploy identity may read the secret that signs it, and no other secret in the vault.
resource voterSessionSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' existing = if (smokeTestsVote) {
  parent: vault
  name: 'VoterSessionSecret'
}

resource githubReadsVoterSessionSecret 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (smokeTestsVote) {
  scope: voterSessionSecret
  name: guid(voterSessionSecret.id, githubDeployer.id, keyVaultSecretsUser)
  properties: {
    principalId: githubDeployer.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUser)
  }
}

// Each app imports the domain's certificate from the vault (app.bicep). The import is done by App Service itself (its
// Microsoft.Azure.WebSites service principal), not the app's identity. That principal serves every App Service app in
// the tenant, so it may read this one secret, not the vault's others (the voter keys, tokens).
resource certificateSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' existing = if (!empty(customDomain)) {
  parent: vault
  name: customDomainCertificate
}

resource appServiceReadsCertificate 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(customDomain)) {
  scope: certificateSecret
  name: guid(certificateSecret.id, appServicePrincipalId, keyVaultSecretsUser)
  properties: {
    principalId: appServicePrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUser)
  }
}

module app 'app.bicep' = {
  name: 'app${nameSuffix}'
  dependsOn: [appServiceReadsCertificate]
  params: {
    location: location
    appName: appName
    planName: '${prefix}-plan${nameSuffix}'
    storageAccountName: storageAccountName
    settings: concat(settings, [
      {
        name: 'Cosmos__Region'
        value: cosmosRegions[location]
      }
      {
        name: 'OTEL_RESOURCE_ATTRIBUTES'
        value: 'deployment.environment.name=${environmentName},cloud.region=${location}'
      }
    ])
    allowedIp: allowedIp
    admitsOnlyCloudflare: admitsOnlyCloudflare
    admitsTrafficManager: hasStandby
    vaultName: vault.name
    aiAccountName: ai.name
    cosmosAccountName: cosmos.name
    databaseName: database.name
    snapshotAccountName: snapshotAccount.name
    snapshotContainerName: snapshotContainer.name
    githubDeployerPrincipalId: githubDeployer.properties.principalId
    customDomain: customDomain
    customDomainCertificate: customDomainCertificate
  }
}

module standby 'app.bicep' = if (hasStandby) {
  name: 'app${nameSuffix}-${standbyLocation}'
  dependsOn: [appServiceReadsCertificate]
  params: {
    location: standbyLocation
    appName: standbyAppName
    planName: '${prefix}-plan${nameSuffix}-${standbyLocation}'
    storageAccountName: standbyStorageAccountName
    settings: concat(settings, [
      {
        name: 'Cosmos__Region'
        value: cosmosRegions[standbyLocation]
      }
      // The first app's service name, so the dashboards and alerts count both; cloud.region tells them apart.
      {
        name: 'OTEL_RESOURCE_ATTRIBUTES'
        value: 'deployment.environment.name=${environmentName},cloud.region=${standbyLocation},service.name=${appName}'
      }
    ], map(standbyTimers, timer => {
      name: 'AzureWebJobs.${timer}.Disabled'
      value: 'true'
    }))
    allowedIp: allowedIp
    admitsOnlyCloudflare: admitsOnlyCloudflare
    admitsTrafficManager: true
    vaultName: vault.name
    aiAccountName: ai.name
    cosmosAccountName: cosmos.name
    databaseName: database.name
    snapshotAccountName: snapshotAccount.name
    snapshotContainerName: snapshotContainer.name
    githubDeployerPrincipalId: githubDeployer.properties.principalId
    customDomain: customDomain
    customDomainCertificate: customDomainCertificate
  }
}

module traffic 'trafficManager.bicep' = if (hasStandby) {
  name: 'traffic${nameSuffix}'
  params: {
    name: '${prefix}-traffic${nameSuffix}'
    relativeName: '${prefix}${nameSuffix}'
    routing: 'Priority'
    // Probes every 30 seconds kept the standby running for nothing (about as many executions as the first app's, all
    // of them probes). Unprobed, it scales to zero, and a failover goes to it anyway, waking it.
    firstAlwaysServes: false
    standbyAlwaysServes: true
    ttl: 60
    firstLocation: location
    firstHost: app.outputs.appHost
    standbyLocation: standbyLocation
    standbyHost: standby!.outputs.appHost
  }
}

// The MCP connector (#119): an app of its own in each of the environment's regions, reading the public rites API.
module connector 'connector.bicep' = {
  name: 'connector${nameSuffix}'
  dependsOn: [appServiceReadsCertificate]
  params: {
    location: location
    appName: connectorAppName
    planName: '${prefix}-plan-mcp${nameSuffix}'
    storageAccountName: connectorStorageAccountName
    ritesApiBaseUrl: ritesApiBaseUrl
    settings: concat(telemetrySettings, [
      {
        name: 'OTEL_RESOURCE_ATTRIBUTES'
        value: 'deployment.environment.name=${environmentName},cloud.region=${location},service.name=${connectorAppName}'
      }
    ])
    allowedIp: allowedIp
    admitsOnlyCloudflare: admitsOnlyCloudflare
    admitsTrafficManager: hasStandby
    githubDeployerPrincipalId: githubDeployer.properties.principalId
    vaultName: vault.name
    customDomain: connectorDomain
    customDomainCertificate: customDomainCertificate
  }
}

module standbyConnector 'connector.bicep' = if (hasStandby) {
  name: 'connector${nameSuffix}-${standbyLocation}'
  dependsOn: [appServiceReadsCertificate]
  params: {
    location: standbyLocation
    appName: '${connectorAppName}-${standbyLocation}'
    planName: '${prefix}-plan-mcp${nameSuffix}-${standbyLocation}'
    storageAccountName: standbyConnectorStorageAccountName
    ritesApiBaseUrl: ritesApiBaseUrl
    // The first region's service name, as for the site's standby: one service, told apart by cloud.region.
    settings: concat(telemetrySettings, [
      {
        name: 'OTEL_RESOURCE_ATTRIBUTES'
        value: 'deployment.environment.name=${environmentName},cloud.region=${standbyLocation},service.name=${connectorAppName}'
      }
    ])
    allowedIp: allowedIp
    admitsOnlyCloudflare: admitsOnlyCloudflare
    admitsTrafficManager: true
    githubDeployerPrincipalId: githubDeployer.properties.principalId
    vaultName: vault.name
    customDomain: connectorDomain
    customDomainCertificate: customDomainCertificate
  }
}

module connectorTraffic 'trafficManager.bicep' = if (hasStandby) {
  name: 'traffic-mcp${nameSuffix}'
  params: {
    name: '${prefix}-traffic-mcp${nameSuffix}'
    relativeName: '${prefix}-mcp${nameSuffix}'
    // Both regions at once: the connector keeps nothing and reads the public API, so either answers the same. Neither
    // is probed: probes every 30 seconds would cost far more than its few users' calls, so both scale to zero. A
    // region's outage fails about half the calls until its endpoint is disabled; Grafana's synthetic check shows it
    // (infra/README.md, "The MCP connector").
    routing: 'Weighted'
    firstAlwaysServes: true
    standbyAlwaysServes: true
    // Short, so a client's retry soon gets a fresh pick, an even chance of the other region; Traffic Manager's charge
    // per DNS query is a fraction of a cent at the connector's few calls.
    ttl: 10
    firstLocation: location
    firstHost: connector.outputs.appHost
    standbyLocation: standbyLocation
    standbyHost: standbyConnector!.outputs.appHost
  }
}

output appHost string = app.outputs.appHost
output standbyAppHost string = hasStandby ? standby!.outputs.appHost : ''
output trafficManagerHost string = hasStandby ? traffic!.outputs.host : ''
output connectorHost string = connector.outputs.appHost
output standbyConnectorHost string = hasStandby ? standbyConnector!.outputs.appHost : ''
output connectorTrafficManagerHost string = hasStandby ? connectorTraffic!.outputs.host : ''
output githubClientId string = githubDeployer.properties.clientId
