// What an environment's app may reach: its own storage, vault and database, and the models. A separate module so the
// assignments can be named after the app's principal id, which is only known once the app exists: a recreated app gets a
// new principal, and an assignment named after the old one couldn't be updated (RoleAssignmentUpdateNotPermitted).
param principalId string
param storageAccountName string
param vaultName string
param aiAccountName string
param cosmosAccountName string
param databaseName string

var roles = {
  storageBlobDataOwner: 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
  storageQueueDataContributor: '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
  storageTableDataContributor: '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
  keyVaultSecretsUser: '4633458b-17de-408a-b874-0445c86b69e6'
  cognitiveServicesOpenAiUser: '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
}

resource storage 'Microsoft.Storage/storageAccounts@2024-01-01' existing = {
  name: storageAccountName
}

resource vault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: vaultName
}

resource ai 'Microsoft.CognitiveServices/accounts@2025-06-01' existing = {
  name: aiAccountName
}

resource cosmos 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' existing = {
  name: cosmosAccountName
}

// The Functions host keeps blob leases, deployment packages, queues and tables in its storage.
resource storageBlobs 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storage
  name: guid(storage.id, principalId, roles.storageBlobDataOwner)
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.storageBlobDataOwner)
  }
}

resource storageQueues 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storage
  name: guid(storage.id, principalId, roles.storageQueueDataContributor)
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.storageQueueDataContributor)
  }
}

resource storageTables 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storage
  name: guid(storage.id, principalId, roles.storageTableDataContributor)
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.storageTableDataContributor)
  }
}

resource secrets 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: vault
  name: guid(vault.id, principalId, roles.keyVaultSecretsUser)
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsUser)
  }
}

resource models 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: ai
  name: guid(ai.id, principalId, roles.cognitiveServicesOpenAiUser)
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.cognitiveServicesOpenAiUser)
  }
}

// Data Contributor on this environment's database only.
resource data 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-11-15' = {
  parent: cosmos
  name: guid(cosmos.id, principalId, 'data-contributor')
  properties: {
    principalId: principalId
    roleDefinitionId: '${cosmos.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
    scope: '${cosmos.id}/dbs/${databaseName}'
  }
}
