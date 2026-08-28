using System.ComponentModel.DataAnnotations;

namespace CouponService.Api.Contracts.Preview;

public sealed class PreviewRequest
{
    [Required]
    public string Code { get; init; } = string.Empty;

    [Required]
    public string CustomerId { get; init; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int ConfirmedOrderCount { get; init; }

    [Required]
    public CartRequest Cart { get; init; } = new();
}

public sealed class CartRequest
{
    [Required]
    [MinLength(1)]
    public IReadOnlyList<CartLineRequest> Lines { get; init; } = [];
}

public sealed class CartLineRequest
{
    [Required]
    public string LineId { get; init; } = string.Empty;

    [Required]
    public string PizzaId { get; init; } = string.Empty;

    [Required]
    public string Category { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal UnitPrice { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}
