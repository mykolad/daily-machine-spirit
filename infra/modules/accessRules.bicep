// The access rules every public app shares (the site's apps, app.bicep, and the connector's, connector.bicep), imported
// where they're used: a module of its own would put the owner's address into its outputs, unmasked.

// Readers reach production through Cloudflare alone, so its cache and rate limit can't be gone around. Cloudflare
// publishes its ranges, and a weekly workflow (Check Cloudflare's Addresses) says when they change.
var cloudflare = loadJsonContent('../cloudflare-ips.json')

@export()
var cloudflareRules = [for (range, index) in concat(cloudflare.ipv4, cloudflare.ipv6): {
  name: 'cloudflare-${index}'
  ipAddress: range
  action: 'Allow'
  priority: 200 + index
}]

// Traffic Manager's health checks (#113) come from Azure's own addresses, which the service tag follows.
@export()
var trafficManagerRule = {
  name: 'traffic-manager'
  tag: 'ServiceTag'
  ipAddress: 'AzureTrafficManager'
  action: 'Allow'
  priority: 150
}

@export()
func ownerRules(allowedIp string) array => empty(allowedIp) ? [] : [
  {
    name: 'owner'
    ipAddress: '${allowedIp}/32'
    action: 'Allow'
    priority: 100
  }
]
