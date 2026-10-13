using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Files;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

internal sealed class AssistantAttachmentContentReader(IAssistantAttachmentRepository repository, IFileStore files, ApplicationDbContext db) : IAssistantAttachmentContentReader
{
    public async Task<Result<IReadOnlyList<AssistantAttachmentContent>>> ReadAsync(Guid owner, Access access, Guid conversationId, IReadOnlyList<Guid> attachmentIds, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null) throw new InvalidOperationException("File reads must run outside a transaction.");
        if (attachmentIds.Count > AssistantTechnicalLimits.MaxAttachments) return AssistantErrors.AttachmentTooMany;
        var rows = await repository.ListAsync(owner, access, conversationId, attachmentIds, ct);
        if (rows.Count != attachmentIds.Count) return AssistantErrors.AttachmentNotFound;
        var output = new List<AssistantAttachmentContent>();
        foreach (var row in rows)
        {
            await using var stream = await files.OpenReadAsync(row.StorageKey, ct);
            if (stream is null) return AssistantErrors.AttachmentNotFound;
            if (row.ByteLength > AssistantTechnicalLimits.MaxAttachmentBytes) return AssistantErrors.AttachmentTooLarge;
            var bytes = new byte[checked((int)row.ByteLength)];
            try { await stream.ReadExactlyAsync(bytes, ct); }
            catch (EndOfStreamException) { return AssistantErrors.AttachmentNotFound; }
            if (await stream.ReadAsync(new byte[1], ct) != 0) return AssistantErrors.AttachmentTooLarge;
            output.Add(new AssistantAttachmentContent(row.Id, row.MediaType, bytes));
        }
        return output;
    }
}
