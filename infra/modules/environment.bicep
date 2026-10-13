// One environment (staging or production): its Cosmos DB database, Key Vault, storage, Functions app, and the identity
// GitHub deploys the app with. Each app reaches only its own database, vault and storage.
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
param aiAccountName string
param ownerPrincipalId string
param githubSubjectPrefix string
@description('Whether the midnight timer publishes a rite (Generation:PublishDaily): off for production until launch.')
param publishesDaily bool
@description('Whether the app serves the Scriptorium, the moderators\' page. Only where the page is already restricted: staging\'s IP rule, or Cloudflare Access in production.')
param scriptoriumEnabled bool
@description('Lets whoever reaches the Scriptorium in without a sign-in: only where the app admits its owner\'s address alone (staging).')
param scriptoriumWithoutSignIn bool
@description('The Cloudflare Access team and application guarding the Scriptorium (CloudflareAccessOptions); empty until set up.')
param cloudflareAccessTeamDomain string
param cloudflareAccessAudience string
@description('The domain readers reach the app at, through Cloudflare (production); empty for none.')
param customDomain string
@description('The name of the vault secret holding the Cloudflare Origin CA certificate for the domain, a PFX (infra/README.md).')
param customDomainCertificate string
@description('The GitHub OAuth app readers sign in with to vote (GitHubOptions); empty until it exists. Not the deploy identity.')
param signInGitHubClientId string
@description('Where GitHub sends readers back after signing in: exactly the OAuth app\'s callback URL.')
param signInCallbackUrl string
@description('The Cloudflare zone readers reach the site through, whose cached pages the app purges when they change (CloudflareOptions); empty where nothing caches the pages.')
param cloudflareZoneId string
@description('The address of the site in that zone, e.g. https://example.com.')
param cloudflareSiteUrl string

var keyVaultSecretsOfficer = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'
var appName = '${prefix}-app${nameSuffix}'
var databaseName = '${prefix}${nameSuffix}'
var deploymentContainer = 'deployments'
// The Grafana Cloud stack's OTLP gateway (its region, not a secret).
var otlpEndpoint = 'https://otlp-gateway-prod-eu-north-0.grafana.net/otlp'
// Readers reach production through Cloudflare alone, so its cache and rate limit can't be gone around. Cloudflare
// publishes its ranges, and a weekly workflow (Check Cloudflare's Addresses) says when they change.
var cloudflare = loadJsonContent('../cloudflare-ips.json')
var cloudflareRules = [for (range, index) in concat(cloudflare.ipv4, cloudflare.ipv6): {
  name: 'cloudflare-${index}'
  ipAddress: range
  action: 'Allow'
  priority: 200 + index
}]
var ownerRules = empty(allowedIp) ? [] : [
  {
    name: 'owner'
    ipAddress: '${allowedIp}/32'
    action: 'Allow'
    priority: 100
  }
]
var accessRules = concat(ownerRules, admitsOnlyCloudflare ? cloudflareRules : [])

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
    options: {
      throughput: 400
    }
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

// The Functions host's own storage (timer leases, the deployment package). Keys are off: the app uses its identity.
resource storage 'Microsoft.Storage/storageAccounts@2024-01-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    allowSharedKeyAccess: false
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource blobs 'Microsoft.Storage/storageAccounts/blobServices@2024-01-01' = {
  parent: storage
  name: 'default'
}

resource deployments 'Microsoft.Storage/storageAccounts/blobServices/containers@2024-01-01' = {
  parent: blobs
  name: deploymentContainer
}

resource queues 'Microsoft.Storage/storageAccounts/queueServices@2024-01-01' = {
  parent: storage
  name: 'default'
}

