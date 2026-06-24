@description('Prefix applied to all resource names. Max 16 characters.')
param namePrefix string

@description('Deployment environment name (e.g. dev, staging, prod).')
param environment string

@description('Azure region for the Log Analytics workspace.')
param location string

@description('Data retention in days (30–730).')
@minValue(30)
@maxValue(730)
param retentionInDays int = 30

@description('Tags to apply to the workspace.')
param tags object = {}

var laName = '${namePrefix}-la-${environment}'

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2025-02-01' = {
  name: laName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionInDays
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

output workspaceId string = logAnalyticsWorkspace.id
