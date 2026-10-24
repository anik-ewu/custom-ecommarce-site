using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Orders;

namespace Shop.Api.Orders;

[ApiController]
[Authorize]
[Route("api")]
public sealed class CheckoutController(CheckoutService checkout) : ControllerBase
{
    [HttpGet("checkout/summary")]
    public Task<CheckoutSummaryDto> Summary(CancellationToken ct) => checkout.GetSummaryAsync(UserId, ct);

    [HttpPost("checkout")]
    public Task<OrderDto> Place(CheckoutRequest request, CancellationToken ct) =>
        checkout.PlaceOrderAsync(UserId, request, ct);

    [HttpGet("orders/{number}")]
    public Task<OrderDto> Order(string number, CancellationToken ct) => checkout.GetOrderAsync(UserId, number, ct);

    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);
}
