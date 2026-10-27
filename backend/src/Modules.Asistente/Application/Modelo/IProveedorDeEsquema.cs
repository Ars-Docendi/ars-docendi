namespace Modules.Asistente.Application;

/// <summary>
/// Arma el prefijo estable del prompt de sistema del carril SQL (RNF-14). Son dos prefijos con dos
/// huellas, cacheados por separado.
/// </summary>
public interface IProveedorDeEsquema
{
    /// <summary>Devuelve el prefijo del rol correspondiente, calculándolo la primera vez.</summary>
    Task<EsquemaParaPrompt> ObtenerAsync(bool conDatosPersonales, CancellationToken ct);
}

/// <summary>El prefijo de sistema y su huella.</summary>
public sealed record EsquemaParaPrompt(string Prefijo, string Huella);
