# Production Deployment Guide — Azure App Service + Azure SQL

This guide deploys Survey Maker to Azure App Service on Linux using Kestrel (no IIS). It replaces the local `MSI\DONEMERSON` SQL Server with Azure SQL Database.

**Tools you need installed locally:**
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) (`az`)
- .NET 9 SDK
- Node.js 22+

---

## Part 1 — Create Azure SQL Database

Your local connection string uses Windows Authentication, which only works on your machine. In Azure you use a SQL login (username + password) instead.

### Step 1.1 — Sign in to Azure

```powershell
az login
```

A browser window opens. Sign in with your Azure account.

### Step 1.2 — Create a resource group

All your Azure resources (database, app service) will live in this group. Pick a region close to your users.

```powershell
az group create --name surveymaker-rg --location eastus
```

### Step 1.3 — Create an Azure SQL Server

Replace `<admin-password>` with a strong password (store it somewhere safe — you will not put it in code).

```powershell
az sql server create `
  --name surveymaker-sqlserver `
  --resource-group surveymaker-rg `
  --location eastus `
  --admin-user surveyadmin `
  --admin-password "<admin-password>"
```

> The server name `surveymaker-sqlserver` must be globally unique across Azure. If it is taken, add a number or your initials, e.g. `surveymaker-sqlserver-dem`.

### Step 1.4 — Allow Azure services to connect

This lets your App Service reach the database.

```powershell
az sql server firewall-rule create `
  --resource-group surveymaker-rg `
  --server surveymaker-sqlserver `
  --name AllowAzureServices `
  --start-ip-address 0.0.0.0 `
  --end-ip-address 0.0.0.0
```

### Step 1.5 — (Optional) Allow your own IP to connect

This lets you run EF migrations from your machine. Replace `<your-ip>` — you can find it at https://whatismyip.com.

```powershell
az sql server firewall-rule create `
  --resource-group surveymaker-rg `
  --server surveymaker-sqlserver `
  --name MyDevMachine `
  --start-ip-address <your-ip> `
  --end-ip-address <your-ip>
```

### Step 1.6 — Create the database

The Basic tier (5 DTU) is fine for development/early production. Upgrade later as needed.

```powershell
az sql db create `
  --resource-group surveymaker-rg `
  --server surveymaker-sqlserver `
  --name SurveyMakerDb `
  --service-objective Basic
```

### Step 1.7 — Note your production connection string

The format for Azure SQL with a SQL login (not Windows Auth):

```
Server=tcp:surveymaker-sqlserver.database.windows.net,1433;Database=SurveyMakerDb;User Id=surveyadmin;Password=<admin-password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

Keep this string handy — you will use it in Part 3.

---

## Part 2 — Create Azure App Service

### Step 2.1 — Create an App Service Plan (Linux)

The B1 plan (~$13/month) is the minimum that supports custom domains and is suitable for a real app. Free (F1) works but sleeps after 20 minutes of inactivity.

```powershell
az appservice plan create `
  --name surveymaker-plan `
  --resource-group surveymaker-rg `
  --sku B1 `
  --is-linux
```

### Step 2.2 — Create the Web App

Replace `<app-name>` with a unique name — this becomes `https://<app-name>.azurewebsites.net`.

```powershell
az webapp create `
  --resource-group surveymaker-rg `
  --plan surveymaker-plan `
  --name <app-name> `
  --runtime "DOTNETCORE:9.0"
```

On Linux, Azure App Service runs your .NET app with Kestrel directly. There is no IIS involved.

### Step 2.3 — Enable HTTPS only

Forces all HTTP traffic to redirect to HTTPS automatically.

```powershell
az webapp update `
  --resource-group surveymaker-rg `
  --name <app-name> `
  --https-only true
```

---

## Part 3 — Configure Secrets and Connection String

Azure App Service stores configuration as environment variables. ASP.NET Core reads them automatically and they override anything in `appsettings.json`. **They are encrypted at rest.**

On Linux, nested configuration keys that use `:` in .NET (e.g. `Authentication:Google:ClientId`) must use `__` (double underscore) when set as environment variables.

### Step 3.1 — Set the Google OAuth credentials

```powershell
az webapp config appsettings set `
  --resource-group surveymaker-rg `
  --name <app-name> `
  --settings `
    "Authentication__Google__ClientId=<your-google-client-id>" `
    "Authentication__Google__ClientSecret=<your-google-client-secret>"
```

These map to `Authentication:Google:ClientId` and `Authentication:Google:ClientSecret` in .NET configuration — the same keys the app already reads.

