namespace Modules.Asistente.Application;

/// <summary>
/// La ficha de un elemento: qué es, con qué identificador se abre y sus datos clave.
/// </summary>
public sealed record FichaDelElemento(
    string Tipo,
    string Id,
    IReadOnlyDictionary<string, string?> Datos);

/// <summary>Pide la ficha de un elemento que el resultado ya vinculó, al módulo dueño.</summary>
public interface IProveedorDeFichas
{
    /// <summary>La ficha del elemento, o <c>null</c> si este proveedor no la tiene.</summary>
    Task<FichaDelElemento?> ObtenerAsync(string tipo, string id, CancellationToken ct);
}

/// <summary>La implementación con que arranca el módulo: no tiene fichas.</summary>
internal sealed class SinFichas : IProveedorDeFichas
{
    public Task<FichaDelElemento?> ObtenerAsync(string tipo, string id, CancellationToken ct) =>
        Task.FromResult<FichaDelElemento?>(null);
}
