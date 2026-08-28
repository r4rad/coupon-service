# Swagger / Redoc-style API docs in Insomnia

Insomnia does not ship Swagger UI or Redoc, but its **Design Document** gives a similar experience: rendered OpenAPI preview, schema browsing, and one-click request generation.

## Two imports, two jobs

| File | Import as | Use for |
|---|---|---|
| `insomnia/coupon-service-design.insomnia.json` | **Design Document** | Browse docs (like Swagger / Redoc) |
| `insomnia/order-api-design.insomnia.json` | **Design Document** | Browse Order API docs |
| `insomnia/coupon-service.insomnia.json` | **Request Collection** | Ready-made **Send** buttons (Try it out) |

The collection is organized by service (**Coupon Service**, **Order API**) with subfolders from `insomnia/routes/*.routes.json` — e.g. **Preview**, **Health**, **Admin — Policies**.

Regenerate all of them:

```powershell
dotnet build CouponService.slnx
```

---

## Part 1 — Browse documentation (Swagger / Redoc-like)

### 1. Import the design document

1. Open Insomnia
2. **Application → Import → From File**
3. Choose `insomnia/coupon-service-design.insomnia.json`
4. When asked, import as a **Design Document** (not a plain collection)

Repeat for `insomnia/order-api-design.insomnia.json` if you use Order API.

### 2. Open the Design tab

1. In the sidebar, open **Coupon Service (Design)**
2. Click the **Design** tab (top of the centre panel)

You should see a **rendered OpenAPI preview** — routes, descriptions, request/response schemas — similar to Swagger UI or Redoc.

If preview is blank, check the lint console at the bottom for spec errors.

### 3. Inspect schemas

- Expand operations in the sidebar under the spec
- Click an operation to see parameters, request body, and response shapes
- This mirrors Redoc's schema panels

---

## Part 2 — Try it out (Send requests)

Design preview alone is read-only. To **execute** calls like Swagger "Try it out":

### Option A — Generate collection from the design doc (recommended)

1. Open **Coupon Service (Design)**
2. Click the **gear / Settings** icon on the document
3. Choose **Generate collection** (or **Generate requests**)
4. Switch to the **Debug** tab
5. Select an operation, edit the body if needed, click **Send**

Insomnia wires URLs and examples from the OpenAPI spec.

### Option B — Use the pre-built request collection

1. **Import →** `insomnia/coupon-service.insomnia.json` as a **Request Collection**
2. Select **Local** environment (top-left)
3. Open **Coupon Service** → **Preview** (or **Health**, **Admin — Policies**, …) → pick a request → **Send**

Start the API first:

```powershell
dotnet run --project src/CouponService.Api
```

---

## Environment (base URLs and auth)

Select **Local** in the environment dropdown.

| Variable | Default | Purpose |
|---|---|---|
| `coupon_base_url` | `http://localhost:5174` | Coupon Service |
| `order_base_url` | `http://localhost:5043` | Order API |
| `customer_token` | *(empty)* | Bearer JWT for customer routes |
| `admin_token` | *(empty)* | Admin routes |
| `redeem_token` | *(empty)* | Reservation routes |

Edit `insomnia/environments/local.json`, then rebuild to merge changes.

---

## After new endpoints are added

Add a route to `insomnia/routes/coupon-service.routes.json` (or implement the endpoint in code), then:

```powershell
dotnet build CouponService.slnx
```

Then re-import (choose **Replace**):

- Design docs → refreshed preview (route catalog merged into OpenAPI)
- Collection → new requests appear in the matching folder

Or import live spec from the running API:

**Import → From URL →** `http://localhost:5174/openapi/v1.json` as Design Document

---

## Swagger UI vs Insomnia Design tab

| Feature | Swagger UI / Redoc | Insomnia Design + Debug |
|---|---|---|
| Rendered docs | Browser page | Design tab |
| Schemas | Yes | Yes (from OpenAPI) |
| Try it out | Try it out button | Debug tab → Send |
| Auth | Authorize dialog | Environment variables |
| Saved examples | Limited | Collection + environments |

---

## Troubleshooting

| Issue | Fix |
|---|---|
| Preview says "Unable to render" | Spec must be OpenAPI 3.x; run `dotnet build` and re-import |
| No operations listed | Rebuild and re-import design doc; routes come from `insomnia/routes/*.routes.json` until controllers land (CS-15+) |
| Connection refused on Send | Run `dotnet run --project src/CouponService.Api` |
| Imported as collection, no preview | Re-import `*-design.insomnia.json` as **Design Document** |
