using Microsoft.Extensions.Logging;

namespace Modules.Asistente.Application;

/// <summary>
/// Decide, para cada turno, si la pregunta corresponde al carril determinista.
/// </summary>
/// <remarks>
/// <b>Decide y no ejecuta.</b> Devuelve la intención resuelta o nada; no llama a
/// ninguna API ni arma ninguna respuesta. Esa separación es lo que permite
/// construirlo hoy: los adaptadores necesitan los edges hacia los
/// <c>Contracts</c>, los edges necesitan el acuerdo del equipo, y esa aprobación
/// conviene pedirla con datos sobre cuánto captura el catálogo de verdad.
///
/// <b>Corre después del reescritor y antes del detector de ambigüedad</b>, y las
/// dos vecindades tienen motivo. Después del reescritor porque «¿y el de Pérez?» no
/// tiene slot que resolver hasta que se resuelve la anáfora. Antes del detector
/// porque una pregunta que el catálogo cubre con todos sus slots únicos no es
/// ambigua, y hacerla pasar por el menú sería preguntar algo ya decidido.
///
/// <b>El default es SQL, nunca API, y no es una preferencia.</b> Enrutar mal hacia
/// la API devuelve cero filas, y «cero filas» es indistinguible de «no hay» —la
/// mentira que la política de abstención existe para prohibir—. Fallar hacia el
/// carril más caro es fallar hacia el carril que PUEDE responder.
///
/// Cuesta cero llamadas al modelo, y se verifica sobre las dependencias: este tipo
/// no recibe por dónde llamar.
/// </remarks>
public sealed class EnrutadorDeDominio(
    ResolutorDeIntenciones resolutor, ILogger<EnrutadorDeDominio> log)
{
    /// <summary>
    /// La intención que cubriría la pregunta, o nulo si sigue al carril SQL.
    /// </summary>
    /// <remarks>
    /// <b>Nulo es el caso normal y no un error.</b> Un catálogo de cinco intenciones
    /// no cubre la mayoría de las preguntas y no pretende hacerlo; que no capturar
    /// sea la respuesta silenciosa es lo que hace que agregar una intención no
    /// pueda romper las preguntas que ya funcionaban.
    /// </remarks>
    public async Task<IntencionResuelta?> DecidirAsync(string pregunta, CancellationToken ct)
    {
        var resuelta = await resolutor.ResolverAsync(pregunta, ct);

        if (resuelta is null)
        {
            return null;
        }

        log.LogInformation(
            "El turno corresponde a la intención {Intencion}, con destino {Destino}.",
            resuelta.Intencion.Nombre,
            resuelta.Destino);

        return resuelta;
    }
}
