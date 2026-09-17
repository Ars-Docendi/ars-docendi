using Modules.Portal.Contracts.Dtos;

namespace Modules.Portal.Api;

public sealed record GuardarContactoRequest(string? Telefono, string? Mail);
public sealed record GuardarCvRequest(string Nombre, string? Uri);
public sealed record GuardarTagsRequest(IReadOnlyList<string> Terminos);
