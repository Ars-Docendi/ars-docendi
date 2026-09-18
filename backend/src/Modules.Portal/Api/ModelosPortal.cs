using Modules.Portal.Contracts.Dtos;

namespace Modules.Portal.Api;

public sealed record GuardarContactoRequest(string? Telefono, string? Mail);
public sealed record GuardarCvRequest(Guid ArchivoId);
public sealed record GuardarTagsRequest(IReadOnlyList<string> Terminos);
