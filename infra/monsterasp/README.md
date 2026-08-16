# MonsterASP staging deployment (`csir.runasp.net`)

Treat this host as **staging only**. Free tier: 256 MB RAM, 1 GB SQL, no automatic backups.

## Prerequisites

1. Wait until `csir.runasp.net` is no longer in **Preparing DNS** status.
2. Create a free **MSSQL** database in the MonsterASP control panel.
3. Enable **WebDeploy** under Websites → Manage → Deploy / FTP / WebDeploy / Git.
4. Do **not** use MonsterASP **Enable Github deploy** for this API. That feature clones source into `/wwwroot`; IIS needs a published `dotnet publish` output deployed via WebDeploy from GitHub Actions.

## Database migration (one-time)

Do **not** run `Database.Migrate()` on API startup.

Run all commands from the **`api/` repository root** (the folder that contains `Csir.Spme.sln`):

```bash
cd /path/to/spme-v2/api
```

1. Temporarily enable **remote SQL** on the MonsterASP database (Users and Remote).
2. Choose **one** of the options below.

### Option A — generate SQL script (run locally, apply in MonsterASP SQL panel)

Requires the `dotnet-ef` global tool:

```bash
dotnet tool install -g dotnet-ef   # once, if not installed
dotnet ef migrations script --idempotent \
  --project src/Csir.Spme.Infrastructure/Csir.Spme.Infrastructure.csproj \
  --startup-project src/Csir.Spme.Api/Csir.Spme.Api.csproj \
  --output monsterasp-migration.sql
```

Upload/run `monsterasp-migration.sql` against the MonsterASP database.

### Option B — apply migrations directly from your machine

Point at the **remote** MonsterASP connection string (while remote SQL is enabled). The migrator loads the same infrastructure options as the API, so supply at least the secrets below:

```bash
export ConnectionStrings__DefaultConnection='Server=dbXXXX.databaseasp.net;Database=dbXXXX;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=true'
export DatabaseProvider__UseSqlite=false
export DatabaseMigration__Apply=true
export PasswordReset__HashKey='your-32-byte-or-longer-secret'

dotnet run --project tools/Csir.Spme.Tools.DatabaseMigrator/Csir.Spme.Tools.DatabaseMigrator.csproj
```

Use the same `PasswordReset__HashKey` value you set in MonsterASP environment variables.

3. Create and download a `.bak` backup after schema is applied.
4. Disable remote SQL access again.

## Environment variables

Set under **Websites → csir.runasp.net → Manage → Scripting → Environment Variables**, then restart the site.

Use double underscores (`__`) for nested keys (e.g. `Jwt__Key` → `Jwt:Key`).

### Required for Production startup

| Key | Value |
|-----|-------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | Internal MonsterASP connection string (`dbXXXX.databaseasp.net`) |
| `Jwt__Key` | Random secret, **at least 32 UTF-8 bytes** (not a placeholder) |
| `AccountActivation__HashKey` | Random secret, **at least 32 UTF-8 bytes** |
| `PasswordReset__HashKey` | Random secret, **at least 32 UTF-8 bytes** |
| `DatabaseProvider__UseSqlite` | `false` |
| `Storage__Provider` | `local` |
| `Messaging__DispatcherEnabled` | `false` |
| `OpenApi__ServerUrl` | `https://csir.runasp.net` (or `http://` until HTTPS is enabled) |

These have safe defaults in `appsettings.json` and usually do not need env overrides unless you want explicit values:

| Key | Default |
|-----|---------|
| `Jwt__Issuer` | `csir-spme-api` |
| `Jwt__Audience` | `csir-spme-client` |
| `Jwt__ExpiryMinutes` | `15` |
| `Jwt__RefreshTokenExpiryDays` | `7` |
| `Storage__ContainerName` | `spme-private` |
| `Storage__ReadUrlLifetime` | `00:05:00` |
| `PasswordReset__TokenLifespan` | `1.00:00:00` (must stay 24 hours) |

