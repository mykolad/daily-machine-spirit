// Everything in the resource group: what both environments share (the models, the Cosmos DB account, the identity that
// deploys this file) and one module per environment. main.bicep deploys it the first time (with the group); routine
// changes deploy it into the existing group, which is all the infrastructure identity may do.
param location string = resourceGroup().location
param prefix string = 'machinespirit'
param ownerPrincipalId string
// Required: an empty value would leave staging open to everyone (empty means "no restriction" only for production).
// Secure, so deployment history and CLI output mask it: it identifies the owner.
@secure()
@minLength(7)
param stagingAllowedIp string
param githubSubjectPrefix string = 'repo:mykolad@2202717/daily-machine-spirit@1406418170'
// Off until launch (see main.bicep). Leaving it off later doesn't delete production, but stops updating it.
param deployProduction bool = false

var roles = {
  cognitiveServicesOpenAiUser: '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
}

// The models. Keys are off: the apps and the owner call them with Entra ID.
resource ai 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: '${prefix}-ai'
  location: location
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: '${prefix}-ai'
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
  }
}

// Sol writes the rites; Luna is the fallback when Sol fails. Capacity is in thousands of tokens a minute: one rite a day
// needs a tiny fraction.
resource sol 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: ai
  name: 'gpt-6-sol'
  sku: {
    name: 'GlobalStandard'
    capacity: 10
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-6-sol'
      version: '2026-09-22'
    }
  }
}

resource luna 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: ai
  name: 'gpt-6-luna'
  // An account takes one deployment change at a time.
  dependsOn: [sol]
  sku: {
    name: 'GlobalStandard'
    capacity: 10
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-6-luna'
      version: '2026-09-22'
    }
  }
}

resource ownerModels 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: ai
  name: guid(ai.id, ownerPrincipalId, roles.cognitiveServicesOpenAiUser)
  properties: {
    principalId: ownerPrincipalId
    principalType: 'User'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.cognitiveServicesOpenAiUser)
  }
}

// One free-tier account per subscription: 1000 RU/s and 25 GB free for its lifetime. Each environment's database takes
// 400 RU/s of it. The free tier can only be chosen when the account is created.
resource cosmos 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' = {
  name: '${prefix}-cosmos'
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    enableFreeTier: true
    disableLocalAuth: true
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
  }
}

// The owner reads and writes both databases (local development, the portal's Data Explorer).
resource ownerData 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-11-15' = {
  parent: cosmos
  name: guid(cosmos.id, ownerPrincipalId, 'data-contributor')
  properties: {
    principalId: ownerPrincipalId
    roleDefinitionId: '${cosmos.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
    scope: cosmos.id
  }
}

// What the infrastructure workflow logs in as to deploy this file. Owner of this resource group only, because the file
// assigns roles; the "infrastructure" GitHub environment controls when it can be used.
resource infrastructureDeployer 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-github-infrastructure'
  location: location
}

resource infrastructureDeployerTrust 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: infrastructureDeployer
  name: 'github-infrastructure'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: '${githubSubjectPrefix}:environment:infrastructure'
    audiences: ['api://AzureADTokenExchange']
  }
}

module infrastructureDeployerOwner 'modules/resourceGroupOwner.bicep' = {
  name: 'infrastructure-deployer-owner'
  params: {
    principalId: infrastructureDeployer.properties.principalId
  }
}

module staging 'modules/environment.bicep' = {
  name: 'staging'
  params: {
    location: location
    prefix: prefix
    environmentName: 'staging'
    nameSuffix: '-staging'
    storageAccountName: '${prefix}staging'
    allowedIp: stagingAllowedIp
    cosmosAccountName: cosmos.name
    aiAccountName: ai.name
    ownerPrincipalId: ownerPrincipalId
    githubSubjectPrefix: githubSubjectPrefix
  }
}

module production 'modules/environment.bicep' = if (deployProduction) {
  name: 'production'
  params: {
    location: location
    prefix: prefix
    environmentName: 'production'
    nameSuffix: ''
    storageAccountName: '${prefix}prod'
    allowedIp: ''
    cosmosAccountName: cosmos.name
    aiAccountName: ai.name
    ownerPrincipalId: ownerPrincipalId
    githubSubjectPrefix: githubSubjectPrefix
  }
}

output stagingAppHost string = staging.outputs.appHost
output productionAppHost string = deployProduction ? production!.outputs.appHost : ''
output githubClientIds object = union(
  {
    infrastructure: infrastructureDeployer.properties.clientId
    staging: staging.outputs.githubClientId
  },
  deployProduction ? { production: production!.outputs.githubClientId } : {}
)
