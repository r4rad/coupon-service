# API testing with Insomnia

[Insomnia](https://insomnia.rest/) is the API client for this repository.

**Want Swagger / Redoc-style browsing inside Insomnia?** See **[insomnia-swagger-redoc-guide.md](./insomnia-swagger-redoc-guide.md)**.

## Quick start (try it out)

1. Install Insomnia.
2. Build the solution:

   ```powershell
   dotnet build CouponService.slnx
   ```

3. Import for **documentation preview** (Swagger / Redoc-like):
   - **Import → From File →** `insomnia/coupon-service-design.insomnia.json`
   - Choose **Design Document**
   - Open the **Design** tab

4. Import for **ready-made requests**:
   - **Import → From File →** `insomnia/coupon-service.insomnia.json`
   - Choose **Request Collection**

5. Select the **Local** environment.
6. Start the API:

   ```powershell
   dotnet run --project src/CouponService.Api
   ```

7. **Design tab** to browse · **Debug / Collection** to **Send**

## Generated files

| File | Import as |
|---|---|
| `insomnia/coupon-service-design.insomnia.json` | Design Document (preview) |
| `insomnia/order-api-design.insomnia.json` | Design Document (preview) |
| `insomnia/coupon-service.insomnia.json` | Request Collection (Send) |
| `docs/api/generated/*-openapi.json` | Design Document (alternative) |

## How auto-sync works

```text
Controllers / minimal APIs in code
        ↓  dotnet build
docs/api/generated/*-openapi.json     ← build-time OpenAPI (Microsoft.Extensions.ApiDescription.Server)
insomnia/routes/*.routes.json         ← dedicated /v1 routes (folders, examples, auth)
        ↓  scripts/sync-insomnia-from-openapi.ps1
insomnia/coupon-service.insomnia.json ← Insomnia workspace (organized requests + docs)
```

**Dedicated routes** live in `insomnia/routes/` — one JSON file per service. Each file defines folders (Preview, Health, Admin, …) and named requests with method, path, auth, and example body. The sync tool merges these into both the Insomnia collection and the design-document OpenAPI preview.

**When you add a new endpoint**, either:

1. Add a route to `insomnia/routes/coupon-service.routes.json` (or `order-api.routes.json`) for a ready-made Insomnia request with examples, then rebuild; or
2. Implement the endpoint in code so it appears in generated OpenAPI, then rebuild.

```powershell
dotnet build CouponService.slnx
```

Then in Insomnia: **Import → From File →** `insomnia/coupon-service.insomnia.json` and choose **Replace** (or re-import the file your workspace is linked to). New routes appear with method, URL, example body, and description from your OpenAPI metadata.

You can also import a single spec directly for a quick look:

- `docs/api/generated/coupon-service-openapi.json`
- `docs/api/generated/order-api-openapi.json`

While the API is running in Development:

```text
GET http://localhost:5174/openapi/v1.json
```

## Files

| Path | Edit by hand? | Purpose |
|---|---|---|
| `insomnia/coupon-service.insomnia.json` | **No** — generated | Request collection (Send) |
| `insomnia/coupon-service-design.insomnia.json` | **No** — generated | Design document (Swagger/Redoc preview) |
| `insomnia/order-api-design.insomnia.json` | **No** — generated | Order API design document |
| `insomnia/environments/local.json` | **Yes** | Local URLs and tokens (preserved on sync) |
| `insomnia/routes/coupon-service.routes.json` | **Yes** | Dedicated Coupon Service routes (folders + examples) |
| `insomnia/routes/order-api.routes.json` | **Yes** | Dedicated Order API routes |
| `docs/api/generated/*.openapi.json` | **No** — generated | OpenAPI contract from code |
| `tools/OpenApiInsomniaSync/` | Yes | Generator that merges OpenAPI → Insomnia |
| `scripts/sync-insomnia-from-openapi.ps1` | Yes | Manual sync entry point |

Run sync manually without a full build:

```powershell
./scripts/sync-insomnia-from-openapi.ps1
```

## Environment variables

Edit `insomnia/environments/local.json`:

| Variable | Default | Used for |
|---|---|---|
| `coupon_base_url` | `http://localhost:5174` | Coupon Service |
| `order_base_url` | `http://localhost:5043` | Order API |
| `customer_token` | *(empty)* | Customer JWT |
| `admin_token` | *(empty)* | `Coupon.Admin` |
| `redeem_token` | *(empty)* | `Coupon.Redeem` |
| `correlation_id` | `insomnia-local-001` | `X-Correlation-Id` header |

Sync preserves this file; it is merged into the generated workspace on every build.

## Adding a dedicated route

Edit `insomnia/routes/coupon-service.routes.json` (or `order-api.routes.json`). Each folder groups related requests:

```json
{
  "name": "Preview",
  "description": "Advisory pricing — never writes.",
  "routes": [
    {
      "name": "POST /v1/coupons/preview — SAVE10 applied",
      "method": "POST",
      "path": "/v1/coupons/preview",
      "auth": "customer",
      "body": { "code": "SAVE10", "customerId": "customer-1", "cart": { "lines": [] } }
    }
  ]
}
```

`auth` values: `customer`, `admin`, `redeem`, or `none`. Rebuild to regenerate Insomnia files.

## Contract reminders

- Rejected coupons return HTTP **200** with `status: "Rejected"` — not 4xx.
- Preview never writes (AC-1.6).
- Errors use RFC 7807 with a correlation id.

## Design reference

Human-readable architecture and planned routes: [solution-architecture.md](../solution-architecture.md) section 10. The **generated OpenAPI** is the authoritative list of what is implemented today.
