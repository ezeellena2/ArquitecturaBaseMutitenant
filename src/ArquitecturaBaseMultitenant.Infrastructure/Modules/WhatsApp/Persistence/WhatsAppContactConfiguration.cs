using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppUniqueKeys = ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.UniqueKeys;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;
internal sealed class WhatsAppContactConfiguration : IEntityTypeConfiguration<WhatsAppContact>
{
    public void Configure(EntityTypeBuilder<WhatsAppContact> builder)
    {
        builder.ToTable("WhatsAppContacts", Schemas.Identity);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.WaId).HasMaxLength(PhoneNumber.MaxLength).IsRequired();
        builder.HasIndex(value => value.WaId).IsUnique().HasDatabaseName(WhatsAppUniqueKeys.ContactNumber);
        builder.HasOne<LoginMethod>().WithMany().HasForeignKey(value => value.MethodId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(value => value.MethodId);
    }
}
