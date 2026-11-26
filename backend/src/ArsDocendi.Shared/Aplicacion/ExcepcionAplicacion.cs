namespace ArsDocendi.Shared.Aplicacion;

/// <summary>
/// Error esperado de un caso de uso. Describe la categoría y un código estable
/// sin acoplar servicios de aplicación a ASP.NET Core ni a un status HTTP.
/// </summary>
public sealed class ExcepcionAplicacion(
    TipoErrorAplicacion tipo,
    string codigo,
    string mensaje,
    IReadOnlyDictionary<string, string[]>? errores = null)
    : Exception(mensaje)
{
    public TipoErrorAplicacion Tipo { get; } = tipo;
    public string Codigo { get; } = codigo;
    public IReadOnlyDictionary<string, string[]>? Errores { get; } = errores;
}

public enum TipoErrorAplicacion
{
    Validacion,
    NoAutenticado,
    Prohibido,
    NoEncontrado,
    Conflicto,
    ReglaDeNegocio,

    /// <summary>Un servicio externo respondió con error o con datos inutilizables.</summary>
    ServicioExternoFallido,

    /// <summary>Un servicio externo no respondió dentro del tiempo de espera.</summary>
    ServicioExternoSinRespuesta,

    /// <summary>La integración con un servicio externo no está configurada en este ambiente.</summary>
    ServicioExternoNoConfigurado,
}
