using System.Reflection;
using CouponService.Domain;

namespace CouponService.UnitTests.Domain;

public sealed class MonetaryTypeContractTests
{
    [Fact]
    public void Domain_monetary_members_use_decimal_not_binary_floating_point()
    {
        var monetaryMemberNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "UnitPrice",
            "Amount",
            "Subtotal",
            "Discount",
            "Total",
        };

        var violations = typeof(CartLine).Assembly
            .GetTypes()
            .Where(type => type.Namespace == typeof(CartLine).Namespace)
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Where(member => member is PropertyInfo or FieldInfo)
            .Select(member => member switch
            {
                PropertyInfo property => (Member: member, Type: property.PropertyType),
                FieldInfo field => (Member: member, Type: field.FieldType),
                _ => default,
            })
            .Where(entry => entry.Member is not null)
            .Where(entry => monetaryMemberNames.Contains(entry.Member!.Name))
            .Where(entry => entry.Type != typeof(decimal))
            .Select(entry => $"{entry.Member!.DeclaringType!.Name}.{entry.Member.Name} is {entry.Type.Name}")
            .ToArray();

        Assert.Empty(violations);
    }
}
