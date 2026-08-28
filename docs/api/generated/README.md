# Generated OpenAPI documents

These files are **generated from the API code on every build**. Do not edit them by hand.

| File | Source project |
|---|---|
| `coupon-service-openapi.json` | `src/CouponService.Api` |
| `order-api-openapi.json` | `src/OrderApi` |

When you add controllers or minimal API routes, run:

```powershell
dotnet build CouponService.slnx
```

The build also regenerates `insomnia/coupon-service.insomnia.json` via `scripts/sync-insomnia-from-openapi.ps1`.
