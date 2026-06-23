<#
.SYNOPSIS
    GenService Platform — One-time Azure resource setup script
    Run this ONCE to create all Azure resources for production deployment.

.NOTES
    Prerequisites:
      1. Azure CLI installed: https://aka.ms/installazurecliwindows
      2. Logged in: run  az login  first

    After this script completes:
      1. Download the App Service publish profile (see instructions at end)
      2. Get the Static Web App API token (see instructions at end)
      3. Add them as GitHub Secrets
      4. Push to main → auto-deploy fires
#>

# ── Config — change these if needed ──────────────────────────────────────────
$RESOURCE_GROUP   = "rg-genservice-desicon"
$LOCATION         = "westeurope"
$APP_SERVICE_PLAN = "asp-genservice-desicon"
$WEBAPP_NAME      = "genservice-desicon"
$SQL_SERVER       = "sql-genservice-desicon"
$SQL_DB           = "GenServiceProd"
$SQL_ADMIN        = "gensvcadmin"
$SQL_PASSWORD     = "GenService2026Prod!"
$STATIC_WEB_APP   = "swa-genservice-desicon"
$JWT_SECRET       = "GenServiceProdJwtSecret2026Key32chars!"

# ── Helper ────────────────────────────────────────────────────────────────────
function Write-Step  { param($msg) Write-Host "`n► $msg" -ForegroundColor Cyan  }
function Write-OK    { param($msg) Write-Host "  ✅ $msg" -ForegroundColor Green }
function Write-Warn  { param($msg) Write-Host "  ⚠️  $msg" -ForegroundColor Yellow }

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════╗" -ForegroundColor Magenta
Write-Host "║     GenService — Azure One-Time Setup                ║" -ForegroundColor Magenta
Write-Host "╚══════════════════════════════════════════════════════╝" -ForegroundColor Magenta

# ── Check Azure CLI is installed ──────────────────────────────────────────────
Write-Step "Checking Azure CLI..."
try {
    az --version | Out-Null
    Write-OK "Azure CLI found"
} catch {
    Write-Host "  ❌ Azure CLI not found. Install from: https://aka.ms/installazurecliwindows" -ForegroundColor Red
    exit 1
}

# ── Check logged in ───────────────────────────────────────────────────────────
Write-Step "Checking Azure login..."
$account = az account show 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Warn "Not logged in. Running az login..."
    az login
}
$sub = (az account show --query "name" -o tsv)
Write-OK "Logged in to subscription: $sub"

# ── 1. Resource Group ─────────────────────────────────────────────────────────
Write-Step "Creating resource group: $RESOURCE_GROUP..."
az group create --name $RESOURCE_GROUP --location $LOCATION | Out-Null
Write-OK "Resource group ready"

# ── 2. App Service Plan (B1 Linux — cheapest production tier) ─────────────────
Write-Step "Creating App Service Plan (B1 Linux)..."
az appservice plan create `
    --name $APP_SERVICE_PLAN `
    --resource-group $RESOURCE_GROUP `
    --sku B1 `
    --is-linux `
    --location $LOCATION | Out-Null
Write-OK "App Service Plan created (~€12/month)"

# ── 3. Web App (.NET 8) ───────────────────────────────────────────────────────
Write-Step "Creating Web App: $WEBAPP_NAME..."
az webapp create `
    --name $WEBAPP_NAME `
    --resource-group $RESOURCE_GROUP `
    --plan $APP_SERVICE_PLAN `
    --runtime "DOTNETCORE:8.0" | Out-Null
Write-OK "Web App created → https://$WEBAPP_NAME.azurewebsites.net"

# ── 4. SQL Server ─────────────────────────────────────────────────────────────
Write-Step "Creating Azure SQL Server: $SQL_SERVER..."
az sql server create `
    --name $SQL_SERVER `
    --resource-group $RESOURCE_GROUP `
    --location $LOCATION `
    --admin-user $SQL_ADMIN `
    --admin-password $SQL_PASSWORD | Out-Null
Write-OK "SQL Server created"

# Allow Azure services through the firewall
Write-Step "Configuring SQL firewall (allow Azure services)..."
az sql server firewall-rule create `
    --resource-group $RESOURCE_GROUP `
    --server $SQL_SERVER `
    --name "AllowAzureServices" `
    --start-ip-address 0.0.0.0 `
    --end-ip-address 0.0.0.0 | Out-Null
Write-OK "SQL firewall configured"

# ── 5. SQL Database (Serverless — pauses when idle, cheapest) ─────────────────
Write-Step "Creating SQL Database (Serverless, auto-pause 1hr)..."
az sql db create `
    --name $SQL_DB `
    --resource-group $RESOURCE_GROUP `
    --server $SQL_SERVER `
    --edition GeneralPurpose `
    --family Gen5 `
    --capacity 1 `
    --compute-model Serverless `
    --auto-pause-delay 60 `
    --min-capacity 0.5 | Out-Null
