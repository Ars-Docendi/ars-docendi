using System.Globalization;
using ArsDocendi.Evaluacion.Nucleo.Dataset;
using ArsDocendi.Evaluacion.Nucleo.Puntuacion;
using Modules.Asistente.Application;

namespace ArsDocendi.Evaluacion.Nucleo.Runner;

/// <summary>Cómo terminó una corrida.</summary>
/// <param name="Codigo">Código de salida del proceso: cero solo si la corrida vale.</param>
/// <param name="Reporte">El reporte, o nulo si la corrida no llegó a producir uno.</param>
/// <param name="Motivo">Por qué no hay reporte, cuando no lo hay.</param>
public sealed record ResultadoDeCorrida(int Codigo, Reporte? Reporte, string? Motivo)
{
    /// <summary>Si la corrida produjo un reporte que se puede escribir.</summary>
    public bool HayReporte => Reporte is not null;
}

/// <summary>
/// Resuelve el identificador del actor de un ítem.
/// </summary>
/// <remarks>
/// El dataset nombra a los actores por alcance —«global», «carrera»— para que se
/// lea sin tener el fixture al lado. Quién es cada uno lo sabe el fixture.
/// </remarks>
public interface IResolutorDeActores
{
    /// <summary>Devuelve el identificador del actor con ese alcance.</summary>
    Guid Resolver(string actor);
}

