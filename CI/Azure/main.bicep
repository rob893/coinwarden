targetScope = 'resourceGroup'

@description('Short prefix applied to every resource name (e.g. "myapp"). Max 16 chars keeps all derived resource names within Azure limits.')
@maxLength(16)
param namePrefix string

@description('Azure region; defaults to the resource group location.')
param location string = resourceGroup().location

@description('Deployment environment name (e.g. dev, staging, prod).')
param environment string = 'dev'

@description('PostgreSQL flexible server administrator login name.')
param postgresAdminLogin string

@description('PostgreSQL flexible server administrator password. Pass via --parameters or a GitHub secret — never store in plain text.')
@secure()
param postgresAdminPassword string

@description('App Service plan SKU name (e.g. B1, B2, S1, P1v3).')
param appServiceSku string = 'B1'

@description('PostgreSQL flexible server compute SKU name (e.g. Standard_B1ms, Standard_D2s_v3).')
param postgresSkuName string = 'Standard_B1ms'

@description('PostgreSQL flexible server compute tier.')
@allowed([
  'Burstable'
  'GeneralPurpose'
  'MemoryOptimized'
])
param postgresSkuTier string = 'Burstable'

@description('PostgreSQL flexible server storage size in GB.')
@allowed([
  32
  64
  128
  256
  512
  1024
  2048
  4096
  8192
  16384
])
param postgresStorageSizeGB int = 32

@description('PostgreSQL automated backup retention in days (7–35).')
@minValue(7)
@maxValue(35)
param postgresBackupRetentionDays int = 7

@description('PostgreSQL high availability mode.')
@allowed([
  'Disabled'
  'SameZone'
  'ZoneRedundant'
])
param postgresHighAvailabilityMode string = 'Disabled'

@description('PostgreSQL public network access. Defaults to Enabled because this template has no VNet/private endpoint and the App Service reaches the database over the public endpoint. For production, set to Disabled and add a private endpoint.')
@allowed([
  'Enabled'
  'Disabled'
])
param postgresPublicNetworkAccess string = 'Enabled'

@description('Create the AllowAllAzureServices firewall rule (0.0.0.0–0.0.0.0 sentinel) on Postgres. The module default is false (secure); the dev parameters file opts in because the App Service has no VNet integration. Prefer scoping IPs via postgresAllowedClientIpRanges or going private in production.')
param postgresAllowAzureServicesAccess bool = false

@description('Scoped Postgres client IP ranges allowed through the firewall. Each element is { name, startIpAddress, endIpAddress }.')
param postgresAllowedClientIpRanges array = []

@description('Log Analytics workspace data retention in days (30–730).')
@minValue(30)
@maxValue(730)
param logAnalyticsRetentionInDays int = 30

@description('Application Insights data retention in days.')
@allowed([
  30
  60
  90
  120
  180
  270
  365
  550
  730
])
param appInsightsRetentionInDays int = 90

@description('Key Vault public network access. Defaults to Enabled because this template has no VNet/private endpoint and the App Service reads secrets over the public endpoint via managed identity. For production, set to Disabled and add a private endpoint.')
@allowed([
  'Enabled'
  'Disabled'
])
param keyVaultPublicNetworkAccess string = 'Enabled'

@description('Enable Key Vault purge protection. One-way switch, so it defaults to true for non-dev environments and false (unset) for dev to keep teardown/recreate simple.')
param keyVaultEnablePurgeProtection bool = (environment != 'dev')

@description('Additional resource tags merged over the defaults (environment, project, managedBy).')
param tags object = {}

// Merge caller-supplied tags over opinionated defaults
var resolvedTags = union(
  {
    environment: environment
    project: 'coinwarden'
    managedBy: 'bicep'
  },
  tags
)

// Map infrastructure environment name → ASP.NET Core environment name
var aspNetCoreEnv = environment == 'dev' ? 'Development' : 'Production'

// ── Log Analytics workspace ──────────────────────────────────────────────────
module logAnalytics 'modules/logAnalytics.bicep' = {
  name: 'logAnalytics'
  params: {
    namePrefix: namePrefix
    environment: environment
    location: location
    retentionInDays: logAnalyticsRetentionInDays
    tags: resolvedTags
  }
}

// ── Application Insights (workspace-based) ──────────────────────────────────
module appInsights 'modules/appInsights.bicep' = {
  name: 'appInsights'
  params: {
    namePrefix: namePrefix
    environment: environment
    location: location
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
    retentionInDays: appInsightsRetentionInDays
    tags: resolvedTags
  }
}

// ── Key Vault (RBAC authorization mode) ─────────────────────────────────────
module keyVault 'modules/keyVault.bicep' = {
  name: 'keyVault'
  params: {
    namePrefix: namePrefix
    environment: environment
    location: location
    keyVaultPublicNetworkAccess: keyVaultPublicNetworkAccess
    keyVaultEnablePurgeProtection: keyVaultEnablePurgeProtection
    tags: resolvedTags
  }
}

// ── PostgreSQL Flexible Server + database ────────────────────────────────────
module postgres 'modules/postgres.bicep' = {
  name: 'postgres'
  params: {
    namePrefix: namePrefix
    environment: environment
    location: location
    adminLogin: postgresAdminLogin
    adminPassword: postgresAdminPassword
    skuName: postgresSkuName
    skuTier: postgresSkuTier
    storageSizeGB: postgresStorageSizeGB
    backupRetentionDays: postgresBackupRetentionDays
    highAvailabilityMode: postgresHighAvailabilityMode
    publicNetworkAccess: postgresPublicNetworkAccess
    allowAzureServicesAccess: postgresAllowAzureServicesAccess
    allowedClientIpRanges: postgresAllowedClientIpRanges
    tags: resolvedTags
  }
}

// ── App Service plan + Web App (system-assigned MI, .NET 10, HTTPS-only) ────
module appService 'modules/appService.bicep' = {
  name: 'appService'
  params: {
    namePrefix: namePrefix
    environment: environment
    location: location
    sku: appServiceSku
    keyVaultUri: keyVault.outputs.keyVaultUri
    appInsightsConnectionString: appInsights.outputs.connectionString
    aspNetCoreEnvironment: aspNetCoreEnv
    tags: resolvedTags
  }
}

// ── RBAC: grant Web App MI access to Key Vault secrets + App Insights metrics
module rbac 'modules/rbac.bicep' = {
  name: 'rbac'
  params: {
    webAppPrincipalId: appService.outputs.principalId
    keyVaultName: keyVault.outputs.keyVaultName
    appInsightsName: appInsights.outputs.appInsightsName
  }
}

// ── Outputs for downstream use (pipeline variables, post-deploy config) ──────
output webAppName string = appService.outputs.webAppName
output webAppDefaultHostName string = appService.outputs.webAppDefaultHostName
output keyVaultName string = keyVault.outputs.keyVaultName
output postgresFqdn string = postgres.outputs.fqdn
output appInsightsConnectionString string = appInsights.outputs.connectionString
