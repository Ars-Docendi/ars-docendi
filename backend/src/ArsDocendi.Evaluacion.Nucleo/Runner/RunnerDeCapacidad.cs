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
    CarrilSql carril,
    IEjecutorDeConsulta ejecutor,
    IResolutorDeActores actores,
    IProveedorDeModelo proveedor)
{
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

    private async Task<ResultadoDeItem> EvaluarAsync(ItemDeCapacidad item, CancellationToken ct)
    {
        var actor = actores.Resolver(item.Actor);

        ResultadoDelTurno turno;
        try
        {
            turno = await carril.ResponderAsync(actor, item.Pregunta, null, ct);
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