// Asks for drafts to be written (RefillBacklogFunction.QueueName): after each publishing, and when the Scribes summon.
resource backlogRefills 'Microsoft.Storage/storageAccounts/queueServices/queues@2024-01-01' = {
  parent: queues
  name: 'backlog-refills'
}

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: '${prefix}-plan${nameSuffix}'
  location: location
  kind: 'functionapp'
  sku: {
    tier: 'FlexConsumption'
    name: 'FC1'
  }
  properties: {
    reserved: true
  }
}

resource app 'Microsoft.Web/sites@2024-04-01' = {
  name: appName
  location: location
  kind: 'functionapp,linux'
  // Flex Consumption needs the deployment container to exist when the app is created, and the URL below only names it.
  dependsOn: [deployments]
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storage.properties.primaryEndpoints.blob}${deploymentContainer}'
          authentication: {
            type: 'SystemAssignedIdentity'
          }
        }
      }
      scaleAndConcurrency: {
        // The smallest instance, and a cap on scale-out, so a traffic spike can't run up the bill.
        instanceMemoryMB: 512
        maximumInstanceCount: 10
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
    }
    siteConfig: {
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      ipSecurityRestrictionsDefaultAction: empty(accessRules) ? 'Allow' : 'Deny'
      ipSecurityRestrictions: accessRules
      // Deploys go to the SCM site from GitHub's runners, whose addresses change, so the IP rule applies to visitors
      // only. The SCM site takes Entra ID alone: basic-auth publishing credentials are off (below).
      scmIpSecurityRestrictionsUseMain: false
      appSettings: [
        {
          name: 'AzureWebJobsStorage__accountName'
          value: storage.name
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
          name: 'Generation__PublishDaily'
          value: publishesDaily ? 'true' : 'false'
        }
        {
          name: 'Scriptorium__Enabled'
          value: scriptoriumEnabled ? 'true' : 'false'
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
        // Telemetry to Grafana Cloud, read by both the Functions host and the app (Program.cs). The header carries the
        // stack's token, from the vault (infra/README.md).
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
        {
          name: 'OTEL_RESOURCE_ATTRIBUTES'
          value: 'deployment.environment.name=${environmentName}'
        }
      ]
    }
  }
}

resource noBasicAuthScm 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = {
  parent: app
  name: 'scm'
  properties: {
    allow: false
  }
}

resource noBasicAuthFtp 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = {
  parent: app
  name: 'ftp'
  properties: {
    allow: false
  }
}

module appAccess 'appAccess.bicep' = {
  name: 'app-access${nameSuffix}'
  params: {
    principalId: app.identity.principalId
    storageAccountName: storage.name
    vaultName: vault.name
    aiAccountName: ai.name
    cosmosAccountName: cosmos.name
    databaseName: database.name
  }
}

// What GitHub deploys this app's code as: it can change this app and nothing else.
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

// The domain, through Cloudflare: Cloudflare's Origin CA certificate, imported from the vault by the app's own identity
// (Key Vault Secrets User, appAccess), and bound to the name. Azure checks the domain's asuid TXT record first, so the
// DNS comes before this (infra/README.md).
resource domainCertificate 'Microsoft.Web/sites/certificates@2024-11-01' = if (!empty(customDomain)) {
  parent: app
  name: customDomainCertificate
  location: location
  dependsOn: [appAccess]
  properties: {
    keyVaultId: vault.id
    keyVaultSecretName: customDomainCertificate
  }
}

resource domainBinding 'Microsoft.Web/sites/hostNameBindings@2024-11-01' = if (!empty(customDomain)) {
  parent: app
  name: customDomain
  properties: {
    siteName: app.name
    hostNameType: 'Verified'
    sslState: 'SniEnabled'
    thumbprint: domainCertificate!.properties.thumbprint
  }
}

module githubDeploysApp 'appDeployerAccess.bicep' = {
  name: 'app-deployer-access${nameSuffix}'
  params: {
    principalId: githubDeployer.properties.principalId
    appName: app.name
  }
}

output appHost string = app.properties.defaultHostName
output githubClientId string = githubDeployer.properties.clientId