### Step 3.2 — Set the database connection string

Azure App Service has a dedicated **Connection Strings** store. When you set a connection string there with type `SQLAzure`, .NET reads it as `ConnectionStrings:DefaultConnection` — exactly what the app uses.

```powershell
az webapp config connection-string set `
  --resource-group surveymaker-rg `
  --name <app-name> `
  --connection-string-type SQLAzure `
  --settings DefaultConnection="Server=tcp:surveymaker-sqlserver.database.windows.net,1433;Database=SurveyMakerDb;User Id=surveyadmin;Password=<admin-password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
```

### Step 3.3 — Verify in the Azure Portal (optional)

1. Go to **portal.azure.com** → **App Services** → your app
2. Left menu: **Settings → Environment variables**
3. You should see your Google settings under **App settings** and the connection string under **Connection strings**
4. Values are hidden by default — click **Show value** to confirm they were saved

---

## Part 4 — Update Google Cloud Console

Google needs to know your production URL is a valid redirect destination.

1. Go to [console.cloud.google.com](https://console.cloud.google.com)
2. **APIs & Services → Credentials → your OAuth 2.0 Client ID**
3. Under **Authorized redirect URIs**, add:
   ```
   https://<app-name>.azurewebsites.net/signin-google
   ```
4. Click **Save**

Keep your development URI (`http://localhost:44443/signin-google`) in the list — it does not affect production.

---

## Part 5 — Run EF Core Migrations Against Azure SQL

Before deploying the app, the database schema needs to be created. Run this from your development machine (Step 1.5 must be done first to allow your IP through the firewall).

Set the production connection string temporarily as an environment variable so EF uses it instead of the local SQL Server:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=tcp:surveymaker-sqlserver.database.windows.net,1433;Database=SurveyMakerDb;User Id=surveyadmin;Password=<admin-password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

dotnet ef database update --project src/SurveyMaker.Infrastructure

# Clear it when done
Remove-Item Env:ConnectionStrings__DefaultConnection
```

This applies the `AddIdentity` migration (and any future migrations) to the Azure SQL database.

---

## Part 6 — Build and Deploy

### Step 6.1 — Publish the app

This compiles the .NET app **and** runs `ng build` to compile Angular into static files, then bundles everything together.

```powershell
dotnet publish src/SurveyMaker.Api -c Release -o ./publish
```

### Step 6.2 — Zip the output

```powershell
Compress-Archive -Path ./publish/* -DestinationPath ./publish.zip -Force
```

### Step 6.3 — Deploy to Azure

```powershell
az webapp deploy `
  --resource-group surveymaker-rg `
  --name <app-name> `
  --src-path ./publish.zip `
  --type zip
```

Azure will restart the app after deployment. The first request after restart may be slow (cold start) on the B1 plan.

### Step 6.4 — Verify

Open `https://<app-name>.azurewebsites.net` in a browser. You should see the Survey Maker home page. Click **Sign in with Google** and confirm the full OAuth flow works end-to-end.

---

## Redeploying After Code Changes

Every time you want to deploy an update, repeat Steps 6.1–6.3. New EF migrations (if any) should be applied first by repeating Step 5.

```powershell
# Apply any new migrations first (if you added any)
$env:ConnectionStrings__DefaultConnection = "<azure-connection-string>"
dotnet ef database update --project src/SurveyMaker.Infrastructure
Remove-Item Env:ConnectionStrings__DefaultConnection

# Then rebuild and redeploy
dotnet publish src/SurveyMaker.Api -c Release -o ./publish
Compress-Archive -Path ./publish/* -DestinationPath ./publish.zip -Force
az webapp deploy --resource-group surveymaker-rg --name <app-name> --src-path ./publish.zip --type zip
```

---

## Cost Summary

| Resource | Tier | Approximate Monthly Cost |
|---|---|---|
| App Service Plan (Linux) | B1 | ~$13 USD |
| Azure SQL Database | Basic (5 DTU) | ~$5 USD |
| **Total** | | **~$18 USD** |

Upgrade the SQL tier (`Standard S1` ~$15/month) if you need more than 2 GB database size.

---

## Sources

- [Quickstart: Deploy an ASP.NET web app — Azure App Service](https://learn.microsoft.com/en-us/azure/app-service/quickstart-dotnetcore)
- [Configure an App Service App — environment variables and connection strings](https://learn.microsoft.com/en-us/azure/app-service/configure-common)
- [Connect to Azure SQL Database using .NET](https://learn.microsoft.com/en-us/azure/azure-sql/database/azure-sql-dotnet-quickstart?view=azuresql)
