# Authentication

Entra applications, APIM JWT validation, and the Order-to-Coupon managed-identity hop (**CS-28**). Secrets never live in git; client IDs and placeholders do.

## Principals

| Principal | Identity | Roles | Reaches |
|---|---|---|---|
| Customer | Entra External ID (or workforce stand-in) SPA via auth code + PKCE | none required at role level for preview/orders | APIM **customer** product → preview, pizzas, orders |
| Campaign manager | Workforce Entra ID | `Coupon.Admin` | APIM **admin** product → `/v1/admin/policies`, manifest |
| Order API | User-assigned managed identity `id-order-api-{env}` | `Coupon.Redeem` | Direct call to Coupon Service `/v1/reservations*` — **not** through APIM |

## Entra app registrations (one-time)

Create three app registrations in the demo tenant. Record the IDs below; put any client secrets in Key Vault or a pipeline variable group, never in this repository.

### 1. Coupon Service API (resource)

Applied by [`scripts/setup-entra-app.ps1`](../scripts/setup-entra-app.ps1) rather than by hand — see [`docs/pipeline-prerequisites.md`](pipeline-prerequisites.md) section 4.

- Application ID URI / audience: `api://coupon-service` (override with Bicep `couponApiAudience` if needed).
- `api.requestedAccessTokenVersion` = **2**. `JwtBearer` matches `ValidIssuer` exactly against the v2 authority, so a version 1 registration issues `iss = https://sts.windows.net/{tenant}/` and every call fails with 401.
- App roles:

| Value | Description | Allowed member types |
|---|---|---|
| `Coupon.Redeem` | Reserve, confirm, release | Applications |
| `Coupon.Admin` | Policy administration | Applications and Users |

`Coupon.Admin` includes `Application` so the CD pipeline's service principal can hold it; a Users-only role cannot be assigned to a service principal and the resulting token carries no `roles` claim.

- Expose the API; keep the Application ID URI stable so APIM and JwtBearer stay aligned.

### 2. Customer / SPA public client

- Public client (no secret). Redirect URIs for the SPA origin.
- API permission: delegated access to the Coupon Service API (preview / order submit via APIM).
- Documented client ID placeholder: `{customer-spa-client-id}`.

### 3. Admin public or confidential client

- Workforce users obtain tokens that include `Coupon.Admin` (group → app role assignment, or direct assignment).
- Documented client ID placeholder: `{admin-client-id}`.

### Assign `Coupon.Redeem` to the Order API managed identity

After Bicep creates `id-order-api-demo`, grant the app role (Graph `appRoleAssignedTo`). Example shape (replace IDs after registration):

```powershell
# Coupon API app object id, role id for Coupon.Redeem, Order MI principal id — from portal or az ad.
# Prefer az rest against https://graph.microsoft.com/v1.0/servicePrincipals/{mi-sp-id}/appRoleAssignments
```

Until the role assignment exists, the Coupon Service returns **403** on reservation routes even when a token is present.

## Configuration placeholders

| Setting | Where | Example / placeholder |
|---|---|---|
| `entraTenantId` | Bicep (defaults to `tenant().tenantId`) | Deployment tenant; override only for a different IdP |
| `couponApiAudience` | Bicep param + `Authentication:Jwt:Audience` | `api://coupon-service` |
| `couponApiClientId` | Bicep param + `Authentication:Jwt:ClientId` | `{coupon-api-client-id}`, printed by `scripts/setup-entra-app.ps1` |
| Customer SPA client ID | this doc / pipeline vars | `{customer-spa-client-id}` |
| Admin client ID | this doc / pipeline vars | `{admin-client-id}` |
| Order MI client ID | Bicep output `orderIdentityClientId` | injected as `AZURE_CLIENT_ID` |

JwtBearer on the Coupon Service (defence in depth, **AC-7.6**):

```text
Authentication__Jwt__Authority = https://login.microsoftonline.com/{tenant}/v2.0
Authentication__Jwt__Audience  = api://coupon-service
Authentication__Jwt__ClientId  = {coupon-api-client-id}
Authentication__Jwt__Issuer    = https://login.microsoftonline.com/{tenant}/v2.0
Authentication__TestToken__Enabled = false   # required outside Development/Test (AC-7.5)
```

### Two accepted audiences, and why

