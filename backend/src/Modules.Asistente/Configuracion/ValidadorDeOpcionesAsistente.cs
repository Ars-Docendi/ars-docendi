using Microsoft.Extensions.Options;

namespace Modules.Asistente;

/// <summary>
/// Verifica que las perillas del módulo tengan valores con los que el pipeline pueda funcionar.
/// </summary>
internal sealed class ValidadorDeOpcionesAsistente : IValidateOptions<OpcionesAsistente>
{
    /// <summary>Las perillas que usan el cero como apagado deliberado y documentado.</summary>
    private static readonly (string Nombre, Func<OpcionesAsistente, int> Leer)[] ConCeroComoApagado =
    [
        (nameof(OpcionesAsistente.FallosParaAbrirElBreaker), o => o.FallosParaAbrirElBreaker),
        (nameof(OpcionesAsistente.PresupuestoDelTurnoSegundos), o => o.PresupuestoDelTurnoSegundos),
        (nameof(OpcionesAsistente.TopeDeTurnosDelHistorial), o => o.TopeDeTurnosDelHistorial),
        // Sin compuerta de concurrencia (asistente-proveedor-local, D4).
        (nameof(OpcionesAsistente.MaximoDeLlamadasConcurrentes), o => o.MaximoDeLlamadasConcurrentes),
        // Sin num_ctx: vale el del servidor (asistente-proveedor-ollama, D3).
        (nameof(OpcionesAsistente.ContextoDelModelo), o => o.ContextoDelModelo),
        // Sin caché de consultas generadas (asistente-optimizaciones-modelo-local, D6).
        (nameof(OpcionesAsistente.VigenciaDeCacheDeConsultasMinutos), o => o.VigenciaDeCacheDeConsultasMinutos),
        (nameof(OpcionesAsistente.MaximoDeTokensDeSegundaGeneracion), o => o.MaximoDeTokensDeSegundaGeneracion),
    ];

    /// <summary>Las que en cero o en negativo dejan al pipeline sin poder trabajar.</summary>
    private static readonly (string Nombre, Func<OpcionesAsistente, int> Leer)[] EstrictamentePositivas =
    [
        (nameof(OpcionesAsistente.MaximoDeLlamadasPorTurno), o => o.MaximoDeLlamadasPorTurno),
        (nameof(OpcionesAsistente.MaximoDeIntentosDeTransporte), o => o.MaximoDeIntentosDeTransporte),
        (nameof(OpcionesAsistente.EsperaMaximaMs), o => o.EsperaMaximaMs),
        (nameof(OpcionesAsistente.TopeDeFilas), o => o.TopeDeFilas),
        (nameof(OpcionesAsistente.TimeoutDeSentenciaMs), o => o.TimeoutDeSentenciaMs),
        (nameof(OpcionesAsistente.TimeoutDeComandoSegundos), o => o.TimeoutDeComandoSegundos),
        (nameof(OpcionesAsistente.VigenciaDelHiloMinutos), o => o.VigenciaDelHiloMinutos),
        (nameof(OpcionesAsistente.TimeoutDeLlamadaSegundos), o => o.TimeoutDeLlamadaSegundos),
        (nameof(OpcionesAsistente.EsperaDelBreakerSegundos), o => o.EsperaDelBreakerSegundos),
        (nameof(OpcionesAsistente.RetencionDeRegistrosDias), o => o.RetencionDeRegistrosDias),
        (nameof(OpcionesAsistente.RetencionDeHistorialDias), o => o.RetencionDeHistorialDias),
        (nameof(OpcionesAsistente.RetencionDeAuditoriaDeSoporteDias), o => o.RetencionDeAuditoriaDeSoporteDias),
        (nameof(OpcionesAsistente.RetencionDeAuditoriaDeAdministracionDias), o => o.RetencionDeAuditoriaDeAdministracionDias),
        (nameof(OpcionesAsistente.RetencionDeAuditoriaDeCalidadDias), o => o.RetencionDeAuditoriaDeCalidadDias),
        (nameof(OpcionesAsistente.PeriodoDePurgaHoras), o => o.PeriodoDePurgaHoras),
        (nameof(OpcionesAsistente.VentanaDeDeshacerSegundos), o => o.VentanaDeDeshacerSegundos),
        (nameof(OpcionesAsistente.PeriodoDeBarridoDeBorradosSegundos),
            o => o.PeriodoDeBarridoDeBorradosSegundos),
        (nameof(OpcionesAsistente.VigenciaDeIdempotenciaMinutos), o => o.VigenciaDeIdempotenciaMinutos),
        (nameof(OpcionesAsistente.VigenciaDeRetroalimentacionMinutos), o => o.VigenciaDeRetroalimentacionMinutos),
        (nameof(OpcionesAsistente.MaximoDeIntentosDeAclaracion), o => o.MaximoDeIntentosDeAclaracion),
        (nameof(OpcionesAsistente.MaximoDeTokensDeGeneracion), o => o.MaximoDeTokensDeGeneracion),
        (nameof(OpcionesAsistente.MaximoDeTokensDeRedaccion), o => o.MaximoDeTokensDeRedaccion),
        (nameof(OpcionesAsistente.MaximoDeTokensDeReescritura), o => o.MaximoDeTokensDeReescritura),
        (nameof(OpcionesAsistente.EsperaMaximaEnColaSegundos), o => o.EsperaMaximaEnColaSegundos),
        (nameof(OpcionesAsistente.MuestrasDelPlan), o => o.MuestrasDelPlan),
    ];

