namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;

/// <summary>El aviso de que la pantalla guardó una propuesta: al editar, la versión nueva del registro; si no, sin cuerpo útil.</summary>
public sealed record AssistantProposalAppliedHttpRequest(uint? Version);
