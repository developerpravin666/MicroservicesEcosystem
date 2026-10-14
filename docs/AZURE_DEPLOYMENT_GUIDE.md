# Automatic Azure Deployment for AccountingInventory.Api (.NET 10)

This setup automatically deploys **AccountingInventory.Api** to Azure App Service whenever code is pushed or merged into the **`main`** branch in Git. No local scripts or manual deployments are required.

---

## 1. How the Automated Pipeline Works

The workflow [`.github/workflows/main_sm-accounting-inventory-api.yml`](file:///.github/workflows/main_sm-accounting-inventory-api.yml) is triggered automatically on every:
- **Code push or merge to the `main` branch**
- Manual trigger via **Actions > Run workflow** in GitHub

### Pipeline Execution Steps:
1. **Pulls Code**: Checks out the repository.
2. **Sets up .NET 10 SDK**: Installs the latest .NET 10 SDK (`10.0.x` preview).
3. **Builds Self-Contained Linux Package**:
   ```bash
   dotnet publish src/AccountingInventory/AccountingInventory.Api/AccountingInventory.Api.csproj \
     -c Release -r linux-x64 --self-contained true
   ```
   *Because it is self-contained, Azure does not need .NET 10 pre-installed on the host container.*
4. **Prepares Production Package**: Removes any development settings (`appsettings.Development.json`).
5. **Deploys to Azure Web App**: Automatically deploys the package using `azure/webapps-deploy@v3` via the configured GitHub Secret / Publish Profile.

---

## 2. One-Time Setup in GitHub Repository

To connect your GitHub repository to your Azure Web App, complete these two quick steps once:

### Step 1: Add the Web App Name (Repository Variable)
1. On GitHub, navigate to:  
   **Settings > Secrets and variables > Actions > Variables tab**
2. Click **New repository variable**:
   - **Name**: `AZURE_WEBAPP_NAME_PRODUCTION`
   - **Value**: Your target Azure Web App name (e.g., `siddhi-api-prod` or `sm-accounting-inventory-api`)

### Step 2: Add the Azure Publish Profile (Repository Secret)
1. In the **Azure Portal**, open your Web App.
2. In the top toolbar of the Overview page, click **"Get publish profile"** (this downloads an XML file).
3. On GitHub, navigate to:  
   **Settings > Secrets and variables > Actions > Secrets tab**
4. Click **New repository secret**:
   - **Name**: `AZURE_WEBAPP_PROFILE_PRODUCTION` or `AZURE_WEBAPP_PUBLISH_PROFILE_PRODUCTION`
   - **Value**: Open the downloaded XML file, copy the **entire content**, and paste it into the value field.

---

## 3. Azure Web App Configuration

Ensure your Azure App Service (Linux) has the following configured under **Settings > Configuration** (or **Environment variables**):

| Setting Key | Value | Description |
|---|---|---|
| **Startup Command** | `./AccountingInventory.Api` | Executes the self-contained Linux binary |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Activates production configuration |
| `ConnectionStrings__AccountingInventoryDb` | `Host=...;Port=5432;Database=accounting_inventory;Username=...;Password=...;SslMode=Require;Trust Server Certificate=true;` | PostgreSQL database connection string |
| `Jwt__SigningKey` | *(At least 32 characters secret key)* | Secret for signing tokens |
| `Frontend__BaseUrl` | `https://ambitious-stone-07cd17110.6.azurestaticapps.net` | Frontend URL for CORS |
| `Swagger__Enabled` | `true` | Enables Swagger UI at `/swagger` |
| `RabbitMq__UseInMemory` | `true` | Runs MassTransit in-memory bus |
| `Email__OfflineMode` | `true` | Allows booting without active SMTP |
| `PORT` | `8080` | Azure App Service port binding |

---

## 4. Verification

After any merge to `main`, go to your repository's **Actions** tab to watch the build and deploy. Once complete:

- **Swagger UI**: `https://<your-app-name>.azurewebsites.net/swagger`
- **Health Check**: `https://<your-app-name>.azurewebsites.net/health`
- **Readiness (DB Connected)**: `https://<your-app-name>.azurewebsites.net/health/ready`
