using Microsoft.Extensions.Logging;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Avisa, exactamente una vez por período, el primer momento en que un
/// usuario o la organización cruza 50/80/100% de su presupuesto (tarea 9.7 de
/// asistente-administracion-de-uso).
/// </summary>
/// <remarks>
/// <b>Singleton en memoria, a propósito.</b> Es sólo una deduplicación de log
/// —nunca una decisión de negocio—, así que un redespliegue perdiendo el
/// estado sólo puede repetir un aviso, nunca callar uno real. Con más de una
/// instancia del Host, cada una puede avisar una vez por su cuenta: es
/// duplicación de observabilidad, no un defecto de negocio (a diferencia de
/// la cuota o el candado de turno, que sí necesitan estado compartido de
/// verdad y por eso viven en Postgres).
/// </remarks>
internal sealed class DetectorDeUmbrales
{
    private static readonly int[] Umbrales = [100, 80, 50];

    private readonly HashSet<string> _avisados = [];
    private readonly Lock _candado = new();

    public void RegistrarSiCorresponde<T>(ILogger<T> log, string ambito, string clave, string periodo, double porcentaje)
    {
        foreach (var umbral in Umbrales)
        {
            if (porcentaje < umbral)
            {
                continue;
            }

            var llave = $"{ambito}:{clave}:{periodo}:{umbral}";

            bool esNuevo;
            lock (_candado)
            {
                esNuevo = _avisados.Add(llave);
            }

            if (esNuevo)
            {
                log.LogWarning(
                    "{Ambito} {Clave} cruzó el {Umbral}% de su presupuesto en el período {Periodo}.",
                    ambito, clave, umbral, periodo);
            }

            // Sin `break`: un salto de 0% a 100% en un solo turno (cupo chico)
            // tiene que avisar los tres umbrales que cruzó de una, no sólo el
            // más alto.
        }
    }
}
