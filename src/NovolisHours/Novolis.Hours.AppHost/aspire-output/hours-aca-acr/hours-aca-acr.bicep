@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

resource hours_aca_acr 'Microsoft.ContainerRegistry/registries@2025-04-01' = {
  name: take('hoursacaacr${uniqueString(resourceGroup().id)}', 50)
  location: location
  sku: {
    name: 'Basic'
  }
  tags: {
    'aspire-resource-name': 'hours-aca-acr'
  }
}

output name string = hours_aca_acr.name

output loginServer string = hours_aca_acr.properties.loginServer

output id string = hours_aca_acr.id