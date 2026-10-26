// One app of the MCP connector (#119, src/DailyMachineSpirit.Mcp), in one region: an MCP server over the public rites
// API, which the Functions host runs as a custom handler. Apart from the site's apps, so a flood of connector calls has
// its own instances and ceiling, never the site's. It reads only the public API, so it has no data access: its own
// storage for the host, the telemetry's token from the vault, the domain through Cloudflare, and GitHub's right to
// deploy it.
import { cloudflareRules, ownerRules, trafficManagerRule } from 'accessRules.bicep'

param location string
param appName string
param planName string
@description('Storage account names allow only lowercase letters and digits, up to 24.')
param storageAccountName string
@description('Where the tools read the rites API, e.g. https://dailymachinespirit.fyi/ (through Cloudflare).')
param ritesApiBaseUrl string
@description('Settings beyond the connector\'s own: its telemetry (environment.bicep).')
param settings array
@description('The only IPv4 address the app admits (staging); empty sets no such rule.')
// Secure, so the nested deployment's history masks it too (secure doesn't carry across module boundaries).
@secure()
param allowedIp string
@description('Whether the app admits only Cloudflare (production), whose address ranges are in infra/cloudflare-ips.json.')
param admitsOnlyCloudflare bool
@description('Whether Traffic Manager\'s health checks may reach /healthz: where it chooses between two apps.')
param admitsTrafficManager bool
@description('GitHub\'s deploy identity for the environment, which may change this app and nothing else.')
param githubDeployerPrincipalId string
@description('The environment\'s vault, holding the telemetry\'s token and the domain\'s certificate.')
param vaultName string
@description('The domain clients reach the connector at, through Cloudflare (production); empty for none.')
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

// The Functions host's own storage (its keys, leases, the deployment package). Keys are off: the app uses its identity.
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
        // The smallest instance, and a cap on scale-out, so a flood of calls can't run up the bill.
        instanceMemoryMB: 512
        maximumInstanceCount: 10
      }
      // The .NET runtime the custom handler runs on (host.json starts it with dotnet).
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
      // Deploys go to the SCM site from GitHub's runners, whose addresses change, so the IP rule applies to callers
      // only. The SCM site takes Entra ID alone: basic-auth publishing credentials are off (below).
      scmIpSecurityRestrictionsUseMain: false
      appSettings: concat(
        [
          {
            name: 'AzureWebJobsStorage__accountName'
            value: storage.name
          }
          // Self-hosted MCP servers (custom handlers built with an MCP SDK) are a preview the host enables by this flag.
          {
            name: 'AzureWebJobsFeatureFlags'
            value: 'EnableMcpCustomHandlerPreview'
          }
          {
            name: 'RitesApi__BaseUrl'
            value: ritesApiBaseUrl
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

module connectorAccess 'connectorAccess.bicep' = {
  name: 'connector-access-${appName}'
  params: {
    principalId: app.identity.principalId
    storageAccountName: storage.name
    vaultName: vaultName
  }
}

module githubDeploysApp 'appDeployerAccess.bicep' = {
  name: 'app-deployer-access-${appName}'
  params: {
    principalId: githubDeployerPrincipalId
    appName: app.name
  }
}

// The domain through Cloudflare, with the site's Origin CA certificate, which covers its subdomains too. Azure checks the
// subdomain's own asuid TXT record first, so the DNS comes before this (infra/README.md).
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
