using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

namespace ArquitecturaBaseMultitenant.Api.RequestContext;

/// <summary>IP ya procesada por ForwardedHeaders y user agent de la petición.</summary>
internal sealed class RequestInfo(IHttpContextAccessor httpContextAccessor) : IRequestInfo
{
    public string? IpAddress => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
