

namespace ArquitecturaBaseMultitenant.Application.Models.Files;

public sealed record FileObject(Guid ObjectId, string MediaType, long ByteLength, string Category = "general");
