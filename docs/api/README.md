# API documentation (Scalar + ReDoc)

Interactive API docs are hosted **in the running API** — no Insomnia import step.

| UI | Route | Purpose |
|---|---|---|
| **Scalar** | `/scalar` | Browse and **try** endpoints |
| **ReDoc** | `/redoc` | Read-only reference docs |
| OpenAPI JSON | `/openapi/v1.json` | Machine-readable contract |

## Quick start

```powershell
dotnet run --project src/CouponService.Api
```

Then open:

- Scalar: http://localhost:5174/scalar
- ReDoc: http://localhost:5174/redoc
- OpenAPI: http://localhost:5174/openapi/v1.json

Order API (when running):

```powershell
dotnet run --project src/OrderApi
```

- Scalar: http://localhost:5043/scalar
- ReDoc: http://localhost:5043/redoc

Docs UIs are mapped in **Development** and **Testing** only (not Production).

## How docs stay up to date

Scalar and ReDoc read the OpenAPI document generated from controllers at runtime.

When you expose a new endpoint:

1. Implement the controller action with `[Http*]`, `[ProducesResponseType]`, and preferably `[Tags]`, `[EndpointSummary]`, `[EndpointDescription]`.
2. Run the API (or build to refresh `docs/api/generated/*-openapi.json`).
3. Refresh `/scalar` — the new operation appears and is testable from the UI.

```text
Controllers with OpenAPI attributes
        ↓  runtime (MapOpenApi) + build (ApiDescription.Server)
/openapi/v1.json  ·  docs/api/generated/*-openapi.json
        ↓
/scalar  (try it out)    /redoc  (reference)
```

## Generated files

| Path | Edit by hand? | Purpose |
|---|---|---|
| `docs/api/generated/coupon-service-openapi.json` | **No** | Build-time OpenAPI for Coupon Service |
| `docs/api/generated/order-api-openapi.json` | **No** | Build-time OpenAPI for Order API |

```powershell
dotnet build CouponService.slnx
```

## Contract reminders

- Rejected coupons return HTTP **200** with `status: "Rejected"` — not 4xx.
- Preview never writes (AC-1.6).
- Errors use RFC 7807 with a correlation id.
- Reservation routes live under `/v1/reservations` (separate from customer `/v1/coupons`).

## Design reference

Human-readable architecture: [solution-architecture.md](../solution-architecture.md) section 10. **Generated OpenAPI** is authoritative for what is implemented today.

## Legacy Insomnia

Insomnia sync on build has been retired. Prefer Scalar for try-it-out and ReDoc for browsing. Residual files under `insomnia/` and `tools/OpenApiInsomniaSync/` are unused leftovers.
