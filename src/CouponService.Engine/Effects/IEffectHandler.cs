using System.Text.Json;
using CouponService.Domain;

namespace CouponService.Engine.Effects;

public interface IEffectHandler
{
    string Operator { get; }

    DiscountPlan Apply(JsonElement node, EffectScope scope);
}
