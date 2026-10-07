namespace ArquitecturaBaseMultitenant.Api.Contracts.PublicSite;

public sealed record DirectoryQuery(int? Page, int? PageSize, string? Sort, string? Search);
