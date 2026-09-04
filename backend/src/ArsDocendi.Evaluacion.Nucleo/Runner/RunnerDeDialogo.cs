using ArsDocendi.Evaluacion.Nucleo.Dataset;
using ArsDocendi.Evaluacion.Nucleo.Puntuacion;
using Modules.Asistente.Application;

namespace ArsDocendi.Evaluacion.Nucleo.Runner;

/// <summary>
/// Corre el eje de diálogo: conversaciones de varios turnos, sobre la capa completa.
/// </summary>
/// <remarks>
/// Es el único eje que ejercita la capa conversacional. El de capacidad manda turnos
/// autocontenidos al carril, así que el reescritor, el reconocedor de aclaraciones y
/// el detector de cambio de tema no están medidos por ningún número.
///
/// <b>Lo que este eje mide de más es lo que NO tiene que aparecer.</b> Un diálogo
/// puede dar 100% mientras el sistema arrastra el filtro del turno anterior: si el
/// turno de prueba es autocontenido, el arrastre no cambia el resultado.
///
/// <b>Una capa nueva por turno.</b> El techo de llamadas al modelo es por turno y en
/// producción vive con el alcance del request; sostener una sola instancia para toda
/// la conversación lo convertiría en un techo del diálogo entero, y el tercer turno
/// resolvería degradado por una razón que no tiene nada que ver con lo que el eje
/// mide. El hilo se conserva igual: no lo guarda la capa, lo guarda el almacén.
/// </remarks>
public sealed class RunnerDeDialogo(
    Func<CapaConversacional> capaPorTurno,
    IEjecutorDeConsulta ejecutor,
    IResolutorDeActores actores,
    IProveedorDeModelo proveedor)
{
    /// <summary>Corre el eje.</summary>
    public async Task<ResultadoDeCorrida> CorrerAsync(
        DatasetDeDialogo dataset, SelloDeIdentidad sello, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        var preflight = await Preflight.VerificarAsync(proveedor, ct);
        if (!preflight.Aprobado)
        {
            return new ResultadoDeCorrida(
                RunnerDeCapacidad.CodigoDePreflightFallido, null, preflight.Motivo);
        }

        var resultados = new List<ResultadoDeItem>(dataset.Turnos);

        foreach (var dialogo in dataset.Dialogos)
        {
            resultados.AddRange(await CorrerDialogoAsync(dialogo, ct));
        }

        var conteos = dataset.Dialogos.ToDictionary(
            dialogo => dialogo.Id, dialogo => dialogo.Turnos.Count, StringComparer.Ordinal);

        return new ResultadoDeCorrida(
            0, new Reporte("diálogo", sello, resultados, conteos), null);
    }

    private async Task<IReadOnlyList<ResultadoDeItem>> CorrerDialogoAsync(
        DialogoDePrueba dialogo, CancellationToken ct)
    {
        var actor = actores.Resolver(dialogo.Actor);
        var resultados = new List<ResultadoDeItem>(dialogo.Turnos.Count);
        Guid? hilo = null;

        for (var indice = 0; indice < dialogo.Turnos.Count; indice++)
        {
            var turno = dialogo.Turnos[indice];
            var id = $"{dialogo.Id}#{indice + 1}";

            ResultadoDelTurno respuesta;
            try
            {
                respuesta = await capaPorTurno().ResponderAsync(actor, hilo, turno.Pregunta, ct);
            }
            catch (Exception excepcion) when (excepcion is not OperationCanceledException)
            {
                // El turno que se cayó se marca y el diálogo se corta acá; los
                // anteriores conservan su desenlace. Seguir con el hilo perdido
                // mediría otra cosa y la mediría mal.
                resultados.Add(new ResultadoDeItem(
                    id, dialogo.Id, DesenlaceDeItem.Fallo,
                    $"El turno lanzó `{excepcion.GetType().Name}`; el diálogo se corta acá."));
                break;
            }

            hilo = respuesta.Hilo;

            if (respuesta.Estado == EstadoDelTurno.ServicioDegradado)
            {
                resultados.Add(new ResultadoDeItem(
                    id, dialogo.Id, DesenlaceDeItem.Fallo, "El turno resolvió servicio degradado."));
                break;
            }

            resultados.Add(await EvaluarTurnoAsync(id, dialogo, turno, respuesta, actor, ct));
        }

        return resultados;
    }

    private async Task<ResultadoDeItem> EvaluarTurnoAsync(
        string id,
        DialogoDePrueba dialogo,
        TurnoDeDialogo turno,
        ResultadoDelTurno respuesta,
        Guid actor,
        CancellationToken ct)
    {
        // EL CHEQUEO NEGATIVO VA PRIMERO. Un turno que arrastra puede devolver el
        // resultado correcto de casualidad —o porque el filtro arrastrado no cambiaba
        // nada en este fixture— y contarlo como acierto escondería el defecto.
        var arrastrado = TerminoArrastrado(turno, respuesta.PreguntaInterpretada);
        if (arrastrado is not null)
        {
            return new ResultadoDeItem(
                id, dialogo.Id, DesenlaceDeItem.TraduccionIncorrecta,
                $"Arrastró «{arrastrado}» del turno anterior en la pregunta interpretada.");
        }

        if (turno.EsperaAclaracion)
        {
            var pidio = respuesta.Estado == EstadoDelTurno.NecesitaAclaracion;

            return new ResultadoDeItem(
                id,
                dialogo.Id,
                pidio ? DesenlaceDeItem.AbstencionCorrecta : DesenlaceDeItem.IntentoSobreLoInfactible,
                pidio ? "Pidió aclaración, como corresponde." : "No pidió la aclaración esperada.");
        }

        if (turno.SqlReferencia is null)
        {
            var seAbstuvo = respuesta.Estado == EstadoDelTurno.NoContestable;

            return new ResultadoDeItem(
                id,
                dialogo.Id,
                seAbstuvo ? DesenlaceDeItem.AbstencionCorrecta : DesenlaceDeItem.IntentoSobreLoInfactible,
                seAbstuvo ? "Se abstuvo, como corresponde." : "Respondió lo que no debía.");
        }

        if (respuesta.Estado != EstadoDelTurno.Respondida)
        {
            return new ResultadoDeItem(
                id, dialogo.Id, DesenlaceDeItem.AbstencionSobreloFactible,
                "Se abstuvo ante un turno que se podía responder.");
        }

        ResultadoDeConsulta referencia;
        try
        {
            referencia = await ejecutor.EjecutarAsync(turno.SqlReferencia, actor, false, ct);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            return new ResultadoDeItem(
                id, dialogo.Id, DesenlaceDeItem.Fallo,
                $"La consulta de referencia falló: {excepcion.GetType().Name}.");
        }

        var obtenido = new ResultadoDeConsulta(respuesta.Columnas, respuesta.Filas, respuesta.Truncado);
        var coinciden = ComparadorDeResultados.Coinciden(obtenido, referencia, ordenImporta: false);

        return new ResultadoDeItem(
            id,
            dialogo.Id,
            coinciden ? DesenlaceDeItem.TraduccionCorrecta : DesenlaceDeItem.TraduccionIncorrecta,
            coinciden ? "Mismo resultado que la referencia." : "Resultado distinto del de la referencia.");
    }

    /// <summary>
    /// Busca un término prohibido en la pregunta interpretada.
    /// </summary>
    /// <remarks>
    /// Mira la <b>pregunta interpretada</b> y no la respuesta, porque es la única
    /// superficie donde el arrastre es visible antes de convertirse en filas. Cuando
    /// no hay interpretada —el turno era autocontenido y no se reescribió— no hay
    /// nada que arrastrar.
    ///
    /// La comparación ignora mayúsculas y acentos por la misma razón por la que el
    /// resto del módulo lo hace: «Análisis» y «analisis» son el mismo arrastre.
    /// </remarks>
    public static string? TerminoArrastrado(TurnoDeDialogo turno, string? preguntaInterpretada)
    {
        ArgumentNullException.ThrowIfNull(turno);

        if (turno.TerminosProhibidos.Count == 0 || string.IsNullOrWhiteSpace(preguntaInterpretada))
        {
            return null;
        }

        var normalizada = NormalizadorLexico.SinAcentos(preguntaInterpretada.ToLowerInvariant());

        return turno.TerminosProhibidos.FirstOrDefault(termino =>
            normalizada.Contains(
                NormalizadorLexico.SinAcentos(termino.ToLowerInvariant()), StringComparison.Ordinal));
    }
}
