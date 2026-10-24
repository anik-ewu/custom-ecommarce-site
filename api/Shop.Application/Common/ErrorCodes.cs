namespace Shop.Application.Common;

/// <summary>Stable error codes returned in ProblemDetails.code. Keep in sync with PROJECT_PROMPT.md §7.</summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string ItemReserved = "ITEM_RESERVED";
    public const string OutOfStock = "OUT_OF_STOCK";
    public const string PriceChanged = "PRICE_CHANGED";
    public const string Conflict = "CONFLICT";
    public const string RateLimited = "RATE_LIMITED";
    public const string InvalidStatusTransition = "INVALID_STATUS_TRANSITION";
    public const string ImageStorageNotConfigured = "IMAGE_STORAGE_NOT_CONFIGURED";
    public const string InternalError = "INTERNAL_ERROR";
}
