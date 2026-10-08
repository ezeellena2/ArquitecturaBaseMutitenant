namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
public interface IWhatsAppSignatureValidator
{
    bool IsValidSignature(ReadOnlySpan<byte> body, string? signature);
    bool IsValidVerifyToken(string? token);
}
