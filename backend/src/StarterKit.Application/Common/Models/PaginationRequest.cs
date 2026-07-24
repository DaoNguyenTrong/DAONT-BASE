namespace StarterKit.Application.Common.Models;

public sealed record PaginationRequest(int PageNumber = 1, int PageSize = 10, string? Search = null);
