using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface IMemberRepository
{
    Task<Member?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    void Add(Member member);
}
