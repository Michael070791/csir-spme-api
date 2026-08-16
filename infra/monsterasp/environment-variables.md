# MonsterASP environment variables (`csir.runasp.net`)

Set these under **Websites → csir.runasp.net → Manage → Scripting → Environment Variables**, then **restart** the site.

Nested `appsettings.json` keys use double underscores: `Jwt:Key` → `Jwt__Key`.

Copy secret values from this machine (do not commit them):

```bash
cd /path/to/spme-v2/api
dotnet user-secrets list --project src/Csir.Spme.Api
```

Those User Secrets already contain `Jwt:Key`, `AccountActivation:HashKey`, `PasswordReset:HashKey`, ZeptoMail tokens, and the MNotify API key. Paste the same values into MonsterASP. Keep `ConnectionStrings:DefaultConnection` as the **internal** MonsterASP SQL string (`dbXXXX.databaseasp.net`), not the local Docker string and not the public remote host.

Do **not** set `DatabaseMigration__Apply` on the website.

---

## 1. Process and database (required)

| Key | Value |
|-----|-------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `DatabaseProvider__UseSqlite` | `false` |
| `ConnectionStrings__DefaultConnection` | Internal MonsterASP SQL string. Must include `Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=true` |

Do not set `DatabaseProvider__SqlitePath`. Do not set `ConnectionStrings__BlobStorage`.

---

## 2. Signing secrets (required, 32+ UTF-8 bytes)

| Key | Value |
|-----|-------|
| `Jwt__Key` | From User Secrets `Jwt:Key` |
| `Jwt__Issuer` | `csir-spme-api` |
| `Jwt__Audience` | `csir-spme-client` |
| `Jwt__ExpiryMinutes` | `15` |
| `Jwt__RefreshTokenExpiryDays` | `7` |
| `AccountActivation__HashKey` | From User Secrets `AccountActivation:HashKey` |
| `AccountActivation__OtpExpiryMinutes` | `10` |
| `AccountActivation__OtpLength` | `6` |
| `AccountActivation__MaximumAttempts` | `5` |
| `AccountActivation__ResendLimit` | `3` |
| `AccountActivation__ResendWindowMinutes` | `60` |
| `PasswordReset__HashKey` | From User Secrets `PasswordReset:HashKey` (same value the migrator used) |
| `PasswordReset__TokenLifespan` | `1.00:00:00` |
| `PasswordReset__PermitLimit` | `5` |

---

## 3. CORS, portals, OpenAPI, email-change links (required)

Portals talk to the API through Vercel same-origin proxies, but CORS and branded email still need these HTTPS origins.

