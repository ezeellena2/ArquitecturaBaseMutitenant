using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public enum AssistantExportFormat
{
    [JsonStringEnumMemberName("markdown")] Markdown,
    [JsonStringEnumMemberName("pdf")] Pdf,
}
