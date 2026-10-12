using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Common.Text;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Files;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Files;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantAttachmentService(AssistantAttachmentWriter writer, IAssistantAttachmentRepository repository,
    ICurrentUser user, AssistantConversationPolicy policy, IFileStore files, IFileStoreMaintenance maintenance, IUnitOfWork unitOfWork, IRequestValidator validator,
    TimeProvider clock, ILogger<AssistantAttachmentService> logger) : IAssistantAttachmentService
{
    public Task<Result<IdempotentReplayResponse<AssistantAttachmentSummaryResponse>>> UploadAsync(Guid id, AssistantUploadRequest input, CancellationToken ct) =>
        OperationLog.RunAsync<IdempotentReplayResponse<AssistantAttachmentSummaryResponse>>(logger, clock, "AssistantAttachmentUpload", async () =>
        {
            input = input with { FileName = TextNormalizer.Clean(input.FileName?.Replace('\\', '/').Split('/')[^1]) };
            if (input.ByteLength is > 10485760) return AssistantErrors.AttachmentTooLarge;
            if (await validator.ValidateAsync(input, ct) is { } invalid) return invalid;
            var allowed = await policy.AuthorizeAsync(ct);
            if (allowed.IsFailure) return allowed.Error;
            if (input.Content is null) return AssistantErrors.AttachmentUnsupportedType;
            var header = new byte[12];
            var count = 0;
            while (count < header.Length)
            {
                var read = await input.Content.ReadAsync(header.AsMemory(count), ct);
                if (read == 0) break;
                count += read;
            }
            var valid = AssistantAttachmentPolicy.Validate(input.MediaType, header.AsSpan(0, count), input.ByteLength!.Value);
            if (valid.IsFailure) return valid.Error;
            // The bounded buffer also supports non-seekable multipart bodies; storage remains outside UoW.
            using var content = new MemoryStream();
            content.Write(header, 0, count);
            var buffer = new byte[81920];
            int bytes;
            while ((bytes = await input.Content.ReadAsync(buffer, ct)) != 0)
            {
                if (content.Length + bytes > 10485760) return AssistantErrors.AttachmentTooLarge;
                content.Write(buffer, 0, bytes);
            }
            if (content.Length != input.ByteLength) return AssistantErrors.AttachmentUnsupportedType;
            content.Position = 0;
            var stored = await files.PutAsync(new FileObject(Guid.NewGuid(), input.MediaType!, content.Length, "assistant"), content, ct);
            var retained = false;
            try
            {
                var saved = await unitOfWork.ExecuteInTransactionAsync(token => writer.UploadAsync(id, input, stored.StorageKey, token), CommitPolicy.OnSuccess, ct);
                if (saved.IsFailure) return saved.Error;
                retained = saved.Value.Row is not null;
                if (retained) await maintenance.ConfirmAsync(stored.StorageKey, "assistant", CancellationToken.None);
                var summary = saved.Value.Row is { } row ? saved.Value.Summary with { Version = row.Version } : saved.Value.Summary;
                return new IdempotentReplayResponse<AssistantAttachmentSummaryResponse>(saved.Value.Reference, summary);
            }
            finally { if (!retained) await files.DeleteAsync(stored.StorageKey, CancellationToken.None); }
        });

    public Task<Result<AssistantFileResponse>> DownloadAsync(Guid id, Guid attachmentId, CancellationToken ct) =>
        OperationLog.RunAsync<AssistantFileResponse>(logger, clock, "AssistantAttachmentDownload", async () =>
        {
            var allowed = await policy.AuthorizeAsync(ct);
            if (allowed.IsFailure) return allowed.Error;
            var row = await repository.GetAsync(user.UserId!.Value, user.Access!.Value, id, attachmentId, ct);
            if (row is null) return AssistantErrors.AttachmentNotFound;
            var content = await files.OpenReadAsync(row.StorageKey, ct);
            if (content is null) return AssistantErrors.AttachmentNotFound;
            allowed = await policy.AuthorizeAsync(ct);
            if (allowed.IsFailure || await repository.GetAsync(user.UserId.Value, user.Access.Value, id, attachmentId, ct) is null)
            {
                await content.DisposeAsync();
                return allowed.IsFailure ? allowed.Error : AssistantErrors.AttachmentNotFound;
            }
            return new AssistantFileResponse(content, row.MediaType, row.FileName);
        });

    public Task<Result> DeleteAsync(Guid id, Guid attachmentId, uint? version, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "AssistantAttachmentDelete", async () =>
        {
            if (await validator.ValidateAsync(new AssistantDeleteRequest(version), ct) is { } invalid) return invalid;
            return await unitOfWork.ExecuteInTransactionAsync(token => writer.DeleteAsync(id, attachmentId, version!.Value, token), CommitPolicy.OnSuccess, ct);
        });
}
