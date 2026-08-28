namespace CouponService.ApiTests.Reservations;

internal static class ReservationTestRequests
{
    internal static object Reserve(
        string code,
        string orderId,
        string customerId = "customer-1") =>
        new
        {
            orderId,
            code,
            customerId,
            confirmedOrderCount = 0,
            cart = StandardCart(),
            clientTotal = 999.99m,
        };

    internal static object Save10Reserve(string orderId, string customerId = "customer-1") =>
        Reserve("SAVE10", orderId, customerId);

    internal static object LimitedOneReserve(string code, string orderId, string customerId) =>
        Reserve(code, orderId, customerId);

    private static object StandardCart() =>
        new
        {
            lines = new[]
            {
                new
                {
                    lineId = "line-1",
                    pizzaId = "margherita",
                    category = "classic",
                    unitPrice = 9.50m,
                    quantity = 2,
                },
                new
                {
                    lineId = "line-2",
                    pizzaId = "bbq-chicken",
                    category = "meat",
                    unitPrice = 12.00m,
                    quantity = 1,
                },
            },
        };
}

internal static class ReservationTestDocuments
{
    internal static string Save10Document(string code = "SAVE10", string policyId = "save10") =>
        $$"""
        {
          "policyId": "{{policyId}}",
          "code": "{{code}}",
          "trigger": "code",
          "status": "Active",
          "engineSchema": "1.0",
          "condition": { "gte": [ { "fact": "cart.subtotal" }, 0 ] },
          "effect": {
            "percentage": {
              "value": 10,
              "of": {
                "lines": {
                  "where": { "gte": [ { "fact": "line.quantity" }, 1 ] }
                }
              }
            }
          }
        }
        """;

    internal static string LimitedOneUseDocument(string code = "LIMITED1", string policyId = "limited1") =>
        $$"""
        {
          "policyId": "{{policyId}}",
          "code": "{{code}}",
          "trigger": "code",
          "status": "Active",
          "engineSchema": "1.0",
          "limits": { "totalUses": 1 },
          "condition": {
            "lt": [ { "fact": "coupon.uses.total" }, 1 ]
          },
          "effect": {
            "percentage": {
              "value": 10,
              "of": {
                "lines": {
                  "where": { "gte": [ { "fact": "line.quantity" }, 1 ] }
                }
              }
            }
          }
        }
        """;
}
