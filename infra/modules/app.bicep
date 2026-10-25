// One Functions app of an environment, in one region: its plan, the host's own storage, the domain through Cloudflare,
// and what the app and GitHub's deploy identity may reach. Staging has one; production two (#113), in different regions,
// sharing its database, vault and snapshot.
import { cloudflareRules, ownerRules, trafficManagerRule } from 'accessRules.bicep'

param location string
param appName string
param planName string
@description('Storage account names allow only lowercase letters and digits, up to 24.')
param storageAccountName string
@description('The app settings, but for the host\'s storage, which is this module\'s.')
param settings array
@description('The only IPv4 address the app admits (staging); empty sets no such rule.')
// Secure, so the nested deployment's history masks it too (secure doesn't carry across module boundaries).
@secure()
param allowedIp string
@description('Whether the app admits only Cloudflare (production), whose address ranges are in infra/cloudflare-ips.json.')
param admitsOnlyCloudflare bool
@description('Whether Traffic Manager\'s health checks may reach /healthz (#113): where it chooses between two apps.')
param admitsTrafficManager bool
param vaultName string
param aiAccountName string
param cosmosAccountName string
param databaseName string
param snapshotAccountName string
param snapshotContainerName string
@description('GitHub\'s deploy identity for the environment, which may change this app and nothing else.')
param githubDeployerPrincipalId string
@description('The domain readers reach the app at, through Cloudflare (production); empty for none.')
param customDomain string
@description('The name of the vault secret holding the Cloudflare Origin CA certificate for the domain, a PFX (infra/README.md).')
param customDomainCertificate string

var deploymentContainer = 'deployments'
var accessRules = concat(
  ownerRules(allowedIp),
  admitsTrafficManager ? [trafficManagerRule] : [],
  admitsOnlyCloudflare ? cloudflareRules : []
)

resource vault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: vaultName
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
  name: planName
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
      appSettings: concat(
        [
          {
            name: 'AzureWebJobsStorage__accountName'
            value: storage.name
          }
        ],
        settings
      )
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
  name: 'app-access-${appName}'
  params: {
    principalId: app.identity.principalId
    storageAccountName: storage.name
    vaultName: vaultName
    aiAccountName: aiAccountName
    cosmosAccountName: cosmosAccountName
    databaseName: databaseName
    snapshotAccountName: snapshotAccountName
    snapshotContainerName: snapshotContainerName
  }
}

module githubDeploysApp 'appDeployerAccess.bicep' = {
  name: 'app-deployer-access-${appName}'
  params: {
    principalId: githubDeployerPrincipalId
    appName: app.name
  }
}

// The domain, through Cloudflare: Cloudflare's Origin CA certificate, imported from the vault and bound to the name.
// Azure checks the domain's asuid TXT record first, so the DNS comes before this (infra/README.md). The import is done
// by App Service itself, which may read that one secret (environment.bicep).
resource domainCertificate 'Microsoft.Web/sites/certificates@2024-11-01' = if (!empty(customDomain)) {
  parent: app
  name: customDomainCertificate
  location: location
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

output appHost string = app.properties.defaultHostName
