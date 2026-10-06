// The whole Azure side of the site: its resource group and everything in it. Deployed at subscription scope because it
// creates the resource group; see infra/README.md for the first deployment and the steps Bicep can't do.
targetScope = 'subscription'

@description('The region for everything: Flex Consumption, the Cosmos DB free tier and the models are all available here.')
param location string = 'swedencentral'

@description('Prefix of every resource name.')
param prefix string = 'machinespirit'

@description('The owner\'s Entra object id: data, model and secret access for local development and for setting secret values.')
param ownerPrincipalId string

@description('The owner\'s public IPv4 address: the only address staging admits. Given at deploy time, never committed.')
// Required: an empty value would leave staging open to everyone (empty means "no restriction" only for production).
// Secure, so deployment history and CLI output mask it: it identifies the owner.
@secure()
@minLength(7)
param stagingAllowedIp string

@description('GitHub\'s OIDC subject prefix for this repository (immutable format: owner and repository ids).')
param githubSubjectPrefix string = 'repo:mykolad@2202717/daily-machine-spirit@1406418170'

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: '${prefix}-rg'
  location: location
}

module resources 'resources.bicep' = {
  scope: resourceGroup
  name: 'resources'
  params: {
    location: location
    prefix: prefix
    ownerPrincipalId: ownerPrincipalId
    stagingAllowedIp: stagingAllowedIp
    githubSubjectPrefix: githubSubjectPrefix
  }
}

output stagingAppHost string = resources.outputs.stagingAppHost
output productionAppHost string = resources.outputs.productionAppHost
output githubClientIds object = resources.outputs.githubClientIds
