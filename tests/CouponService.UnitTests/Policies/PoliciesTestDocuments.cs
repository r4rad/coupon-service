namespace CouponService.UnitTests.Policies;

internal static class PoliciesTestDocuments
{
    internal static string TuesdayAutomatic =>
        """
        {
          "policyId": "tuesday10",
          "trigger": "automatic",
          "status": "Active",
          "priority": 100,
          "stackable": false,
          "engineSchema": "1.0",
          "condition": { "eq": [ { "fact": "time.localDayOfWeek" }, "Tuesday" ] },
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

    internal static string Save10Coded(int priority = 50) =>
        $$"""
        {
          "policyId": "save10",
          "code": "SAVE10",
          "trigger": "code",
          "status": "Active",
          "priority": {{priority}},
          "stackable": false,
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

    internal static string SiteWideAutomatic(int priority, int percentage) =>
        $$"""
        {
          "policyId": "site-wide",
          "trigger": "automatic",
          "status": "Active",
          "priority": {{priority}},
          "stackable": true,
          "engineSchema": "1.0",
          "condition": { "gte": [ { "fact": "cart.subtotal" }, 0 ] },
          "effect": {
            "percentage": {
              "value": {{percentage}},
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
