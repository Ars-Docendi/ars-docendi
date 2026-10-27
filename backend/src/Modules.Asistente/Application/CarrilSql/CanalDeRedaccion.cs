namespace Modules.Asistente.Application;

/// <summary>
/// Por dónde sale la redacción mientras se escribe, en el request que la pidió
/// (asistente-optimizaciones-modelo-local, design.md D9).
/// </summary>
public sealed class CanalDeRedaccion
{
    /// <summary>Recibe cada fragmento de la redacción; nulo si nadie escucha.</summary>
    public Func<string, CancellationToken, Task>? AlRecibirTexto { get; set; }
}
