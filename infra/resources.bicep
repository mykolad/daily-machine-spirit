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
// Gates the production module alone: off by default, so a change to an environment's own resources reaches staging
// first, and production's are left as they are (nothing deleted, nothing updated). The shared resources above (the
// models, Cosmos DB, the infrastructure identity) are deployed either way, and production uses them too.
param deployProduction bool = false
// Where the rites' snapshot is kept (#112): outside the app's region, so an outage there leaves the copy readable, and
// outside North Europe, the second region planned for the app (#113), so the copy outlives an outage of either.
param snapshotLocation string = 'germanywestcentral'

var roles = {
  cognitiveServicesOpenAiUser: '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
}

// The Cloudflare Access application guarding production's Scriptorium (infra/README.md, "Outside Azure"). Neither
// value is secret, and keeping them here, not in parameters, means no deployment can drop them by leaving one out.
var productionAccess = {
  teamDomain: 'restless-river-ab5e.cloudflareaccess.com'
  audience: '3fac1c3c199e53f8242ae90f5c539f443843eaeff7a9d7ece38afe992f18d1ef'
  // The Expurgators' own application (infra/README.md), on /scriptorium/expurgate*: its policy admits them.
  expurgatorAudience: 'cb167b1fdfb104ff7bd074d8a4ca986b20014ebd48b5d68c1d465811650fe5d7'
}

// The GitHub OAuth apps readers sign in with to vote, one per environment, since each has its own callback URL
// (infra/README.md). A client id isn't secret; the client secrets are in the vaults.
// Where readers reach production, through Cloudflare, and the vault secret holding Cloudflare's Origin CA certificate
// for it (infra/README.md, "Outside Azure").
var productionDomain = {
  name: 'dailymachinespirit.fyi'
  certificateSecret: 'dailymachinespirit-origin'
  // Cloudflare dashboard → the domain → Overview → Zone ID; not secret. The app purges the zone's cached pages with it.
  zoneId: '143ab86fe8cf72cb735e20bfaac6ebcc'
}

// App Service's own service principal (Microsoft.Azure.WebSites, app id abfa0a7c-a6b6-4736-8310-5855508787cd): it
// imports a domain's certificate from the vault. Its object id differs per tenant: az ad sp show --id <app id>.
var appServicePrincipalId = '2247c71e-28bc-459c-9559-a0aa439217f4'

var signInGitHubClientIds = {
  staging: 'Ov23liMxz7fLNPla4SU6'
  production: 'Ov23liuQFkbEDRwqBWq6'
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

// One free-tier account per subscription: 1000 RU/s and 25 GB free for its lifetime. Production's database takes
// 400 RU/s of it, and so would each region it's replicated to (#113), so staging has an account of its own (below). The
// free tier can only be chosen when the account is created.
resource cosmos 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' = {
  name: '${prefix}-cosmos'
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    enableFreeTier: true
    disableLocalAuth: true
    // Azure's default for new accounts; with one region it does nothing, but leaving it out would turn it off.
    enableAutomaticFailover: true
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

// Staging's own account (#129), serverless: no throughput reserved, only the request units it uses (cents a month, a
// load test included), and none of the free tier's, which production keeps for its regions. Serverless has one region.
resource stagingCosmos 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' = {
  name: '${prefix}-cosmos-staging'
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
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

// The owner reads and writes every database (local development, the portal's Data Explorer, copying staging's data).
resource ownerData 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-11-15' = {
  parent: cosmos
  name: guid(cosmos.id, ownerPrincipalId, 'data-contributor')
  properties: {
    principalId: ownerPrincipalId
    roleDefinitionId: '${cosmos.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
    scope: cosmos.id
  }
}

resource ownerStagingData 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-11-15' = {
  parent: stagingCosmos
  name: guid(stagingCosmos.id, ownerPrincipalId, 'data-contributor')
  properties: {
    principalId: ownerPrincipalId
    roleDefinitionId: '${stagingCosmos.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
    scope: stagingCosmos.id
  }
}

// The rites' snapshot (#112): every published rite as one blob per environment, which the app reads while Cosmos DB
// doesn't answer. Outside the app's region, so the copy outlives an outage there. Each environment's container and the
// app's right to write it are in modules/environment.bicep.
resource snapshots 'Microsoft.Storage/storageAccounts@2024-01-01' = {
  name: '${prefix}snapshots'
  location: snapshotLocation
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

resource snapshotBlobs 'Microsoft.Storage/storageAccounts/blobServices@2024-01-01' = {
  parent: snapshots
  name: 'default'
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
  // The snapshot's blob service holds each environment's container.
  dependsOn: [snapshotBlobs]
  params: {
    location: location
    prefix: prefix
    environmentName: 'staging'
    nameSuffix: '-staging'
    storageAccountName: '${prefix}staging'
    allowedIp: stagingAllowedIp
    admitsOnlyCloudflare: false
    cosmosAccountName: stagingCosmos.name
    // Serverless (stagingCosmos): no throughput to reserve.
    databaseThroughput: 0
    aiAccountName: ai.name
    ownerPrincipalId: ownerPrincipalId
    githubSubjectPrefix: githubSubjectPrefix
    scriptoriumWithoutSignIn: true
    cloudflareAccessTeamDomain: ''
    cloudflareAccessAudience: ''
    cloudflareAccessExpurgatorAudience: ''
    signInGitHubClientId: signInGitHubClientIds.staging
    signInCallbackUrl: 'https://${prefix}-app-staging.azurewebsites.net/auth/github/callback'
    customDomain: ''
    customDomainCertificate: ''
    cloudflareZoneId: ''
    cloudflareSiteUrl: ''
    appServicePrincipalId: appServicePrincipalId
    smokeTestsVote: true
    snapshotAccountName: snapshots.name
  }
}

module production 'modules/environment.bicep' = if (deployProduction) {
  name: 'production'
  dependsOn: [snapshotBlobs]
  params: {
    location: location
    prefix: prefix
    environmentName: 'production'
    nameSuffix: ''
    storageAccountName: '${prefix}prod'
    allowedIp: ''
    admitsOnlyCloudflare: true
    cosmosAccountName: cosmos.name
    databaseThroughput: 400
    aiAccountName: ai.name
    ownerPrincipalId: ownerPrincipalId
    githubSubjectPrefix: githubSubjectPrefix
    // Scribes sign in with Cloudflare Access (productionAccess): the app checks its token on every Scriptorium request.
    scriptoriumWithoutSignIn: false
    cloudflareAccessTeamDomain: productionAccess.teamDomain
    cloudflareAccessAudience: productionAccess.audience
    cloudflareAccessExpurgatorAudience: productionAccess.expurgatorAudience
    signInGitHubClientId: signInGitHubClientIds.production
    signInCallbackUrl: 'https://${productionDomain.name}/auth/github/callback'
    customDomain: productionDomain.name
    customDomainCertificate: productionDomain.certificateSecret
    cloudflareZoneId: productionDomain.zoneId
    cloudflareSiteUrl: 'https://${productionDomain.name}'
    appServicePrincipalId: appServicePrincipalId
    // A test vote would show in the public counts.
    smokeTestsVote: false
    snapshotAccountName: snapshots.name
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
