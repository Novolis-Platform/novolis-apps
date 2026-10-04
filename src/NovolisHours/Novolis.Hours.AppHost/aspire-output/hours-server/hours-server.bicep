@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param hours_aca_outputs_azure_container_apps_environment_default_domain string

param hours_aca_outputs_azure_container_apps_environment_id string

param hours_server_containerimage string

param hours_server_identity_outputs_id string

@secure()
param hours_initial_administrator_password_value string

param hours_storage_outputs_tableendpoint string

param hours_server_identity_outputs_clientid string

param hours_aca_outputs_azure_container_registry_endpoint string

param hours_aca_outputs_azure_container_registry_managed_identity_id string

resource hours_server 'Microsoft.App/containerApps@2025-10-02-preview' = {
  name: 'hours-server'
  location: location
  properties: {
    configuration: {
      secrets: [
        {
          name: 'hours--initialadministratorpassword'
          value: hours_initial_administrator_password_value
        }
      ]
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
          probes: [
            {
              failureThreshold: 12
              httpGet: {
                path: '/health/startup'
                port: int('8080')
                scheme: 'HTTP'
              }
              initialDelaySeconds: 5
              periodSeconds: 10
              successThreshold: 1
              timeoutSeconds: 10
              type: 'Startup'
            }
            {
              failureThreshold: 3
              httpGet: {
                path: '/health/ready'
                port: int('8080')
                scheme: 'HTTP'
              }
              initialDelaySeconds: 5
              periodSeconds: 10
              successThreshold: 1
              timeoutSeconds: 10
              type: 'Readiness'
            }
            {
              failureThreshold: 3
              httpGet: {
                path: '/health/live'
                port: int('8080')
                scheme: 'HTTP'
              }
              initialDelaySeconds: 5
              periodSeconds: 30
              successThreshold: 1
              timeoutSeconds: 5
              type: 'Liveness'
            }
          ]
          image: hours_server_containerimage
          name: 'hours-server'
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
              name: 'Hours__InitialAdministratorPassword'
              secretRef: 'hours--initialadministratorpassword'
            }
            {
              name: 'Hours__EnableDemoAdminCredentials'
              value: 'false'
            }
            {
              name: 'Hours__UseInMemoryJournal'
              value: 'false'
            }
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'Hours__StorageProvider'
              value: 'azure-tables'
            }
            {
              name: 'Hours__AzureTablesConnectionString'
              value: hours_storage_outputs_tableendpoint
            }
            {
              name: 'Hours__AzureTablesTablePrefix'
              value: 'novolis-hours'
            }
            {
              name: 'Hours__RequireHttps'
              value: 'true'
            }
            {
              name: 'OTEL_SERVICE_NAME'
              value: 'novolis-hours'
            }
            {
              name: 'ConnectionStrings__hours-tables'
              value: hours_storage_outputs_tableendpoint
            }
            {
              name: 'ConnectionStrings__hours_tables'
              value: hours_storage_outputs_tableendpoint
            }
            {
              name: 'HOURS_TABLES_URI'
              value: hours_storage_outputs_tableendpoint
            }
            {
              name: 'Hours__TrustedProxyAddresses__0'
              value: '*'
            }
            {
              name: 'Hours__AllowedClientOrigins__0'
              value: 'https://hours-client-blazor.${hours_aca_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: hours_server_identity_outputs_clientid
            }
            {
              name: 'AZURE_TOKEN_CREDENTIALS'
              value: 'ManagedIdentityCredential'
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
      '${hours_server_identity_outputs_id}': { }
      '${hours_aca_outputs_azure_container_registry_managed_identity_id}': { }
    }
  }
}