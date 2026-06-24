@description('Prefix applied to all resource names. Max 16 characters.')
param namePrefix string

@description('Deployment environment name (e.g. dev, staging, prod).')
param environment string

@description('Azure region for the PostgreSQL Flexible Server.')
param location string

@description('PostgreSQL flexible server administrator login name.')
param adminLogin string

@description('PostgreSQL flexible server administrator password.')
@secure()
param adminPassword string

@description('Name of the initial database to create.')
param databaseName string = 'coinwarden'

@description('Compute SKU name (e.g. Standard_B1ms, Standard_D2s_v3).')
param skuName string = 'Standard_B1ms'

@description('Compute tier.')
@allowed([
  'Burstable'
  'GeneralPurpose'
  'MemoryOptimized'
])
param skuTier string = 'Burstable'

@description('Storage size in GB.')
param storageSizeGB int = 32

@description('Automated backup retention in days (7–35).')
@minValue(7)
@maxValue(35)
param backupRetentionDays int = 7

@description('High availability mode.')
@allowed([
  'Disabled'
  'SameZone'
  'ZoneRedundant'
])
param highAvailabilityMode string = 'Disabled'

@description('PostgreSQL public network access. Defaults to Enabled because this template ships without a VNet/private endpoint and the App Service reaches the database over the public endpoint. For production, set this to Disabled and front the server with a private endpoint.')
@allowed([
  'Enabled'
  'Disabled'
])
param publicNetworkAccess string = 'Enabled'

@description('Create the AllowAllAzureServices firewall rule (0.0.0.0–0.0.0.0 sentinel). That sentinel allows ALL Azure services in ANY tenant to reach the server, so it defaults to false (secure). Enable it only as a convenience when the App Service has no VNet integration; prefer scoping IPs via allowedClientIpRanges or going private in production.')
param allowAzureServicesAccess bool = false

@description('Scoped client IP ranges allowed through the firewall. Each element is { name, startIpAddress, endIpAddress }. Prefer these narrow ranges over the allow-all-Azure-services sentinel.')
param allowedClientIpRanges array = []

@description('Tags to apply to the resources.')
param tags object = {}

var pgServerName = '${namePrefix}-pg-${environment}'

resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: pgServerName
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: skuTier
  }
  properties: {
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    version: '16'
    storage: {
      storageSizeGB: storageSizeGB
    }
    backup: {
      backupRetentionDays: backupRetentionDays
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: highAvailabilityMode
    }
    network: {
      publicNetworkAccess: publicNetworkAccess
    }
    // Password auth keeps the VNet-less template deploying out of the box; prefer Entra (AAD) auth for production.
    // The app connection string should use SslMode=Require (Flexible Server enforces TLS by default); that is
    // configured outside Bicep via a Key Vault secret, so there is nothing to set here.
    authConfig: {
      activeDirectoryAuth: 'Disabled'
      passwordAuth: 'Enabled'
    }
  }
}

resource postgresDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgresServer
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// 0.0.0.0 → 0.0.0.0 is the Azure "allow all Azure services" sentinel for Flexible Server.
// Only created when explicitly opted in, since it exposes the server to all Azure services in any tenant.
resource firewallAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = if (allowAzureServicesAccess) {
  parent: postgresServer
  name: 'AllowAllAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Scoped client IP ranges — the preferred way to grant public access in production.
resource firewallClientRanges 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = [
  for rule in allowedClientIpRanges: {
    parent: postgresServer
    name: rule.name
    properties: {
      startIpAddress: rule.startIpAddress
      endIpAddress: rule.endIpAddress
    }
  }
]

output fqdn string = postgresServer.properties.fullyQualifiedDomainName
