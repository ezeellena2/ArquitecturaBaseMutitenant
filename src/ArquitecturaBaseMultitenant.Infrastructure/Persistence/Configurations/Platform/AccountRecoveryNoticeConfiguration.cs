using ArquitecturaBaseMultitenant.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform;

/// <summary>Marca técnica global, única por pedido; la baja del pedido elimina también su marca.</summary>
internal sealed class AccountRecoveryNoticeConfiguration : IEntityTypeConfiguration<AccountRecoveryNotice>
{
    public void Configure(EntityTypeBuilder<AccountRecoveryNotice> builder)
    {
        builder.ToTable("AccountRecoveryNotices", Schemas.Platform);
        builder.HasKey(notice => notice.Id);
        builder.Property(notice => notice.Id).ValueGeneratedNever();
        builder.HasOne<AccountRecoveryRequest>().WithOne().HasForeignKey<AccountRecoveryNotice>(notice => notice.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
