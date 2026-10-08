// GitHub's deploy identity may change this app and nothing else. Named after the identity's principal id (see
// appAccess.bicep for why).
param principalId string
param appName string

var websiteContributor = 'de139f84-1756-47ae-9be6-808fbbe84772'

resource app 'Microsoft.Web/sites@2024-04-01' existing = {
  name: appName
}

resource deploysApp 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: app
  name: guid(app.id, principalId, websiteContributor)
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', websiteContributor)
  }
}