Version 2 access tokens always carry the **client id of the resource application** in `aud`; only version 1 tokens echo back the Application ID URI that the caller requested. Since the registration requests version 2 (see above), every real token — the SPA's delegated token, the Order API's managed-identity token, the pipeline's client-credentials token — arrives with the GUID.

Confirmed against the live tenant with a client-credentials token:

```text
aud   : {coupon-api-client-id}          # not api://coupon-service
iss   : https://login.microsoftonline.com/{tenant}/v2.0
roles : Coupon.Redeem
```

So `api://coupon-service` remains the identifier callers *request* (`api://coupon-service/.default`), while the value that must *validate* is the client id. Both are accepted, in the application (`ValidAudiences`) and at the APIM edge (two `<audience>` entries fed by the `jwt-audience` and `jwt-client-id` named values), so neither token version is rejected and the readable URI stays the public contract.

`couponApiClientId` is a Bicep parameter set in `main.*.bicepparam`. Leave it empty and Bicep falls back to the Application ID URI for the APIM audience, which is why a fresh tenant fails with 401 until `scripts/setup-entra-app.ps1` has run and the printed client id has been copied in.

Order API managed-identity hop (**AC-7.7**):

```text
OrderApi__UseManagedIdentity     = true
OrderApi__CouponServiceBaseUrl   = https://{coupon-app-fqdn}
OrderApi__CouponServiceResource  = api://coupon-service
OrderApi__CouponServiceScope     = api://coupon-service/.default
# OrderApi__CouponServiceToken is NOT set in Azure — no shared secret
```

Locally, leave `UseManagedIdentity` false and supply `OrderApi:CouponServiceToken` via user-secrets (test-token scheme on the Coupon Service).

## Seeding needs no token (AC-9.5 / AC-9.6)

CD no longer authenticates to seed. The Coupon Service seeds the deterministic policy set as it starts (`Seeding__Enabled`), and the pipeline verifies through the anonymous `/v1/health/ready` probe. See [`docs/pipeline-prerequisites.md`](pipeline-prerequisites.md#the-pipeline-holds-no-admin-credential).

`Coupon.Admin` is still required for the human administration path: `scripts/setup-entra-app.ps1 -AdminPrincipalId <sp-or-user-object-id>` assigns it, and `/v1/admin/policies` and APIM's admin product both enforce it. `scripts/seed-policies.ps1` remains available for driving the admin API by hand, and needs such a token.

## APIM edge (**AC-9.7**, **AC-7.6**)

Policy XML lives under `infra/bicep/policies/`:

| File | Applied to | Behaviour |
|---|---|---|
| `customer-product.xml` | product `customer` | CORS + `validate-jwt` (health paths exempt) |
| `customer-api-rate-limit.xml` | APIs `coupon-service`, `order-service` | `rate-limit` 60/min (Consumption-safe API scope) |
| `admin-product.xml` | product `admin` | `validate-jwt` requiring `roles` claim `Coupon.Admin` |

Published operations exclude `/v1/reservations*`. Mutation traffic stays on the Order → Coupon managed-identity hop.

Named values (not secrets): `entra-tenant-id`, `jwt-audience`, `spa-origin`.

### Manual gateway check (after Entra IDs are real)

```powershell
$gateway = "<apimGatewayUrl from deployment output>"
# Anonymous must fail:
Invoke-WebRequest -Uri "$gateway/coupons/v1/coupons/preview" -Method POST -ContentType "application/json" -Body "{}"
# Expect 401

# Valid customer token must pass gateway JWT (app still re-validates):
# Invoke-WebRequest ... -Headers @{ Authorization = "Bearer $token" }
```

Do not commit tokens. Capture only status codes in the PR if you run this live.

## Local test-token scheme (P-8)

Registered only when **both** are true:

1. `Authentication:TestToken:Enabled` is `true` in configuration, and
2. `IHostEnvironment` is `Development`, `Test`, or `Testing`.

`TestTokenStartupGuard` throws at startup if the flag is enabled in any other environment (**AC-7.5**). The guard message names the actual environment so misconfiguration surfaces before the first request. Deployed Container Apps set `Authentication__TestToken__Enabled=false` and use Entra JwtBearer exclusively (**AC-7.6** at the application layer).

Locally, generate tokens with the same issuer, audience and signing key as `appsettings.Development.json` / user-secrets, or use the BDD `TokenProvider` (`TokenStrategy: TestToken`). Never enable the test scheme in `appsettings.json` for Production or Staging profiles.