/// <summary>
/// Corre el eje de capacidad y produce el reporte.
/// </summary>
/// <remarks>
/// Está acá —en la biblioteca que <b>sí</b> está en la solución— y no en el
/// ejecutable, a propósito. Es la pieza donde un error hace que el número mienta,
/// así que tiene que tener tests en el CI. Lo que queda afuera es únicamente lo
/// que instancia un proveedor real.
/// </remarks>
public sealed class RunnerDeCapacidad(
    Func<CarrilSql> carrilPorItem,
    IEjecutorDeConsulta ejecutor,
    IResolutorDeActores actores,
    IProveedorDeModelo proveedor)
{
    // UN CARRIL NUEVO POR ÍTEM, y no es un detalle de construcción.
    //
    // `ContadorDeLlamadasDelTurno` es POR TURNO: en producción vive con el alcance
    // del request. Un runner que sostuviera una sola instancia para todo el dataset
    // convertiría ese techo —cuatro llamadas— en un techo de la corrida entera: el
    // tercer ítem ya lo habría agotado, resolvería degradado, y el eje reportaría
    // fallo casi total sin que nada explicara por qué.
    //
    // El modo de falla es especialmente malo porque NO da error: da un número.
    /// <summary>Código de salida cuando el preflight rechaza la corrida.</summary>
    public const int CodigoDePreflightFallido = 2;

    /// <summary>
    /// Corre el eje.
    /// </summary>
    /// <remarks>
    /// Si el preflight rechaza, devuelve un código distinto de cero y <b>ningún
    /// reporte</b>. Un reporte escrito sobre una corrida inválida es peor que no
    /// tener reporte: el que no existe se nota, el que miente no.
    /// </remarks>
    public async Task<ResultadoDeCorrida> CorrerAsync(
        DatasetDeCapacidad dataset,
        SelloDeIdentidad sello,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        var preflight = await Preflight.VerificarAsync(proveedor, ct);
        if (!preflight.Aprobado)
        {
            return new ResultadoDeCorrida(CodigoDePreflightFallido, null, preflight.Motivo);
        }

        var resultados = new List<ResultadoDeItem>(dataset.Items.Count);

        foreach (var item in dataset.Items)
        {
            resultados.Add(await EvaluarAsync(item, ct));
        }


        var reporte = new Reporte(
            "capacidad", sello, resultados, dataset.ConteoPorCategoria());

        return new ResultadoDeCorrida(0, reporte, null);
    }

    /// <summary>
    /// Evalúa un ítem suelto.
    /// </summary>
    /// <remarks>
    /// Es público para que el eje de robustez use <b>exactamente esta</b> evaluación
    /// y no una copia. Con dos implementaciones, los números de los dos ejes dejarían
    /// de ser comparables — que es justo la comparación que el eje de robustez existe
    /// para permitir.
    /// </remarks>
    public async Task<ResultadoDeItem> EvaluarAsync(ItemDeCapacidad item, CancellationToken ct)
    {
        var actor = actores.Resolver(item.Actor);

        ResultadoDelTurno turno;
        try
        {
            turno = await carrilPorItem().ResponderAsync(actor, item.Pregunta, null, ct);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            return Fallo(item, $"El turno lanzó `{excepcion.GetType().Name}`.");
        }

        // Un turno degradado NO es una abstención. Es la trampa entera del eje:
        // sin crédito, todos los ítems infactibles devolverían «no contestable»
        // porque el turno falló, y el eje de abstención se inflaría justo cuando
        // el sistema no funciona.
        if (turno.Estado == EstadoDelTurno.ServicioDegradado)
        {
            return Fallo(item, "El turno resolvió servicio degradado.");
        }

        // Tampoco lo es una generación cortada por el techo de tokens: resuelve «no
        // contestable» con el mismo texto que una abstención, pero el modelo no
        // decidió nada. Se cuenta con nombre propio, ni acierto ni fallo del modelo.
        if (turno.Categoria == GeneracionDeSql.CategoriaTruncada)
        {
            return ResultadoDeItem.PorGeneracionTruncada(item.Id, item.Categoria);
        }

        return item.EsInfactible
            ? EvaluarInfactible(item, turno)
            : await EvaluarFactibleAsync(item, turno, actor, ct);
    }

    private static ResultadoDeItem EvaluarInfactible(ItemDeCapacidad item, ResultadoDelTurno turno)
    {
        var seAbstuvo = turno.Estado is EstadoDelTurno.NoContestable
            or EstadoDelTurno.NecesitaAclaracion;

        return new ResultadoDeItem(
            item.Id,
            item.Categoria,
            seAbstuvo ? DesenlaceDeItem.AbstencionCorrecta : DesenlaceDeItem.IntentoSobreLoInfactible,
            seAbstuvo
                ? "Se abstuvo, como corresponde."
                : "Respondió una pregunta que no debía responder.");
    }

    private async Task<ResultadoDeItem> EvaluarFactibleAsync(
        ItemDeCapacidad item, ResultadoDelTurno turno, Guid actor, CancellationToken ct)
    {
        if (item.SqlReferencia is null)
        {
            throw new InvalidOperationException(
                $"El ítem '{item.Id}' es factible y no declara consulta de referencia.");
        }

        if (turno.Estado != EstadoDelTurno.Respondida)
        {
            return new ResultadoDeItem(
                item.Id,
                item.Categoria,
                DesenlaceDeItem.AbstencionSobreloFactible,
                "Se abstuvo ante una pregunta que se podía responder.");
        }

        // La referencia se ejecuta EN VIVO, con el MISMO actor: si se ejecutara
        // con otro alcance, la comparación mediría la diferencia de alcances en
        // lugar de medir la traducción.
        ResultadoDeConsulta referencia;
        try
        {
            referencia = await ejecutor.EjecutarAsync(item.SqlReferencia, actor, false, ct);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            // Una referencia que no ejecuta es un defecto del dataset, no del
            // asistente. Se marca como fallo para que no se le acredite ni se le
            // reste a nadie, y para que se vea en el reporte.
            return Fallo(item, $"La consulta de referencia falló: {excepcion.GetType().Name}.");
        }

        var respuesta = new ResultadoDeConsulta(turno.Columnas, turno.Filas, turno.Truncado);
        var coinciden = ComparadorDeResultados.Coinciden(respuesta, referencia, item.OrdenImporta);

        return new ResultadoDeItem(
            item.Id,
            item.Categoria,
            coinciden ? DesenlaceDeItem.TraduccionCorrecta : DesenlaceDeItem.TraduccionIncorrecta,
            coinciden
                ? "Mismo resultado que la referencia."
                : string.Create(CultureInfo.InvariantCulture,
                    $"Devolvió {respuesta.Filas.Count} fila(s); la referencia, {referencia.Filas.Count}."));
    }

    private static ResultadoDeItem Fallo(ItemDeCapacidad item, string detalle) =>
        new(item.Id, item.Categoria, DesenlaceDeItem.Fallo, detalle);
}
