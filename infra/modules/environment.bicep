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
param allowedIp string
param cosmosAccountName string
param aiAccountName string
param ownerPrincipalId string
param githubSubjectPrefix string

var roles = {
  storageBlobDataOwner: 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
  storageQueueDataContributor: '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
  storageTableDataContributor: '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
  keyVaultSecretsUser: '4633458b-17de-408a-b874-0445c86b69e6'
  keyVaultSecretsOfficer: 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'
  cognitiveServicesOpenAiUser: '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
  websiteContributor: 'de139f84-1756-47ae-9be6-808fbbe84772'
}
var appName = '${prefix}-app${nameSuffix}'
var databaseName = '${prefix}${nameSuffix}'
var deploymentContainer = 'deployments'

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

// Matches RiteRepository: one container, partition key /pk.
resource rites 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: database
  name: 'rites'
  properties: {
    resource: {
      id: 'rites'
      partitionKey: {
        paths: ['/pk']
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
  name: guid(vault.id, ownerPrincipalId, roles.keyVaultSecretsOfficer)
  properties: {
    principalId: ownerPrincipalId
    principalType: 'User'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsOfficer)
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

resource appStorage 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storage
  name: guid(storage.id, app.id, roles.storageBlobDataOwner)
  properties: {
    principalId: app.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.storageBlobDataOwner)
  }
}

// The Functions host also keeps queues and tables in its storage, besides the blob leases and deployment packages.
resource appStorageQueues 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storage
  name: guid(storage.id, app.id, roles.storageQueueDataContributor)
  properties: {
    principalId: app.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.storageQueueDataContributor)
  }
}

resource appStorageTables 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storage
  name: guid(storage.id, app.id, roles.storageTableDataContributor)
  properties: {
    principalId: app.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.storageTableDataContributor)
  }
}

resource appSecrets 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: vault
  name: guid(vault.id, app.id, roles.keyVaultSecretsUser)
  properties: {
    principalId: app.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsUser)
  }
}

resource appModels 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: ai
  name: guid(ai.id, app.id, roles.cognitiveServicesOpenAiUser)
  properties: {
    principalId: app.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.cognitiveServicesOpenAiUser)
  }
}

// Data Contributor on this environment's database only.
resource appData 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-11-15' = {
  parent: cosmos
  name: guid(cosmos.id, app.id, 'data-contributor')
  properties: {
    principalId: app.identity.principalId
    roleDefinitionId: '${cosmos.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
    scope: '${cosmos.id}/dbs/${database.name}'
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

resource githubDeploysApp 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: app
  name: guid(app.id, githubDeployer.id, roles.websiteContributor)
  properties: {
    principalId: githubDeployer.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.websiteContributor)
  }
}

output appHost string = app.properties.defaultHostName
output githubClientId string = githubDeployer.properties.clientId
