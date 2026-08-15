# MonsterASP staging deployment (`csir.runasp.net`)

Treat this host as **staging only**. Free tier: 256 MB RAM, 1 GB SQL, no automatic backups.

## Prerequisites

1. Wait until `csir.runasp.net` is no longer in **Preparing DNS** status.
2. Create a free **MSSQL** database in the MonsterASP control panel.
3. Enable **WebDeploy** under Websites → Manage → Deploy / FTP / WebDeploy / Git.

## Database migration (one-time)

Do **not** run `Database.Migrate()` on API startup.

1. Temporarily enable **remote SQL** on the database (Users and Remote).
2. Generate an idempotent script locally:

```bash
dotnet ef migrations script --idempotent --project src/Csir.Spme.Infrastructure --startup-project src/Csir.Spme.Api --output monsterasp-migration.sql
```

Or run the migrator tool with apply enabled:

```bash
DatabaseMigration__Apply=true dotnet run --project tools/Csir.Spme.Tools.DatabaseMigrator
```

3. Create and download a `.bak` backup after schema is applied.
4. Disable remote SQL access again.

## Environment variables

Set under **Websites → csir.runasp.net → Manage → Scripting → Environment Variables**, then restart the site.

| Key | Value |
|-----|-------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | Internal MonsterASP connection string (`dbXXXX.databaseasp.net`) |
| `Jwt__Issuer` | `csir-spme-api` |
| `Jwt__Audience` | `csir-spme-client` |
| `Jwt__Key` | Random 32+ byte secret (not in git) |
| `AccountActivation__HashKey` | Random secret |
| `PasswordReset__HashKey` | Random secret |
| `Cors__AllowedOrigins__0` | Staff portal HTTPS origin |
| `Cors__AllowedOrigins__1` | HR portal HTTPS origin |
| `DatabaseProvider__UseSqlite` | `false` |
| `Storage__Provider` | `local` |
| `Messaging__DispatcherEnabled` | `false` |
| `OpenApi__ServerUrl` | `https://csir.runasp.net` |

Optional seed (staging only, via env — never commit passwords):

- `Identity__SeedAdmin__UserName`, `Identity__SeedAdmin__Email`, `Identity__SeedAdmin__Password`

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
