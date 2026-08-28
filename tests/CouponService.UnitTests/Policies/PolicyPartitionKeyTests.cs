using CouponService.Application.Policies;

namespace CouponService.UnitTests.Policies;

public sealed class PolicyPartitionKeyTests
{
    [Fact]
    public void ForCode_uses_the_code_as_partition_key()
    {
        Assert.Equal("SAVE10", PolicyPartitionKey.ForCode("SAVE10"));
    }

    [Fact]
    public void ForAutomatic_uses_auto_prefix_and_policy_id()
    {
        Assert.Equal("AUTO#tuesday10", PolicyPartitionKey.ForAutomatic("tuesday10"));
    }

    [Fact]
    public void PolicyRecordFactory_assigns_partition_keys_by_trigger()
    {
        var coded = PolicyRecordFactory.FromDocument(
            """
            {
              "policyId": "save10",
              "code": "SAVE10",
              "trigger": "code",
              "status": "Active",
              "engineSchema": "1.0",
              "condition": { "gte": [ { "fact": "cart.subtotal" }, 0 ] },
              "effect": { "percentage": { "value": 10, "of": { "lines": { "where": { "gte": [ { "fact": "line.quantity" }, 1 ] } } } } }
            }
            """);

        var automatic = PolicyRecordFactory.FromDocument(
            """
            {
              "policyId": "tuesday10",
              "trigger": "automatic",
              "status": "Active",
              "engineSchema": "1.0",
              "condition": { "eq": [ { "fact": "time.localDayOfWeek" }, "Tuesday" ] },
              "effect": { "percentage": { "value": 10, "of": { "lines": { "where": { "gte": [ { "fact": "line.quantity" }, 1 ] } } } } }
            }
            """);

        Assert.Equal("SAVE10", coded.PartitionKey);
        Assert.Equal("SAVE10", coded.Code);
        Assert.Equal(PolicyTrigger.Code, coded.Trigger);
        Assert.Equal("AUTO#tuesday10", automatic.PartitionKey);
        Assert.Null(automatic.Code);
        Assert.Equal(PolicyTrigger.Automatic, automatic.Trigger);
    }
}