Write-OK "SQL Database created (Serverless — ~€3-5/month)"

# Build connection string
$CONN_STR = "Server=$SQL_SERVER.database.windows.net;Database=$SQL_DB;User Id=$SQL_ADMIN;Password=$SQL_PASSWORD;TrustServerCertificate=false;Encrypt=true;Connection Timeout=30;MultipleActiveResultSets=true"

# ── 6. Static Web App (Free tier) ────────────────────────────────────────────
Write-Step "Creating Static Web App (Free tier)..."
az staticwebapp create `
    --name $STATIC_WEB_APP `
    --resource-group $RESOURCE_GROUP `
    --location $LOCATION `
    --sku Free | Out-Null

$SWA_URL = (az staticwebapp show --name $STATIC_WEB_APP --resource-group $RESOURCE_GROUP --query "defaultHostname" -o tsv)
Write-OK "Static Web App created → https://$SWA_URL"

# ── 7. Configure App Service settings ────────────────────────────────────────
Write-Step "Configuring App Service environment variables..."
az webapp config appsettings set `
    --name $WEBAPP_NAME `
    --resource-group $RESOURCE_GROUP `
    --settings `
        "ASPNETCORE_ENVIRONMENT=Production" `
        "Auth__Mode=DevJwt" `
        "Auth__DevJwt__Secret=$JWT_SECRET" `
        "Auth__DevJwt__Issuer=genservice-prod" `
        "Auth__DevJwt__Audience=genservice-web" `
        "Cors__AllowedOrigins=https://$SWA_URL" | Out-Null
Write-OK "App settings configured"

# Set connection string
az webapp config connection-string set `
    --name $WEBAPP_NAME `
    --resource-group $RESOURCE_GROUP `
    --settings "DefaultConnection=$CONN_STR" `
    --connection-string-type SQLServer | Out-Null
Write-OK "Database connection string set"

# ── 8. Get the publish profile for GitHub Secrets ────────────────────────────
Write-Step "Retrieving publish profile for GitHub Secrets..."
$PUBLISH_PROFILE = az webapp deployment list-publishing-profiles `
    --name $WEBAPP_NAME `
    --resource-group $RESOURCE_GROUP `
    --xml
$PUBLISH_PROFILE | Set-Clipboard
Write-OK "Publish profile copied to clipboard!"

# Get Static Web App token
$SWA_TOKEN = az staticwebapp secrets list `
    --name $STATIC_WEB_APP `
    --resource-group $RESOURCE_GROUP `
    --query "properties.apiKey" -o tsv

# ── Done — print summary ──────────────────────────────────────────────────────
Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║                Azure resources created successfully!                 ║" -ForegroundColor Green
Write-Host "╠══════════════════════════════════════════════════════════════════════╣" -ForegroundColor Green
Write-Host "║  Backend API  →  https://$WEBAPP_NAME.azurewebsites.net  ║" -ForegroundColor Green
Write-Host "║  Frontend     →  https://$SWA_URL" -ForegroundColor Green
Write-Host "╠══════════════════════════════════════════════════════════════════════╣" -ForegroundColor Green
Write-Host "║                 NEXT STEPS — GitHub Secrets                          ║" -ForegroundColor Yellow
Write-Host "╠══════════════════════════════════════════════════════════════════════╣" -ForegroundColor Yellow
Write-Host ""
Write-Host "  Go to your GitHub repo → Settings → Secrets → Actions" -ForegroundColor White
Write-Host ""
Write-Host "  1. AZURE_WEBAPP_PUBLISH_PROFILE" -ForegroundColor Cyan
Write-Host "     → Already copied to your clipboard! Just paste it." -ForegroundColor White
Write-Host ""
Write-Host "  2. AZURE_STATIC_WEB_APPS_API_TOKEN" -ForegroundColor Cyan
Write-Host "     → Value: $SWA_TOKEN" -ForegroundColor White
Write-Host ""
Write-Host "  3. AZURE_SQL_CONNECTION_STRING (optional, for future use)" -ForegroundColor Cyan
Write-Host "     → $CONN_STR" -ForegroundColor White
Write-Host ""
Write-Host "  Then go to Settings → Variables → Actions:" -ForegroundColor White
Write-Host "  4. AZURE_WEBAPP_NAME  =  $WEBAPP_NAME" -ForegroundColor Cyan
Write-Host "  5. VITE_API_URL       =  https://$WEBAPP_NAME.azurewebsites.net" -ForegroundColor Cyan
Write-Host ""
Write-Host "  After adding all secrets, push to main to trigger deployment." -ForegroundColor Green
Write-Host ""
