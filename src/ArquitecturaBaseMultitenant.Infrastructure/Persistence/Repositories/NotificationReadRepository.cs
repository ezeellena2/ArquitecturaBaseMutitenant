using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

internal sealed class NotificationReadRepository(ApplicationDbContext context) : INotificationReadRepository
{
    public void Add(NotificationRead read)
    {
        context.RequireTransaction();
        context.NotificationReads.Add(read);
    }
}
