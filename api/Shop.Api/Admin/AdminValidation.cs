using Shop.Application.Common;

namespace Shop.Api.Admin;

internal static class AdminValidation
{
    public static AppException Missing(string field, string message) =>
        new(400, ErrorCodes.ValidationFailed, message, new Dictionary<string, string[]> { [field] = [message] });
}