### Recommended for staging

| Key | Notes |
|-----|-------|
| `Cors__AllowedOrigins__0` | Staff portal origin (HTTPS when available) |
| `Cors__AllowedOrigins__1` | HR portal origin (HTTPS when available) |
| `PortalUrls__StaffPortalUrl` | Used in emails; must be HTTPS in Production |
| `PortalUrls__HrPortalUrl` | Used in emails; must be HTTPS in Production |
| `PortalUrls__StaffPasswordResetUrl` | Must be HTTPS in Production |
| `PortalUrls__HrPasswordResetUrl` | Must be HTTPS in Production |
| `PortalUrls__LogoUrl` | Optional; leave unset or use HTTPS |
| `Documentation__SiteUrl` | OpenAPI contact/docs link metadata |
| `Documentation__SupportEmail` | OpenAPI support contact (optional) |

If unset, `PortalUrls` fall back to the production URLs already in `appsettings.json` / `appsettings.Production.json`.

### Optional — initial admin users (staging only)

Never commit passwords. Set only while seeding, then remove or rotate.

| Key | Purpose |
|-----|---------|
| `Identity__SeedAdmin__UserName` | Platform admin username |
| `Identity__SeedAdmin__Email` | Platform admin email |
| `Identity__SeedAdmin__Password` | Platform admin password |
| `Identity__SeedHrAdmin__UserName` | HR admin username |
| `Identity__SeedHrAdmin__Email` | HR admin email |
| `Identity__SeedHrAdmin__Password` | HR admin password |
| `Identity__SeedHrAdmin__InstituteCode` | Institute code for seeded HR admin |

### Not required on MonsterASP free staging (disabled by default)

`ZeptoMail` and `MNotify` default to **`Enabled: false`** in `appsettings.json`. With that setting, **no tokens or API keys are required** and the API will start without them. Email/SMS are queued in the outbox but not sent while `Messaging__DispatcherEnabled` is `false`.

When you are ready to send mail/SMS in staging, set `Messaging__DispatcherEnabled` to `true` and configure the provider you enable.

#### ZeptoMail (only if `ZeptoMail__Enabled=true`)

| Key | Required when enabled |
|-----|------------------------|
| `ZeptoMail__Enabled` | `true` |
| `ZeptoMail__SendMailToken` | Yes |
| `ZeptoMail__FromEmail` | Yes |
| `ZeptoMail__FromName` | Optional (default `CSIR SPME System`) |
| `ZeptoMail__ApiBaseUrl` | Optional (default `https://api.zeptomail.com`) |
| `ZeptoMail__AuthSendMailToken` | Only with matching `ZeptoMail__AuthFromEmail` |
| `ZeptoMail__AuthFromEmail` | Only with matching `ZeptoMail__AuthSendMailToken` |
| `ZeptoMail__AuthFromName` | Optional |
| `ZeptoMail__NotifySendMailToken` | Only with matching `ZeptoMail__NotifyFromEmail` |
| `ZeptoMail__NotifyFromEmail` | Only with matching `ZeptoMail__NotifySendMailToken` |
| `ZeptoMail__NotifyFromName` | Optional |
| `ZeptoMail__BounceAddress` | Optional |
| `ZeptoMail__WebhookSecret` | Optional (webhook verification) |
| `ZeptoMail__TrackOpens` | Optional (`true` / `false`) |
| `ZeptoMail__TrackClicks` | Optional (`true` / `false`) |
| `ZeptoMail__TimeoutSeconds` | Optional (default `30`) |

#### MNotify (only if `MNotify__Enabled=true`)

| Key | Required when enabled |
|-----|------------------------|
| `MNotify__Enabled` | `true` |
| `MNotify__ApiKey` | Yes |
| `MNotify__SenderId` | Yes (max 11 chars; default `CSIR`) |
| `MNotify__BaseUrl` | Optional (default `https://api.mnotify.com/api`) |
| `MNotify__SmsEndpoint` | Optional |
| `MNotify__OtpExpiryMinutes` | Optional |
| `MNotify__OtpLength` | Optional |
| `MNotify__OtpMessageTemplate` | Optional |

