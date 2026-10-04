@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

resource hours_storage 'Microsoft.Storage/storageAccounts@2024-01-01' = {
  name: take('hoursstorage${uniqueString(resourceGroup().id)}', 24)
  kind: 'StorageV2'
  location: location
  sku: {
    name: 'Standard_GRS'
  }
  properties: {
    accessTier: 'Hot'
    allowSharedKeyAccess: false
    isHnsEnabled: false
    minimumTlsVersion: 'TLS1_2'
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
  tags: {
    'aspire-resource-name': 'hours-storage'
  }
}

output blobEndpoint string = hours_storage.properties.primaryEndpoints.blob

output dataLakeEndpoint string = hours_storage.properties.primaryEndpoints.dfs

output queueEndpoint string = hours_storage.properties.primaryEndpoints.queue

output tableEndpoint string = hours_storage.properties.primaryEndpoints.table

output name string = hours_storage.name

output id string = hours_storage.id