    /// <summary>La única donde el cero es sensato y el negativo no.</summary>
    private static readonly (string Nombre, Func<OpcionesAsistente, int> Leer)[] NoNegativas =
    [
        (nameof(OpcionesAsistente.EsperaBaseMs), o => o.EsperaBaseMs),
    ];

    /// <summary>
    /// Valida las claves maestras SÓLO cuando alguien va a cifrar o descifrar una credencial
    /// (asistente-modelo-en-caliente, tarea 1.5).
    /// </summary>
    internal static IReadOnlyList<string> ValidarClavesMaestras(OpcionesAsistente options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var fallas = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ClaveMaestra))
        {
            fallas.Add(
                $"'{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.ClaveMaestra)}' no está configurada: "
                + "sin ella no se pueden guardar ni leer claves de API.");
        }
        else if (!EsClaveDe32Bytes(options.ClaveMaestra))
        {
            fallas.Add(
                $"'{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.ClaveMaestra)}' tiene que ser "
                + "base64 de exactamente 32 bytes.");
        }

        if (!string.IsNullOrWhiteSpace(options.ClaveMaestraAnterior)
            && !EsClaveDe32Bytes(options.ClaveMaestraAnterior))
        {
            fallas.Add(
                $"'{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.ClaveMaestraAnterior)}' tiene que ser "
                + "base64 de exactamente 32 bytes, o estar vacía.");
        }

        return fallas;
    }

    internal static bool EsClaveDe32Bytes(string base64)
    {
        Span<byte> destino = stackalloc byte[33];
        return Convert.TryFromBase64String(base64.Trim(), destino, out var escritos) && escritos == 32;
    }

    public ValidateOptionsResult Validate(string? name, OpcionesAsistente options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var fallas = new List<string>();

        foreach (var (perilla, leer) in EstrictamentePositivas)
        {
            var valor = leer(options);
            if (valor <= 0)
            {
                fallas.Add($"'{OpcionesAsistente.Seccion}:{perilla}' es {valor} y tiene que ser mayor que cero.");
            }
        }

        foreach (var (perilla, leer) in NoNegativas)
        {
            var valor = leer(options);
            if (valor < 0)
            {
                fallas.Add($"'{OpcionesAsistente.Seccion}:{perilla}' es {valor} y no puede ser negativa.");
            }
        }

        foreach (var (perilla, leer) in ConCeroComoApagado)
        {
            var valor = leer(options);
            if (valor < 0)
            {
                fallas.Add(
                    $"'{OpcionesAsistente.Seccion}:{perilla}' es {valor}. El cero es válido —apaga la "
                    + "función a propósito— pero un negativo no significa nada.");
            }
        }

        // Las dos relaciones. No son rangos: son afirmaciones sobre PARES de
        // perillas, y cada una está documentada en el XML-doc de su miembro.
        if (options.EsperaMaximaMs < options.EsperaBaseMs)
        {
            fallas.Add(
                $"'{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.EsperaMaximaMs)}' "
                + $"({options.EsperaMaximaMs}) es menor que "
                + $"'{nameof(OpcionesAsistente.EsperaBaseMs)}' ({options.EsperaBaseMs}): el tope de "
                + "una espera individual no puede quedar por debajo de la espera base.");
        }

        // El de comando va en SEGUNDOS y el de sentencia en MILISEGUNDOS.
        if (options.TimeoutDeComandoSegundos * 1000L <= options.TimeoutDeSentenciaMs)
        {
            fallas.Add(
                $"'{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.TimeoutDeComandoSegundos)}' "
                + $"({options.TimeoutDeComandoSegundos} s) no está por encima de "
                + $"'{nameof(OpcionesAsistente.TimeoutDeSentenciaMs)}' "
                + $"({options.TimeoutDeSentenciaMs} ms). Tienen que estar en ese orden para que, cuando "
                + "los dos apliquen, corte primero el del servidor —que además libera el backend—.");
        }

        // El plan compilado (asistente-plan-compilado, D5): sus muestras son llamadas del mismo
        // turno, así que no pueden superar su techo.
        if (options.PlanCompilado && options.MuestrasDelPlan > options.MaximoDeLlamadasPorTurno)
        {
            fallas.Add(
                $"'{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.MuestrasDelPlan)}' "
                + $"({options.MuestrasDelPlan}) supera "
                + $"'{nameof(OpcionesAsistente.MaximoDeLlamadasPorTurno)}' "
                + $"({options.MaximoDeLlamadasPorTurno}): el plan no podría terminar dentro del turno.");
        }

        if (options.TemperaturaDeMuestrasDelPlan is < 0m or > 2m)
        {
            fallas.Add(
                $"'{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.TemperaturaDeMuestrasDelPlan)}' "
                + $"es {options.TemperaturaDeMuestrasDelPlan} y tiene que estar entre 0 y 2.");
        }

        return fallas.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(fallas);
    }
}
