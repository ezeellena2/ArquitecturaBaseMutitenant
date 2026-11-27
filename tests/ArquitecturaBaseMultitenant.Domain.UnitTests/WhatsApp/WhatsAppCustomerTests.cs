using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.WhatsApp;

/// <summary>C14 de la E12: un contacto sin nombre toma el de su perfil de WhatsApp una sola vez.</summary>
public sealed class WhatsAppCustomerTests
{
    private static readonly DateTime Now = new(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_contact_without_a_name_takes_the_profile_name_only_once()
    {
        var customer = WhatsAppCustomer.Create(PhoneNumber.Create("+5491134567890").Value, Now);
        Assert.False(customer.TakeProfileName(null, Now));
        Assert.Null(customer.ProfileNameTakenAtUtc);
        Assert.True(customer.TakeProfileName("Ana Pérez", Now));
        Assert.Equal(("Ana Pérez", Now), (customer.Name, customer.ProfileNameTakenAtUtc));
        customer.Rename(null);
        Assert.False(customer.TakeProfileName("Anita", Now.AddDays(1)));
        Assert.Equal((null, Now), (customer.Name, customer.ProfileNameTakenAtUtc));
    }

    [Fact]
    public void A_name_set_by_a_person_is_never_replaced_and_the_profile_counts_as_taken()
    {
        var customer = WhatsAppCustomer.Create(PhoneNumber.Create("+5491134567890").Value, Now);
        customer.Rename("Ana de Ventas");
        Assert.False(customer.TakeProfileName("Anita", Now));
        Assert.Equal(("Ana de Ventas", Now), (customer.Name, customer.ProfileNameTakenAtUtc));
    }
}
