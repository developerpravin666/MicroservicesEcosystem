# Automatic Azure deployment: AccountingInventory.Api

The workflow `.github/workflows/deploy-production.yml` runs on every push to `main`. It also supports a manual run on `main`. It restores and publishes only `src/AccountingInventory/AccountingInventory.Api/AccountingInventory.Api.csproj`, then deploys its publish output to Azure App Service. Referenced Application, Infrastructure, Domain and BuildingBlocks libraries are included as dependencies. API Gateway, Identity API, other services and React are not deployed.

## One-time setup

1. Create the Azure Linux App Service for this API, with a .NET 10 runtime. Set its startup command to `dotnet AccountingInventory.Api.dll` and `ASPNETCORE_ENVIRONMENT` to `Production`.
2. In the **MicroservicesEcosystem GitHub repository**, open Settings > Secrets and variables > Actions.
3. Add repository **variable** `AZURE_WEBAPP_NAME_PRODUCTION` with the exact target App Service name, such as `siddhi-api-prod`.
4. Download that App Service's publish profile from Azure Portal and add its entire XML content as repository **secret** `AZURE_WEBAPP_PUBLISH_PROFILE_PRODUCTION`. Publish-profile deployment requires SCM basic publishing authentication to be enabled on the App Service. If organizational policy disables that authentication, use a federated Azure identity workflow instead.
5. Configure production PostgreSQL connections and database target, JWT signing key, frontend HTTPS origin/CORS, SMTP and RabbitMQ in App Service settings or Key Vault references. AccountingInventory.Api is standalone but requires reachable PostgreSQL and RabbitMQ. Review the published base/Production settings; production credentials should come from App Service settings rather than committed files. The workflow excludes the Development settings file and does not print deployment credentials.
6. Commit and push the workflow to `main`. In Actions, open **Deploy AccountingInventory API to Azure** to check the publish and deployment steps. Configuration validation stops deployment if either the app-name variable or publish-profile secret is missing.
7. After deployment, verify `/health`, login and a tenant-scoped request at the App Service's actual hostname. The frontend calls that API directly with its `/api` base URL.

Active deployment runs are not cancelled by a newer push. Deployment failures require checking the Actions log and App Service application logs. Database migrations and production backups remain part of the release procedure in `accounting-p0-release.md`.

This file is in the backend Git repository so the instructions and workflow are pushed together. The sibling `deploy` directory is outside this repository and its provisioning guide is not automatically included in a backend push.

References:
- https://learn.microsoft.com/en-us/azure/app-service/deploy-github-actions
- https://github.com/Azure/webapps-deploy
