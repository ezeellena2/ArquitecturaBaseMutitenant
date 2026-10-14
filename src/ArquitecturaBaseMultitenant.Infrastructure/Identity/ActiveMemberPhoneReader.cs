using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity;

/// <summary>El teléfono verificado de una cuenta activa que es miembro activo de la organización actual; nada sobre las demás.</summary>
internal sealed class ActiveMemberPhoneReader(ApplicationDbContext context, ITenantContext tenant) : IActiveMemberPhoneReader
{
    public Task<Guid?> FindMemberUserIdAsync(PhoneNumber phone, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(phone);
        var tenantId = tenant.RequiredTenantId;
        return (from method in context.LoginMethods.AsNoTracking()
                join user in context.Users.AsNoTracking() on method.UserId equals user.Id
                join member in context.Members.AsNoTracking() on user.Id equals member.UserId
                where method.Type == LoginMethodType.Phone && method.Value == phone.Value && method.VerifiedAtUtc != null
                    && user.Status == UserStatus.Active && user.DeletedAtUtc == null
                    && member.TenantId == tenantId && member.Status == MemberStatus.Active
                select (Guid?)user.Id).FirstOrDefaultAsync(cancellationToken);
    }
}
