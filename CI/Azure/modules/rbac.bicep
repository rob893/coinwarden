@description('Principal ID of the Web App system-assigned managed identity.')
param webAppPrincipalId string

@description('Name of the Key Vault to grant the Secrets User role on.')
param keyVaultName string

@description('Name of the Application Insights instance to grant the Monitoring Metrics Publisher role on.')
param appInsightsName string

// Well-known Azure built-in role definition GUIDs:
//   Key Vault Secrets User:        4633458b-17de-408a-b874-0445c86b69e6
//   Monitoring Metrics Publisher:  3913510d-42f4-4e42-8a64-420c390055eb
var kvSecretsUserRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '4633458b-17de-408a-b874-0445c86b69e6'
)
var metricsPublisherRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '3913510d-42f4-4e42-8a64-420c390055eb'
)

resource vault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: keyVaultName
}

resource appInsightsComp 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

// Allows the Web App MI to read secrets — required for DefaultAzureCredential KV config load
resource kvSecretsUserAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, webAppPrincipalId, kvSecretsUserRoleId)
  scope: vault
  properties: {
    roleDefinitionId: kvSecretsUserRoleId
    principalId: webAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Allows the Web App MI to publish custom metrics to App Insights
resource metricsPublisherAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appInsightsComp.id, webAppPrincipalId, metricsPublisherRoleId)
  scope: appInsightsComp
  properties: {
    roleDefinitionId: metricsPublisherRoleId
    principalId: webAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}
