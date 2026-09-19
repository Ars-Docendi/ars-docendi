using Microsoft.Extensions.Options;

namespace Modules.Asistente;

/// <summary>
/// Verifica que las perillas del módulo tengan valores con los que el pipeline
/// pueda funcionar.
/// </summary>
/// <remarks>
/// <b>Ninguna de estas 22 perillas tenía validación.</b> Un cero o un negativo en
/// la configuración de un ambiente entraba tal cual, y el modo de falla dependía
/// de cuál: algunos rompen ruidosamente en el primer turno, y otros —los peores—
/// <b>apagan una defensa en silencio</b>.
///
/// Los dos que hacen eso están acá y merecen leerse con cuidado:
/// <c>TimeoutDeSentenciaMs</c> en cero es <c>statement_timeout = 0</c>, que en
/// PostgreSQL significa <b>sin límite</b>; <c>TimeoutDeComandoSegundos</c> en cero
/// es lo mismo del lado de Npgsql. En los dos casos, el número que parece
/// «desactivado» desactiva la cota que impide que una consulta generada con un
/// producto cartesiano ocupe un backend hasta terminar.
///
/// <b>No se registra con <c>ValidateOnStart</c>, y es una decisión.</b> Con
/// arranque validado, un <c>Asistente:TopeDeFilas</c> en cero tumbaría el Host
/// entero —incluido <c>/api/tareas/ping</c>, que no tiene nada que ver—. Eso
/// invierte la postura que el módulo ya tiene escrita en su composición: el
/// proveedor se registra como fábrica «para que un nombre desconocido o una clave
/// faltante falle recién cuando alguien lo pida, y no impida arrancar: el ping
/// tiene que responder en cualquier ambiente». El invariante #3 pide que el ping
/// distinga «el módulo está cargado» de «la base responde»; un Host que no arranca
/// no distingue nada.
///
/// Así, la validación corre la primera vez que alguien lee las opciones —o sea, en
/// el primer turno del asistente— y falla ahí, nombrando la perilla. El resto del
/// sistema sigue en pie.
/// </remarks>
internal sealed class ValidadorDeOpcionesAsistente : IValidateOptions<OpcionesAsistente>
{
    /// <summary>
    /// Las cuatro perillas que usan el cero como apagado deliberado y documentado.
    /// </summary>
    /// <remarks>
    /// No llevan regla de positividad justamente porque el cero es un valor con
    /// significado: sin cupo por actor, sin corte del proveedor, sin presupuesto de
    /// turno, sin historial para el reescritor. Ponerles <c>[Range(1, …)]</c>
    /// —que es el reflejo obvio— rompería una capacidad que el módulo ofrece.
    /// </remarks>
    private static readonly (string Nombre, Func<OpcionesAsistente, int> Leer)[] ConCeroComoApagado =
    [
        (nameof(OpcionesAsistente.CupoDeLlamadasPorActor), o => o.CupoDeLlamadasPorActor),
        (nameof(OpcionesAsistente.FallosParaAbrirElBreaker), o => o.FallosParaAbrirElBreaker),
        (nameof(OpcionesAsistente.PresupuestoDelTurnoSegundos), o => o.PresupuestoDelTurnoSegundos),
        (nameof(OpcionesAsistente.TopeDeTurnosDelHistorial), o => o.TopeDeTurnosDelHistorial),
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
        (nameof(OpcionesAsistente.VentanaDeCuotaMinutos), o => o.VentanaDeCuotaMinutos),
        (nameof(OpcionesAsistente.EsperaDelBreakerSegundos), o => o.EsperaDelBreakerSegundos),
        (nameof(OpcionesAsistente.RetencionDeRegistrosDias), o => o.RetencionDeRegistrosDias),
        (nameof(OpcionesAsistente.PeriodoDePurgaHoras), o => o.PeriodoDePurgaHoras),
        (nameof(OpcionesAsistente.VigenciaDeIdempotenciaMinutos), o => o.VigenciaDeIdempotenciaMinutos),
        (nameof(OpcionesAsistente.MaximoDeIntentosDeAclaracion), o => o.MaximoDeIntentosDeAclaracion),
        (nameof(OpcionesAsistente.MaximoDeTokensDeGeneracion), o => o.MaximoDeTokensDeGeneracion),
        (nameof(OpcionesAsistente.MaximoDeTokensDeRedaccion), o => o.MaximoDeTokensDeRedaccion),
        (nameof(OpcionesAsistente.MaximoDeTokensDeReescritura), o => o.MaximoDeTokensDeReescritura),
    ];

    /// <summary>
    /// La única donde el cero es sensato y el negativo no.
    /// </summary>
    /// <remarks>
    /// Espera base en cero es «reintentar sin esperar», que es una configuración
    /// legítima —agresiva, pero legítima— para un ambiente de prueba. Negativa no
    /// significa nada.
    /// </remarks>
    private static readonly (string Nombre, Func<OpcionesAsistente, int> Leer)[] NoNegativas =
    [
        (nameof(OpcionesAsistente.EsperaBaseMs), o => o.EsperaBaseMs),
    ];

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

        return fallas.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(fallas);
    }
}
