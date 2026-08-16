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

**Important — two connection strings:**

| Use case | Host in connection string |
|----------|---------------------------|
| MonsterASP **website env vars** (API on hosting) | **Local/internal**: `dbXXXX.databaseasp.net` |
| **Migrator from your PC** or SSMS | **Public/remote**: `dbXXXX.public.databaseasp.net` (only while remote access is **Enabled**) |

Remote connections from your machine **cannot** use the internal/local string. Both need `Encrypt=True;TrustServerCertificate=True` when connecting from outside MonsterASP.

### Option A — run SQL script in MonsterASP (no remote access from PC)

**Do not use `--idempotent` in MonsterASP's web SQL runner.** Idempotent EF scripts split one migration into multiple `IF` blocks; SQL Server validates column names at compile time and fails with errors like `Invalid column name 'DisplayName'` even when an earlier block would add the column.

**Fresh empty database** — generate a linear script (no `--idempotent`):

```bash
cd /path/to/spme-v2/api
dotnet ef migrations script \
  --project src/Csir.Spme.Infrastructure/Csir.Spme.Infrastructure.csproj \
  --startup-project src/Csir.Spme.Api/Csir.Spme.Api.csproj \
  --output monsterasp-migration-fresh.sql
```

Run `monsterasp-migration-fresh.sql` in MonsterASP **only on an empty database** (no existing tables). If a previous attempt partially created objects, delete/recreate the database in the control panel first, or drop all user tables/schemas before running.

A copy may already exist locally as `api/monsterasp-migration-fresh.sql`.

### Option B — apply migrations with EF migrator (recommended if remote SQL works)

Requires **remote SQL enabled** and the **public** connection string from the control panel (Users and Remote → show connection string):

```bash
export ConnectionStrings__DefaultConnection='Server=dbXXXX.public.databaseasp.net;Database=dbXXXX;User Id=dbXXXX;Password=...;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=true'
export DatabaseProvider__UseSqlite=false
export DatabaseMigration__Apply=true
export DatabaseMigration__ConnectionTimeoutSeconds=120
export PasswordReset__HashKey='your-32-byte-or-longer-secret'

dotnet run --project tools/Csir.Spme.Tools.DatabaseMigrator/Csir.Spme.Tools.DatabaseMigrator.csproj
```

Use the same `PasswordReset__HashKey` value you set in MonsterASP environment variables.

3. Create and download a `.bak` backup after schema is applied.
4. Disable remote SQL access again.

## Seeding business data (employees, institutes, leave, etc.)

Schema migration alone creates **empty tables**. The API startup only seeds:

| Source | What it adds |
|--------|----------------|
| `IdentitySeedHostedService` | System roles, permission claims, optional PlatformAdmin/HR admin users, employee Identity accounts (when employees already exist) |
| `PromotionCatalogSeedHostedService` | Canonical Senior Staff grades, Sections 20-22 paths, and the open **2027** (1 January) promotion cycle |
| `PromotionRequirementTemplateSeedHostedService` | Promotion document templates **only after** promotion cycles/paths exist |
| `PromotionDemoStaffSeedHostedService` | Optional demo Senior Staff (eligible via verified B.Sc.) and Senior Member (coming soon) logins |

**All CSIR business data** (institutes, ~2,500 employees, leave, planning, projects, reports, memos, legacy users) comes from **`Csir.Spme.Tools.LegacyImport`** reading the July 2026 legacy BACPACs. See [`docs/legacy-import.md`](../../../docs/legacy-import.md).

**MonsterASP free tier warning:** 1 GB SQL / 256 MB RAM. A full legacy import may fit, but monitor database size in the control panel. Legacy BACPAC restore runs **locally**; only the import **target** is MonsterASP.

### Prerequisites

1. Schema applied on `db63934` (22 migrations — you already have this).
2. **At least one user** in the target DB (PlatformAdmin preferred). Restart the API once with `Identity__SeedAdmin__*` env vars set, or confirm a user exists:

```sql
SELECT u.UserName, r.Name
FROM iam.Users u
JOIN iam.UserRoles ur ON ur.UserId = u.Id
JOIN iam.Roles r ON r.Id = ur.RoleId;
```

3. **Remote SQL enabled** on MonsterASP while importing from your PC.
4. Local SQL Server (Docker `api/infra/docker` or existing) with legacy BACPACs restored.

### Step 1 — restore legacy sources locally

BACPAC paths (from project docs):

- `/home/csir/Documents/CSIR/BUCKUP/22-06-2026/csir-auth-spme-db-2026-7-28-22-32.bacpac`
- `/home/csir/Documents/CSIR/BUCKUP/22-06-2026/csir-spme-db-2026-7-28-22-33.bacpac`

