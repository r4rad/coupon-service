namespace CouponService.ApiTests.Preview;

internal static class PreviewTestRequests
{
    internal static object Standard() =>
        new
        {
            code = "SAVE10",
            customerId = "customer-1",
            cart = new
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
            },
        };

    internal static object MinimumNotMet() =>
        new
        {
            code = "MIN25",
            customerId = "customer-1",
            cart = new
            {
                lines = new[]
                {
                    new
                    {
                        lineId = "line-1",
                        pizzaId = "margherita",
                        category = "classic",
                        unitPrice = 21.90m,
                        quantity = 1,
                    },
                },
            },
        };

    internal static object Expired() =>
        new
        {
            code = "OLDCODE",
            customerId = "customer-1",
            cart = new
            {
                lines = new[]
                {
                    new
                    {
                        lineId = "line-1",
                        pizzaId = "margherita",
                        category = "classic",
                        unitPrice = 31.00m,
                        quantity = 1,
                    },
                },
            },
        };
}

internal static class PreviewTestDocuments
{
    internal static string Save10Document =>
        """
        {
          "policyId": "save10",
          "code": "SAVE10",
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

    internal static string MinimumOrderDocument =>
        """
        {
          "policyId": "min25",
          "code": "MIN25",
          "trigger": "code",
          "status": "Active",
          "engineSchema": "1.0",
          "condition": { "gte": [ { "fact": "cart.subtotal" }, 25.00 ] },
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

    internal static string ExpiredDocument =>
        """
        {
          "policyId": "oldcode",
          "code": "OLDCODE",
          "trigger": "code",
          "status": "Active",
          "engineSchema": "1.0",
          "window": { "to": "2026-01-01T00:00:00Z" },
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
}
