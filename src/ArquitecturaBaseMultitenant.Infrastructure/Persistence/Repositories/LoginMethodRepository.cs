using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

internal sealed class LoginMethodRepository(ApplicationDbContext context) : ILoginMethodRepository
{
    public Task<LoginMethod?> GetByIdAsync(Guid methodId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.LoginMethods.SingleOrDefaultAsync(method => method.Id == methodId, cancellationToken);
    }

    public void Add(LoginMethod method)
    {
        ArgumentNullException.ThrowIfNull(method);
        context.RequireTransaction();
        context.LoginMethods.Add(method);
    }
}
