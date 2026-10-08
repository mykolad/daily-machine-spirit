// Owner of the resource group this module is deployed to. Named after the principal id (see appAccess.bicep for why).
param principalId string

var owner = '8e3af657-a8ff-443c-a75c-2fe8c4bcb635'

resource ownerAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, principalId, owner)
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', owner)
  }
}
