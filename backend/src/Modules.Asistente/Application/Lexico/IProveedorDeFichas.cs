namespace Modules.Asistente.Application;

/// <summary>
/// La ficha de un elemento: qué es, con qué identificador se abre y sus datos clave.
/// </summary>
/// <param name="Tipo">El mismo nombre estable que usa el vínculo del elemento.</param>
/// <param name="Id">El mismo identificador que usa el vínculo del elemento.</param>
/// <param name="Datos">
/// Los datos clave, en un diccionario con <b>claves documentadas por tipo</b>. Para
/// <c>pedido-designacion</c>: <c>numero</c>, <c>novedad</c>, <c>materia</c>,
/// <c>estado</c>, <c>estadoEtiqueta</c>, <c>periodo</c>, <c>cargo</c> y
/// <c>dedicacion</c> (sólo si el trámite los tiene) y <c>ultimaActividad</c> en
/// ISO 8601.
/// </param>
/// <remarks>
/// <b>Es un diccionario y no el DTO del módulo dueño, y es deliberado.</b> El asistente
/// no puede referenciar los contracts de Designaciones (ARS-46); la forma tipada la
/// conoce el adaptador del Host y el cliente. Acá se transporta, no se interpreta.
///
/// <b>Estos datos no pasan por el modelo.</b> Viajan del adaptador a la respuesta y de
/// ahí al cliente; lo que el modelo ve sigue siendo lo que el enmascarador le deja ver.
/// </remarks>
public sealed record FichaDelElemento(
    string Tipo,
    string Id,
    IReadOnlyDictionary<string, string?> Datos);

/// <summary>
/// Pide la ficha de un elemento que el resultado ya vinculó, al módulo dueño.
/// </summary>
/// <remarks>
/// <b>Es un puerto por la misma razón que <see cref="IResolutorDeVinculos"/>:</b> qué
/// datos tiene un trámite, y si el actor puede verlos, lo sabe Designaciones. La
/// implementación vive en el composition root.
///
/// Devuelve <c>null</c> cuando el módulo no tiene ficha para ese elemento —no existe, el
/// actor no lo alcanza, el tipo no es suyo— sin distinguir el motivo, igual que los
/// vínculos: decirlo sería un canal de inferencia.
/// </remarks>
public interface IProveedorDeFichas
{
    /// <summary>La ficha del elemento, o <c>null</c> si este proveedor no la tiene.</summary>
    Task<FichaDelElemento?> ObtenerAsync(string tipo, string id, CancellationToken ct);
}

/// <summary>
/// La implementación con que arranca el módulo: no tiene fichas.
/// </summary>
/// <remarks>
/// Sin el adaptador del Host el turno responde igual, con su texto, su tabla y sus
/// vínculos, y sin elemento principal.
/// </remarks>
internal sealed class SinFichas : IProveedorDeFichas
{
    public Task<FichaDelElemento?> ObtenerAsync(string tipo, string id, CancellationToken ct) =>
        Task.FromResult<FichaDelElemento?>(null);
}
