

namespace ArquitecturaBaseMultitenant.Application.Models.Profile;

public sealed record ModuleCapabilityResponse(string Key, bool Available, bool CanConfigure, bool CanReadUsage);
