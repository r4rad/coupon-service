# API testing with Insomnia

[Insomnia](https://insomnia.rest/) is the API client for this repository. Request collections are **generated automatically from the code** whenever you build an API project.

## Quick start

1. Install Insomnia.
2. Build the solution (generates the workspace):

   ```powershell
   dotnet build CouponService.slnx
   ```

3. Import the workspace:
   - **File → Import → From File**
   - Choose `insomnia/coupon-service.insomnia.json`
4. Select the **Local** environment.
5. Start the API:

   ```powershell
   dotnet run --project src/CouponService.Api
   ```

6. Send requests from the generated folders (**Coupon Service**, **Order API**).

## How auto-sync works

```text
Controllers / minimal APIs in code
        ↓  dotnet build
docs/api/generated/*-openapi.json     ← build-time OpenAPI (Microsoft.Extensions.ApiDescription.Server)
        ↓  scripts/sync-insomnia-from-openapi.ps1
insomnia/coupon-service.insomnia.json ← Insomnia workspace (requests + docs)
```

**When you add a new endpoint**, rebuild and refresh Insomnia:

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
| `insomnia/coupon-service.insomnia.json` | **No** — generated | Import into Insomnia |
| `insomnia/environments/local.json` | **Yes** | Local URLs and tokens (preserved on sync) |
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

## Contract reminders

- Rejected coupons return HTTP **200** with `status: "Rejected"` — not 4xx.
- Preview never writes (AC-1.6).
- Errors use RFC 7807 with a correlation id.

## Design reference

Human-readable architecture and planned routes: [solution-architecture.md](../solution-architecture.md) section 10. The **generated OpenAPI** is the authoritative list of what is implemented today.
