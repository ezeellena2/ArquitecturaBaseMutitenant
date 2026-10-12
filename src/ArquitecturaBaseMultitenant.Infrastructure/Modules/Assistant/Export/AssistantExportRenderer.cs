using System.Text;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Export;

internal sealed class AssistantExportRenderer : IAssistantExportRenderer
{
    private static readonly object FontLock = new();

    public AssistantFileResponse Render(AssistantConversationSummaryResponse conversation, IReadOnlyList<AssistantMessageResponse> messages, AssistantExportFormat format)
    {
        var title = conversation.Title ?? AssistantTexts.Get("Export.Title");
        if (format == AssistantExportFormat.Markdown)
        {
            var text = new StringBuilder().Append("# ").AppendLine(Escape(title)).AppendLine();
            foreach (var message in messages)
            {
                text.Append("## ").AppendLine(Role(message.Role)).AppendLine().AppendLine(message.Text.Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal)).AppendLine();
                foreach (var attachment in message.Attachments) text.Append("- ").AppendLine(Escape(attachment.FileName));
            }
            return new AssistantFileResponse(new MemoryStream(Encoding.UTF8.GetBytes(text.ToString()), writable: false), "text/markdown; charset=utf-8", "conversation.md");
        }
        lock (FontLock) GlobalFontSettings.FontResolver ??= new AssistantFontResolver();
        var document = new Document();
        document.Info.Title = title;
        document.Styles[StyleNames.Normal]!.Font.Name = "AssistantSans";
        document.Styles[StyleNames.Normal]!.Font.Size = 10;
        document.Styles[StyleNames.Normal]!.ParagraphFormat.SpaceAfter = Unit.FromPoint(6);
        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        section.PageSetup.RightMargin = Unit.FromCentimeter(2);
        section.AddParagraph(title, StyleNames.Heading1);
        foreach (var message in messages)
        {
            section.AddParagraph(Role(message.Role), StyleNames.Heading2);
            // Plain text only: no HTML/Markdown interpreter, downloads or attachment originals.
            foreach (var line in message.Text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n')) section.AddParagraph(line);
            foreach (var attachment in message.Attachments) section.AddParagraph(attachment.FileName);
        }
        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.AddPageField();
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        var output = new MemoryStream();
        renderer.PdfDocument.Save(output, closeStream: false);
        output.Position = 0;
        return new AssistantFileResponse(output, "application/pdf", "conversation.pdf");
    }

    private static string Role(AssistantMessageRole role) => AssistantTexts.Get(role == AssistantMessageRole.User ? "Export.User" : "Export.Assistant");
    private static string Escape(string value) => value.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal)
        .Replace("[", "\\[", StringComparison.Ordinal).Replace("]", "\\]", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
}

internal sealed class AssistantFontResolver : IFontResolver
{
    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) => new("AssistantSans", bold, italic);
    public byte[]? GetFont(string faceName)
    {
        using var resource = typeof(AssistantFontResolver).Assembly.GetManifestResourceStream("AssistantExport.NotoSans.ttf")
            ?? throw new InvalidOperationException("The embedded export font is missing.");
        using var output = new MemoryStream();
        resource.CopyTo(output);
        return output.ToArray();
    }
}
