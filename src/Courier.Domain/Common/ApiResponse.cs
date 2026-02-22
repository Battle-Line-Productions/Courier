namespace Courier.Domain.Common;

public record ApiResponse
{
    public ApiError? Error { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public bool Success => Error is null;
}

public record ApiResponse<T> : ApiResponse
{
    public T? Data { get; init; }
}

public record PagedApiResponse<T> : ApiResponse
{
    public IReadOnlyList<T> Data { get; init; } = [];
    public PaginationMeta Pagination { get; init; } = default!;
}

public record PaginationMeta(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public record ApiError(
    int Code,
    string SystemMessage,
    string Message,
    IReadOnlyList<FieldError>? Details = null);

public record FieldError(
    string Field,
    string Message);
