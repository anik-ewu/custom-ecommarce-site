using Microsoft.AspNetCore.Mvc;
using Shop.Application.Common;

namespace Shop.Api.Errors;

public static class ProblemDetailsSetup
{
    public static IServiceCollection AddShopProblemDetails(this IServiceCollection services)
    {
        services.AddExceptionHandler<AppExceptionHandler>();
        services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
        {
            // Every error carries a stable code, including framework ones (404 routes, 401 challenges, ...).
            var problem = ctx.ProblemDetails;
            if (!problem.Extensions.ContainsKey("code"))
                problem.Extensions["code"] = CodeFor(problem.Status ?? ctx.HttpContext.Response.StatusCode);
        });

        services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = ctx =>
        {
            var problem = new ValidationProblemDetails(ctx.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Please check the highlighted fields.",
                Extensions = { ["code"] = ErrorCodes.ValidationFailed },
            };
            return new BadRequestObjectResult(problem);
        });
        return services;
    }

    public static string CodeFor(int status) => status switch
    {
        400 => ErrorCodes.ValidationFailed,
        401 => ErrorCodes.Unauthorized,
        403 => ErrorCodes.Forbidden,
        404 => ErrorCodes.NotFound,
        409 => ErrorCodes.Conflict,
        429 => ErrorCodes.RateLimited,
        _ => ErrorCodes.InternalError,
    };
}
