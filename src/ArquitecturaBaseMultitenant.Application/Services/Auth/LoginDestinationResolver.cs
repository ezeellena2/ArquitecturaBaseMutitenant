using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Phones;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Normaliza el contacto y resuelve su canal sin conocer módulos; la política de alta pertenece al canal.</summary>
internal sealed class LoginDestinationResolver(IPhoneNumberParser phones, IEnumerable<ILoginCodeChannel> channels)
{
    internal Result<LoginCodeDestination> Resolve(Email? email, PhoneInputRequest? phone, string? channel)
    {
        if ((email is null) == (phone is null))
            return new ValidationError(new Dictionary<string, string[]> { ["email"] = [ValidationTexts.EmailOrPhone], ["phone"] = [ValidationTexts.EmailOrPhone] });
        if (phone is null)
        {
            if (channel is not null && channel != LoginCodeChannel.Email) return LoginChannelErrors.Unavailable;
            return channels.Any(value => value.Key == LoginCodeChannel.Email && value.IsAvailable)
                ? LoginCodeDestination.ForEmail(email!) : LoginChannelErrors.Unavailable;
        }
        if (!channels.Any(value => value.Key == channel && value.SupportedMethodType == LoginMethodType.Phone && value.IsAvailable))
            return LoginChannelErrors.Unavailable;
        var parsed = phones.Parse(phone.Country, phone.Number, PhoneUsage.Mobile);
        return parsed.IsSuccess ? LoginCodeDestination.ForPhone(parsed.Value, channel!) : FieldErrors.On(parsed.Error, "phone");
    }

    internal Result<LoginCodeDestination> FromMethod(LoginMethod method) => FromContact(method.Type, method.Value);

    internal Result<LoginCodeDestination> FromContact(LoginMethodType type, string value)
    {
        if (type == LoginMethodType.Email) return Resolve(Email.Create(value).Value, null, null);
        var channel = channels.FirstOrDefault(channel => channel.SupportedMethodType == type && channel.IsAvailable);
        return channel is null ? LoginChannelErrors.Unavailable
            : LoginCodeDestination.ForPhone(PhoneNumber.Create(value).Value, channel.Key);
    }

    internal Result ValidateNew(LoginCodeDestination destination)
    {
        var channel = channels.SingleOrDefault(value => value.Key == destination.Channel && value.IsAvailable);
        if (channel is null) return LoginChannelErrors.Unavailable;
        var valid = channel.ValidateNewDestination(destination, null);
        return valid.IsFailure ? FieldErrors.On(valid.Error, Field(destination)) : Result.Success();
    }

    internal static LoginMethodType Type(LoginCodeDestination destination) => destination.Channel == LoginCodeChannel.Email ? LoginMethodType.Email : LoginMethodType.Phone;
    internal static string Field(LoginCodeDestination destination) => Type(destination) == LoginMethodType.Email ? "email" : "phone";
    internal static LoginMethod Create(Guid userId, LoginCodeDestination destination) => Type(destination) == LoginMethodType.Email
        ? LoginMethod.CreateEmail(userId, Email.Create(destination.Value).Value)
        : LoginMethod.CreatePhone(userId, PhoneNumber.Create(destination.Value).Value);
}