### Not used with `Storage__Provider=local`

Do **not** set these on MonsterASP unless you switch to Azure Blob storage:

- `ConnectionStrings__BlobStorage`
- `Storage__ServiceUri`
- `Storage__ExternalServiceUri`
- `Storage__CreateContainer`
- `Storage__ManagedIdentityClientId`

### Upload limits (optional overrides)

Defaults in `appsettings.json` are fine unless you need different limits:

- `PromotionUploadOptions__MaximumFileBytes` (default 200 MiB)
- `StaffReportUploadOptions__*` (concept note, image sizes, session minutes)
- `ProfileDocumentOptions__MaximumFileBytes`, `ProfileDocumentOptions__UploadSessionMinutes`

### Pagination (optional)

- `Pagination__CursorSigningKey` — only if you want a separate HMAC key; otherwise JWT key is reused
- `Pagination__DefaultLimit`, `Pagination__MaxLimit` — optional tuning

## GitHub Actions secrets (WebDeploy)

| Secret | Source |
|--------|--------|
| `WEBSITE_NAME` | WebDeploy site id (`siteXXXX`) |
| `SERVER_COMPUTER_NAME` | WebDeploy URL (`https://siteXXXX.siteasp.net:8172`) |
| `SERVER_USERNAME` | WebDeploy username |
| `SERVER_PASSWORD` | WebDeploy password |

Never commit `.publishsettings` or `.pubxml.user` files.

## API documentation on staging

- OpenAPI: `/openapi/v2.json`
- Scalar: `/scalar/v2`
- Both require a **PlatformAdmin** JWT in Production (anonymous in Development/Test).
- Public smoke test: `GET /health` (no auth).

## HTTPS

Enable Let's Encrypt in the control panel only after `/health` returns 200. Confirm HTTPS works before sending real credentials or JWTs.

## Post-deploy checks

| Check | Expected |
|-------|----------|
| `GET /health` | 200 |
| `GET /readyz` (after DB wired) | 200 |
| Protected API without JWT | 401 |
| `GET /openapi/v2.json` without JWT | 401 |
| `GET /scalar/v2` without JWT | 401 |
| `GET /metrics` | 404 (not mapped in Production) |

Monitor RAM in the MonsterASP resource panel. Stay under ~200 MB sustained on the free plan.

## Troubleshooting HTTP 500.30 (app failed to start)

This means the ASP.NET Core process crashed during startup — before `/health` can run. It is **not** a migration or routing issue.

Check in this order:

1. **Environment variables** in MonsterASP (Scripting → Environment Variables). All three secrets must be **32+ UTF-8 bytes** and not placeholders:
   - `Jwt__Key`
   - `AccountActivation__HashKey`
   - `PasswordReset__HashKey` (same value used for the migrator)
2. **Connection string** uses the **internal** MonsterASP SQL host (`dbXXXX.databaseasp.net`), not the temporary remote connection string used for migration.
3. **Restart the site** after changing env vars.
4. **Stdout logs** under `/wwwroot/logs/` after redeploy (deploy workflow enables `stdoutLogEnabled`). Open the newest `stdout_*.log` in the MonsterASP file manager — the exception message is usually on the last lines.
5. **Architecture** — publish target must be `win-x64` for MonsterASP IIS (not `win-x86`).

Common log messages:

| Error | Fix |
|-------|-----|
| `Jwt:Key must come from a secret provider...` | Set `Jwt__Key` (32+ bytes) |
| `AccountActivation:HashKey must...` | Set `AccountActivation__HashKey` |
| `PasswordReset:HashKey must...` | Set `PasswordReset__HashKey` |
| `ConnectionStrings:DefaultConnection is required` | Set internal SQL connection string |
| `Could not load file or assembly` / bitness mismatch | Redeploy with `win-x64` publish |
