# Coinwarden

> A **batteries-included** full-stack template: a .NET 10 Web API + a React SPA, wired for auth,
> observability, and one-click-ish deployment to Azure App Service (API) and GitHub Pages (UI).

Click **“Use this template”** on GitHub to start a new project, then follow
[Using this template](#using-this-template) to rename things.

## What's included

- **Versioned REST API** (`/api/v1`, `/api/v2`) with an interactive **Scalar** UI.
- **Authentication** out of the box: ASP.NET Identity, JWT access tokens, HttpOnly refresh-token
  cookies with double-submit CSRF, and **Google + GitHub social logins**.
- **EF Core + PostgreSQL** with the repository pattern, a generated initial migration, and a seeder CLI.
- **Result pattern** (`Result<T>` → `ProblemDetailsWithErrors`) and **cursor-based pagination**.
- **Observability**: Application Insights via OpenTelemetry, correlation IDs on every response.
- **Secret management**: Azure Key Vault config provider (non-Dev), `appsettings.Local.json` (Dev).
- CORS, rate limiting, health checks, global exception handling, forwarded-headers hardening.
- **React 19 + Vite + Tailwind v4 + shadcn/ui** SPA with TanStack Query, axios auth plumbing, and a
  sample authenticated page that calls the API and pages through data.
- **CI/CD**: GitHub Actions (PR validation, API → App Service via OIDC, UI → GitHub Pages) and
  **Azure Bicep** that provisions the whole environment.
- Sample **Hello World** (v1 + v2) and a **Notes** resource demonstrating the full stack end-to-end
  (these are disposable demos — see [Using this template](#using-this-template) to remove them).

## Tech stack

| Layer    | Tech                                                                                          |
| -------- | --------------------------------------------------------------------------------------------- |
| Backend  | .NET 10, ASP.NET Core, EF Core, PostgreSQL, ASP.NET Identity, JWT, OpenTelemetry/App Insights |
| Frontend | React 19, Vite, TypeScript, Tailwind v4, shadcn/ui, TanStack Query, axios                     |
| Infra    | Azure Bicep — App Service, App Insights + Log Analytics, Key Vault, Postgres Flexible Server  |
| CI/CD    | GitHub Actions (OIDC to Azure; GitHub Pages)                                                  |
| Tests    | xUnit + Moq (API), Vitest (UI unit), Playwright (UI e2e)                                      |

## Repository structure

```
.
├─ Coinwarden.API/          # .NET 10 Web API (extension-driven startup, v1/v2, auth, EF Core)
├─ Coinwarden.API.Tests/    # xUnit + Moq, mirrored folders
├─ coinwarden-ui/          # React + Vite SPA (shadcn/ui, Vitest)
├─ CI/Azure/                # Bicep infra (modules + dev parameters) — see CI/README.md
├─ .github/workflows/       # ci.yml, build-and-deploy-api.yml, build-and-deploy-ui.yml
├─ AGENTS.md                # architecture & conventions (read this!)
└─ Coinwarden.slnx          # solution
```

See **[`AGENTS.md`](./AGENTS.md)** for architecture/conventions and **[`CI/README.md`](./CI/README.md)**
for the full infrastructure and deployment guide.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 24+](https://nodejs.org/)
- A PostgreSQL instance (local Docker is fine: `docker run --name pg -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d postgres`)
- (Optional) [`dotnet-ef`](https://learn.microsoft.com/ef/core/cli/dotnet): `dotnet tool install --global dotnet-ef`
- (Optional, for deploy) Azure CLI + Bicep

## Quick start

### 1. API

```bash
# From the repo root — create your local secrets file:
cp Coinwarden.API/appsettings.Local.example.json Coinwarden.API/appsettings.Local.json
# Edit it: set Postgres connection, a JWT APISecret (>= 64 chars, for HMAC-SHA512), and OAuth client IDs/secrets.

# Apply the migration to your database:
dotnet ef database update --project Coinwarden.API
# ...or use the seeder CLI:  dotnet run --project Coinwarden.API -- seeder migrate seed --password <SeederPassword>

# Trust the ASP.NET dev cert (once) so the HTTPS refresh-cookie flow works:
dotnet dev-certs https --trust

# Run with hot reload:
npm run start                        # => https://localhost:7234
```

Open the **Scalar API docs** at `https://localhost:7234/scalar/v1` (and `/scalar/v2`). The
`GET /api/v1/hello/ping` endpoint is anonymous; `GET /api/v1/hello` and the `Notes` endpoints require a
bearer token (register/login via `/api/v1/auth/*`).

### 2. UI

```bash
cd coinwarden-ui
npm install
# .env.local already points at the local API; adjust VITE_API_BASE_URL if needed.
npm run dev                          # => http://localhost:5173
```

## Configuration & secrets

Configuration is layered: `appsettings.json` → `appsettings.{Environment}.json` →
`appsettings.Local.json` (Dev only, git-ignored) → **Azure Key Vault** (non-Dev).

### Local (development)

Put secrets in `Coinwarden.API/appsettings.Local.json` (copied from the `.example`). Never commit it.

### Production (Azure Key Vault)

In non-Development environments the app loads secrets from the Key Vault at `KeyVaultUrl` using its
managed identity. The `PrefixKeyVaultSecretManager` only reads secrets prefixed with `Coinwarden--` (or
`All--`) and maps `--` → `:`. Create these Key Vault secrets:

| Key Vault secret name                                 | Maps to config key                           |
| ----------------------------------------------------- | -------------------------------------------- |
| `Coinwarden--Authentication--APISecret`               | `Authentication:APISecret` (JWT signing key) |
| `Coinwarden--Postgres--DefaultConnection`             | `Postgres:DefaultConnection`                 |
| `Coinwarden--Authentication--GoogleOAuthClientSecret` | Google OAuth secret                          |
| `Coinwarden--Authentication--GitHubOAuthClientSecret` | GitHub OAuth secret                          |

> The Application Insights connection string is injected by the Bicep as the `ApplicationInsightsConnectionString`
> app setting, so it does **not** need a Key Vault secret. You may still override it with one
> (`Coinwarden--ApplicationInsightsConnectionString`) if you prefer.

After deploying, also add your GitHub Pages origin to the API's allowed CORS origins (via
`appsettings.Production.json`, an app setting `Cors__AllowedOrigins__0`, or Key Vault).

## API at a glance

- **Versioning**: URL segment — `GET /api/v1/...`, `GET /api/v2/...`.
- **Auth**: global `[Authorize]`; opt out with `[AllowAnonymous]`. JWT bearer + refresh-token cookie + CSRF.
- **Errors**: `ProblemDetailsWithErrors` + `X-Correlation-Id` header on every response.
- **Pagination**: cursor-based — query with `?first=20&after=<cursor>&includeTotal=true`.
- **Sample endpoints**: `GET /api/v1/hello`, `GET /api/v2/hello`, `GET /api/v1/hello/ping`,
  and full CRUD under `GET|POST|PUT|DELETE /api/v1/notes`.

## Testing

```bash
dotnet test                                   # API: xUnit + Moq
cd coinwarden-ui && npm run test             # UI unit: Vitest
cd coinwarden-ui && npm run test:e2e         # UI e2e: Playwright (needs the app running)
```

## Deployment

Full details (Bicep resources, OIDC federated-credential setup, required secrets/variables, GitHub Pages
configuration, base-path notes) are in **[`CI/README.md`](./CI/README.md)**. In short:

1. Provision infra: `az deployment group create --resource-group <rg> --template-file CI/Azure/main.bicep --parameters @CI/Azure/parameters/main.parameters.dev.json --parameters postgresAdminPassword=<pw>`.
2. Configure repo **secrets** (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `POSTGRES_ADMIN_PASSWORD`) and **variables** (`AZURE_RESOURCE_GROUP`, `AZURE_WEBAPP_NAME`, `VITE_API_BASE_URL`).
3. Push to `main` — the API deploys to App Service (OIDC) and the UI deploys to GitHub Pages.

## Using this template

1. Click **“Use this template”** on GitHub (or clone).
2. Pick your app name and replace `Coinwarden` / `coinwarden` throughout:
   - Solution/projects: rename `Coinwarden.API`, `Coinwarden.API.Tests`, `Coinwarden.slnx` and the
     `RootNamespace`/`AssemblyName`/`Product` in the `.csproj`.
   - Do a find-and-replace for `Coinwarden` (namespaces, `PrefixKeyVaultSecretManager` prefixes in
     `Program.cs`) and `coinwarden` (UI package name, Bicep `project` tag, `namePrefix`).
3. Update `LICENSE` copyright, this `README`, and `AGENTS.md`.
4. Set your real values in `appsettings.Local.json` / Key Vault and the `.env` files.
5. Configure OAuth apps (Google, GitHub) with redirect URIs pointing at `/api/v1/auth/{provider}/callback`.
6. **Enable CI/CD.** The GitHub Actions triggers are intentionally commented out so this template repo
   stays idle. Once your secrets/variables are configured, uncomment the `pull_request`/`push` triggers
   in `.github/workflows/ci.yml`, `build-and-deploy-api.yml`, and `build-and-deploy-ui.yml`.
7. **Remove the sample code.** The `Hello` endpoints and the `Notes` resource are demos that exercise the
   full stack — delete them once you start building your own features:
   - **Hello:** `Coinwarden.API/Controllers/V1/HelloController.cs`, `Controllers/V2/HelloController.cs`,
     `Models/Responses/HelloResponse.cs`, and `Coinwarden.API.Tests/Controllers/HelloControllerTests.cs`.
   - **Notes:** `Controllers/V1/NotesController.cs`, `Services/Domain/NoteService.cs` (+ `INoteService.cs`),
     `Data/Repositories/NoteRepository.cs` (+ `INoteRepository.cs`), `Models/Entities/Note.cs`,
     `Models/Dtos/NoteDto.cs`, `Models/Requests/CreateNoteRequest.cs` / `UpdateNoteRequest.cs`,
     `Models/QueryParameters/NoteQueryParameters.cs`, and `Coinwarden.API.Tests/Services/NoteServiceTests.cs`.
   - Then remove their wiring: the `DbSet<Note>` + `builder.Entity<Note>` config in `Data/DataContext.cs`,
     the `Notes` navigation in `Models/Entities/User.cs`, the `INoteService`/`INoteRepository` registrations
     in `ApplicationStartup/ServiceCollectionExtensions/`, any Notes seeding in `Data/DatabaseSeeder.cs`,
     and the Notes table in the EF migration (or regenerate the initial migration).
   - **UI:** replace `coinwarden-ui/src/pages/HomePage.tsx` and remove the `notes`/`hello` helpers in
     `src/hooks/api.ts`, `src/services/api.ts`, and `src/types/models.ts`.

## License

[MIT](./LICENSE) — update the copyright holder for your project.