| Key | Value |
|-----|-------|
| `Cors__AllowedOrigins__0` | `https://www.portal.csirstrategicplan.org` |
| `Cors__AllowedOrigins__1` | `https://portal.csirstrategicplan.org` |
| `Cors__AllowedOrigins__2` | `https://hr.csirstrategicplan.org` |
| `Cors__AllowedOrigins__3` | `https://docs.csirstrategicplan.org` |
| `PortalUrls__StaffPortalUrl` | `https://www.portal.csirstrategicplan.org` |
| `PortalUrls__HrPortalUrl` | `https://hr.csirstrategicplan.org` |
| `PortalUrls__StaffPasswordResetUrl` | `https://www.portal.csirstrategicplan.org/reset-password` |
| `PortalUrls__HrPasswordResetUrl` | `https://hr.csirstrategicplan.org/reset-password` |
| `PortalUrls__LogoUrl` | leave empty unless you have an HTTPS logo URL |
| `EmailChange__Url` | `https://www.portal.csirstrategicplan.org/settings` |
| `OpenApi__ServerUrl` | `http://csir.runasp.net` (change to `https://csir.runasp.net` after Let's Encrypt) |
| `Documentation__SiteUrl` | `https://docs.csirstrategicplan.org` |
| `Documentation__SupportEmail` | `admin@csirstrategicplan.org` |

---

## 4. Storage (required for this host)

MonsterASP free staging uses local disk, not Azure Blob.

| Key | Value |
|-----|-------|
| `Storage__Provider` | `local` |
| `Storage__ContainerName` | `spme-private` |
| `Storage__ReadUrlLifetime` | `00:05:00` |
| `Storage__CreateContainer` | `false` |

Do **not** set `Storage__ServiceUri`, `Storage__ExternalServiceUri`, or `Storage__ManagedIdentityClientId`.

---

## 5. ZeptoMail (enable on this host)

Local User Secrets already have ZeptoMail enabled with verified sender `admin@csirstrategicplan.org`. Copy the three tokens from User Secrets. Auth and notify pairs must stay together.

| Key | Value |
|-----|-------|
| `ZeptoMail__Enabled` | `true` |
| `ZeptoMail__ApiBaseUrl` | `https://api.zeptomail.com` |
| `ZeptoMail__SendMailToken` | From User Secrets `ZeptoMail:SendMailToken` |
| `ZeptoMail__FromEmail` | `admin@csirstrategicplan.org` |
| `ZeptoMail__FromName` | `CSIR SPME System` |
| `ZeptoMail__AuthSendMailToken` | From User Secrets `ZeptoMail:AuthSendMailToken` |
| `ZeptoMail__AuthFromEmail` | `admin@csirstrategicplan.org` |
| `ZeptoMail__AuthFromName` | `CSIR STRATEGIC PLAN` |
| `ZeptoMail__NotifySendMailToken` | From User Secrets `ZeptoMail:NotifySendMailToken` |
| `ZeptoMail__NotifyFromEmail` | `admin@csirstrategicplan.org` |
| `ZeptoMail__NotifyFromName` | `CSIR SPME System` |
| `ZeptoMail__BounceAddress` | leave empty unless ZeptoMail gave you a bounce address |
| `ZeptoMail__WebhookSecret` | From local `.env` `SPME_ZEPTOMAIL_WEBHOOK_SECRET` if you use inbound webhooks; otherwise leave empty |
| `ZeptoMail__TrackOpens` | `false` |
| `ZeptoMail__TrackClicks` | `false` |
| `ZeptoMail__TimeoutSeconds` | `30` |

---

## 6. MNotify (enable on this host)

| Key | Value |
|-----|-------|
| `MNotify__Enabled` | `true` |
| `MNotify__ApiKey` | From User Secrets `MNotify:ApiKey` |
| `MNotify__SenderId` | `CSIR` |
| `MNotify__BaseUrl` | `https://api.mnotify.com/api` |
| `MNotify__SmsEndpoint` | `/sms/quick` |
| `MNotify__DeliveryReportEndpoint` | `/campaign/{campaignId}/{status}` |
| `MNotify__RequestTimeoutSeconds` | `8` |
| `MNotify__RetryCount` | `1` |
| `MNotify__RetryDelayMilliseconds` | `250` |
| `MNotify__OtpExpiryMinutes` | `10` |
| `MNotify__OtpLength` | `6` |
| `MNotify__OtpMessageTemplate` | `Your CSIR verification code is %otp_code%. It expires in %expiry% minutes.` |

---

## 7. Messaging dispatcher (required if ZeptoMail or MNotify is enabled)

If this stays `false`, activation/reset/OTP mail and SMS are written to the outbox and never sent.

| Key | Value |
|-----|-------|
| `Messaging__DispatcherEnabled` | `true` |
| `Messaging__WorkerBatchSize` | `50` |
| `Messaging__MaximumAttempts` | `8` |
| `Messaging__LeaseSeconds` | `60` |

Watch RAM after enabling the dispatcher on the free 256 MB plan. If the site recycles, lower `Messaging__WorkerBatchSize` to `10`.

---

## 8. Upload limits

| Key | Value |
|-----|-------|
| `PromotionUploadOptions__MaximumFileBytes` | `209715200` |
| `PromotionUploadOptions__UploadSessionMinutes` | `60` |
| `StaffReportUploadOptions__ConceptNoteMaximumFileBytes` | `62914560` |
| `StaffReportUploadOptions__ImageMaximumFileBytes` | `20971520` |
| `StaffReportUploadOptions__MaximumImagesPerReport` | `3` |
| `StaffReportUploadOptions__UploadSessionMinutes` | `60` |
| `ProfileDocumentOptions__MaximumFileBytes` | `52428800` |
| `ProfileDocumentOptions__UploadSessionMinutes` | `60` |

Do not set `PromotionUploadOptions__DevelopmentScanResult` or `StaffReportUploadOptions__DevelopmentScanResult` in Production.

---

## 9. Pagination and idempotency

| Key | Value |
|-----|-------|
| `Pagination__DefaultLimit` | `50` |
| `Pagination__MaxLimit` | `100` |
| `Idempotency__MaximumStoredResponseBytes` | `262144` |

Leave `Pagination__CursorSigningKey` unset so the API reuses `Jwt__Key`.

---

## 10. Optional staging logins

Set these only if you want seeded accounts. Passwords are not stored in this file. After the first successful seed you can remove the password keys.

### Platform admin

| Key | Example |
|-----|---------|
| `Identity__SeedAdmin__UserName` | your platform admin username |
| `Identity__SeedAdmin__Email` | your platform admin email |
| `Identity__SeedAdmin__Password` | strong unique password |

### Institute HR admin

`Identity__SeedHrAdmin__InstituteCode` must already exist in the database (Production will not create `DEV-HR`).

| Key | Example |
|-----|---------|
| `Identity__SeedHrAdmin__UserName` | your HR admin username |
| `Identity__SeedHrAdmin__Email` | your HR admin email |
| `Identity__SeedHrAdmin__Password` | strong unique password |
| `Identity__SeedHrAdmin__InstituteCode` | existing institute code (for example `CSIR`) |

### Demo Senior Staff (can begin a 2027 application)

| Key | Value |
|-----|-------|
| `Identity__SeedDemoStaff__UserName` | `demo.seniorstaff` |
| `Identity__SeedDemoStaff__Email` | `demo.seniorstaff@csir.local` |
| `Identity__SeedDemoStaff__Password` | staging-only password |
| `Identity__SeedDemoStaff__InstituteCode` | existing institute code, or leave empty to use the first active institute |
| `Identity__SeedDemoStaff__StaffId` | `DEMO-SS-001` |

### Demo Senior Member (coming soon, cannot apply)

| Key | Value |
|-----|-------|
| `Identity__SeedDemoSeniorMember__UserName` | `demo.seniormember` |
| `Identity__SeedDemoSeniorMember__Email` | `demo.seniormember@csir.local` |
| `Identity__SeedDemoSeniorMember__Password` | staging-only password |
| `Identity__SeedDemoSeniorMember__InstituteCode` | same institute rule as demo Senior Staff |
| `Identity__SeedDemoSeniorMember__StaffId` | `DEMO-SM-001` |

---

## 11. Logging (optional)

| Key | Value |
|-----|-------|
| `Logging__LogLevel__Default` | `Information` |
| `Logging__LogLevel__Microsoft.AspNetCore` | `Warning` |

`AllowedHosts` already defaults to `*` in `appsettings.json`.

---

## Do not set on this host

- `DatabaseMigration__Apply`
- `DatabaseMigration__ConnectionTimeoutSeconds`
- `ConnectionStrings__BlobStorage`
- `Storage__ServiceUri`
- `Storage__ExternalServiceUri`
- `Storage__ManagedIdentityClientId`
- `Storage__CreateContainer=true`
- `PromotionUploadOptions__DevelopmentScanResult`
- `StaffReportUploadOptions__DevelopmentScanResult`
- `ProfileDocumentOptions__DevelopmentScanResult`

---

## After saving

1. Restart the website.
2. `GET http://csir.runasp.net/health` → 200.
3. `GET http://csir.runasp.net/readyz` → 200.
4. Request a password reset or account activation to a mailbox you control and confirm ZeptoMail delivery.
5. Confirm an activation SMS on a Ghana number you control if MNotify is in use.
6. Watch the MonsterASP RAM panel.
