@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param hours_storage_outputs_name string

param principalId string

resource hours_storage 'Microsoft.Storage/storageAccounts@2024-01-01' existing = {
  name: hours_storage_outputs_name
}

resource hours_storage_StorageTableDataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(hours_storage.id, principalId, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'))
  properties: {
    principalId: principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3')
    principalType: 'ServicePrincipal'
  }
  scope: hours_storage
}