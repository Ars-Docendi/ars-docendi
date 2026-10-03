namespace Modules.Asistente.Application;

/// <summary>
/// Arma el prefijo estable del prompt de sistema del carril SQL (RNF-14).
/// </summary>
/// <remarks>
/// El prefijo se deriva de los privilegios de lectura <b>efectivos</b> de la
/// conexión, no de una lista de tablas embebida en el código. Una lista embebida
/// se desincroniza en silencio y falla en las dos direcciones: si alguien concede
/// una columna, el prompt sigue describiendo el esquema viejo; si alguien la
/// revoca, el prompt se la sigue ofreciendo al modelo, que la pide, y el turno
/// falla con <c>permission denied</c> en vez de abstenerse.
///
/// Consecuencia buscada: el prefijo del rol básico y el del rol con datos
/// personales son <b>distintos</b>, porque sus privilegios lo son. Son dos
/// prefijos con dos huellas, cacheados por separado.
/// </remarks>
public interface IProveedorDeEsquema
{
    /// <summary>
    /// Devuelve el prefijo del rol correspondiente, calculándolo la primera vez.
    /// </summary>
    /// <param name="conDatosPersonales">
    /// Si se pide el prefijo del rol con acceso a datos personales.
    /// </param>
    Task<EsquemaParaPrompt> ObtenerAsync(bool conDatosPersonales, CancellationToken ct);
}

/// <summary>
/// El prefijo de sistema y su huella.
/// </summary>
/// <param name="Prefijo">
/// Texto completo del prompt de sistema. Estable entre turnos: no contiene la
/// fecha de referencia, ni el actor, ni la pregunta, ni los ejemplos.
/// </param>
/// <param name="Huella">
/// Huella estable del prefijo completo. Los reportes de evaluación se sellan con
/// ella, para que un reporte no pueda volver a describir un esquema que ya no
/// existe sin que se note.
/// </param>
public sealed record EsquemaParaPrompt(string Prefijo, string Huella);
