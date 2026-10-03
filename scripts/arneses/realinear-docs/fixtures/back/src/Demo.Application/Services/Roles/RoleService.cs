namespace Demo.Application.Services.Roles;

public sealed class RoleService : IRoleService
{
    public Task<Result> CreateAsync(CancellationToken ct) => Task.FromResult(Result.Success());
}
