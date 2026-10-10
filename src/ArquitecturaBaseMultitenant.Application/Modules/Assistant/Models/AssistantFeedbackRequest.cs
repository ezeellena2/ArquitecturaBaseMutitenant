namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantFeedbackRequest(AssistantRating? Rating, uint? Version);
