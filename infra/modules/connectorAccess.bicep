// What the connector's app may reach: its own storage, where the Functions host keeps its deployment package, keys and
// leases, and the telemetry's token in the vault, which its settings reference (#145). Nothing else: it reads only the
// public API. Named after the app's principal id (see appAccess.bicep for why).
param principalId string
param storageAccountName string
param vaultName string

var storageBlobDataOwner = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var keyVaultSecretsUser = '4633458b-17de-408a-b874-0445c86b69e6'

resource storage 'Microsoft.Storage/storageAccounts@2024-01-01' existing = {
  name: storageAccountName
}

resource vault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: vaultName
}

resource otlpHeaders 'Microsoft.KeyVault/vaults/secrets@2024-11-01' existing = {
  parent: vault
  name: 'OtlpHeaders'
}

resource storageBlobs 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storage
  name: guid(storage.id, principalId, storageBlobDataOwner)
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataOwner)
  }
}

// That one secret, not the vault: the vault holds the site's secrets too, which the connector has no use for.
resource readsOtlpHeaders 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: otlpHeaders
  name: guid(otlpHeaders.id, principalId, keyVaultSecretsUser)
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUser)
  }
}
