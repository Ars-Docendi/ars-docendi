using System.Text;
using System.Text.Json.Serialization;

namespace Modules.Asistente.Api;

// DTOs of the model administration surface (asistente-modelo-en-caliente, slice 3).

/// <summary>
/// A company and model to test or apply. <c>ClaveNueva</c> is a not-yet-saved key for a
/// cloud company. Any member the contract does not name (a URL, a host) is rejected.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PedidoDeModeloDto(string Empresa, string Modelo, string? ClaveNueva = null)
{
    // The generated ToString would print the key, and the framework logs bound
    // parameters at Debug/Trace. Redacted here so it cannot reach a log by accident.
    private bool PrintMembers(StringBuilder constructor)
    {
        constructor.Append("Empresa = ").Append(Empresa).Append(", Modelo = ").Append(Modelo).Append(", ClaveNueva = ***");
        return true;
    }
}

/// <summary>
/// A price for one catalog model, in USD per million tokens. <c>VigenteDesde</c> is a
/// calendar date: the new version starts at 00:00 UTC of that day, the same UTC day
/// boundary the rest of the module uses. A missing cache price means cached tokens are
/// priced like input tokens, never as zero. Amounts and the date are nullable only so
/// that a missing member is answered with a code instead of silently reading as zero.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PedidoDePrecioDto(
    string Empresa,
    string Modelo,
    decimal? EntradaPorMillon,
    decimal? SalidaPorMillon,
    decimal? CachePorMillon,
    DateOnly? VigenteDesde);

/// <summary>A price in force, in USD per million tokens, and the date it started.</summary>
public sealed record PrecioDelModeloDto(
    string Empresa,
    string Modelo,
    decimal EntradaPorMillon,
    decimal SalidaPorMillon,
    decimal CachePorMillon,
    int Version,
    DateOnly VigenteDesde);

/// <summary>The replacement key of a cloud company.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PedidoDeReemplazoDeClaveDto(string Clave)
{
    // Same reason as PedidoDeModeloDto: the key never prints.
    private bool PrintMembers(StringBuilder constructor)
    {
        constructor.Append("Clave = ***");
        return true;
    }
}

/// <summary>What is in use: <c>Origen</c> is <c>servidor</c> (server configuration) or <c>pantalla</c>.</summary>
public sealed record ModeloEnUsoDto(string Origen, string Empresa, string Modelo);

/// <summary>Whether a company has a stored key: <c>guardada</c> with its last-4 hint, or <c>faltante</c>.</summary>
public sealed record CredencialDeEmpresaDto(string Estado, string? Pista);

/// <summary>One catalog group. <c>Tipo</c> is <c>nube</c> or <c>local</c>; local groups carry no credential.</summary>
public sealed record EmpresaDelCatalogoDto(
    string Empresa, string Tipo, CredencialDeEmpresaDto? Credencial, IReadOnlyList<string> Modelos);

/// <summary>
/// The model screen: what is in use, the local servers the deployment configures
/// (identifiers only, never a URL), the catalog by company and the price in force of
/// every priced catalog model (a model absent from <c>Precios</c> is "not priced").
/// </summary>
public sealed record EstadoDelModeloDto(
    ModeloEnUsoDto EnUso,
    IReadOnlyList<string> ServidoresLocales,
    IReadOnlyList<EmpresaDelCatalogoDto> Catalogo,
    bool CifradoDisponible,
    IReadOnlyList<PrecioDelModeloDto> Precios);

/// <summary>Result of a connection test: a latency, or one failure code.</summary>
public sealed record ResultadoDePruebaDto(bool Exito, int? LatenciaMs, string? Codigo);

/// <summary>Result of applying a selection.</summary>
public sealed record ModeloAplicadoDto(ModeloEnUsoDto EnUso, int LatenciaMs, string? Pista);

/// <summary>Result of a key replacement: the company and the new last-4 hint.</summary>
public sealed record CredencialReemplazadaDto(string Empresa, string Pista);
