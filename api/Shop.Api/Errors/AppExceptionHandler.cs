using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Common;

namespace Shop.Api.Errors;

/// <summary>Turns AppException into ProblemDetails; anything else becomes a 500 with INTERNAL_ERROR.</summary>
public sealed class AppExceptionHandler(IProblemDetailsService problems, ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        ProblemDetails problem;
        if (exception is AppException app)
        {
            problem = app.Errors is null
                ? new ProblemDetails()
                : new ValidationProblemDetails(app.Errors);
            problem.Status = app.Status;
            problem.Title = app.Message;
            problem.Extensions["code"] = app.Code;
        }
        else
        {
            logger.LogError(exception, "Unhandled exception");
            problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Something went wrong. Please try again.",
                Extensions = { ["code"] = ErrorCodes.InternalError },
            };
        }

        http.Response.StatusCode = problem.Status!.Value;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