```bash
cd /home/csir/Desktop/projects/spme-v2
export BACKUP_DIR=/home/csir/Documents/CSIR/BUCKUP/22-06-2026
export SQL_SERVER=localhost,15433
export SQL_PASSWORD='your-local-sa-password'
bash scripts/legacy/restore-bacpacs.sh
```

This creates read-only source databases `LegacyAuthSpme` and `LegacySpme` on your machine.

### Step 2 — dry-run import into MonsterASP

```bash
cd /home/csir/Desktop/projects/spme-v2/api

export LEGACY_AUTH_CONNECTION_STRING='Server=localhost,15433;Database=LegacyAuthSpme;User Id=sa;Password=...;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=true'
export LEGACY_SPME_CONNECTION_STRING='Server=localhost,15433;Database=LegacySpme;User Id=sa;Password=...;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=true'
export TARGET_CONNECTION_STRING='Server=db63934.public.databaseasp.net;Database=db63934;User Id=db63934;Password=...;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=true'

dotnet run --project tools/Csir.Spme.Tools.LegacyImport/Csir.Spme.Tools.LegacyImport.csproj -- \
  --auth-backup-path /home/csir/Documents/CSIR/BUCKUP/22-06-2026/csir-auth-spme-db-2026-7-28-22-32.bacpac \
  --spme-backup-path /home/csir/Documents/CSIR/BUCKUP/22-06-2026/csir-spme-db-2026-7-28-22-33.bacpac \
  --dry-run
```

Dry-run applies everything in a transaction and **rolls back** — safe verification.

### Step 3 — apply import

```bash
dotnet run --project tools/Csir.Spme.Tools.LegacyImport/Csir.Spme.Tools.LegacyImport.csproj -- \
  --auth-backup-path /home/csir/Documents/CSIR/BUCKUP/22-06-2026/csir-auth-spme-db-2026-7-28-22-32.bacpac \
  --spme-backup-path /home/csir/Documents/CSIR/BUCKUP/22-06-2026/csir-spme-db-2026-7-28-22-33.bacpac \
  --apply
```

Expect roughly **19k+ inserts** (employees, leave, org structure, reports, etc.). Re-running with the same BACPAC checksum is a no-op.

### Step 4 — post-import

1. Download a `.bak` backup from MonsterASP.
2. **Disable remote SQL**.
3. **Restart** the website.
4. Verify: `curl http://csir.runasp.net/readyz` and log in with a seeded or imported account.

### What is not imported automatically

- Promotion cycles, grades, equivalencies (configure via HR API)
- File attachments / blob bytes (metadata may be quarantined in `ops.LegacyImportIssues`)
- Legacy management roles mapped to V2 (`Admin`/`HR`/`Director` stay as policy-ambiguity — assign `HrAdmin` explicitly in V2)

### Minimal staging (no legacy data)

If you only need admin login and empty institutes, skip LegacyImport. Set `Identity__SeedAdmin__*` and `Identity__SeedHrAdmin__*` env vars, restart the API, then create data through the HR portal/API.

## Environment variables

Set under **Websites → csir.runasp.net → Manage → Scripting → Environment Variables**, then restart the site.

**Complete paste list (every `appsettings` key, including ZeptoMail and MNotify):**
[`environment-variables.md`](./environment-variables.md)

Use double underscores (`__`) for nested keys (e.g. `Jwt__Key` → `Jwt:Key`). Copy secret values from `dotnet user-secrets list --project src/Csir.Spme.Api`. Do not commit those secrets.

Minimum keys the process will not start without:

| Key | Value |
|-----|-------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | Internal MonsterASP connection string (`dbXXXX.databaseasp.net`) |
| `Jwt__Key` | Random secret, **at least 32 UTF-8 bytes** (not a placeholder) |
| `AccountActivation__HashKey` | Random secret, **at least 32 UTF-8 bytes** |
| `PasswordReset__HashKey` | Random secret, **at least 32 UTF-8 bytes** |
| `DatabaseProvider__UseSqlite` | `false` |
| `Storage__Provider` | `local` |
| `OpenApi__ServerUrl` | `http://csir.runasp.net` until HTTPS is enabled |

To send mail and SMS on this host, also set every ZeptoMail and MNotify key from the complete list and set `Messaging__DispatcherEnabled=true`. Auth/notify ZeptoMail token+sender pairs must be set together. Restart after saving.

Imported employees still need a canonical `GradeId` before they can be assessed. Do not map job titles automatically.

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
