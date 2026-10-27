namespace Modules.Asistente.Application;

/// <summary>
/// A qué apunta un vínculo: qué clase de cosa es y con qué identificador se abre.
/// </summary>
public sealed record DestinoDelVinculo(string Tipo, string Id);

/// <summary>Un vínculo ya ubicado en el resultado: qué celda lo ofrece y a qué apunta.</summary>
public sealed record VinculoDelResultado(int Fila, int Columna, string Tipo, string Id);

/// <summary>
/// Decide, para textos que aparecieron en un resultado, cuáles identifican algo que el actor
/// <b>puede abrir</b> en la aplicación.
/// </summary>
public interface IResolutorDeVinculos
{
    /// <summary>
    /// De los candidatos dados, los que resultaron ser algo abrible por el actor.
    /// </summary>
    Task<IReadOnlyDictionary<string, DestinoDelVinculo>> ResolverAsync(
        IReadOnlyCollection<string> candidatos, CancellationToken ct);
}

/// <summary>La implementación con que arranca el módulo: no resuelve nada.</summary>
internal sealed class SinVinculos : IResolutorDeVinculos
{
    private static readonly IReadOnlyDictionary<string, DestinoDelVinculo> Nada =
        new Dictionary<string, DestinoDelVinculo>(StringComparer.Ordinal);

    public Task<IReadOnlyDictionary<string, DestinoDelVinculo>> ResolverAsync(
        IReadOnlyCollection<string> candidatos, CancellationToken ct) =>
        Task.FromResult(Nada);
}
