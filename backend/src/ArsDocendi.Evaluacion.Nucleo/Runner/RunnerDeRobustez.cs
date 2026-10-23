using ArsDocendi.Evaluacion.Nucleo.Dataset;
using ArsDocendi.Evaluacion.Nucleo.Puntuacion;
using Modules.Asistente.Application;

namespace ArsDocendi.Evaluacion.Nucleo.Runner;

/// <summary>
/// Corre el eje de robustez de fraseo.
/// </summary>
/// <remarks>
/// Reusa <see cref="RunnerDeCapacidad"/> entero, y eso no es economía de código: es
/// el invariante. La evaluación de un ítem de robustez tiene que ser <b>exactamente
/// la misma</b> que la de su origen, o el número de los dos ejes dejaría de ser
/// comparable — que es justo la comparación que este eje existe para permitir.
///
/// Lo único que cambia entre un ítem y su origen es cómo está escrita la pregunta.
/// </remarks>
public sealed class RunnerDeRobustez(RunnerDeCapacidad capacidad, IProveedorDeModelo proveedor)
{
    /// <summary>Corre el eje.</summary>
    public async Task<ResultadoDeCorrida> CorrerAsync(
        DatasetDeRobustez dataset, SelloDeIdentidad sello, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        var preflight = await Preflight.VerificarAsync(proveedor, ct);
        if (!preflight.Aprobado)
        {
            return new ResultadoDeCorrida(
                RunnerDeCapacidad.CodigoDePreflightFallido, null, preflight.Motivo);
        }

        var resultados = new List<ResultadoDeItem>(dataset.Items.Count);

        foreach (var item in dataset.Items)
        {
            var evaluado = await capacidad.EvaluarAsync(item.Item, ct);

            // La categoría del reporte es la CLASE DE PERTURBACIÓN, no la dificultad
            // técnica: un número global que baja no dice si el problema son los
            // tildes o los sinónimos, y son dos arreglos distintos.
            resultados.Add(evaluado with { Categoria = item.Clase });
        }

        return new ResultadoDeCorrida(
            0,
            new Reporte("robustez de fraseo", sello, resultados, dataset.ConteoPorClase()),
            null);
    }
}
