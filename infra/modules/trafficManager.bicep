// Where Cloudflare sends callers when an app has a second region (#113, infra/README.md, "Outside Azure"). Priority: the
// first app while its /healthz answers, else the standby. Weighted: either, evenly, while both answer. Every 30 seconds
// from Azure's probes, out after 3 failures, and Cloudflare keeps the answer a minute, so a failover takes two to three
// minutes, as does coming back.
param name string
@description('The profile\'s address is <relativeName>.trafficmanager.net, unique across Azure.')
param relativeName string
@description('Priority where the standby must stay idle (the site writes to Cosmos DB in the first region); Weighted where either region answers the same.')
@allowed(['Priority', 'Weighted'])
param routing string
@description('Whether the standby is taken as healthy without probing it, so it can scale to zero: only with Priority, where a failover goes to it anyway.')
param standbyAlwaysServes bool
param firstLocation string
param firstHost string
param standbyLocation string
param standbyHost string

resource traffic 'Microsoft.Network/trafficmanagerprofiles@2022-04-01' = {
  name: name
  location: 'global'
  properties: {
    profileStatus: 'Enabled'
    trafficRoutingMethod: routing
    dnsConfig: {
      relativeName: relativeName
      ttl: 60
    }
    monitorConfig: {
      protocol: 'HTTPS'
      port: 443
      path: '/healthz'
      intervalInSeconds: 30
      timeoutInSeconds: 10
      toleratedNumberOfFailures: 3
    }
    // External endpoints: Traffic Manager takes Azure's own only from App Service plans of Standard and up.
    endpoints: [
      {
        name: firstLocation
        type: 'Microsoft.Network/trafficManagerProfiles/externalEndpoints'
        properties: {
          target: firstHost
          endpointStatus: 'Enabled'
          priority: 1
          weight: 1
        }
      }
      {
        name: standbyLocation
        type: 'Microsoft.Network/trafficManagerProfiles/externalEndpoints'
        properties: {
          target: standbyHost
          endpointStatus: 'Enabled'
          priority: 2
          weight: 1
          alwaysServe: standbyAlwaysServes ? 'Enabled' : 'Disabled'
        }
      }
    ]
  }
}

output host string = traffic.properties.dnsConfig.fqdn
