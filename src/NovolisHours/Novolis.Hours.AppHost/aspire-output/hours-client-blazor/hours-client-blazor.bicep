@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param hours_aca_outputs_azure_container_apps_environment_default_domain string

param hours_aca_outputs_azure_container_apps_environment_id string

param hours_client_blazor_containerimage string

param hours_aca_outputs_azure_container_registry_endpoint string

param hours_aca_outputs_azure_container_registry_managed_identity_id string

resource hours_client_blazor 'Microsoft.App/containerApps@2025-10-02-preview' = {
  name: 'hours-client-blazor'
  location: location
  properties: {
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
      }
      registries: [
        {
          server: hours_aca_outputs_azure_container_registry_endpoint
          identity: hours_aca_outputs_azure_container_registry_managed_identity_id
        }
      ]
      runtime: {
        dotnet: {
          autoConfigureDataProtection: true
        }
      }
    }
    environmentId: hours_aca_outputs_azure_container_apps_environment_id
    template: {
      containers: [
        {
          image: hours_client_blazor_containerimage
          name: 'hours-client-blazor'
          env: [
            {
              name: 'OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY'
              value: 'in_memory'
            }
            {
              name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
              value: 'true'
            }
            {
              name: 'HTTP_PORTS'
              value: '8080'
            }
            {
              name: 'NOVOLIS_HOURS_SERVICE_URL'
              value: 'https://hours-server.${hours_aca_outputs_azure_container_apps_environment_default_domain}'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${hours_aca_outputs_azure_container_registry_managed_identity_id}': { }
    }
  }
}