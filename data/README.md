# Catalog seed

**Option B, checked into git:** menu was taken from the Fun API mock (`GET /pizza/v1/menu`) once, then mapped into our schema.

| File | Role |
|---|---|
| `funapi-menu.raw.json` | Unchanged snapshot of the mock response |
| `pizzas.json` | What the API will load at startup |

The running service does **not** call Fun API. Tests stay stable if the mock site changes or is offline.

## Mapping

| Fun API | Ours |
|---|---|
| `id` (number) | `sourceId`; public `id` is a slug from `name` |
| `name` | `name` |
| `price` | `unitPrice` |
| `vegetarian` | `vegetarian` |
| `controversial` | omitted (not needed for pricing) |

Currency is not in the mock payload; we set **`EUR`**.

## Refresh

```bash
curl -s https://funapi.dev/api/pizza/v1/menu -o data/funapi-menu.raw.json
```

Then update `pizzas.json` to match (keep our `id` slugs stable if coupons refer to pizza ids).
