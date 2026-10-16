using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Identity;

/// <summary>El desafío pendiente (E9): el hash del vínculo del navegador y el material temporal protegido.</summary>
internal sealed class PlatformFactorChallengeConfiguration : IEntityTypeConfiguration<PlatformFactorChallenge>
{
    public void Configure(EntityTypeBuilder<PlatformFactorChallenge> builder)
    {
        builder.ToTable("PlatformFactorChallenges", Schemas.Identity);
        builder.HasKey(challenge => challenge.Id);
        builder.Property(challenge => challenge.BindingHash).HasMaxLength(TextLimits.Description).IsRequired();
        builder.Property(challenge => challenge.SecurityStamp).HasMaxLength(TextLimits.Description).IsRequired();
        builder.Property(challenge => challenge.ReturnUrl).HasMaxLength(TextLimits.LongText).IsRequired();
        builder.Property(challenge => challenge.Step).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.HasIndex(challenge => challenge.BindingHash).IsUnique();
        builder.HasIndex(challenge => new { challenge.UserId, challenge.ExpiresAtUtc });
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(challenge => challenge.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
