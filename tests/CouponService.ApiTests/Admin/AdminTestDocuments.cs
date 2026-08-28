namespace CouponService.ApiTests.Admin;

internal static class AdminTestDocuments
{
    internal static string ValidDraft(string policyId = "admin-save10", string code = "ADMIN10") =>
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

    internal static string UnknownFact(string policyId = "bad-fact", string code = "BADFACT") =>
        $$"""
        {
          "policyId": "{{policyId}}",
          "code": "{{code}}",
          "trigger": "code",
          "status": "Draft",
          "engineSchema": "1.0",
          "condition": { "fact": "customer.zodiacSign" },
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

    internal static string UpdatedDraft(string policyId, string code) =>
        $$"""
        {
          "policyId": "{{policyId}}",
          "code": "{{code}}",
          "trigger": "code",
          "status": "Active",
          "engineSchema": "1.0",
          "condition": { "gte": [ { "fact": "cart.subtotal" }, 10 ] },
          "effect": {
            "percentage": {
              "value": 15,
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
