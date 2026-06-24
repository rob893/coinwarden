# CI / Infrastructure

## Azure Bicep (`CI/Azure/`)

Provisions a complete environment for the Coinwarden API in a single resource group.

### Resources provisioned

| Module | Resource | Name pattern |
|--------|----------|-------------|
| `logAnalytics.bicep` | Log Analytics Workspace | `<namePrefix>-la-<env>` |
| `appInsights.bicep` | Application Insights (workspace-based) | `<namePrefix>-ai-<env>` |
| `keyVault.bicep` | Key Vault (RBAC auth, soft-delete) | `<namePrefix>-kv-<env>` |
| `postgres.bicep` | PostgreSQL Flexible Server + database | `<namePrefix>-pg-<env>` |
| `appService.bicep` | App Service Plan (Linux) + Web App (.NET 10) | `<namePrefix>-asp-<env>` / `<namePrefix>-api-<env>` |
| `rbac.bicep` | Role assignments for Web App managed identity | — |

### Security model

- The Web App uses a **system-assigned managed identity** — no secrets in app settings.
- RBAC role assignments (via `rbac.bicep`):
  - **Key Vault Secrets User** on the Key Vault → `DefaultAzureCredential` reads secrets at startup.
  - **Monitoring Metrics Publisher** on App Insights → custom metric publishing.
- Key Vault has `enableRbacAuthorization: true`; no legacy access policies.

### Deploy infrastructure manually

```bash
az deployment group create \
  --resource-group <your-rg> \
  --template-file CI/Azure/main.bicep \
  --parameters @CI/Azure/parameters/main.parameters.dev.json \
  --parameters postgresAdminPassword="<your-password>" \
  --mode Incremental
```

### Parameters (`CI/Azure/parameters/main.parameters.dev.json`)

| Parameter | Default | Description |
|-----------|---------|-------------|
| `namePrefix` | `coinwarden` | Short prefix for all resource names (max 16 chars) |
| `environment` | `dev` | Environment tag; maps to `ASPNETCORE_ENVIRONMENT` |
| `postgresAdminLogin` | `pgadmin` | Postgres admin login |
| `postgresAdminPassword` | *(placeholder)* | **Override via secret — never commit a real password** |
| `appServiceSku` | `B1` | App Service plan SKU |

---

## GitHub Actions (`.github/workflows/`)

### `ci.yml` — PR / branch validation

Triggers on `pull_request` and `push` to non-main/non-master branches.

- **api** job: `dotnet restore` → `dotnet build -c Release` → `dotnet test`
- **ui** job: `npm ci` → `npm run lint` → `npm run test` → `npm run build`

### `build-and-deploy-api.yml` — API deploy to Azure App Service

Triggers on push to `main`/`master` (paths: `Coinwarden.API/**`) and `workflow_dispatch`.

**Auth:** OIDC via `azure/login@v2` — no long-lived credentials stored as secrets.

#### Required secrets

| Secret | Description |
|--------|-------------|
| `AZURE_CLIENT_ID` | App registration / managed identity client ID |
| `AZURE_TENANT_ID` | Azure AD tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID |
| `POSTGRES_ADMIN_PASSWORD` | Postgres admin password (Bicep infra deploy only) |

#### Required repository variables

| Variable | Example | Description |
|----------|---------|-------------|
| `AZURE_RESOURCE_GROUP` | `coinwarden-rg-dev` | Resource group to deploy into |
| `AZURE_WEBAPP_NAME` | `coinwarden-api-dev` | App Service web app name |

#### OIDC setup

1. In Azure: create a federated credential on the app registration.
   - Subject: `repo:<owner>/<repo>:ref:refs/heads/main`
2. Grant the app registration **Contributor** on the resource group (or narrower roles as needed).

### `build-and-deploy-ui.yml` — UI deploy to GitHub Pages

Triggers on push to `main`/`master` (paths: `coinwarden-ui/**`) and `workflow_dispatch`.

Uses the official GitHub Pages action flow (`upload-pages-artifact` + `deploy-pages`).

#### Required repository variables

| Variable | Example | Description |
|----------|---------|-------------|
| `VITE_API_BASE_URL` | `https://coinwarden-api-dev.azurewebsites.net` | API base URL injected at build time |

#### GitHub Pages setup

1. **Settings → Pages → Source → GitHub Actions** (not "Deploy from a branch").
2. For a project site (non-user/org), set `base` in `coinwarden-ui/vite.config.ts`:
   ```ts
   base: '/coinwarden-template/'  // replace with your repo name
   ```
   For a custom domain or user/org site (`username.github.io`), use `base: '/'`.
   The UI uses `HashRouter`, so client-side routing works without server rewrites.
