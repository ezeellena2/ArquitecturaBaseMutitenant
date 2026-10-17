using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using Microsoft.Extensions.AI;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

/// <summary>Conservative complete-context bound, computed only in provider memory before a billable call.</summary>
internal static class AssistantTurnContextGuard
{
    internal static bool Fits(IReadOnlyList<ChatMessage> messages, IReadOnlyList<AITool> tools, AssistantChatBinding binding)
    {
        try
        {
            // UTF8 bytes overbound textual tokens, including opaque reasoning, function arguments,
            // names, call IDs, result values and metadata. Extra framing covers the provider envelope.
            long bytes = 1024 + JsonSerializer.SerializeToUtf8Bytes(new
            {
                binding.ProviderModelId, binding.Effort, MaxOutputTokens = AssistantTechnicalLimits.MaxOutputTokens,
            }, AIJsonUtilities.DefaultOptions).LongLength;
            long payloadBytes = bytes;
            foreach (var message in messages)
            {
                if (message.Contents.Any(content => content is not (TextContent or TextReasoningContent or FunctionCallContent or FunctionResultContent or DataContent)))
                    return false;
                var textOnly = new ChatMessage(message.Role, message.Contents.Where(content => content is not DataContent).ToList())
                    { AuthorName = message.AuthorName, AdditionalProperties = message.AdditionalProperties };
                bytes += 64 + JsonSerializer.SerializeToUtf8Bytes(textOnly, AIJsonUtilities.DefaultOptions).LongLength;
                payloadBytes += 64 + JsonSerializer.SerializeToUtf8Bytes(textOnly, AIJsonUtilities.DefaultOptions).LongLength;
                foreach (var content in message.Contents.OfType<DataContent>())
                {
                    if (content.Data.Length == 0 || content.Data.Length > AssistantTechnicalLimits.MaxAttachmentBytes) return false;
                    payloadBytes += 256 + ((content.Data.Length + 2L) / 3L * 4L);
                    if (payloadBytes > AssistantTechnicalLimits.MaxProviderPayloadBytes) return false;
                    if (Domain.Common.FileMediaTypes.Images.Contains(content.MediaType)) bytes += 8192;
                    else if (content.MediaType == Domain.Common.FileMediaTypes.Pdf)
                    {
                        using var input = new MemoryStream(content.Data.ToArray(), writable: false);
                        using var pdf = PdfReader.Open(input, PdfDocumentOpenMode.Import);
                        if (pdf.SecuritySettings.IsEncrypted) return false;
                        bytes += 8192L * pdf.PageCount;
                        foreach (var dictionary in pdf.Internals.GetAllObjects().OfType<PdfDictionary>())
                        {
                            if (dictionary.Stream is not null) bytes += dictionary.Stream.UnfilteredValue.LongLength;
                            if (bytes > AssistantTechnicalLimits.MaxInputTokens) return false;
                        }
                    }
                    else return false;
                }
                if (bytes > AssistantTechnicalLimits.MaxInputTokens) return false;
            }
            foreach (var tool in tools)
            {
                if (tool is not AIFunctionDeclaration function) return false;
                bytes += 64 + JsonSerializer.SerializeToUtf8Bytes(new
                {
                    function.Name, function.Description, function.JsonSchema, function.ReturnJsonSchema, function.AdditionalProperties,
                }, AIJsonUtilities.DefaultOptions).LongLength;
                if (bytes > AssistantTechnicalLimits.MaxInputTokens) return false;
            }
            return bytes <= AssistantTechnicalLimits.MaxInputTokens;
        }
        catch (JsonException) { return false; }
        catch (NotSupportedException) { return false; }
        catch (PdfReaderException) { return false; }
    }
}
