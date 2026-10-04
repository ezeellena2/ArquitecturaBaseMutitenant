using ArquitecturaBaseMultitenant.Application.Services.Companies;
using ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>
/// Paridad del estado de un miembro: la expresión que los readers traducen a SQL para filtrar, mostrar y contar
/// responde lo mismo que la política de C#, que es la especificación, en cada combinación posible.
/// </summary>
public sealed class MemberStateParityTests
{
    private static readonly DateTime NowUtc = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

    private static IEnumerable<UserStatus?> UserStatuses =>
        Enum.GetValues<UserStatus>().Select(status => (UserStatus?)status).Append(null);

    [Fact]
    public void The_organization_user_state_in_sql_follows_the_policy()
    {
        var withState = OrganizationUserReader.WithState.Compile();
        // Sin invitación, o una invitación en cada estado, vencida, que vence ahora o vigente. Una invitación siempre
        // tiene vencimiento.
        (InvitationStatus? Status, DateTime? ExpiresAtUtc)[] invitations =
        [
            (null, null),
            .. Enum.GetValues<InvitationStatus>().SelectMany(status => new DateTime?[]
                { NowUtc.AddDays(-1), NowUtc, NowUtc.AddDays(1) }.Select(expiresAtUtc =>
                    ((InvitationStatus?)status, expiresAtUtc))),
        ];

        foreach (var memberStatus in Enum.GetValues<MemberStatus>())
        foreach (var userStatus in UserStatuses)
        foreach (var invitation in invitations)
        {
            var expected = OrganizationUserStatePolicy.Resolve(memberStatus, userStatus, invitation.Status,
                invitation.ExpiresAtUtc, NowUtc);
            // El reader lee el vencimiento de la invitación pendiente más nueva; las demás no cuentan.
            var actual = withState(new OrganizationUserReader.StateInput
            {
                MemberStatus = memberStatus,
                UserStatus = userStatus,
                PendingInvitationExpiresAtUtc = invitation.Status == InvitationStatus.Pending
                    ? invitation.ExpiresAtUtc : null,
                NowUtc = NowUtc,
            }).State;

            Assert.True(expected == actual,
                $"{memberStatus}, {userStatus}, {invitation.Status}, {invitation.ExpiresAtUtc:O}: {expected} != {actual}");
        }
    }

    [Fact]
    public void The_company_member_state_in_sql_follows_the_policy()
    {
        var withState = CompanyMembershipReader.WithState.Compile();

        foreach (var memberStatus in Enum.GetValues<MemberStatus>())
        foreach (var userStatus in UserStatuses)
        foreach (var membershipStatus in Enum.GetValues<CompanyMembershipStatus>())
        {
            var expected = CompanyMemberStatePolicy.Resolve(memberStatus, userStatus, membershipStatus);
            var actual = withState(new CompanyMembershipReader.StateInput
            {
                MemberStatus = memberStatus,
                UserStatus = userStatus,
                MembershipStatus = membershipStatus,
            }).State;

            Assert.True(expected == actual, $"{memberStatus}, {userStatus}, {membershipStatus}: {expected} != {actual}");
        }
    }
}
