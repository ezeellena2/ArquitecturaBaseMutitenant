using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "event")]
[JsonDerivedType(typeof(AssistantStartStreamHttpResponse), "start")]
[JsonDerivedType(typeof(AssistantDeltaStreamHttpResponse), "delta")]
[JsonDerivedType(typeof(AssistantToolStreamHttpResponse), "tool")]
[JsonDerivedType(typeof(AssistantTitleStreamHttpResponse), "title")]
[JsonDerivedType(typeof(AssistantDoneStreamHttpResponse), "done")]
[JsonDerivedType(typeof(AssistantErrorStreamHttpResponse), "error")]
public abstract record AssistantStreamHttpResponse;
