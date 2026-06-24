@description('Prefix applied to all resource names. Max 16 characters.')
param namePrefix string

@description('Deployment environment name (e.g. dev, staging, prod).')
param environment string

@description('Azure region for the App Service resources.')
param location string

@description('App Service plan SKU name (e.g. B1, B2, S1, P1v3).')
param sku string = 'B1'

@description('URI of the Key Vault from which the app reads secrets via managed identity.')
param keyVaultUri string

@description('Application Insights connection string for telemetry.')
param appInsightsConnectionString string

@description('ASP.NET Core environment name (e.g. Development, Production).')
param aspNetCoreEnvironment string = 'Production'

@description('Tags to apply to all resources.')
param tags object = {}

var appPlanName = '${namePrefix}-asp-${environment}'
var appName = '${namePrefix}-api-${environment}'

resource appServicePlan 'Microsoft.Web/serverfarms@2024-11-01' = {
  name: appPlanName
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: sku
  }
  properties: {
    reserved: true // required for Linux plans
  }
}

resource webApp 'Microsoft.Web/sites@2024-11-01' = {
  name: appName
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: aspNetCoreEnvironment
        }
        {
          // The app uses DefaultAzureCredential + this URI to load secrets from Key Vault
          name: 'KeyVaultUrl'
          value: keyVaultUri
        }
        {
          // Config key the API reads (ConfigurationKeys.ApplicationInsightsConnectionString).
          // The app wires telemetry in code via UseAzureMonitor, so we do NOT enable the
          // ApplicationInsightsAgent_EXTENSION_VERSION auto-instrumentation agent (that would
          // double-instrument). Name must match the flat config key exactly.
          name: 'ApplicationInsightsConnectionString'
          value: appInsightsConnectionString
        }
      ]
    }
  }
}

output webAppName string = webApp.name
output webAppDefaultHostName string = webApp.properties.defaultHostName
// Used by rbac.bicep to assign roles to this identity
output principalId string = webApp.identity.principalId
