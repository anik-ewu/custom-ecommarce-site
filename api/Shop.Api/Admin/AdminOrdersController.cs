using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Admin;
using Shop.Application.Common;

namespace Shop.Api.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/orders")]
public sealed class AdminOrdersController(AdminOrderService orders) : ControllerBase
{
    [HttpGet]
    public Task<Paged<AdminOrderListItemDto>> List([FromQuery] AdminOrderQuery query, CancellationToken ct) =>
        orders.ListAsync(query, ct);

    [HttpGet("{number}")]
    public Task<AdminOrderDto> Get(string number, CancellationToken ct) => orders.GetAsync(number, ct);

    [HttpPatch("{number}/status")]
    public Task<AdminOrderDto> UpdateStatus(string number, UpdateOrderStatusRequest request, CancellationToken ct) =>
        orders.UpdateStatusAsync(number, request, ct);
}
