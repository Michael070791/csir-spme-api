# CSIR SPME API V2

.NET 10 Web API for the CSIR Strategic Plan Management and Evaluation system.

## Layout

```text
src/          API, Application, Domain, Infrastructure, ServiceDefaults, AppHost
tests/        Unit, integration, and architecture tests
tools/        Database migrator and legacy import
infra/docker/ Dockerfile and docker-compose for local API + SQL Server
infra/monsterasp/ Staging deployment checklist for csir.runasp.net
```

## Local development

### Docker (API + SQL Server)

```bash
cd infra/docker
SPME_DB_SA_PASSWORD='Your_password1' \
SPME_JWT_KEY='your-jwt-signing-key-at-least-32-bytes-long' \
SPME_ACCOUNT_ACTIVATION_HASH_KEY='your-activation-hash-key-32-bytes-min' \
SPME_PASSWORD_RESET_HASH_KEY='your-password-reset-hash-key-32-bytes' \
SPME_SEED_ADMIN_PASSWORD='Your_admin_password1' \
docker compose up --build
```

API: `http://localhost:5082`  
SQL Server host port: `15433`

### Aspire AppHost

```bash
dotnet run --project src/Csir.Spme.AppHost
```

## API contract

- Routes: `/api/v2/*`
- OpenAPI: `/openapi/v2.json`
- Scalar: `/scalar/v2` (anonymous in Development; PlatformAdmin JWT required in Production)
- Health: `/health`, `/healthz`, `/readyz`

## Tests

```bash
dotnet test Csir.Spme.sln -c Release
```

## MonsterASP staging

See [infra/monsterasp/README.md](infra/monsterasp/README.md).
