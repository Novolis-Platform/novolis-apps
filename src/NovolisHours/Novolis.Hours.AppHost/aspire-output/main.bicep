targetScope = 'subscription'

param resourceGroupName string

param location string

param principalId string

resource rg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: resourceGroupName
  location: location
}

module hours_aca_acr 'hours-aca-acr/hours-aca-acr.bicep' = {
  name: 'hours-aca-acr'
  scope: rg
  params: {
    location: location
  }
}

module hours_aca 'hours-aca/hours-aca.bicep' = {
  name: 'hours-aca'
  scope: rg
  params: {
    location: location
    hours_aca_acr_outputs_name: hours_aca_acr.outputs.name
    userPrincipalId: principalId
  }
}

module hours_storage 'hours-storage/hours-storage.bicep' = {
  name: 'hours-storage'
  scope: rg
  params: {
    location: location
  }
}

module hours_server_identity 'hours-server-identity/hours-server-identity.bicep' = {
  name: 'hours-server-identity'
  scope: rg
  params: {
    location: location
  }
}

module hours_server_roles_hours_storage 'hours-server-roles-hours-storage/hours-server-roles-hours-storage.bicep' = {
  name: 'hours-server-roles-hours-storage'
  scope: rg
  params: {
    location: location
    hours_storage_outputs_name: hours_storage.outputs.name
    principalId: hours_server_identity.outputs.principalId
  }
}

output hours_aca_AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = hours_aca.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN

output hours_aca_AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = hours_aca.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_ID

output hours_aca_AZURE_CONTAINER_REGISTRY_ENDPOINT string = hours_aca.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT

output hours_aca_AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID string = hours_aca.outputs.AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID

output hours_server_identity_id string = hours_server_identity.outputs.id

output hours_storage_tableEndpoint string = hours_storage.outputs.tableEndpoint

output hours_server_identity_clientId string = hours_server_identity.outputs.clientId