namespace CouponService.ApiTests.Contract;

internal static class ContractTestData
{
    internal static object PreviewAppliedRequest() =>
        new
        {
            code = "SAVE10-CONTRACT",
            customerId = "customer-contract",
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

    internal static object PreviewRejectedRequest() =>
        new
        {
            code = "MIN25-CONTRACT",
            customerId = "customer-contract",
            cart = new
            {
                lines = new[]
                {
                    new
                    {
                        lineId = "line-1",
                        pizzaId = "margherita",
                        category = "classic",
                        unitPrice = 19.00m,
                        quantity = 1,
                    },
                },
            },
        };

    internal static object MalformedPreviewRequest() =>
        new
        {
            code = "",
            customerId = "customer-contract",
            cart = new { lines = Array.Empty<object>() },
        };

    internal static object ReserveRequest(string code, string orderId) =>
        new
        {
            orderId,
            code,
            customerId = "customer-contract",
            confirmedOrderCount = 0,
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

    internal static string Save10Document(string code, string policyId) =>
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

    internal static string MinimumOrderDocument(string code, string policyId) =>
        $$"""
        {
          "policyId": "{{policyId}}",
          "code": "{{code}}",
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

    internal static string LimitedOneUseDocument(string code, string policyId) =>
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

    internal static string AdminDraftDocument(string policyId, string code) =>
        $$"""
        {
          "policyId": "{{policyId}}",
          "code": "{{code}}",
          "trigger": "code",
          "status": "Draft",
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
}
