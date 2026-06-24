@description('Prefix applied to all resource names. KV name = <namePrefix>-kv-<env> (must be 3–24 chars total).')
param namePrefix string

@description('Deployment environment name (e.g. dev, staging, prod).')
param environment string

@description('Azure region for the Key Vault.')
param location string

@description('Tags to apply to the resource.')
param tags object = {}

@description('Key Vault public network access. Defaults to Enabled because this template ships without a VNet/private endpoint and the App Service reads secrets over the public endpoint via managed identity. For production, set this to Disabled and front the vault with a private endpoint.')
@allowed([
  'Enabled'
  'Disabled'
])
param keyVaultPublicNetworkAccess string = 'Enabled'

@description('Enable Key Vault purge protection. Purge protection is a one-way switch (cannot be disabled once on), so it is enabled for non-dev environments and left unset for dev to keep teardown/recreate simple.')
param keyVaultEnablePurgeProtection bool = (environment != 'dev')

// KV names must be 3–24 chars, start with a letter, alphanumeric/hyphen, no trailing hyphen.
// Cap the composed name at 24 chars, then strip a trailing hyphen so truncation stays valid.
var rawVaultName = take('${namePrefix}-kv-${environment}', 24)
var vaultName = endsWith(rawVaultName, '-') ? take(rawVaultName, 23) : rawVaultName

resource vault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: vaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    // RBAC authorization: role assignments control access; no legacy access policies
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: true
    // Purge protection is one-way: only ever set `true` or leave unset (Azure rejects `false` once enabled).
    enablePurgeProtection: keyVaultEnablePurgeProtection ? true : null
    // Public by default so the VNet-less dev deployment works; operators should set Disabled + a private endpoint for production.
    publicNetworkAccess: keyVaultPublicNetworkAccess
    networkAcls: {
      // Deny when the vault is private (production) so only the private endpoint/trusted services reach it; Allow otherwise.
      defaultAction: keyVaultPublicNetworkAccess == 'Disabled' ? 'Deny' : 'Allow'
      bypass: 'AzureServices'
    }
  }
}

output keyVaultName string = vault.name
output keyVaultUri string = vault.properties.vaultUri
