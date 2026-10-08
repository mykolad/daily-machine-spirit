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
@description('The only IPv4 address the app admits; empty admits everyone (production sits behind Cloudflare).')
// Secure, so the nested deployment's history masks it too (secure doesn't carry across module boundaries).
@secure()
param allowedIp string
param cosmosAccountName string
param aiAccountName string
param ownerPrincipalId string
param githubSubjectPrefix string
@description('Whether the app serves the Scriptorium, the moderators\' page. Only where the page is already restricted: staging\'s IP rule, or Cloudflare Access in production.')
param scriptoriumEnabled bool

var keyVaultSecretsOfficer = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'
var appName = '${prefix}-app${nameSuffix}'
var databaseName = '${prefix}${nameSuffix}'
var deploymentContainer = 'deployments'
// The Grafana Cloud stack's OTLP gateway (its region, not a secret).
var otlpEndpoint = 'https://otlp-gateway-prod-eu-north-0.grafana.net/otlp'

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
      ipSecurityRestrictionsDefaultAction: empty(allowedIp) ? 'Allow' : 'Deny'
      ipSecurityRestrictions: empty(allowedIp) ? [] : [
        {
          name: 'owner'
          ipAddress: '${allowedIp}/32'
          action: 'Allow'
          priority: 100
        }
      ]
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
          name: 'Scriptorium__Enabled'
          value: scriptoriumEnabled ? 'true' : 'false'
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

module githubDeploysApp 'appDeployerAccess.bicep' = {
  name: 'app-deployer-access${nameSuffix}'
  params: {
    principalId: githubDeployer.properties.principalId
    appName: app.name
  }
}

output appHost string = app.properties.defaultHostName
output githubClientId string = githubDeployer.properties.clientId
