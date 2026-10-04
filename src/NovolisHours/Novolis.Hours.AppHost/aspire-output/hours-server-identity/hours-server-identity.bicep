@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

resource hours_server_identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: take('hours_server_identity-${uniqueString(resourceGroup().id)}', 128)
  location: location
}

output id string = hours_server_identity.id

output clientId string = hours_server_identity.properties.clientId

output principalId string = hours_server_identity.properties.principalId

output principalName string = hours_server_identity.name

output name string = hours_server_identity.name