namespace CouponService.Bdd.Support;

internal static class PolicyDocuments
{
    internal static string PercentageOff(string policyId, string code, int percent) =>
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
              "value": {{percent}},
              "of": {
                "lines": {
                  "where": { "gte": [ { "fact": "line.quantity" }, 1 ] }
                }
              }
            }
          }
        }
        """;

    internal static string Expired(string policyId, string code) =>
        $$"""
        {
          "policyId": "{{policyId}}",
          "code": "{{code}}",
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

    internal static string MinimumOrder(string policyId, string code, decimal minimum) =>
        $$"""
        {
          "policyId": "{{policyId}}",
          "code": "{{code}}",
          "trigger": "code",
          "status": "Active",
          "engineSchema": "1.0",
          "condition": { "gte": [ { "fact": "cart.subtotal" }, {{minimum.ToString(System.Globalization.CultureInfo.InvariantCulture)}} ] },
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

    internal static string LineCountAtLeast(string policyId, string code, int lineCount) =>
        $$"""
        {
          "policyId": "{{policyId}}",
          "code": "{{code}}",
          "trigger": "code",
          "status": "Active",
          "engineSchema": "1.0",
          "condition": { "gte": [ { "fact": "cart.lineCount" }, {{lineCount}} ] },
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

    internal static string CappedBestOf(string policyId, string code) =>
        $$"""
        {
          "policyId": "{{policyId}}",
          "code": "{{code}}",
          "trigger": "code",
          "status": "Active",
          "engineSchema": "1.0",
          "condition": { "gte": [ { "fact": "cart.subtotal" }, 0 ] },
          "effect": {
            "cap": {
              "max": 10.00,
              "of": {
                "bestOf": [
                  {
                    "percentage": {
                      "value": 15,
                      "of": {
                        "lines": {
                          "where": { "gte": [ { "fact": "line.quantity" }, 1 ] }
                        }
                      }
                    }
                  },
                  { "fixedAmount": { "amount": 5.00 } }
                ]
              }
            }
          }
        }
        """;

    internal static string TuesdayAutomatic(string policyId) =>
        $$"""
        {
          "policyId": "{{policyId}}",
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

    internal static string UnknownFact(string policyId, string code) =>
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

    internal static string Shadow(string policyId, string code) =>
        $$"""
        {
          "policyId": "{{policyId}}",
          "code": "{{code}}",
          "trigger": "code",
          "status": "Shadow",
          "engineSchema": "1.0",
          "condition": { "gte": [ { "fact": "cart.subtotal" }, 0 ] },
          "effect": {
            "percentage": {
              "value": 20,
              "of": {
                "lines": {
                  "where": { "gte": [ { "fact": "line.quantity" }, 1 ] }
                }
              }
            }
          }
        }
        """;

    internal static string LimitedUses(string policyId, string code, int totalUses) =>
        $$"""
        {
          "policyId": "{{policyId}}",
          "code": "{{code}}",
          "trigger": "code",
          "status": "Active",
          "engineSchema": "1.0",
          "limits": { "totalUses": {{totalUses}} },
          "condition": {
            "lt": [ { "fact": "coupon.uses.total" }, {{totalUses}} ]
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
