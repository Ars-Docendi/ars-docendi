namespace Modules.Asistente.Application;

/// <summary>
/// Por dónde sale la redacción mientras se escribe, en el request que la pidió
/// (asistente-optimizaciones-modelo-local, design.md D9).
/// </summary>
/// <remarks>
/// Es <b>scoped</b>: lo llena <c>POST /api/asistente/consultas/flujo</c> antes de
/// empezar el turno y lo lee <see cref="RedactorDeRespuesta"/>, que no sabe nada
/// de HTTP. Sin nadie que lo llene —el endpoint de siempre, un job— queda vacío y
/// la redacción se pide entera, como antes.
/// </remarks>
public sealed class CanalDeRedaccion
{
    /// <summary>Recibe cada fragmento de la redacción; nulo si nadie escucha.</summary>
    public Func<string, CancellationToken, Task>? AlRecibirTexto { get; set; }
}
